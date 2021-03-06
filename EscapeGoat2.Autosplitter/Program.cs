﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Threading;
using LiveSplit.EscapeGoat2;
using LiveSplit.EscapeGoat2.State;
using LiveSplit.EscapeGoat2.Memory;
using LiveSplit.EscapeGoat2.Debugging;

namespace EscapeGoat2.Autosplitter
{
    class Program
    {
        public static GoatState goatState;
        private static bool timeFixed = false;

        static void Main(string[] args) {
            if (args.Contains("-s"))
                LogWriter.SetSlave();
            LogWriter.WriteLine("[GoatSplitter] Launched");
            goatState = new GoatState();

            // Hook into the split triggers
            goatState.goatTriggers.OnSplit += OnSplitTriggered;

            // Hook into the in-game timer updates
            goatState.OnTimerFixed += goatState_OnIGTFixed;
            goatState.OnTimerChanged += goatState_OnIGTChanged;
            goatState.OnTimerUpdated += goatState_OnIGTUpdated;
            goatState.OnDeath += goatState_OnDeath;

            new Thread(new ThreadStart(ReceiveCommands)).Start();

            bool hasExited = false;

            var profiler = Stopwatch.StartNew();
            while (!hasExited) {
                Update();
                profiler.Restart();
            }
        }

        public static void ReceiveCommands() {
            string line;
            while ((line = Console.ReadLine()) != null) {
                LogWriter.WriteLine("Received {0}", line);
                if (line == "reset") {
                    goatState_Reset();
                }
            }
        }

        public static void Update() {
            goatState.Loop();
        }

        static public void OnSplitTriggered(object sender, SplitEventArgs e) {
            // If we have received a Start event trigger, then we want to start our timer Model causing 
            // LiveSplit to begin counting. We then want to immediatly pause the in-game time tracking 
            // as we will be setting this absolutely based on reading the actual in-game time from 
            // the process memory. Pausing the in-game timer stops LiveSplit from getting in our way.
            if (e.name == "Start") {
                Console.WriteLine("Start");
            }

            // Escape Goat 2 only has one other condition for splitting, the end of a room. This event is 
                // called when: a door is entered, a soul shard is collected, or a glass fragment is obtained.
                // Due to the differences in IGT and RTA timings for Escape Goat 2, we do not want to split
                // if we are on the final split, we want to instead pause the timer. A more detailed explanation
                // for this is in the comments for `goatState_OnIGTFixed()`.
            else {
                Room room = (Room)e.value;
                Console.WriteLine("Split {0}", room);
            }
        }

        static void goatState_OnIGTFixed(object sender, GoatState.TimerEventArgs e) {
            if (timeFixed) return;
            timeFixed = true;
            Console.WriteLine("IGT Fixed");
        }

        static void goatState_OnIGTChanged(object sender, GoatState.TimerEventArgs e) {
            timeFixed = false;
        }

        static void goatState_OnIGTUpdated(object sender, GoatState.TimerEventArgs e) {
            if (!timeFixed)
                Console.WriteLine("IGT {0}", e.gameTime);
        }

        static void goatState_OnDeath(object sender, GoatState.DeathEventArgs e) {
            Console.WriteLine("DEAD {0}", e.room.id);
        }

        static public void goatState_Reset() {
            // Reset the autosplitter state whenever LiveSplit is reset
            goatState.Reset();
        }
    }
}
