using System;
using System.Diagnostics;

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

    public class DragStripResult
    {
        public DragSplit[] Splits { get; set; }
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
        }
    }

    public class DragStrip
    {
        private Stopwatch timer;
        private double lastTimestamp;
        private double distanceMeters;

        // Live readouts
        public bool Running { get; private set; }
        public bool Staged { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double DistanceMeters { get { return distanceMeters; } }
        public double CurrentSpeedKmh { get; private set; }
        public int CurrentGear { get; private set; }
        public double CurrentRPM { get; private set; }

        // Best run
        public DragStripResult LastRun { get; private set; }
        public DragStripResult BestRun { get; private set; }

        // Settings
        public int SelectedDistance { get; set; } = 402; // default 1/4 mile

        public DragStrip()
        {
            timer = new Stopwatch();
            LastRun = new DragStripResult(SelectedDistance);
            BestRun = new DragStripResult(SelectedDistance);
        }

        public void Stage()
        {
            if (Running) return;
            Staged = true;
            Reset();
            LastRun = new DragStripResult(SelectedDistance);
        }

        public void Go()
        {
            if (!Staged || Running) return;
            Running = true;
            timer.Restart();
            lastTimestamp = 0;
            distanceMeters = 0;
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

            if (!Running) return;

            double now = timer.Elapsed.TotalSeconds;
            double dt = now - lastTimestamp;
            lastTimestamp = now;

            if (dt <= 0 || dt > 0.5) return; // guard against bad frames

            // Integrate distance: meters += speed(m/s) * dt
            distanceMeters += speedMps * dt;
            ElapsedSeconds = now;

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
            return clone;
        }

        public void Stop()
        {
            if (Running)
            {
                Running = false;
                Staged = false;
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
            timer.Reset();
            distanceMeters = 0;
            ElapsedSeconds = 0;
            lastTimestamp = 0;
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
    }
}
