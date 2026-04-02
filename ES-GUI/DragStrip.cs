using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace ES_GUI
{
    public enum DragDistance
    {
        Foot60 = 18,       // 60 feet
        Foot330 = 101,     // 330 feet
        EighthMile = 201,  // 1/8 mile
        Foot1000 = 305,    // 1000 feet
        QuarterMile = 402, // 1/4 mile
        HalfMile = 805,    // 1/2 mile
        FullMile = 1609    // 1 mile
    }

    public class DragSplit
    {
        public string Name { get; set; }
        public int DistanceMeters { get; set; }
        public double ElapsedTime { get; set; } = -1;
        public double SpeedKmh { get; set; } = -1;
        public bool Hit { get; set; } = false;
    }

    public class SpeedRangeSplit
    {
        public string Name { get; set; }
        public double StartSpeedKmh { get; set; }
        public double EndSpeedKmh { get; set; }
        public double StartTime { get; set; } = -1;
        public double EndTime { get; set; } = -1;
        public bool Started { get; set; } = false;
        public bool Finished { get; set; } = false;

        public double ElapsedTime
        {
            get
            {
                if (Finished && StartTime >= 0 && EndTime >= 0)
                    return EndTime - StartTime;
                return -1;
            }
        }
    }

    public class DragStripResult
    {
        public DragSplit[] Splits { get; set; }
        public SpeedRangeSplit[] SpeedRanges { get; set; }
        public bool Finished { get; set; }
        public int SelectedDistance { get; set; }

        public DragStripResult(int selectedDistance)
        {
            SelectedDistance = selectedDistance;
            Splits = new DragSplit[]
            {
                new DragSplit { Name = "60 ft",    DistanceMeters = 18 },
                new DragSplit { Name = "330 ft",   DistanceMeters = 101 },
                new DragSplit { Name = "1/8 mi",   DistanceMeters = 201 },
                new DragSplit { Name = "1000 ft",  DistanceMeters = 305 },
                new DragSplit { Name = "1/4 mi",   DistanceMeters = 402 },
                new DragSplit { Name = "1/2 mi",   DistanceMeters = 805 },
                new DragSplit { Name = "1 mi",     DistanceMeters = 1609 },
            };
            SpeedRanges = new SpeedRangeSplit[]
            {
                new SpeedRangeSplit { Name = "0-60 mph",      StartSpeedKmh = 0,       EndSpeedKmh = 96.5606 },  // 60 mph
                new SpeedRangeSplit { Name = "0-100 km/h",    StartSpeedKmh = 0,  EndSpeedKmh = 100.0 },
                new SpeedRangeSplit { Name = "60-130 mph",    StartSpeedKmh = 96.5606, EndSpeedKmh = 209.215 },   // 60-130 mph
                new SpeedRangeSplit { Name = "100-200 km/h",  StartSpeedKmh = 100.0,   EndSpeedKmh = 200.0 },
                new SpeedRangeSplit { Name = "0-100 mph",     StartSpeedKmh = 0,       EndSpeedKmh = 160.934 },   // 100 mph
                new SpeedRangeSplit { Name = "0-200 km/h",    StartSpeedKmh = 0,    EndSpeedKmh = 200.0 },
            };
        }

        public void Reset()
        {
            Finished = false;
            foreach (var s in Splits)
            {
                s.ElapsedTime = -1;
                s.SpeedKmh = -1;
                s.Hit = false;
            }
            foreach (var sr in SpeedRanges)
            {
                sr.StartTime = -1;
                sr.EndTime = -1;
                sr.Started = false;
                sr.Finished = false;
            }
        }
    }

    public class DragStrip
    {
        private Stopwatch timer;
        private double lastTimestamp;
        private double distanceMeters;
        private double lastSpeedMps;

        // Logging run data
        public List<double> LoggedDistances { get; private set; }
        public List<double> LoggedSpeeds { get; private set; }
        public List<double> LoggedTimestamps { get; private set; }
        public List<double> LoggedAccelG { get; private set; }

        // Live readouts
        public bool Running { get; set; }   
        public bool Staged { get; set; }
        public bool WaitingForLaunch { get; set; }
        public double ElapsedSeconds { get; set; }
        public double DistanceMeters { get { return distanceMeters; } set { distanceMeters = value; } }
        public double CurrentSpeedKmh { get; set; }
        public int CurrentGear { get; set; }
        public double CurrentRPM { get; set; }

        // Best run
        public DragStripResult LastRun { get; set; }
        public DragStripResult BestRun { get; set; }

        // Settings
        public int SelectedDistance { get; set; } = 402; // default 1/4 mile

        public DragStrip()
        {
            timer = new Stopwatch();
            LastRun = new DragStripResult(SelectedDistance);
            BestRun = new DragStripResult(SelectedDistance);
            LoggedDistances = new List<double>();
            LoggedSpeeds = new List<double>();
            LoggedTimestamps = new List<double>();
            LoggedAccelG = new List<double>();
        }

        public void Stage()
        {
            if (Running || WaitingForLaunch) return;
            Reset(); // Call Reset first
            Staged = true; // Then set Staged to true
            LastRun = new DragStripResult(SelectedDistance);
        }

        public void Go()
        {
            if (!Staged || Running || WaitingForLaunch) return;
            LoggedDistances.Clear();
            LoggedSpeeds.Clear();
            LoggedTimestamps.Clear();
            LoggedAccelG.Clear();
            WaitingForLaunch = true;
        }

        /// <summary>
        /// Called each frame with the current engine telemetry.
        /// speedMps = vehicleSpeed from engineUpdate (m/s)
        /// </summary>
        public void Update(double speedMps, double rpm, int gear)
        {
            CurrentSpeedKmh = speedMps * 3.6;
            CurrentRPM = rpm;
            CurrentGear = gear;

            // Start the timer once the car begins moving
            if (WaitingForLaunch && Math.Abs(speedMps) > 0.05)
            {
                WaitingForLaunch = false;
                Running = true;
                timer.Restart();
                lastTimestamp = 0;
                distanceMeters = 0;
                ElapsedSeconds = 0;
                lastSpeedMps = speedMps;
            }

            if (!Running) return;

            double now = timer.Elapsed.TotalSeconds;
            double dt = now - lastTimestamp;
            lastTimestamp = now;

            if (dt <= 0 || dt > 0.5) return; // guard against bad frames

            // Integrate distance: meters += speed(m/s) * dt
            distanceMeters += speedMps * dt;
            ElapsedSeconds = now;

            // Calculate acceleration in g (1 g = 9.80665 m/s²)
            double accelMps2 = 0;
            if (dt > 0)
            {
                accelMps2 = (speedMps - lastSpeedMps) / dt;
            }
            double accelG = accelMps2 / 9.80665;
            
            // limit absurd spikes (noise/physics glitches)
            if (accelG > 10) accelG = 10;
            if (accelG < -10) accelG = -10;

            lastSpeedMps = speedMps;

            LoggedDistances.Add(distanceMeters);
            LoggedSpeeds.Add(CurrentSpeedKmh);
            LoggedTimestamps.Add(now);
            LoggedAccelG.Add(accelG);

            // Check split markers
            foreach (var split in LastRun.Splits)
            {
                if (!split.Hit && distanceMeters >= split.DistanceMeters)
                {
                    split.Hit = true;
                    split.ElapsedTime = now;
                    split.SpeedKmh = CurrentSpeedKmh;
                }
            }

            // Check speed range markers
            foreach (var sr in LastRun.SpeedRanges)
            {
                if (!sr.Started && CurrentSpeedKmh >= sr.StartSpeedKmh)
                {
                    sr.Started = true;
                    // For 0-X ranges, start time is the beginning of the run
                    if (sr.StartSpeedKmh < 1.0)
                        sr.StartTime = 0;
                    else
                        sr.StartTime = now;
                }
                if (sr.Started && !sr.Finished && CurrentSpeedKmh >= sr.EndSpeedKmh)
                {
                    sr.Finished = true;
                    sr.EndTime = now;
                }
            }

            // Check if we hit the finish distance
            if (distanceMeters >= SelectedDistance)
            {
                Finish();
            }
        }

        private void Finish()
        {
            Running = false;
            Staged = false;
            WaitingForLaunch = false;
            timer.Stop();
            LastRun.Finished = true;

            // Update best run if this is better
            if (BestRun.Finished)
            {
                var bestQuarter = GetSplitTime(BestRun, "1/4 mi");
                var lastQuarter = GetSplitTime(LastRun, "1/4 mi");
                if (lastQuarter >= 0 && (bestQuarter < 0 || lastQuarter < bestQuarter))
                {
                    BestRun = CloneResult(LastRun);
                }
            }
            else
            {
                BestRun = CloneResult(LastRun);
            }
        }

        private double GetSplitTime(DragStripResult result, string name)
        {
            foreach (var s in result.Splits)
            {
                if (s.Name == name) return s.ElapsedTime;
            }
            return -1;
        }

        private DragStripResult CloneResult(DragStripResult src)
        {
            var clone = new DragStripResult(src.SelectedDistance);
            clone.Finished = src.Finished;
            for (int i = 0; i < src.Splits.Length; i++)
            {
                clone.Splits[i].Hit = src.Splits[i].Hit;
                clone.Splits[i].ElapsedTime = src.Splits[i].ElapsedTime;
                clone.Splits[i].SpeedKmh = src.Splits[i].SpeedKmh;
            }
            for (int i = 0; i < src.SpeedRanges.Length; i++)
            {
                clone.SpeedRanges[i].Started = src.SpeedRanges[i].Started;
                clone.SpeedRanges[i].Finished = src.SpeedRanges[i].Finished;
                clone.SpeedRanges[i].StartTime = src.SpeedRanges[i].StartTime;
                clone.SpeedRanges[i].EndTime = src.SpeedRanges[i].EndTime;
            }
            return clone;
        }

        public void Stop()
        {
            if (Running || WaitingForLaunch)
            {
                Running = false;
                Staged = false;
                WaitingForLaunch = false;
                timer.Stop();
                LastRun.Finished = true;

                // Save partial results
                if (!BestRun.Finished)
                {
                    BestRun = CloneResult(LastRun);
                }
            }
        }

        public void Reset()
        {
            Running = false;
            Staged = false;
            WaitingForLaunch = false;
            timer.Reset();
            distanceMeters = 0;
            ElapsedSeconds = 0;
            lastTimestamp = 0;
            lastSpeedMps = 0;
            CurrentSpeedKmh = 0;
        }

        public static string FormatTime(double seconds)
        {
            if (seconds < 0) return "--.---";
            return seconds.ToString("F3");
        }

        public static string FormatSpeed(double kmh)
        {
            if (kmh < 0) return "---";
            return kmh.ToString("F1");
        }

        public static string FormatDistance(double meters)
        {
            if (meters < 1000)
                return $"{meters:F0} m";
            return $"{meters / 1000.0:F2} km";
        }

        public string GetDistanceName(int meters)
        {
            switch (meters)
            {
                case 402: return "1/4 Mile";
                case 805: return "1/2 Mile";
                case 1609: return "1 Mile";
                default: return $"{meters} m";
            }
        }

        /// <summary>
        /// Compute smoothed g-force values using a simple moving average.
        /// </summary>
        public static List<double> SmoothAccelG(List<double> raw, int windowSize)
        {
            var result = new List<double>();
            if (raw.Count == 0) return result;
            for (int i = 0; i < raw.Count; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(raw.Count - 1, i + windowSize / 2);
                double sum = 0;
                for (int j = start; j <= end; j++)
                    sum += raw[j];
                result.Add(sum / (end - start + 1));
            }
            return result;
        }

        public double PeakAccelG
        {
            get
            {
                double peak = 0;
                foreach (var g in LoggedAccelG)
                {
                    if (g > peak) peak = g;
                }
                return peak;
            }
        }
    }

    public class DragStripUtil
    {
        public DragStrip Model { get; private set; }

        public Label StatusLabel { get; set; }
        public ComboBox DistanceCombo { get; set; }
        public RoundButton StageBtn { get; set; }
        public RoundButton GoBtn { get; set; }
        public RoundButton StopBtn { get; set; }
        public RoundButton ResetBtn { get; set; }
        public Label TimerLabel { get; set; }
        public Label SpeedLabel { get; set; }
        public Label GearLabel { get; set; }
        public Label RPMLabel { get; set; }
        public Label DistanceLabel { get; set; }

        public Label[] SplitNameLabels { get; set; }
        public Label[] SplitTimeLabels { get; set; }
        public Label[] SplitSpeedLabels { get; set; }
        public Label[] BestSplitTimeLabels { get; set; }

        public Label LastTimeLabel { get; set; }
        public Label LastSpeedLabel { get; set; }
        public Label BestTimeLabel { get; set; }
        public Label BestSpeedLabel { get; set; }

        public Label[] SpeedRangeNameLabels { get; set; }
        public Label[] SpeedRangeTimeLabels { get; set; }
        public Label PeakGLabel { get; set; }
        public Label NoDataMsg { get; set; }
        public Chart DragChart { get; set; }

        private System.Windows.Forms.Timer updateTimer;

        public DragStripUtil(DragStrip model = null)
        {
            Model = model ?? new DragStrip();
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 16;
            updateTimer.Tick += UpdateTimer_Tick;
        }

        public void Initialize()
        {
            if (DragChart != null)
                ThemeManager.ApplyTheme(DragChart);

            if (DistanceCombo != null)
            {
                DistanceCombo.SelectedIndexChanged += DistanceCombo_Changed;
                if (DistanceCombo.Items.Count > 0 && DistanceCombo.SelectedIndex == -1)
                    DistanceCombo.SelectedIndex = 0;
            }
            if (StageBtn != null) StageBtn.Click += Stage_Click;
            if (GoBtn != null) GoBtn.Click += Go_Click;
            if (StopBtn != null) StopBtn.Click += Stop_Click;
            if (ResetBtn != null) ResetBtn.Click += Reset_Click;

            updateTimer.Start();
        }

        public void Stop()
        {
            updateTimer.Stop();
        }

        private void DistanceCombo_Changed(object sender, EventArgs e)
        {
            if (DistanceCombo == null) return;
            switch (DistanceCombo.SelectedIndex)
            {
                case 0: Model.SelectedDistance = 402; break;  // 1/4 mile
                case 1: Model.SelectedDistance = 805; break;  // 1/2 mile
                case 2: Model.SelectedDistance = 1609; break; // 1 mile
            }
        }

        private void Stage_Click(object sender, EventArgs e)
        {
            Model.Stage();
            if (StatusLabel != null)
            {
                StatusLabel.Text = "STAGED";
                StatusLabel.ForeColor = Color.FromArgb(200, 160, 0);
            }
            if (GoBtn != null) GoBtn.Enabled = true;
            if (StageBtn != null) StageBtn.Enabled = false;
            if (StopBtn != null) StopBtn.Enabled = false;
            ClearDragSplits();
        }

        private void Go_Click(object sender, EventArgs e)
        {
            Model.Go();
            if (StatusLabel != null)
            {
                StatusLabel.Text = "RUNNING";
                StatusLabel.ForeColor = Color.FromArgb(0, 160, 0);
            }
            if (GoBtn != null) GoBtn.Enabled = false;
            if (StageBtn != null) StageBtn.Enabled = false;
            if (StopBtn != null) StopBtn.Enabled = true;
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            Model.Stop();
            if (StatusLabel != null)
            {
                StatusLabel.Text = "ABORTED";
                StatusLabel.ForeColor = Color.FromArgb(180, 30, 30);
            }
            if (StageBtn != null) StageBtn.Enabled = true;
            if (GoBtn != null) GoBtn.Enabled = false;
            if (StopBtn != null) StopBtn.Enabled = false;
            UpdateDragResults();
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            Model.Reset();
            if (StatusLabel != null)
            {
                StatusLabel.Text = "IDLE";
                StatusLabel.ForeColor = Color.Gray;
            }
            if (TimerLabel != null) TimerLabel.Text = "0.000";
            if (SpeedLabel != null) SpeedLabel.Text = "0.0 km/h";
            if (GearLabel != null) GearLabel.Text = "N";
            if (RPMLabel != null) RPMLabel.Text = "0 RPM";
            if (DistanceLabel != null) DistanceLabel.Text = "0 m";

            if (StageBtn != null) StageBtn.Enabled = true;
            if (GoBtn != null) GoBtn.Enabled = false;
            if (StopBtn != null) StopBtn.Enabled = false;

            ClearDragSplits();

            if (DragChart != null)
            {
                if (DragChart.Series.IndexOf("Speed") >= 0) DragChart.Series["Speed"].Points.Clear();
                if (DragChart.Series.IndexOf("Accel (g)") >= 0) DragChart.Series["Accel (g)"].Points.Clear();
                DragChart.Invalidate();
            }

            if (PeakGLabel != null) PeakGLabel.Text = "Peak G: ---";
            if (SpeedRangeTimeLabels != null)
            {
                foreach (var lbl in SpeedRangeTimeLabels)
                {
                    if (lbl != null) lbl.Text = "--.--- s";
                }
            }
        }

        public void ClearDragSplits()
        {
            if (SplitTimeLabels != null && SplitSpeedLabels != null)
            {
                for (int i = 0; i < SplitTimeLabels.Length; i++)
                {
                    if (SplitTimeLabels[i] != null) SplitTimeLabels[i].Text = "--.---";
                    if (SplitSpeedLabels[i] != null) SplitSpeedLabels[i].Text = "---";
                    if (SplitTimeLabels[i] != null) SplitTimeLabels[i].ForeColor = ThemeManager.getThemeState() ? Color.White : Color.Black;
                }
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (Model == null) return;

            if (TimerLabel != null) TimerLabel.Text = DragStrip.FormatTime(Model.ElapsedSeconds);
            if (SpeedLabel != null) SpeedLabel.Text = $"{Model.CurrentSpeedKmh:F1} km/h";
            if (DistanceLabel != null) DistanceLabel.Text = DragStrip.FormatDistance(Model.DistanceMeters);

            if (GearLabel != null)
                GearLabel.Text = Model.CurrentGear == -1 ? "N" : (Model.CurrentGear + 1).ToString();

            if (RPMLabel != null) RPMLabel.Text = $"{Model.CurrentRPM:F0} RPM";

            if (Model.LastRun != null)
            {
                if (SplitTimeLabels != null && SplitSpeedLabels != null)
                {
                    for (int i = 0; i < Model.LastRun.Splits.Length && i < SplitTimeLabels.Length; i++)
                    {
                        var split = Model.LastRun.Splits[i];
                        if (split.Hit && SplitTimeLabels[i] != null)
                        {
                            SplitTimeLabels[i].Text = DragStrip.FormatTime(split.ElapsedTime);
                            SplitSpeedLabels[i].Text = $"{DragStrip.FormatSpeed(split.SpeedKmh)} km/h";
                            SplitTimeLabels[i].ForeColor = ThemeManager.getThemeState() ? Color.White : Color.Black;
                        }
                    }
                }

                if (SpeedRangeTimeLabels != null)
                {
                    for (int i = 0; i < Model.LastRun.SpeedRanges.Length && i < SpeedRangeTimeLabels.Length; i++)
                    {
                        var sr = Model.LastRun.SpeedRanges[i];
                        if (sr.Finished && SpeedRangeTimeLabels[i] != null)
                        {
                            SpeedRangeTimeLabels[i].Text = DragStrip.FormatTime(sr.ElapsedTime) + " s";
                        }
                    }
                }
            }

            if (Model.Running)
            {
                if (StatusLabel != null)
                {
                    StatusLabel.Text = "RUNNING";
                    StatusLabel.ForeColor = Color.FromArgb(0, 160, 0);
                }
            }
            else if (Model.LastRun != null && Model.LastRun.Finished && !Model.Staged)
            {
                if (StatusLabel != null && StatusLabel.Text != "FINISHED" && StatusLabel.Text != "ABORTED")
                {
                    StatusLabel.Text = "FINISHED";
                    StatusLabel.ForeColor = Color.MediumBlue;
                    if (StageBtn != null) StageBtn.Enabled = true;
                    if (GoBtn != null) GoBtn.Enabled = false;
                    if (StopBtn != null) StopBtn.Enabled = false;
                    UpdateDragResults();
                }
            }

            if (Model.BestRun != null && Model.BestRun.Finished && BestSplitTimeLabels != null)
            {
                for (int i = 0; i < Model.BestRun.Splits.Length && i < BestSplitTimeLabels.Length; i++)
                {
                    var split = Model.BestRun.Splits[i];
                    if (split.Hit && BestSplitTimeLabels[i] != null)
                    {
                        BestSplitTimeLabels[i].Text = DragStrip.FormatTime(split.ElapsedTime);
                    }
                }
            }

            if (Model.ElapsedSeconds > 0 && NoDataMsg != null && NoDataMsg.Visible)
            {
                NoDataMsg.Visible = false;
            }

            // Real-time chart update if there's enough data
            if (Model.Running && Model.LoggedDistances.Count > 0)
            {
                if (DragChart != null && DragChart.Series.IndexOf("Speed") >= 0)
                {
                    double currentDist = Model.LoggedDistances.Last();
                    double currentSpeed = Model.LoggedSpeeds.Last();

                    // Only append new points to avoid rebinding thousands of items per frame
                    var sSpeed = DragChart.Series["Speed"];
                    if (sSpeed.Points.Count == 0 || currentDist > sSpeed.Points.Last().XValue)
                    {
                        sSpeed.Points.AddXY(currentDist, currentSpeed);
                    }
                    
                    var sAccel = DragChart.Series.IndexOf("Accel (g)") >= 0 ? DragChart.Series["Accel (g)"] : null;
                    if (sAccel != null && Model.LoggedAccelG.Count > 0)
                    {
                        // Calculate a quick moving average specifically for the new point
                        int windowSize = 10;
                        int count = Model.LoggedAccelG.Count;
                        int start = Math.Max(0, count - windowSize);
                        double sum = 0;
                        for (int i = start; i < count; i++) sum += Model.LoggedAccelG[i];
                        double smoothedG = sum / (count - start);

                        if (sAccel.Points.Count == 0 || currentDist > sAccel.Points.Last().XValue)
                        {
                            sAccel.Points.AddXY(currentDist, smoothedG);
                        }
                    }

                    // Dynamically adjust axes
                    if (currentDist > DragChart.ChartAreas[0].AxisX.Maximum)
                    {
                        DragChart.ChartAreas[0].AxisX.Maximum = Math.Ceiling((currentDist + 100) / 100) * 100;
                    }

                    double newMaxY = Math.Ceiling((currentSpeed + 5) / 10) * 10;
                    if (newMaxY > DragChart.ChartAreas[0].AxisY.Maximum)
                    {
                        DragChart.ChartAreas[0].AxisY.Maximum = newMaxY;
                    }
                    
                    DragChart.Invalidate();
                }
            }
        }

        private void UpdateDragResults()
        {
            if (DragChart != null && Model.LoggedDistances.Count > 0 && Model.LoggedSpeeds.Count > 0)
            {
                if (DragChart.Series.IndexOf("Speed") >= 0)
                    DragChart.Series["Speed"].Points.DataBindXY(Model.LoggedDistances, Model.LoggedSpeeds);

                var sAccel = DragChart.Series.IndexOf("Accel (g)") >= 0 ? DragChart.Series["Accel (g)"] : null;
                if (sAccel != null) sAccel.Points.Clear();

                if (sAccel != null && Model.LoggedAccelG.Count > 0)
                {
                    var smoothedG = DragStrip.SmoothAccelG(Model.LoggedAccelG, 10);
                    for (int i = 0; i < smoothedG.Count && i < Model.LoggedDistances.Count; i++)
                    {
                        sAccel.Points.AddXY(Model.LoggedDistances[i], smoothedG[i]);
                    }

                    double maxG = 0;
                    foreach (var g in smoothedG) if (g > maxG) maxG = g;
                    double newMaxG = Math.Ceiling((maxG + 0.1) * 10) / 10.0;
                    if (newMaxG < 0.5) newMaxG = 0.5;
                    if (newMaxG > 5.0) newMaxG = 5.0;
                    DragChart.ChartAreas[0].AxisY2.Maximum = newMaxG;
                }

                double maxY = Model.LoggedSpeeds.Max();
                double newMaxY = Math.Ceiling((maxY + 5) / 10) * 10;
                DragChart.ChartAreas[0].AxisY.Maximum = newMaxY > 0 ? newMaxY : 100;
                DragChart.ChartAreas[0].AxisX.Maximum = Model.SelectedDistance;
                DragChart.Invalidate();
            }

            if (PeakGLabel != null)
            {
                double peakG = Model.PeakAccelG;
                PeakGLabel.Text = peakG > 0 ? $"Peak G: {peakG:F2} g" : "Peak G: ---";
            }

            if (Model.LastRun != null && SpeedRangeTimeLabels != null)
            {
                for (int i = 0; i < Model.LastRun.SpeedRanges.Length && i < SpeedRangeTimeLabels.Length; i++)
                {
                    var sr = Model.LastRun.SpeedRanges[i];
                    if (sr.Finished)
                    {
                        if (SpeedRangeTimeLabels[i] != null) SpeedRangeTimeLabels[i].Text = DragStrip.FormatTime(sr.ElapsedTime) + " s";
                    }
                    else if (sr.Started)
                    {
                        if (SpeedRangeTimeLabels[i] != null) SpeedRangeTimeLabels[i].Text = "DNF";
                    }
                }
            }

            if (Model.LastRun != null && Model.LastRun.Finished)
            {
                var mainSplit = GetMainSplit(Model.LastRun);
                if (mainSplit != null && mainSplit.Hit)
                {
                    if (LastTimeLabel != null) LastTimeLabel.Text = $"ET: {DragStrip.FormatTime(mainSplit.ElapsedTime)} s";
                    if (LastSpeedLabel != null) LastSpeedLabel.Text = $"Trap: {DragStrip.FormatSpeed(mainSplit.SpeedKmh)} km/h";
                }
                else
                {
                    if (LastTimeLabel != null) LastTimeLabel.Text = "ET: DNF";
                    if (LastSpeedLabel != null) LastSpeedLabel.Text = $"Trap: {DragStrip.FormatSpeed(Model.CurrentSpeedKmh)} km/h";
                }
            }

            if (Model.BestRun != null && Model.BestRun.Finished)
            {
                var mainSplit = GetMainSplit(Model.BestRun);
                if (mainSplit != null && mainSplit.Hit)
                {
                    if (BestTimeLabel != null) BestTimeLabel.Text = $"ET: {DragStrip.FormatTime(mainSplit.ElapsedTime)} s";
                    if (BestSpeedLabel != null) BestSpeedLabel.Text = $"Trap: {DragStrip.FormatSpeed(mainSplit.SpeedKmh)} km/h";
                }
            }
        }

        private DragSplit GetMainSplit(DragStripResult result)
        {
            foreach (var s in result.Splits)
                if (s.DistanceMeters == Model.SelectedDistance) return s;
            DragSplit last = null;
            foreach (var s in result.Splits)
                if (s.Hit) last = s;
            return last;
        }

        public void BuildProgrammaticUI(TabPage tabPage)
        {
            int x = 20, y = 15;
            int labelH = 22;
            Color headerColor = Color.FromArgb(50, 50, 50);
            Color accentColor = Color.MediumBlue;
            Color goodColor = Color.FromArgb(0, 160, 0);

            StatusLabel = new Label { Text = "IDLE", Location = new Point(x, y), AutoSize = false, Size = new Size(300, 32), Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Gray, BackColor = Color.Transparent };
            tabPage.Controls.Add(StatusLabel);
            y += 40;

            DistanceCombo = new ComboBox { Location = new Point(x, y), Size = new Size(140, 24), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
            DistanceCombo.Items.AddRange(new string[] { "1/4 Mile (1320 ft)", "1/2 Mile", "1 Mile" });
            tabPage.Controls.Add(DistanceCombo);
            y += 32;

            StageBtn = new RoundButton { Text = "STAGE", Location = new Point(x, y), Size = new Size(110, 50), CircleColor = Color.FromArgb(200, 160, 0), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.Transparent };
            tabPage.Controls.Add(StageBtn);

            GoBtn = new RoundButton { Text = "GO!", Location = new Point(x + 120, y), Size = new Size(95, 50), CircleColor = goodColor, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Enabled = false, BackColor = Color.Transparent };
            tabPage.Controls.Add(GoBtn);

            StopBtn = new RoundButton { Text = "ABORT", Location = new Point(x + 225, y), Size = new Size(100, 50), CircleColor = Color.FromArgb(180, 30, 30), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Enabled = false, BackColor = Color.Transparent };
            tabPage.Controls.Add(StopBtn);

            ResetBtn = new RoundButton { Text = "RESET", Location = new Point(x + 330, y), Size = new Size(100, 50), CircleColor = accentColor, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.Transparent };
            tabPage.Controls.Add(ResetBtn);
            y += 56;

            TimerLabel = new Label { Text = "0.000", Location = new Point(x, y), AutoSize = false, Size = new Size(240, 60), Font = new Font("Consolas", 36, FontStyle.Bold), ForeColor = headerColor, BackColor = Color.Transparent };
            tabPage.Controls.Add(TimerLabel);

            SpeedLabel = new Label { Text = "0.0 km/h", Location = new Point(x + 260, y), AutoSize = false, Size = new Size(160, 24), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.DimGray, BackColor = Color.Transparent };
            tabPage.Controls.Add(SpeedLabel);

            GearLabel = new Label { Text = "N", Location = new Point(x + 260, y + 28), AutoSize = false, Size = new Size(60, 24), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.DimGray, BackColor = Color.Transparent };
            tabPage.Controls.Add(GearLabel);

            RPMLabel = new Label { Text = "0 RPM", Location = new Point(x + 300, y + 28), AutoSize = false, Size = new Size(150, 24), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.DimGray, BackColor = Color.Transparent };
            tabPage.Controls.Add(RPMLabel);

            DistanceLabel = new Label { Text = "0 m", Location = new Point(x + 450, y), AutoSize = false, Size = new Size(140, 24), Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.DimGray, BackColor = Color.Transparent };
            tabPage.Controls.Add(DistanceLabel);
            y += 65;

            tabPage.Controls.Add(new Label { Text = "Split", Location = new Point(x, y), AutoSize = false, Size = new Size(90, labelH), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = headerColor, BackColor = Color.Transparent });
            tabPage.Controls.Add(new Label { Text = "Time (s)", Location = new Point(x + 100, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = headerColor, BackColor = Color.Transparent });
            tabPage.Controls.Add(new Label { Text = "Speed", Location = new Point(x + 210, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = headerColor, BackColor = Color.Transparent });
            tabPage.Controls.Add(new Label { Text = "Best Time", Location = new Point(x + 320, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = goodColor, BackColor = Color.Transparent });
            y += labelH + 4;

            string[] splitNames = { "60 ft", "330 ft", "1/8 mi", "1000 ft", "1/4 mi", "1/2 mi", "1 mi" };
            SplitNameLabels = new Label[splitNames.Length];
            SplitTimeLabels = new Label[splitNames.Length];
            SplitSpeedLabels = new Label[splitNames.Length];
            BestSplitTimeLabels = new Label[splitNames.Length];

            for (int i = 0; i < splitNames.Length; i++)
            {
                SplitNameLabels[i] = new Label { Text = splitNames[i], Location = new Point(x, y), AutoSize = false, Size = new Size(90, labelH), Font = new Font("Segoe UI", 10), BackColor = Color.Transparent };
                tabPage.Controls.Add(SplitNameLabels[i]);

                SplitTimeLabels[i] = new Label { Text = "--.---", Location = new Point(x + 100, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Consolas", 10), BackColor = Color.Transparent };
                tabPage.Controls.Add(SplitTimeLabels[i]);

                SplitSpeedLabels[i] = new Label { Text = "---", Location = new Point(x + 210, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Consolas", 10), BackColor = Color.Transparent };
                tabPage.Controls.Add(SplitSpeedLabels[i]);

                BestSplitTimeLabels[i] = new Label { Text = "--.---", Location = new Point(x + 320, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Consolas", 10), ForeColor = goodColor, BackColor = Color.Transparent };
                tabPage.Controls.Add(BestSplitTimeLabels[i]);

                y += labelH + 4;
            }
            y += 12;

            tabPage.Controls.Add(new Label { Text = "Last Run", Location = new Point(x, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = headerColor, BackColor = Color.Transparent });
            y += labelH + 2;

            LastTimeLabel = new Label { Text = "ET: --.--- s", Location = new Point(x + 10, y), AutoSize = false, Size = new Size(180, labelH), Font = new Font("Segoe UI", 10), BackColor = Color.Transparent };
            tabPage.Controls.Add(LastTimeLabel);

            LastSpeedLabel = new Label { Text = "Trap: --- km/h", Location = new Point(x + 200, y), AutoSize = false, Size = new Size(180, labelH), Font = new Font("Segoe UI", 10), BackColor = Color.Transparent };
            tabPage.Controls.Add(LastSpeedLabel);
            y += labelH + 8;

            tabPage.Controls.Add(new Label { Text = "Best Run", Location = new Point(x, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = goodColor, BackColor = Color.Transparent });
            y += labelH + 2;

            BestTimeLabel = new Label { Text = "ET: --.--- s", Location = new Point(x + 10, y), AutoSize = false, Size = new Size(180, labelH), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = goodColor, BackColor = Color.Transparent };
            tabPage.Controls.Add(BestTimeLabel);

            BestSpeedLabel = new Label { Text = "Trap: --- km/h", Location = new Point(x + 200, y), AutoSize = false, Size = new Size(180, labelH), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = goodColor, BackColor = Color.Transparent };
            tabPage.Controls.Add(BestSpeedLabel);
            y += labelH + 12;

            Color rangeColor = Color.FromArgb(180, 80, 0);
            tabPage.Controls.Add(new Label { Text = "Speed Ranges", Location = new Point(x, y), AutoSize = false, Size = new Size(150, labelH), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = rangeColor, BackColor = Color.Transparent });
            y += labelH + 4;

            string[] srNames = { "0-60 mph", "0-100 km/h", "60-130 mph", "100-200 km/h", "0-100 mph", "0-200 km/h" };
            SpeedRangeNameLabels = new Label[srNames.Length];
            SpeedRangeTimeLabels = new Label[srNames.Length];

            for (int i = 0; i < srNames.Length; i++)
            {
                SpeedRangeNameLabels[i] = new Label { Text = srNames[i], Location = new Point(x, y), AutoSize = false, Size = new Size(110, labelH), Font = new Font("Segoe UI", 9), ForeColor = rangeColor, BackColor = Color.Transparent };
                tabPage.Controls.Add(SpeedRangeNameLabels[i]);

                SpeedRangeTimeLabels[i] = new Label { Text = "--.--- s", Location = new Point(x + 120, y), AutoSize = false, Size = new Size(100, labelH), Font = new Font("Consolas", 10), ForeColor = rangeColor, BackColor = Color.Transparent };
                tabPage.Controls.Add(SpeedRangeTimeLabels[i]);

                y += labelH + 2;
            }
            y += 6;

            PeakGLabel = new Label { Text = "Peak G: ---", Location = new Point(x, y), AutoSize = false, Size = new Size(200, labelH), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(0, 100, 180), BackColor = Color.Transparent };
            tabPage.Controls.Add(PeakGLabel);

            DragChart = new Chart { Dock = DockStyle.Right, Width = tabPage.Width - 450, BackColor = tabPage.BackColor };
            var area = new ChartArea { Name = "SpeedVsDistance" };
            area.Position = new ElementPosition(0, 0, 90, 100);
            area.InnerPlotPosition = new ElementPosition(5, 5, 85, 87);
            area.AxisX.Title = "Distance (m)";
            area.AxisX.Minimum = 0;
            area.AxisY.Title = "Speed (km/h)";
            area.AxisY.Minimum = 0;
            area.AxisX.MajorGrid.Enabled = true;
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            area.AxisY.MajorGrid.Enabled = true;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            DragChart.ChartAreas.Add(area);

            DragChart.Series.Add(new Series { Name = "Speed", ChartType = SeriesChartType.Line, BorderWidth = 2, Color = Color.Red, ChartArea = "SpeedVsDistance" });
            DragChart.Series.Add(new Series { Name = "Accel (g)", ChartType = SeriesChartType.Line, BorderWidth = 2, Color = Color.FromArgb(0, 100, 180), ChartArea = "SpeedVsDistance", YAxisType = AxisType.Secondary });

            area.AxisY2.Title = "Acceleration (g)";
            area.AxisY2.Minimum = 0;
            area.AxisY2.Maximum = 5;
            area.AxisY2.Enabled = AxisEnabled.True;
            area.AxisY2.MajorGrid.Enabled = false;
            area.AxisY2.LineColor = Color.FromArgb(0, 100, 180);
            area.AxisY2.LabelStyle.ForeColor = Color.FromArgb(0, 100, 180);
            area.AxisY2.TitleForeColor = Color.FromArgb(0, 100, 180);

            DragChart.Legends.Add(new Legend { Name = "Legend1", Docking = Docking.Top });
            tabPage.Controls.Add(DragChart);
            DragChart.BringToFront();

            NoDataMsg = new Label { Text = "NO DATA", Location = new Point(500, 220), AutoSize = false, Size = new Size(400, 80), Font = new Font("Segoe UI", 48), ForeColor = Color.Black, BackColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Visible = true };
            tabPage.Controls.Add(NoDataMsg);
            NoDataMsg.BringToFront();
        }
    }
}
