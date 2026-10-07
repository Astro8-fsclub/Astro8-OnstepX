using System;
using System.Collections;
using System.Runtime.InteropServices;
using ASCOM.DeviceInterface;

namespace ASTRO8_OnStepHID
{
    /// <summary>TrackingRates 集合：恒星/太阴/太阳/King 四种。</summary>
    [ComVisible(false)]
    public sealed class TrackingRates : ITrackingRates
    {
        private static readonly DriveRates[] Rates =
        {
            DriveRates.driveSidereal,
            DriveRates.driveLunar,
            DriveRates.driveSolar,
            DriveRates.driveKing,
        };

        public int Count { get { return Rates.Length; } }

        public DriveRates this[int index] { get { return Rates[index]; } }

        public IEnumerator GetEnumerator() { return Rates.GetEnumerator(); }

        public void Dispose() { }
    }

    /// <summary>AxisRates 集合：MoveAxis 的速率范围（deg/s，OnStep 以导星速率移动，范围取 ±2）。</summary>
    [ComVisible(false)]
    public sealed class AxisRates : IAxisRates
    {
        private static readonly Rate[] Rates = { new Rate(-2.0, 2.0) };

        public int Count { get { return Rates.Length; } }

        public IRate this[int index] { get { return Rates[index]; } }

        public IEnumerator GetEnumerator() { return Rates.GetEnumerator(); }

        public void Dispose() { }
    }

    /// <summary>单个速率范围。</summary>
    [ComVisible(false)]
    public sealed class Rate : IRate
    {
        public Rate(double min, double max) { Minimum = min; Maximum = max; }
        public double Maximum { get; set; }
        public double Minimum { get; set; }
        public void Dispose() { }
    }
}
