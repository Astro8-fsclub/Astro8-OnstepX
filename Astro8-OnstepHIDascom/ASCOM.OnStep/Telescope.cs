using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;

namespace ASCOM.OnStep;

[Guid("1829364f-690b-4138-b455-17d891d19bea")]
[ProgId("ASCOM.Astro8OnstepHIDascom.Telescope")]
[ServedClassName("OnStep Telescope")]
[ClassInterface(ClassInterfaceType.None)]
public class Telescope : ITelescopeV3
{
	private enum CommandErrors
	{
		CE_NONE,
		CE_0,
		CE_CMD_UNKNOWN,
		CE_REPLY_UNKNOWN,
		CE_PARAM_RANGE,
		CE_PARAM_FORM,
		CE_ALIGN_FAIL,
		CE_ALIGN_NOT_ACTIVE,
		CE_NOT_PARKED_OR_AT_HOME,
		CE_PARKED,
		CE_PARK_FAILED,
		CE_NOT_PARKED,
		CE_NO_PARK_POSITION_SET,
		CE_GOTO_FAIL,
		CE_LIBRARY_FULL,
		CE_GOTO_ERR_BELOW_HORIZON,
		CE_GOTO_ERR_ABOVE_OVERHEAD,
		CE_SLEW_ERR_IN_STANDBY,
		CE_SLEW_ERR_IN_PARK,
		CE_GOTO_ERR_GOTO,
		CE_GOTO_ERR_OUTSIDE_LIMITS,
		CE_SLEW_ERR_HARDWARE_FAULT,
		CE_MOUNT_IN_MOTION,
		CE_GOTO_ERR_UNSPECIFIED,
		CE_NULL
	}

	public string s_csDriverID = "ASCOM.OnStep.Telescope";

	internal string driverID = "ASCOM.OnStep.Telescope";

	internal static string comPort;

	internal static int baudRate;

	internal static string ipAddress;

	internal static string hidVid = "1A86";

	internal static string hidPid = "55D4";

	internal static string hidSerial = "A8-0001";

	internal static bool dtrControl = false;

	internal static bool errorCorrection = false;

	internal static int readTimeout = 3;

	internal static bool autoDateTime = false;

	internal static double apertureArea = 0.0;

	internal static double apertureDiameter = 0.0;

	internal static double focalLength = 0.0;

	internal static double siteElevation = 0.0;

	internal double LastTargetRightAscension = double.NaN;

	internal double LastTargetDeclination = double.NaN;

	private bool connectedState;

	internal string currentComPort;

	private Util utilities;

	private AstroUtils astroUtilities;

	internal TraceLogger tl;

	private static readonly object lockObject = new object();

	static Telescope()
	{
		Compatibility.Ensure();
	}

	private volatile int accessCount;

	public ArrayList SupportedActions
	{
		get
		{
			tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
			return new ArrayList();
		}
	}

	public bool Connected
	{
		get
		{
			LogMessage("Connected", "Get {0}", IsConnected);
			return IsConnected;
		}
		set
		{
			tl.LogMessage("Connected", "Set {0}", value);
			if (value == IsConnected)
			{
				return;
			}
			if (value)
			{
				currentComPort = comPort;
				if (currentComPort == "IP Address")
				{
					currentComPort = ipAddress;
				}
				LogMessage("Connected Set", "Connecting to port {0}", currentComPort);
				if (SharedResources.Connect(currentComPort, (SerialSpeed)baudRate, readTimeout, dtrControl))
				{
					connectedState = true;
					if (autoDateTime)
					{
						DateTime utcNow = DateTime.UtcNow;
						UTCDate = utcNow;
					}
				}
			}
			else
			{
				connectedState = false;
				LogMessage("Connected Set", "Disconnecting from port {0}", currentComPort);
				SharedResources.Disconnect(currentComPort);
				currentComPort = "";
			}
		}
	}

	public string Description
	{
		get
		{
			if (!IsConnected)
			{
				throw new NotConnectedException();
			}
			string text = CommandString(":GVP#", raw: true).TrimEnd('#') + " " + CommandString(":GVN#", raw: true).TrimEnd('#');
			tl.LogMessage("Description Get", text);
			return text;
		}
	}

	public string DriverInfo
	{
		get
		{
			string text = "On-Step Unified ASCOM Driver: Telescope, version " + DriverVersion;
			tl.LogMessage("DriverInfo Get", text);
			return text;
		}
	}

	public string DriverVersion
	{
		get
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			string text = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
			tl.LogMessage("DriverVersion Get", text);
			return text;
		}
	}

	public short InterfaceVersion
	{
		get
		{
			LogMessage("InterfaceVersion Get", "3");
			return Convert.ToInt16("3");
		}
	}

	public string Name
	{
		get
		{
			string text = CommandString(":GVP#", raw: true).TrimEnd('#');
			tl.LogMessage("Name Get", text);
			return text;
		}
	}

	public AlignmentModes AlignmentMode
	{
		get
		{
			if (SharedResources.mountTypeAltAzm)
			{
				tl.LogMessage("AlignmentMode Get", "algAltAz");
				return AlignmentModes.algAltAz;
			}
			if (SharedResources.mountTypeGEM)
			{
				tl.LogMessage("AlignmentMode Get", "algGermanPolar");
				return AlignmentModes.algGermanPolar;
			}
			tl.LogMessage("AlignmentMode Get", "algPolar");
			return AlignmentModes.algPolar;
		}
	}

	public double Altitude
	{
		get
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			double num4 = 0.0;
			string text = CommandString(":GA#", raw: true).TrimEnd('#');
			tl.LogMessage("Altitude Get", text);
			try
			{
				if (text.Length != 9)
				{
					throw new FormatException();
				}
				if (text.Substring(0, 1) != "-" && text.Substring(0, 1) != "+")
				{
					throw new FormatException();
				}
				if (text.Substring(3, 1) != "*" || text.Substring(6, 1) != ":")
				{
					throw new FormatException();
				}
				num3 = Convert.ToInt32(text.Substring(1, 2));
				num2 = Convert.ToInt32(text.Substring(4, 2));
				num = Convert.ToInt32(text.Substring(7, 2));
				if (num3 < 0 || num3 > 90 || num2 < 0 || num2 > 59 || num < 0 || num > 59)
				{
					throw new ArgumentOutOfRangeException();
				}
			}
			catch (FormatException)
			{
				throw new InvalidValueException("Altitude, unexpected value: " + text);
			}
			catch (ArgumentOutOfRangeException)
			{
				throw new InvalidValueException("Altitude, unexpected value: " + text);
			}
			num4 = (double)num3 + (double)num2 / 60.0 + (double)num / 3600.0;
			if (text.Substring(0, 1) == "-")
			{
				num4 = 0.0 - num4;
			}
			return num4;
		}
	}

	public double ApertureArea
	{
		get
		{
			tl.LogMessage("ApertureArea", "Get");
			return apertureArea;
		}
	}

	public double ApertureDiameter
	{
		get
		{
			tl.LogMessage("ApertureDiameter", "Get");
			return apertureDiameter;
		}
	}

	public bool AtHome
	{
		get
		{
			string text = CommandString(":GU#", raw: true).TrimEnd('#');
			tl.LogMessage("AtHome Get", text);
			bool result = false;
			if (text.IndexOf("H") >= 0)
			{
				result = true;
			}
			return result;
		}
	}

	public bool AtPark
	{
		get
		{
			string text = CommandString(":GU#", raw: true).TrimEnd('#');
			tl.LogMessage("AtPark Get", text);
			bool result = false;
			if (text.IndexOf('P') >= 0)
			{
				result = true;
			}
			return result;
		}
	}

	public double Azimuth
	{
		get
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			string text = CommandString(":GZ#", raw: true).TrimEnd('#');
			tl.LogMessage("Azimuth Get", text);
			try
			{
				if (text.Length != 9)
				{
					throw new FormatException();
				}
				if (text.Substring(3, 1) != "*" || text.Substring(6, 1) != ":")
				{
					throw new FormatException();
				}
				num3 = Convert.ToInt32(text.Substring(0, 3));
				num2 = Convert.ToInt32(text.Substring(4, 2));
				num = Convert.ToInt32(text.Substring(7, 2));
				if (num3 < 0 || num3 > 360 || num2 < 0 || num2 > 59 || num < 0 || num > 59)
				{
					throw new ArgumentOutOfRangeException();
				}
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("Azimuth, unexpected value: " + text);
			}
			catch (ArgumentOutOfRangeException)
			{
				throw new InvalidValueException("Azimuth, unexpected value: " + text);
			}
			return (double)num3 + (double)num2 / 60.0 + (double)num / 3600.0;
		}
	}

	public bool CanFindHome => true;

	public bool CanPark => true;

	public bool CanPulseGuide => true;

	public bool CanSetDeclinationRate => false;

	public bool CanSetGuideRates => true;

	public bool CanSetPark => true;

	public bool CanSetPierSide
	{
		get
		{
			if (SharedResources.versionOnStep >= 501 && SharedResources.mountTypeGEM)
			{
				return true;
			}
			return false;
		}
	}

	public bool CanSetRightAscensionRate => false;

	public bool CanSetTracking => true;

	public bool CanSlew => true;

	public bool CanSlewAltAz => true;

	public bool CanSlewAltAzAsync => true;

	public bool CanSlewAsync => true;

	public bool CanSync => true;

	public bool CanSyncAltAz => false;

	public bool CanUnpark => true;

	public double Declination
	{
		get
		{
			double num = 0.0;
			int num2 = 0;
			int num3 = 0;
			double num4 = 0.0;
			string text = ((SharedResources.versionOnStep <= 121) ? CommandString(":GD#", raw: true).TrimEnd('#') : CommandString(":GDe#", raw: true).TrimEnd('#'));
			tl.LogMessage("Declination Get", text);
			try
			{
				if (text.Substring(0, 1) != "-" && text.Substring(0, 1) != "+")
				{
					throw new FormatException();
				}
				if (text.Substring(3, 1) != "*" || (text.Substring(6, 1) != ":" && text.Substring(6, 1) != "'" && text.Substring(6, 1) != "\0x0df"))
				{
					throw new FormatException();
				}
				num3 = Convert.ToInt32(text.Substring(1, 2));
				num2 = Convert.ToInt32(text.Substring(4, 2));
				num = Convert.ToDouble(text.Substring(7), CultureInfo.GetCultureInfo("en-us"));
				if (num3 < 0 || num3 > 90 || num2 < 0 || num2 > 59 || num < 0.0 || num > 59.99999)
				{
					throw new ArgumentOutOfRangeException();
				}
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("Declination, unexpected value: " + text);
			}
			catch (ArgumentOutOfRangeException)
			{
				throw new InvalidOperationException("Declination, unexpected value: " + text);
			}
			num4 = (double)num3 + (double)num2 / 60.0 + num / 3600.0;
			if (text.Substring(0, 1) == "-")
			{
				num4 = 0.0 - num4;
			}
			return num4;
		}
	}

	public double DeclinationRate
	{
		get
		{
			double result = 0.0;
			tl.LogMessage("DeclinationRate Get", result.ToString());
			return result;
		}
		set
		{
			tl.LogMessage("DeclinationRate Set", "Not implemented");
			throw new PropertyNotImplementedException("DeclinationRate", accessorSet: true);
		}
	}

	public bool DoesRefraction
	{
		get
		{
			tl.LogMessage("DoesRefraction Get", (SharedResources.CoordMode != 0).ToString());
			return SharedResources.CoordMode != 0;
		}
		set
		{
			tl.LogMessage("DoesRefraction Set", "Not implemented");
			throw new PropertyNotImplementedException("DoesRefraction", accessorSet: true);
		}
	}

	public EquatorialCoordinateType EquatorialSystem
	{
		get
		{
			EquatorialCoordinateType result = EquatorialCoordinateType.equTopocentric;
			tl.LogMessage("DeclinationRate Get", result.ToString());
			return result;
		}
	}

	public double FocalLength
	{
		get
		{
			tl.LogMessage("FocalLength Get", focalLength.ToString());
			return focalLength;
		}
	}

	public double GuideRateDeclination
	{
		get
		{
			string text = CommandString(":GX90#", raw: true).TrimEnd('#');
			tl.LogMessage("GuideRateDeclination Get", text);
			double num;
			try
			{
				num = Convert.ToDouble(text, CultureInfo.GetCultureInfo("en-us")) * 1.0027;
			}
			catch
			{
				num = 0.0;
			}
			if (num < 0.01)
			{
				throw new InvalidValueException("GuideRateDeclination");
			}
			return num * (1.0 / 240.0);
		}
		set
		{
			tl.LogMessage("GuideRateDeclination Set", value.ToString());
			double num = ((!(value < 0.05)) ? value : (value / (1.0 / 240.0)));
			string text = ((num < 0.375) ? "0" : ((num < 0.75) ? "1" : ((num < 1.5) ? "2" : ((num < 3.0) ? "3" : ((num < 6.0) ? "4" : ((num < 12.0) ? "5" : ((num < 20.0) ? "6" : ((num < 32.0) ? "7" : ((!(num < 50.0)) ? "9" : "8")))))))));
			CommandBlind(":R" + text + "#", raw: true);
		}
	}

	public double GuideRateRightAscension
	{
		get
		{
			string text = CommandString(":GX90#", raw: true).TrimEnd('#');
			tl.LogMessage("GuideRateRightAscension Get", text);
			double num;
			try
			{
				num = Convert.ToDouble(text, CultureInfo.GetCultureInfo("en-us")) * 1.0027;
			}
			catch
			{
				num = 0.0;
			}
			if (num < 0.01)
			{
				throw new InvalidValueException("GuideRateRightAscension");
			}
			return num * (1.0 / 240.0);
		}
		set
		{
			tl.LogMessage("GuideRateRightAscension Set", value.ToString());
			double num = ((!(value < 0.05)) ? value : (value / (1.0 / 240.0)));
			string text = ((num < 0.375) ? "0" : ((num < 0.75) ? "1" : ((num < 1.5) ? "2" : ((num < 3.0) ? "3" : ((num < 6.0) ? "4" : ((num < 12.0) ? "5" : ((num < 20.0) ? "6" : ((num < 32.0) ? "7" : ((!(num < 50.0)) ? "9" : "8")))))))));
			CommandBlind(":R" + text + "#", raw: true);
		}
	}

	public bool IsPulseGuiding
	{
		get
		{
			string text = CommandString(":GU#", raw: true).TrimEnd('#');
			tl.LogMessage("IsPulseGuiding Get", text);
			return text.IndexOf("G") >= 0;
		}
	}

	public double RightAscension
	{
		get
		{
			double num = 0.0;
			int num2 = 0;
			int num3 = 0;
			string text = ((SharedResources.versionOnStep <= 121) ? CommandString(":GR#", raw: true).TrimEnd('#') : CommandString(":GRa#", raw: true).TrimEnd('#'));
			tl.LogMessage("RightAscension Get", text);
			try
			{
				if (text.Substring(2, 1) != ":" || text.Substring(5, 1) != ":")
				{
					throw new FormatException();
				}
				num3 = Convert.ToInt32(text.Substring(0, 2));
				num2 = Convert.ToInt32(text.Substring(3, 2));
				num = Convert.ToDouble(text.Substring(6), CultureInfo.GetCultureInfo("en-us"));
				if (num3 < 0 || num3 > 24 || num2 < 0 || num2 > 59 || num < 0.0 || num > 59.99999)
				{
					throw new ArgumentOutOfRangeException();
				}
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("RightAscension, unexpected value: " + text);
			}
			catch (ArgumentOutOfRangeException)
			{
				throw new InvalidOperationException("RightAscension, unexpected value: " + text);
			}
			return (double)num3 + (double)num2 / 60.0 + num / 3600.0;
		}
	}

	public double RightAscensionRate
	{
		get
		{
			double result = 0.0;
			tl.LogMessage("RightAscensionRate Get", result.ToString());
			return result;
		}
		set
		{
			tl.LogMessage("RightAscensionRate Set", "Not implemented");
			throw new PropertyNotImplementedException("RightAscensionRate", accessorSet: true);
		}
	}

	public PierSide SideOfPier
	{
		get
		{
			PierSide result = PierSide.pierUnknown;
			string text = CommandString(":Gm#", raw: true).TrimEnd('#');
			tl.LogMessage("SideOfPier Get", text);
			if (text.Substring(0, 1) == "E")
			{
				return result = PierSide.pierEast;
			}
			if (text.Substring(0, 1) == "W")
			{
				return result = PierSide.pierWest;
			}
			return result;
		}
		set
		{
			if (SharedResources.versionOnStep >= 501 && SharedResources.mountTypeGEM)
			{
				switch (value)
				{
				case PierSide.pierEast:
				{
					int num2 = CommandInt(":MNe#", raw: true);
					if (num2 != 0)
					{
						ThrowSlewErrorException(num2.ToString());
					}
					break;
				}
				case PierSide.pierWest:
				{
					int num = CommandInt(":MNw#", raw: true);
					if (num != 0)
					{
						ThrowSlewErrorException(num.ToString());
					}
					break;
				}
				default:
					tl.LogMessage("SideOfPier Set", "Unexpected value: " + value);
					throw new InvalidValueException("SideOfPier Set, unexpected value: " + value);
				}
				return;
			}
			tl.LogMessage("SideOfPier Set", "Not implemented");
			throw new PropertyNotImplementedException("SideOfPier", accessorSet: true);
		}
	}

	public double SiderealTime
	{
		get
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			double num4 = 0.0;
			string text = CommandString(":GS#", raw: true).TrimEnd('#');
			tl.LogMessage("SiderealTime Get", text);
			try
			{
				num3 = Convert.ToInt32(text.Substring(0, 2));
				num2 = Convert.ToInt32(text.Substring(3, 2));
				num = Convert.ToInt32(text.Substring(6, 2));
			}
			catch (FormatException)
			{
				throw new InvalidValueException("SiderealTime, unexpected value: " + text);
			}
			finally
			{
				num4 = (double)num3 + (double)num2 / 60.0 + (double)num / 3600.0;
			}
			return num4;
		}
	}

	public double SiteElevation
	{
		get
		{
			tl.LogMessage("SiteElevation Get", siteElevation.ToString());
			return siteElevation;
		}
		set
		{
			if (value < -300.0 || value > 10000.0)
			{
				throw new InvalidValueException("SiteElevation, must be between -300 meters and 10,000 meters");
			}
			tl.LogMessage("SiteElevation Set", value.ToString());
			siteElevation = value;
		}
	}

	public double SiteLatitude
	{
		get
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			double num4 = 0.0;
			string text = ((SharedResources.versionOnStep <= 415) ? (CommandString(":Gt#", raw: true).TrimEnd('#') + ":00") : CommandString(":GtH#", raw: true).TrimEnd('#'));
			tl.LogMessage("SiteLatitude Get", text);
			try
			{
				num3 = Convert.ToInt32(text.Substring(1, 2));
				num2 = Convert.ToInt32(text.Substring(4, 2));
				num = Convert.ToInt32(text.Substring(7, 2));
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("SiteLatitude, unexpected value: " + text);
			}
			finally
			{
				num4 = (double)num3 + (double)num2 / 60.0 + (double)num / 3600.0;
				if (text.Substring(0, 1) == "-")
				{
					num4 = 0.0 - num4;
				}
			}
			return num4;
		}
		set
		{
			if (value < -90.0 || value > 90.0)
			{
				throw new InvalidValueException("SiteLatitude, must be between -90 degrees and 90 degrees");
			}
			string text = ((SharedResources.versionOnStep <= 415) ? SharedResources.doubleToDm(value) : SharedResources.doubleToDms(value));
			tl.LogMessage("SiteLatitude Set", text);
			if (!CommandBool(":St" + text + "#", raw: true))
			{
				ThrowCommandErrorException("SiteLatitude");
			}
		}
	}

	public double SiteLongitude
	{
		get
		{
			int num = 0;
			int num2 = 0;
			double num3 = 0.0;
			string text = ((SharedResources.versionOnStep <= 415) ? (CommandString(":Gg#", raw: true).TrimEnd('#') + ":00") : CommandString(":GgH#", raw: true).TrimEnd('#'));
			tl.LogMessage("SiteLongitude Get", text);
			try
			{
				num2 = Convert.ToInt32(text.Substring(1, 3));
				num = Convert.ToInt32(text.Substring(5, 2));
				Convert.ToInt32(text.Substring(8, 2));
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("SiteLongitude, unexpected value: " + text);
			}
			finally
			{
				num3 = (double)num2 + (double)num / 60.0;
				if (text.Substring(0, 1) != "-")
				{
					num3 = 0.0 - num3;
				}
			}
			return num3;
		}
		set
		{
			if (Math.Abs(value) > 180.0)
			{
				throw new InvalidValueException("SiteLongitude, must be between -180 and 180 degrees");
			}
			string text = ((SharedResources.versionOnStep <= 415) ? SharedResources.doubleToDm2(0.0 - value) : SharedResources.doubleToDms2(0.0 - value));
			tl.LogMessage("SiteLongitude Set", text);
			if (!CommandBool(":Sg" + text + "#", raw: true))
			{
				ThrowCommandErrorException("SiteLongitude");
			}
		}
	}

	public short SlewSettleTime
	{
		get
		{
			tl.LogMessage("SlewSettleTime Get", "2");
			return 2;
		}
		set
		{
			tl.LogMessage("SlewSettleTime Set", "Not implemented");
			throw new PropertyNotImplementedException("SlewSettleTime", accessorSet: true);
		}
	}

	public bool Slewing
	{
		get
		{
			string text = CommandString(":GU#", raw: true).TrimEnd('#');
			tl.LogMessage("Slewing Get", text);
			bool result = text.IndexOf("N") < 0;
			if (text.IndexOf("g") >= 0)
			{
				result = true;
			}
			return result;
		}
	}

	public double TargetDeclination
	{
		get
		{
			if (double.IsNaN(LastTargetDeclination))
			{
				throw new ValueNotSetException("TargetDeclination");
			}
			tl.LogMessage("TargetDeclination Get", LastTargetDeclination.ToString());
			return LastTargetDeclination;
		}
		set
		{
			if (value < -90.0 || value > 90.0)
			{
				throw new InvalidValueException("TargetDeclination, Declination must be between -90 degrees and 90 degrees");
			}
			LastTargetDeclination = value;
			tl.LogMessage("TargetDeclination Set", value.ToString());
		}
	}

	public double TargetRightAscension
	{
		get
		{
			if (double.IsNaN(LastTargetRightAscension))
			{
				throw new ValueNotSetException("TargetRightAscension");
			}
			tl.LogMessage("TargetRightAscension Get", LastTargetRightAscension.ToString());
			return LastTargetRightAscension;
		}
		set
		{
			if (value < 0.0 || value > 24.0)
			{
				throw new InvalidValueException("TargetRightAscension, RightAscension must be between 0 and 24 hours");
			}
			LastTargetRightAscension = value;
			tl.LogMessage("TargetRightAscension Set", value.ToString());
		}
	}

	public bool Tracking
	{
		get
		{
			bool result = false;
			string text = CommandString(":GT#", raw: true).TrimEnd('#');
			tl.LogMessage("Tracking Get", text);
			double num;
			try
			{
				num = Convert.ToDouble(text, CultureInfo.GetCultureInfo("en-us"));
			}
			catch
			{
				throw new InvalidOperationException("Tracking, unexpected value getting tracking state: " + text);
			}
			if (num > 30.0 && num < 90.0)
			{
				result = true;
			}
			return result;
		}
		set
		{
			if (value)
			{
				if (!CommandBool(":Te#", raw: true))
				{
					ThrowCommandErrorException("Tracking, enable Tracking failed");
				}
				tl.LogMessage("Tracking Set", true.ToString());
			}
			else
			{
				if (!CommandBool(":Td#", raw: true))
				{
					ThrowCommandErrorException("Tracking, disable Tracking failed");
				}
				tl.LogMessage("Tracking Set", false.ToString());
			}
		}
	}

	public DriveRates TrackingRate
	{
		get
		{
			string text = CommandString(":GT#", raw: true).TrimEnd('#');
			tl.LogMessage("TrackingRate Get", text);
			double num = Convert.ToDouble(text, CultureInfo.GetCultureInfo("en-us"));
			if (Math.Abs(num - 60.16427) < 0.001)
			{
				return DriveRates.driveSidereal;
			}
			if (Math.Abs(num - 60.136) < 0.001)
			{
				return DriveRates.driveKing;
			}
			if (Math.Abs(num - 60.0) < 0.001)
			{
				return DriveRates.driveSolar;
			}
			if (Math.Abs(num - 57.9) < 0.001)
			{
				return DriveRates.driveLunar;
			}
			return DriveRates.driveSidereal;
		}
		set
		{
			switch (value)
			{
			case DriveRates.driveSidereal:
				CommandBlind(":TQ#", raw: true);
				break;
			case DriveRates.driveKing:
				CommandBlind(":TK#", raw: true);
				break;
			case DriveRates.driveSolar:
				CommandBlind(":TS#", raw: true);
				break;
			case DriveRates.driveLunar:
				CommandBlind(":TL#", raw: true);
				break;
			default:
				throw new PropertyNotImplementedException("TrackingRate: " + value);
			}
			tl.LogMessage("TrackingRate Set", value.ToString());
		}
	}

	public ITrackingRates TrackingRates
	{
		get
		{
			ITrackingRates trackingRates = new TrackingRates();
			tl.LogMessage("TrackingRates", "Get");
			foreach (DriveRates item in trackingRates)
			{
				tl.LogMessage("TrackingRates", "Get: " + item);
			}
			return trackingRates;
		}
	}

	public DateTime UTCDate
	{
		get
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			string text = CommandString(":GL#", raw: true).TrimEnd('#');
			try
			{
				num3 = Convert.ToInt32(text.Substring(0, 2));
				num2 = Convert.ToInt32(text.Substring(3, 2));
				num = Convert.ToInt32(text.Substring(6, 2));
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("UTCDate, unexpected value getting Local Time: " + text);
			}
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			text = CommandString(":GC#", raw: true).TrimEnd('#');
			try
			{
				num4 = Convert.ToInt32(text.Substring(0, 2));
				num5 = Convert.ToInt32(text.Substring(3, 2));
				num6 = Convert.ToInt32(text.Substring(6, 2));
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("UTCDate, unexpected value getting Date");
			}
			num6 = ((num6 <= 12) ? (num6 + 2100) : (num6 + 2000));
			double num7 = 0.0;
			text = CommandString(":GG#", raw: true).TrimEnd('#');
			try
			{
				num7 = Convert.ToDouble(text.Substring(0, 3), CultureInfo.GetCultureInfo("en-us"));
				if (text.Length > 4)
				{
					double num8 = Convert.ToDouble(text.Substring(4, 2), CultureInfo.GetCultureInfo("en-us")) / 60.0;
					num7 = ((!(num7 < 0.0)) ? (num7 + num8) : (num7 - num8));
				}
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("UTCDate, unexpected value getting UTC offset: " + text);
			}
			DateTime dateTime = new DateTime(num6, num4, num5, num3, num2, num).AddHours(num7);
			tl.LogMessage("UTCDate Get", string.Format("MM/dd/yy HH:mm:ss", dateTime));
			return dateTime;
		}
		set
		{
			tl.LogMessage("UTCDate Set", string.Format("MM/dd/yy HH:mm:ss", value));
			double num = 0.0;
			string text = CommandString(":GG#", raw: true).TrimEnd('#');
			try
			{
				num = Convert.ToDouble(text.Substring(0, 3), CultureInfo.GetCultureInfo("en-us"));
				if (text.Length > 4)
				{
					double num2 = Convert.ToDouble(text.Substring(4, 2), CultureInfo.GetCultureInfo("en-us")) / 60.0;
					num = ((!(num < 0.0)) ? (num + num2) : (num - num2));
				}
			}
			catch (FormatException)
			{
				throw new InvalidOperationException("UTCDate, unexpected value getting UTC offset: " + text);
			}
			DateTime dateTime = value.AddHours(0.0 - num);
			string text2 = dateTime.ToString("MM/dd/yy", CultureInfo.GetCultureInfo("en-us"));
			if (!CommandBool(":SC" + text2 + "#", raw: true))
			{
				throw new InvalidOperationException("UTCDate, setting Date failed: " + text2);
			}
			text2 = dateTime.ToString("HH:mm:ss", CultureInfo.GetCultureInfo("en-us"));
			if (!CommandBool(":SL" + text2 + "#", raw: true))
			{
				throw new InvalidOperationException("UTCDate, setting Time failed: " + text2);
			}
		}
	}

	private bool IsConnected
	{
		get
		{
			if (connectedState && !SharedResources.IsConnected(currentComPort))
			{
				connectedState = false;
			}
			return connectedState;
		}
	}

	public Telescope()
	{
		tl = new TraceLogger("", "OnStep");
		ReadProfile();
		tl.LogMessage("Telescope", "Starting initialisation");
		connectedState = false;
		utilities = new Util();
		astroUtilities = new AstroUtils();
		driverID = Marshal.GenerateProgIdForType(GetType());
		tl.LogMessage("Telescope", "Completed initialisation");
	}

	public void SetupDialog()
	{
		if (IsConnected)
		{
			MessageBox.Show("Already connected, just press OK");
		}
		using SetupDialogForm setupDialogForm = new SetupDialogForm(tl);
		if (setupDialogForm.ShowDialog() == DialogResult.OK)
		{
			WriteProfile();
		}
	}

	public string Action(string actionName, string actionParameters)
	{
		LogMessage("", "Action {0}, parameters {1} not implemented", actionName, actionParameters);
		throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
	}

	public void CommandBlind(string command, bool raw)
	{
		lock (lockObject)
		{
			CheckConnected("CommandBlind");
			SharedResources.SendMessage(currentComPort, command, raw, SharedResources.responding.rNone, errorCorrection);
		}
	}

	public bool CommandBool(string command, bool raw)
	{
		lock (lockObject)
		{
			CheckConnected("CommandBool");
			string text = SharedResources.SendMessage(currentComPort, command, raw, SharedResources.responding.rOne, errorCorrection);
			if (raw)
			{
				command = command.TrimStart(':');
				command = command.TrimEnd('#');
			}
			switch (command)
			{
			case "MS":
			case "MA":
			case "MN":
			case "MNe":
			case "MNw":
				if (text == "0")
				{
					return true;
				}
				return false;
			default:
				if (text == "0")
				{
					return false;
				}
				if (text == "1")
				{
					return true;
				}
				throw new InvalidValueException("CommandBool");
			}
		}
	}

	private int CommandInt(string command, bool raw)
	{
		lock (lockObject)
		{
			CheckConnected("CommandInt");
			return Convert.ToInt32(SharedResources.SendMessage(currentComPort, command, raw, SharedResources.responding.rOne, errorCorrection));
		}
	}

	public string CommandString(string command, bool raw)
	{
		lock (lockObject)
		{
			CheckConnected("CommandString");
			return SharedResources.SendMessage(currentComPort, command, raw, SharedResources.responding.rRawString, errorCorrection);
		}
	}

	public void Dispose()
	{
		tl.Enabled = false;
		tl.Dispose();
		tl = null;
		utilities.Dispose();
		utilities = null;
		astroUtilities.Dispose();
		astroUtilities = null;
	}

	public void AbortSlew()
	{
		lock ("OnStepSync")
		{
			ThrowExceptionIfParked("AbortSlew failed");
			CommandBlind(":Q#", raw: true);
			tl.LogMessage("AbortSlew", "Sent");
		}
	}

	public IAxisRates AxisRates(TelescopeAxes Axis)
	{
		tl.LogMessage("AxisRates Get", "Axis (" + Axis.ToString() + ")");
		return new AxisRates(Axis);
	}

	public bool CanMoveAxis(TelescopeAxes Axis)
	{
		tl.LogMessage("CanMoveAxis Get", "Axis (" + Axis.ToString() + ")");
		return Axis switch
		{
			TelescopeAxes.axisPrimary => true, 
			TelescopeAxes.axisSecondary => true, 
			TelescopeAxes.axisTertiary => false, 
			_ => throw new InvalidValueException("CanMoveAxis", Axis.ToString(), "0 to 2"), 
		};
	}

	public PierSide DestinationSideOfPier(double RightAscension, double Declination)
	{
		if (SharedResources.versionOnStep >= 501 && SharedResources.mountTypeGEM)
		{
			ThrowExceptionIfParked("DestinationSideOfPier Get");
			if (RightAscension < 0.0 || RightAscension > 24.0)
			{
				throw new InvalidValueException("DestinationSideOfPier, RightAscension must be between 0 and 24 hours");
			}
			if (Declination < -90.0 || Declination > 90.0)
			{
				throw new InvalidValueException("DestinationSideOfPier, Declination must be between -90 and 90 degrees");
			}
			PierSide result = PierSide.pierUnknown;
			string text = SharedResources.doubleToHms(RightAscension, extraPrecision: true);
			string text2 = SharedResources.doubleToDms(Declination, extraPrecision: true);
			if (!CommandBool(":WSS#", raw: true))
			{
				ThrowCommandErrorException("DestinationSideOfPier Get, store target failed");
			}
			if (!CommandBool(":Sr" + text + "#", raw: true))
			{
				ThrowCommandErrorException("DestinationSideOfPier Get, Sr failed: " + text);
			}
			if (!CommandBool(":Sd" + text2 + "#", raw: true))
			{
				ThrowCommandErrorException("DestinationSideOfPier Get, Sd failed: " + text2);
			}
			int num = CommandInt(":MD#", raw: true);
			if (!CommandBool(":WSR#", raw: true))
			{
				ThrowCommandErrorException("DestinationSideOfPier Get, recall target failed");
			}
			tl.LogMessage("DestinationSideOfPier Get", "Sent");
			if (num == 0)
			{
				result = PierSide.pierEast;
			}
			if (num == 1)
			{
				result = PierSide.pierWest;
			}
			return result;
		}
		tl.LogMessage("DestinationSideOfPier Get", "Not implemented");
		throw new PropertyNotImplementedException("DestinationSideOfPier", accessorSet: false);
	}

	public void FindHome()
	{
		ThrowExceptionIfParked("FindHome failed");
		CommandBlind(":hC#", raw: true);
		tl.LogMessage("FindHome", "Sent");
	}

	public void MoveAxis(TelescopeAxes Axis, double Rate)
	{
		double num = Math.Abs(Rate);
		switch (Axis)
		{
		case TelescopeAxes.axisPrimary:
			ThrowExceptionIfParked("MoveAxis failed");
			if (num == 0.0)
			{
				CommandBlind(":Qw#", raw: true);
			}
			else
			{
				if (num < 0.008334 || Math.Abs(num) > 2.0)
				{
					throw new InvalidValueException("MoveAxis axisPrimary Rate");
				}
				CommandBlind(":RA" + num.ToString("0.00000000", CultureInfo.GetCultureInfo("en-us")) + "#", raw: true);
				if (Rate < 0.0)
				{
					CommandBlind(":Mw#", raw: true);
				}
				else
				{
					CommandBlind(":Me#", raw: true);
				}
			}
			tl.LogMessage("MoveAxis Primary", "Sent");
			break;
		case TelescopeAxes.axisSecondary:
			ThrowExceptionIfParked("MoveAxis failed");
			if (num == 0.0)
			{
				CommandBlind(":Qs#", raw: true);
			}
			else
			{
				if (num < 0.008334 || Math.Abs(num) > 2.0)
				{
					throw new InvalidValueException("MoveAxis axisSecondary Rate");
				}
				CommandBlind(":RE" + num.ToString("0.00000000", CultureInfo.GetCultureInfo("en-us")) + "#", raw: true);
				if (Rate < 0.0)
				{
					CommandBlind(":Ms#", raw: true);
				}
				else
				{
					CommandBlind(":Mn#", raw: true);
				}
			}
			tl.LogMessage("MoveAxis Secondary", "Sent");
			break;
		case TelescopeAxes.axisTertiary:
			tl.LogMessage("MoveAxis Tertiary", "Not Implemented");
			throw new MethodNotImplementedException();
		default:
			tl.LogMessage("MoveAxis Other", "Not Implemented");
			throw new MethodNotImplementedException();
		}
	}

	public void Park()
	{
		if (!AtPark)
		{
			if (!CommandBool(":hP#", raw: true))
			{
				ThrowCommandErrorException("Park failed");
			}
			tl.LogMessage("Park", "Sent");
		}
	}

	public void PulseGuide(GuideDirections Direction, int Duration)
	{
		string text = "?";
		text = Direction switch
		{
			GuideDirections.guideEast => "e", 
			GuideDirections.guideWest => "w", 
			GuideDirections.guideNorth => "n", 
			GuideDirections.guideSouth => "s", 
			_ => throw new InvalidOperationException("PulseGuide failed: direction unknown"), 
		};
		if (SharedResources.versionOnStep > 312)
		{
			if (!CommandBool(":MG" + text + Duration.ToString("D4") + "#", raw: true))
			{
				ThrowCommandErrorException("PulseGuide failed");
			}
		}
		else
		{
			CommandBlind(":Mg" + text + Duration.ToString("D4") + "#", raw: true);
		}
		tl.LogMessage("PulseGuide", "Sent");
	}

	public void SetPark()
	{
		if (!CommandBool(":hQ#", raw: true))
		{
			ThrowCommandErrorException("SetPark failed");
		}
		tl.LogMessage("SetPark", "Sent");
	}

	public void SlewToAltAz(double Azimuth, double Altitude)
	{
		ThrowExceptionIfParked("SlewToAltAz");
		if (Tracking)
		{
			throw new InvalidOperationException("SlewToAltAz, Tracking must be off");
		}
		if (Slewing)
		{
			throw new InvalidOperationException("SlewToAltAz, Already slewing");
		}
		if (Azimuth < 0.0 || Azimuth > 360.0)
		{
			throw new InvalidValueException("SlewToCoordinates, Azimuth must be between 0 and 360 degrees");
		}
		if (Altitude < -90.0 || Altitude > 90.0)
		{
			throw new InvalidValueException("SlewToCoordinates, Altitude must be between -90 and 90 degrees");
		}
		string text = SharedResources.doubleToDms2(Azimuth);
		string text2 = SharedResources.doubleToDms(Altitude);
		if (!CommandBool(":Sz" + text + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToAltAz, Sz failed: " + text);
		}
		if (!CommandBool(":Sa" + text2 + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToAltAz, Sa failed: " + text2);
		}
		int num = CommandInt(":MA#", raw: true);
		if (num != 0)
		{
			ThrowSlewErrorException(num.ToString());
		}
		tl.LogMessage("SlewToAltAz", "Sent");
		if (!WaitForStatus("N", 240))
		{
			throw new InvalidOperationException("SlewToAltAz, timed out waiting for end of slew");
		}
		tl.LogMessage("SlewToAltAz", "Done");
	}

	public void SlewToAltAzAsync(double Azimuth, double Altitude)
	{
		ThrowExceptionIfParked("Telescope, SlewToAltAzAsync");
		if (Tracking)
		{
			throw new InvalidOperationException("SlewToAltAzAsync, Tracking must be off");
		}
		if (Slewing)
		{
			throw new InvalidOperationException("SlewToAltAzAsync, Already slewing");
		}
		if (Azimuth < 0.0 || Azimuth > 360.0)
		{
			throw new InvalidValueException("SlewToCoordinates, Azimuth must be between 0 and 360 degrees");
		}
		if (Altitude < -90.0 || Altitude > 90.0)
		{
			throw new InvalidValueException("SlewToCoordinates, Altitude must be between -90 and 90 degrees");
		}
		string text = SharedResources.doubleToDms2(Azimuth);
		string text2 = SharedResources.doubleToDms(Altitude);
		if (!CommandBool(":Sz" + text + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToAltAzAsync, Sz failed: " + text);
		}
		if (!CommandBool(":Sa" + text2 + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToAltAzAsync, Sa failed: " + text2);
		}
		int num = CommandInt(":MA#", raw: true);
		if (num != 0)
		{
			ThrowSlewErrorException(num.ToString());
		}
		tl.LogMessage("SlewToAltAzAsync", "Sent");
	}

	public void SlewToCoordinates(double RightAscension, double Declination)
	{
		ThrowExceptionIfParked("Telescope, SlewToCoords");
		if (Slewing)
		{
			throw new InvalidOperationException("SlewToCoordinates, Already slewing");
		}
		if (RightAscension < 0.0 || RightAscension > 24.0)
		{
			throw new InvalidValueException("SlewToCoordinates, RightAscension must be between 0 and 24 hours");
		}
		if (Declination < -90.0 || Declination > 90.0)
		{
			throw new InvalidValueException("SlewToCoordinates, Declination must be between -90 and 90 degrees");
		}
		LastTargetRightAscension = RightAscension;
		LastTargetDeclination = Declination;
		string text = SharedResources.doubleToHms(RightAscension, SharedResources.versionOnStep > 121);
		string text2 = SharedResources.doubleToDms(Declination, SharedResources.versionOnStep > 121);
		if (!CommandBool(":Sr" + text + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToCoordinates, Sr failed: " + text);
		}
		if (!CommandBool(":Sd" + text2 + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToCoordinates, Sd failed: " + text2);
		}
		int num = CommandInt(":MS#", raw: true);
		if (num != 0)
		{
			ThrowSlewErrorException(num.ToString());
		}
		tl.LogMessage("SlewToCoordinates", "Sent");
		if (!WaitForStatus("N", 240))
		{
			throw new InvalidOperationException("SlewToCoordinates, timed out waiting for end of slew");
		}
		tl.LogMessage("SlewToCoordinates", "Done");
	}

	public void SlewToCoordinatesAsync(double RightAscension, double Declination)
	{
		ThrowExceptionIfParked("Telescope, SlewToCoordsAsync");
		if (Slewing)
		{
			throw new InvalidOperationException("SlewToCoordinatesAsync, Already slewing");
		}
		if (RightAscension < 0.0 || RightAscension > 24.0)
		{
			throw new InvalidValueException("SlewToCoordinatesAsync, RightAscension must be between 0 and 24 hours");
		}
		if (Declination < -90.0 || Declination > 90.0)
		{
			throw new InvalidValueException("SlewToCoordinatesAsync, Declination must be between -90 and 90 degrees");
		}
		LastTargetRightAscension = RightAscension;
		LastTargetDeclination = Declination;
		string text = SharedResources.doubleToHms(RightAscension, SharedResources.versionOnStep > 121);
		string text2 = SharedResources.doubleToDms(Declination, SharedResources.versionOnStep > 121);
		if (!CommandBool(":Sr" + text + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToCoordinatesAsync, Sr failed: " + text);
		}
		if (!CommandBool(":Sd" + text2 + "#", raw: true))
		{
			ThrowCommandErrorException("SlewToCoordinatesAsync, Sd failed: " + text2);
		}
		int num = CommandInt(":MS#", raw: true);
		if (num != 0)
		{
			ThrowSlewErrorException(num.ToString());
		}
		tl.LogMessage("SlewToCoordinatesAsync", "Sent");
	}

	public void SlewToTarget()
	{
		SlewToCoordinates(LastTargetRightAscension, LastTargetDeclination);
		tl.LogMessage("SlewToTarget", "Sent");
	}

	public void SlewToTargetAsync()
	{
		SlewToCoordinatesAsync(LastTargetRightAscension, LastTargetDeclination);
		tl.LogMessage("SlewToTargetAsync", "Sent");
	}

	public void SyncToAltAz(double Azimuth, double Altitude)
	{
		tl.LogMessage("SyncToAltAz", "Not implemented");
		throw new MethodNotImplementedException("SyncToAltAz");
	}

	public void SyncToCoordinates(double RightAscension, double Declination)
	{
		ThrowExceptionIfParked("Telescope, SyncToCoords");
		if (RightAscension < 0.0 || RightAscension > 24.0)
		{
			throw new InvalidValueException("SyncToCoordinates, RightAscension must be between 0 and 24 hours");
		}
		if (Declination < -90.0 || Declination > 90.0)
		{
			throw new InvalidValueException("SyncToCoordinates, Declination must be between -90 and 90 degrees");
		}
		LastTargetRightAscension = RightAscension;
		LastTargetDeclination = Declination;
		string text = SharedResources.doubleToHms(RightAscension, SharedResources.versionOnStep > 121);
		string text2 = SharedResources.doubleToDms(Declination, SharedResources.versionOnStep > 121);
		if (CommandBool(":Sr" + text + "#", raw: true) && CommandBool(":Sd" + text2 + "#", raw: true))
		{
			CommandBlind(":CS#", raw: true);
		}
		tl.LogMessage("SyncToCoordinates", "Sent");
	}

	public void SyncToTarget()
	{
		SyncToCoordinates(LastTargetRightAscension, LastTargetDeclination);
		tl.LogMessage("SyncToTarget", "Sent");
	}

	public void Unpark()
	{
		if (!CommandBool(":hR#", raw: true))
		{
			ThrowCommandErrorException("Unpark failed");
		}
		tl.LogMessage("Unpark", "Sent");
	}

	private void CheckConnected(string message)
	{
		if (!IsConnected)
		{
			throw new NotConnectedException(message);
		}
	}

	private void ThrowCommandErrorException(string prefix)
	{
		if (SharedResources.versionOnStep < 313)
		{
			throw new InvalidOperationException(prefix);
		}
		CommandErrors commandErrors = CommandErrors.CE_NONE;
		int num = Convert.ToInt16(CommandString(":GE#", raw: true).TrimEnd('#'));
		if (num < 0 || num > 24)
		{
			throw new InvalidOperationException(prefix + ": unknown");
		}
		switch ((CommandErrors)num)
		{
		case CommandErrors.CE_NONE:
			throw new InvalidOperationException(prefix + ": No Errors");
		case CommandErrors.CE_0:
			throw new InvalidOperationException(prefix + ": Reply 0");
		case CommandErrors.CE_CMD_UNKNOWN:
			throw new InvalidOperationException(prefix + ": Command unknown");
		case CommandErrors.CE_REPLY_UNKNOWN:
			throw new InvalidOperationException(prefix + ": Invalid reply");
		case CommandErrors.CE_PARAM_RANGE:
			throw new InvalidOperationException(prefix + ": Parameter out of range");
		case CommandErrors.CE_PARAM_FORM:
			throw new InvalidOperationException(prefix + ": Bad parameter format");
		case CommandErrors.CE_ALIGN_FAIL:
			throw new InvalidOperationException(prefix + ": Align failed");
		case CommandErrors.CE_ALIGN_NOT_ACTIVE:
			throw new InvalidOperationException(prefix + ": Align not active");
		case CommandErrors.CE_NOT_PARKED_OR_AT_HOME:
			throw new InvalidOperationException(prefix + ": Not parked or at home");
		case CommandErrors.CE_PARKED:
			throw new InvalidOperationException(prefix + ": Already parked");
		case CommandErrors.CE_PARK_FAILED:
			throw new InvalidOperationException(prefix + ": Park failed");
		case CommandErrors.CE_NOT_PARKED:
			throw new InvalidOperationException(prefix + ": Not parked");
		case CommandErrors.CE_NO_PARK_POSITION_SET:
			throw new InvalidOperationException(prefix + ": No park position set");
		case CommandErrors.CE_GOTO_FAIL:
			throw new InvalidOperationException(prefix + ": Goto failed");
		case CommandErrors.CE_LIBRARY_FULL:
			throw new InvalidOperationException(prefix + ": Library full");
		case CommandErrors.CE_GOTO_ERR_BELOW_HORIZON:
			throw new InvalidOperationException(prefix + ": Goto below horizon");
		case CommandErrors.CE_GOTO_ERR_ABOVE_OVERHEAD:
			throw new InvalidOperationException(prefix + ": Goto above overhead");
		case CommandErrors.CE_SLEW_ERR_IN_STANDBY:
			throw new InvalidOperationException(prefix + ": Slew in standby");
		case CommandErrors.CE_SLEW_ERR_IN_PARK:
			throw new InvalidOperationException(prefix + ": Slew in park");
		case CommandErrors.CE_GOTO_ERR_GOTO:
			throw new InvalidOperationException(prefix + ": Already in goto");
		case CommandErrors.CE_GOTO_ERR_OUTSIDE_LIMITS:
			throw new InvalidOperationException(prefix + ": Goto outside limits");
		case CommandErrors.CE_SLEW_ERR_HARDWARE_FAULT:
			throw new InvalidOperationException(prefix + ": Hardware fault");
		case CommandErrors.CE_MOUNT_IN_MOTION:
			throw new InvalidOperationException(prefix + ": Mount in motion");
		case CommandErrors.CE_GOTO_ERR_UNSPECIFIED:
			throw new InvalidOperationException(prefix + ": Other");
		case CommandErrors.CE_NULL:
			throw new InvalidOperationException(prefix + ": Null");
		}
	}

	private void ThrowSlewErrorException(string e)
	{
		switch (e)
		{
		case "1":
			throw new InvalidOperationException("SlewError: Coordinates below horizon");
		case "2":
			throw new InvalidOperationException("SlewError: Coordinates above overhead limit");
		case "3":
			throw new InvalidOperationException("SlewError: Controller in standby");
		case "4":
			throw new InvalidOperationException("SlewError: Telescope is Parked");
		case "5":
			throw new InvalidOperationException("SlewError: Prior slew cancelled");
		case "6":
			throw new InvalidOperationException("SlewError: Coordinates outside limits");
		case "7":
			throw new InvalidOperationException("SlewError: Hardware fault");
		case "8":
			throw new InvalidOperationException("SlewError: Already in motion");
		case "9":
			throw new InvalidOperationException("SlewError: Unspecified error");
		default:
			throw new InvalidOperationException("SlewError unknown: " + e);
		}
	}

	private void ThrowExceptionIfParked(string e)
	{
		if (CommandString(":GU#", raw: true).TrimEnd('#').IndexOf("P") >= 0)
		{
			throw new InvalidOperationException(e + ": Telescope is Parked");
		}
	}

	private bool WaitForStatus(string status, int time)
	{
		bool flag = false;
		long num = DateTime.Now.Ticks / 10000;
		long num2 = num;
		string text = "";
		long num3;
		do
		{
			num3 = DateTime.Now.Ticks / 10000;
			if (num3 - num2 > 1000 && accessCount == 0)
			{
				num2 = num3;
				text = CommandString(":GU#", raw: true).TrimEnd('#');
			}
			if (text.IndexOf(status) >= 0)
			{
				flag = true;
			}
		}
		while (!flag || num3 - num > time * 1000);
		return flag;
	}

	internal void ReadProfile()
	{
		using Profile profile = new Profile();
		profile.DeviceType = "Telescope";
		tl.Enabled = Convert.ToBoolean(profile.GetValue(driverID, "Trace Level", string.Empty, "false"));
		comPort = profile.GetValue(driverID, "COM Port", string.Empty, "");
		baudRate = Convert.ToInt32(profile.GetValue(driverID, "Baud Rate", string.Empty, "9600"));
		ipAddress = profile.GetValue(driverID, "IP Address", string.Empty, "192.168.0.1:9999");
		hidVid = profile.GetValue(driverID, "HID VID", string.Empty, "1A86");
		hidPid = profile.GetValue(driverID, "HID PID", string.Empty, "55D4");
		hidSerial = profile.GetValue(driverID, "HID SN", string.Empty, "A8-0001");
		dtrControl = Convert.ToBoolean(profile.GetValue(driverID, "DTR Control", string.Empty, "true"));
		errorCorrection = Convert.ToBoolean(profile.GetValue(driverID, "Error Correction", string.Empty, "false"));
		readTimeout = Convert.ToInt32(profile.GetValue(driverID, "Read Timeout", string.Empty, "3"));
		autoDateTime = Convert.ToBoolean(profile.GetValue(driverID, "Auto Date/Time", string.Empty, "false"));
		apertureArea = Convert.ToDouble(profile.GetValue(driverID, "Aperture Area", string.Empty, "0"), CultureInfo.GetCultureInfo("en-us"));
		apertureDiameter = Convert.ToDouble(profile.GetValue(driverID, "Aperture Diameter", string.Empty, "0"), CultureInfo.GetCultureInfo("en-us"));
		focalLength = Convert.ToDouble(profile.GetValue(driverID, "Focal Length", string.Empty, "0"), CultureInfo.GetCultureInfo("en-us"));
		siteElevation = Convert.ToDouble(profile.GetValue(driverID, "Site Elevation", string.Empty, "0"), CultureInfo.GetCultureInfo("en-us"));
	}

	internal void WriteProfile()
	{
		using Profile profile = new Profile();
		profile.DeviceType = "Telescope";
		profile.WriteValue(driverID, "Trace Level", tl.Enabled.ToString());
		profile.WriteValue(driverID, "COM Port", comPort.ToString());
		profile.WriteValue(driverID, "Baud Rate", baudRate.ToString());
		profile.WriteValue(driverID, "IP Address", ipAddress.ToString());
		profile.WriteValue(driverID, "HID VID", hidVid.ToString());
		profile.WriteValue(driverID, "HID PID", hidPid.ToString());
		profile.WriteValue(driverID, "HID SN", hidSerial.ToString());
		profile.WriteValue(driverID, "DTR Control", dtrControl.ToString());
		profile.WriteValue(driverID, "Error Correction", errorCorrection.ToString());
		profile.WriteValue(driverID, "Read Timeout", readTimeout.ToString());
		profile.WriteValue(driverID, "Auto Date/Time", autoDateTime.ToString());
		profile.WriteValue(driverID, "Aperture Area", apertureArea.ToString(CultureInfo.GetCultureInfo("en-us")));
		profile.WriteValue(driverID, "Aperture Diameter", apertureDiameter.ToString(CultureInfo.GetCultureInfo("en-us")));
		profile.WriteValue(driverID, "Focal Length", focalLength.ToString(CultureInfo.GetCultureInfo("en-us")));
		profile.WriteValue(driverID, "Site Elevation", siteElevation.ToString(CultureInfo.GetCultureInfo("en-us")));
	}

	internal void LogMessage(string identifier, string message, params object[] args)
	{
		string message2 = string.Format(message, args);
		tl.LogMessage(identifier, message2);
	}
}
