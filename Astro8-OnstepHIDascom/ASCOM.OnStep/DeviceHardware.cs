using ASCOM.Utilities;

namespace ASCOM.OnStep;

public class DeviceHardware
{
	internal Serial Serial;

	internal int count { get; set; }

	internal DeviceHardware()
	{
		Serial = new Serial();
	}

	~DeviceHardware()
	{
		Serial.Dispose();
	}
}
