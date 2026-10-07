using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using ASCOM.DeviceInterface;

namespace ASCOM.OnStep;

[Guid("b2c22d90-2eb1-46c7-bf7d-cffac702156e")]
[ClassInterface(ClassInterfaceType.None)]
[ComVisible(true)]
public class TrackingRates : ITrackingRates, IEnumerable, IEnumerator
{
	private readonly DriveRates[] trackingRates;

	private readonly ThreadLocal<int> pos = new ThreadLocal<int>(() => -1);

	private static readonly object lockObj = new object();

	public int Count => trackingRates.Length;

	public DriveRates this[int index] => trackingRates[index - 1];

	public object Current
	{
		get
		{
			lock (lockObj)
			{
				if (pos.Value < 0 || pos.Value >= trackingRates.Length)
				{
					throw new System.InvalidOperationException();
				}
				return trackingRates[pos.Value];
			}
		}
	}

	internal TrackingRates()
	{
		trackingRates = new DriveRates[4]
		{
			DriveRates.driveSidereal,
			DriveRates.driveKing,
			DriveRates.driveLunar,
			DriveRates.driveSolar
		};
	}

	public IEnumerator GetEnumerator()
	{
		pos.Value = -1;
		return this;
	}

	public void Dispose()
	{
	}

	public bool MoveNext()
	{
		lock (lockObj)
		{
			if (++pos.Value >= trackingRates.Length)
			{
				return false;
			}
			return true;
		}
	}

	public void Reset()
	{
		pos.Value = -1;
	}
}
