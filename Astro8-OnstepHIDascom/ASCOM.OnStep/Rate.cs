using System.Runtime.InteropServices;
using ASCOM.DeviceInterface;

namespace ASCOM.OnStep;

[Guid("8db54a4c-4dcb-4600-8535-6c06445e892f")]
[ClassInterface(ClassInterfaceType.None)]
[ComVisible(true)]
public class Rate : IRate
{
	private double maximum;

	private double minimum;

	public double Maximum
	{
		get
		{
			return maximum;
		}
		set
		{
			maximum = value;
		}
	}

	public double Minimum
	{
		get
		{
			return minimum;
		}
		set
		{
			minimum = value;
		}
	}

	internal Rate(double minimum, double maximum)
	{
		this.maximum = maximum;
		this.minimum = minimum;
	}

	public void Dispose()
	{
	}
}
