using System.Collections;
using System.Runtime.InteropServices;
using ASCOM.DeviceInterface;

namespace ASCOM.OnStep;

[Guid("86dca084-0f63-4a51-be69-6e568bbd6f4e")]
[ClassInterface(ClassInterfaceType.None)]
[ComVisible(true)]
public class AxisRates : IAxisRates, IEnumerable
{
	private TelescopeAxes axis;

	private readonly Rate[] rates;

	public int Count => rates.Length;

	public IRate this[int index] => rates[index - 1];

	internal AxisRates(TelescopeAxes axis)
	{
		this.axis = axis;
		switch (axis)
		{
		case TelescopeAxes.axisPrimary:
			rates = new Rate[1]
			{
				new Rate(0.008334, 2.0)
			};
			break;
		case TelescopeAxes.axisSecondary:
			rates = new Rate[1]
			{
				new Rate(0.008334, 2.0)
			};
			break;
		case TelescopeAxes.axisTertiary:
			rates = new Rate[0];
			break;
		}
	}

	public void Dispose()
	{
	}

	public IEnumerator GetEnumerator()
	{
		return rates.GetEnumerator();
	}
}
