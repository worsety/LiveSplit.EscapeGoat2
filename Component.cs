using System;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Diagnostics;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.EscapeGoat2.Debugging;
using System.Collections.Generic;

namespace LiveSplit.EscapeGoat2
{
    public class EscapeGoat2Component : IComponent
    {
        public string ComponentName {
            get { return "Escape Goat 2 Auto Splitter"; }
        }

        private LiveSplitState _state;
        private Process process;

        protected TimerModel Model { get; set; }

        private ComponentRendererComponent InternalComponent;
        private List<IComponent> VisibleComponents = new List<IComponent>();
        private InfoTextComponent deathCounterComponent;
        private ComponentSettings Settings = new ComponentSettings();

        private Dictionary<int, int> runRoomDeaths = new Dictionary<int, int>();
        private Dictionary<int, int> sessionRoomDeaths = new Dictionary<int, int>();
        private Dictionary<int, int> totalRoomDeaths = new Dictionary<int, int>();
        private int runDeathCount = 0, sessionDeathCount = 0, totalDeathCount = 0;

        public float HorizontalWidth => InternalComponent.HorizontalWidth;
        public float VerticalHeight => InternalComponent.VerticalHeight;
        public float MinimumWidth => InternalComponent.MinimumWidth;
        public float MinimumHeight => InternalComponent.MinimumHeight;
        public float PaddingTop => InternalComponent.PaddingTop;
        public float PaddingLeft => InternalComponent.PaddingLeft;
        public float PaddingBottom => InternalComponent.PaddingBottom;
        public float PaddingRight => InternalComponent.PaddingRight;

        public IDictionary<string, Action> ContextMenuControls => null;

        public EscapeGoat2Component(LiveSplitState state) {
            _state = state;

            deathCounterComponent = new InfoTextComponent("", "");
            InternalComponent = new ComponentRendererComponent();
            InternalComponent.VisibleComponents = VisibleComponents;

            ProcessStartInfo processStartInfo;

            processStartInfo = new ProcessStartInfo();
            processStartInfo.CreateNoWindow = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.Arguments = "-s";
            processStartInfo.FileName = "Components/EscapeGoat2.Autosplitter.exe";

            process = new Process();
            process.StartInfo = processStartInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += new DataReceivedEventHandler
            (
                delegate(object sender, DataReceivedEventArgs e) {
                    if (!String.IsNullOrEmpty(e.Data)) {
                        string line = e.Data.ToString();
                        string[] cmd = line.Split(new Char[] { ' ' }, 2);

                        if (cmd[0] == "Start") {
                            DoStart();
                        } else if (cmd[0] == "Split") {
                            DoSplit();
                        } else if (cmd[0] == "IGT") {
                            if (cmd[1] != "Fixed") {
                                _state.SetGameTime(TimeSpan.Parse(cmd[1]));
                                _state.IsGameTimePaused = true;
                            } else {
                                DoEndGameSplit();
                            }
                        } else if (cmd[0] == "Log") {
                            LogWriter.WriteLine("{0}", cmd[1]);
                        } else if (cmd[0] == "DEAD") {
                            DoDeath(cmd[1]);
                        }
                    }
                }
            );
            process.Exited += new EventHandler
            (
                delegate(object sender, EventArgs e) {
                    MessageBox.Show(String.Format("EscapeGoat2.Autosplitter.exe exited at {0} with code {1}\nPlease reload the layout.", process.ExitTime, process.ExitCode),
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            );

            process.Start();
            process.BeginOutputReadLine();
        }

        public void Dispose() {
            InternalComponent.Dispose();

            if (process != null && !process.HasExited) {
                process.CancelOutputRead();
                process.CloseMainWindow();
                process.Kill();
                process.Close();
            }

            if (Model != null) {
                Model.CurrentState.OnReset -= OnReset;
                Model.CurrentState.OnPause -= OnPause;
                Model.CurrentState.OnResume -= OnResume;
                Model.CurrentState.OnStart -= OnStart;
                Model.CurrentState.OnSplit -= OnSplit;
                Model.CurrentState.OnSkipSplit -= OnSkipSplit;
                Model.CurrentState.OnUndoSplit -= OnUndoSplit;
            }
        }

        private void PrepareDraw(LiveSplitState state) {
            if (Settings.showDeathsRun || Settings.showDeathsSession || Settings.showDeathsTotal) {
                if (Settings.showDeathsRun && !Settings.showDeathsSession && !Settings.showDeathsTotal)
                    deathCounterComponent.InformationName = "Deaths (Run)";
                else if (!Settings.showDeathsRun && Settings.showDeathsSession && !Settings.showDeathsTotal)
                    deathCounterComponent.InformationName = "Deaths (Session)";
                else if (!Settings.showDeathsRun && !Settings.showDeathsSession && Settings.showDeathsTotal)
                    deathCounterComponent.InformationName = "Deaths (Total)";
                else {
                    List<string> nameArgs = new List<string>();
                    if (Settings.showDeathsRun) nameArgs.Add("Run");
                    if (Settings.showDeathsSession) nameArgs.Add("S");
                    if (Settings.showDeathsTotal) nameArgs.Add("T");
                    deathCounterComponent.InformationName = String.Format("Deaths ({0})", string.Join("/", nameArgs));
                }
                List<string> formatArgs = new List<string>();
                if (Settings.showDeathsRun) formatArgs.Add("{0}");
                if (Settings.showDeathsSession) formatArgs.Add("{1}");
                if (Settings.showDeathsTotal) formatArgs.Add("{2}");
                deathCounterComponent.InformationValue = String.Format(String.Join("/", formatArgs), runDeathCount, sessionDeathCount, totalDeathCount);

                if (!VisibleComponents.Contains(deathCounterComponent))
                    VisibleComponents.Add(deathCounterComponent);
                deathCounterComponent.NameLabel.ForeColor = state.LayoutSettings.TextColor;
                deathCounterComponent.ValueLabel.ForeColor = state.LayoutSettings.TextColor;
                deathCounterComponent.NameLabel.HasShadow = state.LayoutSettings.DropShadows;
            }
            else if (VisibleComponents.Contains(deathCounterComponent))
                VisibleComponents.Remove(deathCounterComponent);
        }

        public void DrawHorizontal(System.Drawing.Graphics g, LiveSplitState state, float height, System.Drawing.Region clipRegion) {
            PrepareDraw(state);
            InternalComponent.DrawHorizontal(g, state, height, clipRegion);
        }

        public void DrawVertical(System.Drawing.Graphics g, LiveSplitState state, float width, System.Drawing.Region clipRegion) {
            PrepareDraw(state);
            InternalComponent.DrawVertical(g, state, width, clipRegion);
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) {
            // Hook a TimerModel to the current LiveSplit state
            if (Model == null) {
                Model = new TimerModel() { CurrentState = state };
                state.OnReset += OnReset;
                state.OnPause += OnPause;
                state.OnResume += OnResume;
                state.OnStart += OnStart;
                state.OnSplit += OnSplit;
                state.OnSkipSplit += OnSkipSplit;
                state.OnUndoSplit += OnUndoSplit;
            }

            if (invalidator != null)
                InternalComponent.Update(invalidator, state, width, height, mode);
        }

        public void OnUndoSplit(object sender, EventArgs e) {
            // On undo we want to reset the lastRoomID as we do not know the state
            // when the undo occured.
            LogWriter.WriteLine("[LiveSplit] Undo Split.");
            process.StandardInput.WriteLine("undo");
        }

        public void OnReset(object sender, TimerPhase e) {
            // Reset the autosplitter state whenever LiveSplit is reset
            LogWriter.WriteLine("[LiveSplit] Reset.");
            process.StandardInput.WriteLine("reset");
            runDeathCount = 0;
        }

        public void DoStart() {
            LogWriter.WriteLine("[GoatSplitter] Start.");
            Model.Start();
            _state.IsGameTimePaused = true;
        }

        public void DoSplit() {
            LogWriter.WriteLine("[GoatSplitter] RTA Split {0} of {1}", _state.CurrentSplitIndex + 1, _state.Run.Count);
            if (!isLastSplit()) {
                Model.Split();
            } else {
                LogWriter.WriteLine("[GoatSplitter] RTA Last Split, Pausing Timer.");
                Model.Pause();
            }
        }

        public void DoEndGameSplit() {
            // Escape Goat 2 has two different timing methods. RTA and IGT. RTA timings are stopped
            // upon entering the last door (final input), while IGT continues for approximately
            // 2-3 seconds as it stops when the level fades out completely. As a result, we
            // Pause LiveSplit when the final trigger occurs, this "stops" the RTA timer, but
            // we can continue to update the IGT timer directly.

            // As the IGT never updates inside live split, we set its value absolutely,
            // pausing LiveSplit therefore has the effect of "stopping" the RTA timer while
            // IGT continues.

            // Therefore, if we are on the final split, and we receive the "IGT is the same as
            // the last time we checked" event, we know that the IGT has stopped for the last time.
            // We therefore unpause LiveSplit (by calling Pause again) so we can call Split
            // (this cannot be called while paused) and then perform the final split.
            if (isLastSplit() && Model.CurrentState.CurrentPhase == TimerPhase.Paused) {
                LogWriter.WriteLine("[GoatSplitter] IGT Last Split, Stopping Timer.");
                Model.Pause();
                Model.Split();
            }
        }

        public void DoDeath(string arg) {
            List<Dictionary<int, int>> roomDeathDicts = new List<Dictionary<int, int>> { runRoomDeaths, sessionRoomDeaths };

            runDeathCount++; sessionDeathCount++;
            if (Settings.saveStats) {
                totalDeathCount++;
                roomDeathDicts.Add(totalRoomDeaths);
                _state.Layout.HasChanged = true;
            }

            int roomKey = int.Parse(arg);
            foreach (var roomDeaths in roomDeathDicts) {
                int roomDeathCount = 0;
                roomDeaths.TryGetValue(roomKey, out roomDeathCount);
                roomDeaths[roomKey] = roomDeathCount + 1;
            }
        }

        public bool isLastSplit() {
            int idx = _state.CurrentSplitIndex;
            return (idx == _state.Run.Count - 1);
        }

        public void OnSkipSplit(object sender, EventArgs e) {
            LogWriter.WriteLine("[LiveSplit] Skip Split.");
        }

        public void OnSplit(object sender, EventArgs e) {
            LogWriter.WriteLine("[LiveSplit] Split.");
        }

        public void OnResume(object sender, EventArgs e) {
            LogWriter.WriteLine("[LiveSplit] Resume.");
        }

        public void OnPause(object sender, EventArgs e) {
            LogWriter.WriteLine("[LiveSplit] Pause.");
        }

        public void OnStart(object sender, EventArgs e) {
            LogWriter.WriteLine("[LiveSplit] Start.");
        }

        public Control GetSettingsControl(LayoutMode mode) {
            return Settings;
        }

        public void SetSettings(XmlNode settings) {
            Settings.SetSettings(settings);

            XmlNode data = settings["Data"];
            if (data == null)
                return;

            totalDeathCount = SettingsHelper.ParseInt(data["TotalDeaths"]);
            totalRoomDeaths.Clear();
            foreach (XmlElement room in data.SelectNodes("./TotalRoomDeaths/Room")) {
                try {
                    string id = room.GetAttribute("id"), count = room.GetAttribute("deaths");
                    totalRoomDeaths[int.Parse(id)] = int.Parse(count);
                } catch (Exception) { }
            }
        }

        public XmlNode GetSettings(XmlDocument document) {
            XmlNode root = Settings.GetSettings(document);
            XmlElement data = document.CreateElement("Data");

            SettingsHelper.CreateSetting(document, data, "TotalDeaths", totalDeathCount);

            XmlElement roomDeaths = document.CreateElement("TotalRoomDeaths");
            foreach (var room in totalRoomDeaths) {
                XmlElement e = document.CreateElement("Room");
                e.SetAttribute("id", room.Key.ToString());
                e.SetAttribute("deaths", room.Value.ToString());
                roomDeaths.AppendChild(e);
            }
            data.AppendChild(roomDeaths);
            root.AppendChild(data);

            return root;
        }
    }
}
