using System;
using System.Collections.Generic;
using LiveSplit.EscapeGoat2.Memory;
using LiveSplit.EscapeGoat2.Debugging;
using System.Threading;

namespace LiveSplit.EscapeGoat2.State
{
    public enum PlayerState
    {
        Dead = 0,
        Alive,
        Invulnerable,
    }

    public class GoatState
    {
        public bool isOpen = false;
        private bool reset = false;

        public WorldMap map;
        public GoatMemory goatMemory;
        public GoatTriggers goatTriggers;

        public struct State
        {
            public TimeSpan? XnaGameTime;

            public bool isStarted;                        // Set to True when New Game is selected.

            public int roomID;

            public PlayerState playerState;               // Set to Alive when the Goat is created, set to Dead when the Goat is destroyed (death).

            public int collectedShards;                   // The number of collected Glass Fragments (called Shards internally)
            public int collectedSheepOrbs;                // The number of collected Sheep Orbs

            public bool roomTimeRunning;                  // Internal tracking of whether a room timer is active, distinct from below
            public bool enteredDoor;                      // Tracks a room field in the game which is set when you enter a door, and no other time

            public TimeSpan gameTime;
            public TimeSpan roomTime;
            public bool paused;
        }

        private State curState, oldState;

        public class TimerEventArgs : EventArgs
        {
            public TimeSpan gameTime;
            public TimerEventArgs(TimeSpan gameTime)
            {
                this.gameTime = gameTime;
            }
        }

        public class DeathEventArgs : EventArgs
        {
            public Room room;
            public DeathEventArgs(Room room)
            {
                this.room = room;
            }
        }

        public event EventHandler<TimerEventArgs> OnTimerFixed;                         // Fires whenever the IGT between updates has not changed
        public event EventHandler<TimerEventArgs> OnTimerChanged;                       // Fires whenever the IGT between updates has changed
        public event EventHandler<TimerEventArgs> OnTimerUpdated;                       // Fires every update after the IGT is updated.
        public event EventHandler<DeathEventArgs> OnDeath;                              // Fires every time you die, sadly we can't determine why you died.

        private int exceptionsCaught = 0;

        private const int positionChangedSanity = 30;

        public GoatState() {
            map = new WorldMap();
            goatMemory = new GoatMemory();
            goatTriggers = new GoatTriggers();
        }

        public void Reset() {
            reset = true;
        }

        private void DoReset() {
            reset = false;
            oldState = curState = new State();
            exceptionsCaught = 0;
            OnTimerChanged(this, new TimerEventArgs(TimeSpan.Zero)); // reset timer fixed flag, has to be done from this thread
        }

        public void Dispose() {
            // Unhook from reading the game memory
            goatMemory.Dispose();
        }

        public void Loop() {
            if (reset)
                DoReset();

            try {
                // Hook the game process so we can read the memory
                bool isNowOpen = (goatMemory.HookProcess() && !goatMemory.proc.HasExited);

                if (isNowOpen != isOpen) {
                    if (!isNowOpen) LogWriter.WriteLine("escapegoat2.exe is unavailable.");
                    else LogWriter.WriteLine("escapegoat2.exe is available.");
                    isOpen = isNowOpen;
                }

                // If we're open, do all the magic
                if (isOpen) Pulse();
                else Thread.Sleep(250);
            } catch (Exception e) {
                if (exceptionsCaught++ < 100)
                    LogWriter.WriteLine("Exception #{0}: {1}", exceptionsCaught, e.ToString());
                if (exceptionsCaught == 100)
                    LogWriter.WriteLine("Too many total exceptions, no longer logging them.");
            }

            // We cache memory pointers during each pulse inside goatMemory for
            // performance reasons, we need to manually clear the cache here so
            // that goatMemory knows we are done making calls to it and that we
            // do not need these values anymore.
            //
            // We need this to occur even if (actually, especially if) and
            // exception occurs to clear any potentially dead memory pointers
            // that occured due to reading memory just as it's being
            // moved/freed.
        }

        public void Pulse() {
            curState = oldState;

            TimeSpan frame = goatMemory.GetTargetElapsedTime();

            while (!curState.XnaGameTime.HasValue || curState.XnaGameTime == oldState.XnaGameTime) {
                Thread.Sleep(1);
                goatMemory.ClearCaches();
                curState.XnaGameTime = goatMemory.GetXnaGameTime();
            }

            // Update internal model of the game's state
            Update();

            // if we get this far, nothing went wrong!
            TimeSpan? endTime = goatMemory.GetXnaGameTime();
            if (endTime != curState.XnaGameTime)
                return; // GOD DAMN IT SOMETHING WENT WRONG

            // Everything's fine, let's generate notifications
            if (!reset)
                Broadcast();

            if (oldState.XnaGameTime != TimeSpan.Zero && (curState.XnaGameTime - oldState.XnaGameTime)?.Ticks > 5 * frame.Ticks)
                LogWriter.WriteLine("Missed {0} frames of updates", (double)(curState.XnaGameTime - oldState.XnaGameTime)?.Ticks / frame.Ticks);

            // This acts like a transaction commit, while the lack of it if we return or throw is a rollback
            oldState = curState;
        }

        public void Broadcast() {
            if (!curState.isStarted)
                return;

            if (!oldState.isStarted)
                goatTriggers.SplitOnGameStart(curState.isStarted);

            if (oldState.roomTimeRunning && !curState.roomTimeRunning)
            {
                // if we exited by a door, the room time wasn't updated this frame so add one frame
                TimeSpan frame = TimeSpan.FromTicks(166667);
                TimeSpan splittime = oldState.gameTime + curState.roomTime - oldState.roomTime + (curState.enteredDoor ? frame : TimeSpan.Zero);
                if (Math.Abs((curState.gameTime - splittime).Ticks) >= TimeSpan.FromMilliseconds(1).Ticks)
                    LogWriter.WriteLine("Split late by {0} frames", (double)(curState.gameTime - splittime).Ticks / frame.Ticks);

                OnTimerUpdated(this, new TimerEventArgs(splittime));
                goatTriggers.SplitOnEndRoom(this.map.GetRoom(curState.roomID));
            }

            // Call all the relevant IGT based events depending on the time delta since the last pulse.
            if (curState.gameTime != oldState.gameTime)
                if (OnTimerChanged != null) OnTimerChanged(this, new TimerEventArgs(curState.gameTime));

            if (OnTimerUpdated != null) OnTimerUpdated(this, new TimerEventArgs(curState.gameTime));

            if (curState.gameTime == oldState.gameTime && !curState.paused)
                if (OnTimerFixed != null) OnTimerFixed(this, new TimerEventArgs(curState.gameTime));

            if (curState.playerState == PlayerState.Dead && oldState.playerState == PlayerState.Alive)
                OnDeath(this, new DeathEventArgs(map.GetRoom(curState.roomID)));
        }

        public void Update() {
            // If we haven't detected the start of a new game, check the memory
            // for the event
            if (!curState.isStarted)
                curState.isStarted = goatMemory.GetStartOfGame();

            if (!curState.isStarted)
                return;

            curState.gameTime = goatMemory.GetGameTime();

            // All of our checks are dependent on there being an active room
            // available.  This requires both the RoomInstance to be available,
            // and that we are on the "ActionScene" in the SceneManager
            // indicating that the RoomInstance is the active scene.
            var roomInstance = goatMemory.GetRoomInstance();
            bool isOnAction  = goatMemory.GetOnActionScene().Value;

            if (roomInstance.HasValue && isOnAction) {
                UpdatePlayerStatus();
                UpdateLevelStatus();
                curState.paused = goatMemory.GetPaused();
                if (curState.paused && !oldState.paused)
                    LogWriter.WriteLine("Game paused");
                if (oldState.paused && !curState.paused)
                    LogWriter.WriteLine("Game unpaused");
            }
        }

        public void UpdateLevelStatus() {
            curState.roomID = goatMemory.GetRoomID().Value;

            if (curState.roomID != 0 && curState.roomID != oldState.roomID && curState.roomID != oldState.roomID)
                LogWriter.WriteLine("Entering room {0} from {1}", curState.roomID, oldState.roomID);

            curState.roomTime = goatMemory.GetRoomTime();

            if (oldState.roomTimeRunning) {
                curState.enteredDoor = goatMemory.EnteredDoor().Value;

                if (curState.enteredDoor || curState.playerState == PlayerState.Invulnerable) {
                    curState.roomTimeRunning = false;
                    LogWriter.WriteLine("Final room time: {0} (room {1})", curState.roomTime, curState.roomID);

                    bool newSheepOrb = HaveCollectedNewSheepOrb();
                    bool newShard = HaveCollectedNewShard();

                    if (curState.playerState == PlayerState.Invulnerable && !(newSheepOrb || newShard))
                        LogWriter.WriteLine("Player set invulnerable but sheep and shard count unchanged, this is a bug in an unmodified game.");
                }
            } else {
                if (curState.roomTime != oldState.roomTime)
                {
                    curState.roomTimeRunning = true;
                    LogWriter.WriteLine("Room timer started {0} at {1} (room {2})", curState.roomTime, curState.gameTime, curState.roomID);
                }
            }
        }

        public void UpdatePlayerStatus()
        {
            // This checks when the Goat player object exists. It is set to True when entering the first room,
            // and is set to False when the player dies. It is set to True again once respawned.
            var player = goatMemory.GetPlayerObject();
            bool invuln = goatMemory.IsGoatInvuln().GetValueOrDefault();

            curState.playerState = player.HasValue ? (invuln ? PlayerState.Invulnerable : PlayerState.Alive) : PlayerState.Dead;

            if (curState.playerState != oldState.playerState)
            {
                LogWriter.WriteLine("Player Object {0} in Room {1}",
                    new Dictionary<PlayerState, string> {
                            { PlayerState.Dead, "Destroyed" },
                            { PlayerState.Alive, "Created" },
                            { PlayerState.Invulnerable, "Invulnerable (collected something)" },
                    }[curState.playerState],
                    curState.roomID
                );
            }
        }

        public bool HaveCollectedNewSheepOrb() {
            // We detect a Sheep Orb is collected because the length of the game's SheepOrbsCollected array increases.
            curState.collectedSheepOrbs = goatMemory.GetSheepOrbsCollected().Value;

            // Check if we have more sheep orbs than we used to
            if (curState.collectedSheepOrbs != oldState.collectedSheepOrbs)
            {
                LogWriter.WriteLine("Sheep soul count changed: {0} -> {1} (room {2})",
                        oldState.collectedSheepOrbs, curState.collectedSheepOrbs, curState.roomID);
                if (curState.collectedSheepOrbs != oldState.collectedSheepOrbs + 1)
                    LogWriter.WriteLine("Abnormal sheep soul count change.");
            }

            return (curState.collectedSheepOrbs == oldState.collectedSheepOrbs + 1);
        }

        public bool HaveCollectedNewShard() {
            // We detect a Sheep Orb is collected because the length of the game's SecretRoomsBeaten array increases,
            // which is equivalent to saying a Glass Fragment (shard) was collected.
            curState.collectedShards = goatMemory.GetShardsCollected().Value;

            // Check if we have more glass fragments than we used to
            if (curState.collectedShards != oldState.collectedShards) {
                LogWriter.WriteLine("Shard count changed: {0} -> {1} (room {2})",
                        oldState.collectedShards, curState.collectedShards, curState.roomID);
                if (curState.collectedShards != oldState.collectedShards + 1)
                    LogWriter.WriteLine("Abnormal shard count change.");
            }

            return (curState.collectedShards == oldState.collectedShards + 1);
        }
    }
}
