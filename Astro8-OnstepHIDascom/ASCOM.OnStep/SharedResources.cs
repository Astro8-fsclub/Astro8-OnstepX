using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ASCOM.Utilities;
using ASCOM.Utilities.Exceptions;

namespace ASCOM.OnStep;

public static class SharedResources
{
	public enum responding
	{
		rNone,
		rString,
		rRawString,
		rOne
	}

	private static readonly object lockObject = new object();

	public static int versionOnStepMajor = -1;

	public static int versionOnStepMinor = -1;

	public static int versionOnStep = -1;

	public static char versionOnStepPatch = '?';

	public static bool mountTypeAltAzm = false;

	public static bool mountTypeGEM = false;

	public static double stepsPerSecond = -1.0;

	public static int CoordMode = 0;

	private static Dictionary<string, DeviceHardware> connectedDevices = new Dictionary<string, DeviceHardware>();

	private static char seq = 'A';

	private static TcpClient cs = null;

	private static int timeoutIP = 2000;

	private static readonly Dictionary<string, int> hidTimeoutMs = new Dictionary<string, int>();

	public static bool Connect(string deviceId, SerialSpeed serialSpeed, int recvTimeoutSeconds, bool dtrEnable)
	{
		lock (lockObject)
		{
			if (deviceId == "" || deviceId == null)
			{
				return false;
			}
			if (HidTransport.IsHidDeviceId(deviceId))
			{
				return HidConnect(deviceId, recvTimeoutSeconds);
			}
			if (connectedDevices.ContainsKey(deviceId))
			{
				connectedDevices[deviceId].count++;
				return true;
			}
			connectedDevices.Add(deviceId, new DeviceHardware());
			if (!IsIPv4(deviceId))
			{
				connectedDevices[deviceId].Serial.PortName = deviceId;
				connectedDevices[deviceId].Serial.Speed = serialSpeed;
				connectedDevices[deviceId].Serial.DataBits = 8;
				connectedDevices[deviceId].Serial.Parity = SerialParity.None;
				connectedDevices[deviceId].Serial.StopBits = SerialStopBits.One;
				connectedDevices[deviceId].Serial.ReceiveTimeout = recvTimeoutSeconds;
				connectedDevices[deviceId].Serial.DTREnable = dtrEnable;
				connectedDevices[deviceId].Serial.Connected = true;
				if (!connectedDevices[deviceId].Serial.Connected)
				{
					connectedDevices.Remove(deviceId);
					throw new InvalidOperationException("Connect failed: opening com port " + deviceId);
				}
				if (dtrEnable)
				{
					Thread.Sleep(3000);
				}
			}
			else
			{
				timeoutIP = recvTimeoutSeconds * 1000;
				if (!GetIPv4(deviceId, out var address, out var port))
				{
					connectedDevices.Remove(deviceId);
					throw new InvalidOperationException("Connect GetIPv4 failed: Invalid deviceID " + deviceId);
				}
				cs = new TcpClient();
				try
				{
					cs.Connect(address, port);
					cs.SendTimeout = timeoutIP;
					cs.ReceiveTimeout = timeoutIP;
				}
				catch
				{
					cs.Close();
					cs = null;
					connectedDevices.Remove(deviceId);
					throw new InvalidOperationException("Connect failed: opening client socket to " + deviceId);
				}
			}
			string text = "";
			int num = 0;
			do
			{
				num++;
				try
				{
					text = SendMessage(deviceId, ":GVP#", raw: true, responding.rString, checksum: false);
				}
				catch
				{
					text = "";
				}
			}
			while (!(text == "On-Step") && !(text == "OnStepX") && num < 3);
			if (text != "On-Step" && text != "OnStepX")
			{
				if (!IsIPv4(deviceId))
				{
					connectedDevices[deviceId].Serial.Connected = false;
				}
				else
				{
					cs.Close();
					cs = null;
				}
				connectedDevices.Remove(deviceId);
				throw new InvalidOperationException("Connect failed: OnStep wasn't found (" + text + ")");
			}
			GetVersion(deviceId);
			if (versionOnStep < 316)
			{
				if (!IsIPv4(deviceId))
				{
					connectedDevices[deviceId].Serial.Connected = false;
				}
				else
				{
					cs.Close();
					cs = null;
				}
				connectedDevices.Remove(deviceId);
				throw new InvalidOperationException("Connect failed: OnStep version 3.16 or later wasn't found");
			}
			GetCoordMode(deviceId);
			string text2 = SendMessage(deviceId, ":GU#", raw: true, responding.rString, checksum: false);
			if (text2.IndexOf("A") > 0)
			{
				mountTypeAltAzm = true;
			}
			else
			{
				mountTypeAltAzm = false;
			}
			if (text2.IndexOf("E") > 0)
			{
				mountTypeGEM = true;
			}
			else
			{
				mountTypeGEM = false;
			}
			stepsPerSecond = Convert.ToDouble(SendMessage(deviceId, ":VS#", raw: true, responding.rString, checksum: false), CultureInfo.GetCultureInfo("en-us"));
			connectedDevices[deviceId].count = 1;
			return true;
		}
	}

	public static void Disconnect(string deviceId)
	{
		lock (lockObject)
		{
			if (HidTransport.IsHidDeviceId(deviceId))
			{
				HidTransport.Close(deviceId);
				return;
			}
			if (deviceId == "" || deviceId == null || !connectedDevices.ContainsKey(deviceId))
			{
				return;
			}
			connectedDevices[deviceId].count--;
			if (connectedDevices[deviceId].count <= 0)
			{
				if (!IsIPv4(deviceId))
				{
					connectedDevices[deviceId].Serial.Connected = false;
				}
				else
				{
					cs.Close();
					cs = null;
				}
				connectedDevices.Remove(deviceId);
			}
		}
	}

	public static bool IsConnected(string deviceId)
	{
		lock (lockObject)
		{
			if (HidTransport.IsHidDeviceId(deviceId))
			{
				return HidTransport.IsOpen(deviceId);
			}
			if (connectedDevices.ContainsKey(deviceId))
			{
				return connectedDevices[deviceId].count > 0;
			}
			return false;
		}
	}

	/// <summary>HID connection handshake — mirrors the original serial/TCP Connect logic but over HID.</summary>
	private static bool HidConnect(string deviceId, int recvTimeoutSeconds)
	{
		int timeoutMs = recvTimeoutSeconds * 1000;
		lock (hidTimeoutMs)
		{
			hidTimeoutMs[deviceId] = timeoutMs;
		}
		try
		{
			HidTransport.Open(deviceId);
		}
		catch
		{
			throw new InvalidOperationException("Connect failed: opening HID device " + deviceId);
		}
		string text = "";
		int num = 0;
		do
		{
			num++;
			try
			{
				text = SendMessageHID(deviceId, ":GVP#", raw: true, responding.rString, checksum: false, timeoutMs);
			}
			catch
			{
				text = "";
			}
		}
		while (!(text == "On-Step") && !(text == "OnStepX") && num < 3);
		if (text != "On-Step" && text != "OnStepX")
		{
			HidTransport.Close(deviceId);
			throw new InvalidOperationException("Connect failed: OnStep wasn't found (" + text + ")");
		}
		GetVersionHID(deviceId, timeoutMs);
		if (versionOnStep < 316)
		{
			HidTransport.Close(deviceId);
			throw new InvalidOperationException("Connect failed: OnStep version 3.16 or later wasn't found");
		}
		GetCoordModeHID(deviceId, timeoutMs);
		string text2 = SendMessageHID(deviceId, ":GU#", raw: true, responding.rString, checksum: false, timeoutMs);
		if (text2.IndexOf("A") > 0)
		{
			mountTypeAltAzm = true;
		}
		else
		{
			mountTypeAltAzm = false;
		}
		if (text2.IndexOf("E") > 0)
		{
			mountTypeGEM = true;
		}
		else
		{
			mountTypeGEM = false;
		}
		stepsPerSecond = Convert.ToDouble(SendMessageHID(deviceId, ":VS#", raw: true, responding.rString, checksum: false, timeoutMs), CultureInfo.GetCultureInfo("en-us"));
		return true;
	}

	private static void GetVersionHID(string deviceId, int timeoutMs)
	{
		if (versionOnStepMajor != -1)
		{
			return;
		}
		string text = SendMessageHID(deviceId, ":GVN#", raw: true, responding.rString, checksum: false, timeoutMs);
		if (text.Equals(""))
		{
			return;
		}
		int num = text.IndexOf(".");
		int length = text.Length;
		if (length >= 3 && num <= length && num > 0)
		{
			string value = text.Substring(0, num);
			string text2 = text.Substring(num + 1);
			char c = '?';
			if (text2.Length > 1)
			{
				c = text2.Substring(text2.Length - 1).ToCharArray()[0];
			}
			if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == ' ')
			{
				text2 = text2.Substring(0, text2.Length - 1);
				versionOnStepPatch = c;
			}
			try
			{
				versionOnStepMajor = Convert.ToInt32(value);
				versionOnStepMinor = Convert.ToInt32(text2);
				versionOnStep = versionOnStepMajor * 100 + versionOnStepMinor;
			}
			catch
			{
				versionOnStep = -1;
				versionOnStepMajor = -1;
				versionOnStepMinor = -1;
				versionOnStepPatch = '?';
			}
		}
	}

	private static void GetCoordModeHID(string deviceId, int timeoutMs)
	{
		switch (SendMessageHID(deviceId, ":GXEE#", raw: true, responding.rOne, checksum: false, timeoutMs))
		{
		case "0":
			CoordMode = 0;
			break;
		case "1":
			CoordMode = 1;
			break;
		case "2":
			CoordMode = 2;
			break;
		default:
			CoordMode = 0;
			break;
		}
	}

	public static string SendMessage(string deviceId, string message, bool raw, responding Responding, bool checksum)
	{
		lock (lockObject)
		{
			if (HidTransport.IsHidDeviceId(deviceId))
			{
				int hidMs;
				lock (hidTimeoutMs)
				{
					hidTimeoutMs.TryGetValue(deviceId, out hidMs);
				}
				return SendMessageHID(deviceId, message, raw, Responding, checksum, hidMs <= 0 ? 3000 : hidMs);
			}
			if (IsIPv4(deviceId))
			{
				return SendMessageIP(deviceId, message, raw, Responding, checksum);
			}
			string text = "";
			if (!raw)
			{
				message = ":" + message + "#";
			}
			if (checksum)
			{
				message = EncodeChecksum(message);
			}
			connectedDevices[deviceId].Serial.Transmit(message);
			if (checksum)
			{
				text = connectedDevices[deviceId].Serial.ReceiveTerminated("#");
				if (text.IndexOf('#') < 0)
				{
					throw new InvalidValueException("SendMessage, failed: No reply frame");
				}
				text = DecodeChecksum(text);
				if (text.Equals(""))
				{
					throw new InvalidValueException("SendMessage, failed: Recv. checksum");
				}
				if (text.Equals("SQ_FAIL#"))
				{
					throw new InvalidValueException("SendMessage, failed: Recv. sequence");
				}
				if (text.Equals("CK_FAIL#"))
				{
					throw new InvalidValueException("SendMessage, failed: Send checksum");
				}
				switch (Responding)
				{
				case responding.rString:
					text = text.TrimEnd('#');
					break;
				case responding.rOne:
					text = text.TrimEnd('#');
					if (text.Length != 1)
					{
						throw new InvalidValueException("SendMessage, failed: Length should be 1");
					}
					break;
				case responding.rNone:
					text = text.TrimEnd('#');
					if (text.Length != 0)
					{
						throw new InvalidValueException("SendMessage, failed: Length should be 0");
					}
					break;
				}
			}
			else
			{
				if (Responding == responding.rOne)
				{
					text = connectedDevices[deviceId].Serial.ReceiveCounted(1);
				}
				if (Responding == responding.rString)
				{
					text = connectedDevices[deviceId].Serial.ReceiveTerminated("#");
					text = text.TrimEnd('#');
				}
				if (Responding == responding.rRawString)
				{
					text = connectedDevices[deviceId].Serial.ReceiveTerminated("#");
				}
			}
			return text;
		}
	}

	private static void GetVersion(string deviceId)
	{
		if (versionOnStepMajor != -1)
		{
			return;
		}
		string text = SendMessage(deviceId, ":GVN#", raw: true, responding.rString, checksum: false);
		if (text.Equals(""))
		{
			return;
		}
		int num = text.IndexOf(".");
		int length = text.Length;
		if (length >= 3 && num <= length && num > 0)
		{
			string value = text.Substring(0, num);
			string text2 = text.Substring(num + 1);
			char c = '?';
			if (text2.Length > 1)
			{
				c = text2.Substring(text2.Length - 1).ToCharArray()[0];
			}
			if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == ' ')
			{
				text2 = text2.Substring(0, text2.Length - 1);
				versionOnStepPatch = c;
			}
			try
			{
				versionOnStepMajor = Convert.ToInt32(value);
				versionOnStepMinor = Convert.ToInt32(text2);
				versionOnStep = versionOnStepMajor * 100 + versionOnStepMinor;
			}
			catch
			{
				versionOnStep = -1;
				versionOnStepMajor = -1;
				versionOnStepMinor = -1;
				versionOnStepPatch = '?';
			}
		}
	}

	private static void GetCoordMode(string deviceId)
	{
		switch (SendMessage(deviceId, ":GXEE#", raw: true, responding.rOne, checksum: false))
		{
		case "0":
			CoordMode = 0;
			break;
		case "1":
			CoordMode = 1;
			break;
		case "2":
			CoordMode = 2;
			break;
		default:
			CoordMode = 0;
			break;
		}
	}

	private static string EncodeChecksum(string s)
	{
		byte b = 0;
		char[] array = s.ToCharArray();
		string text = "";
		if (array[0] != ':' && array[s.Length - 1] != '#')
		{
			return "";
		}
		for (int i = 1; i < s.Length - 1; i++)
		{
			b += (byte)array[i];
			text += array[i];
		}
		int num = b & 0xF;
		int num2 = (b >> 4) & 0xF;
		seq += '\u0001';
		if (seq > 'z')
		{
			seq = 'A';
		}
		return ";" + text + num2.ToString("X") + num.ToString("X") + seq + "#";
	}

	private static string DecodeChecksum(string s)
	{
		byte b = 0;
		char[] array = s.ToCharArray();
		string text = "";
		if (s.Length < 4)
		{
			return "";
		}
		if (array[s.Length - 1] != '#')
		{
			return "";
		}
		int num = int.Parse(s.Substring(s.Length - 4, 2), NumberStyles.HexNumber);
		int num2 = num & 0xF;
		int num3 = (num >> 4) & 0xF;
		if (num2 < 0 || num2 > 15 || num3 < 0 || num3 > 15)
		{
			return "";
		}
		for (int i = 0; i < s.Length - 4; i++)
		{
			b += (byte)array[i];
			text += array[i];
		}
		int num4 = b & 0xF;
		int num5 = (b >> 4) & 0xF;
		if (num2 != num4 || num3 != num5)
		{
			return "";
		}
		if (array[s.Length - 2] != seq)
		{
			return "SQ_FAIL#";
		}
		return text + "#";
	}

	public static string doubleToHms(double f, bool extraPrecision = false)
	{
		while (f >= 24.0)
		{
			f -= 24.0;
		}
		while (f < 0.0)
		{
			f += 24.0;
		}
		double num = f + 1E-05;
		int num2 = (int)Math.Floor(num);
		double num3 = (num - (double)num2) * 60.0;
		double num4 = (num3 - Math.Floor(num3)) * 60.0;
		string text = $"{num2,2:D2}:";
		string text2 = $"{(int)num3,2:D2}:";
		string text3 = ((!extraPrecision) ? $"{(int)num4,2:D2}" : num4.ToString("00.0000", CultureInfo.GetCultureInfo("en-us")));
		return text + text2 + text3;
	}

	public static string doubleToHm(double f)
	{
		while (f >= 24.0)
		{
			f -= 24.0;
		}
		while (f < 0.0)
		{
			f += 24.0;
		}
		double num = f + 1E-05;
		int num2 = (int)Math.Floor(num);
		double num3 = (num - (double)num2) * 60.0;
		Math.Floor(num3);
		string text = $"{num2,2:D2}:";
		string text2 = $"{(int)num3,2:D2}:";
		return text + text2;
	}

	public static string doubleToDm(double f)
	{
		if (f > 90.0)
		{
			f = 90.0;
		}
		if (f < -90.0)
		{
			f = -90.0;
		}
		string text = "+";
		if (f < 0.0)
		{
			f = 0.0 - f;
			text = "-";
		}
		double num = f + 1E-06;
		int num2 = (int)Math.Floor(num);
		double num3 = (num - (double)num2) * 60.0;
		string text2 = $"{num2,2:D2}*";
		string text3 = $"{(int)num3,2:D2}";
		return text + text2 + text3;
	}

	public static string doubleToDm2(double f)
	{
		if (f > 180.0)
		{
			f = 180.0;
		}
		if (f < -180.0)
		{
			f = -180.0;
		}
		string text = "+";
		if (f < 0.0)
		{
			f = 0.0 - f;
			text = "-";
		}
		double num = f + 1E-06;
		int num2 = (int)Math.Floor(num);
		double num3 = (num - (double)num2) * 60.0;
		string text2 = $"{num2,3:D3}*";
		string text3 = $"{(int)num3,2:D2}";
		return text + text2 + text3;
	}

	public static string doubleToDms(double f, bool extraPrecision = false)
	{
		if (f > 90.0)
		{
			f = 90.0;
		}
		if (f < -90.0)
		{
			f = -90.0;
		}
		string text = "+";
		if (f < 0.0)
		{
			f = 0.0 - f;
			text = "-";
		}
		double num = f + 1E-05;
		int num2 = (int)Math.Floor(num);
		double num3 = (num - (double)num2) * 60.0;
		double num4 = (num3 - Math.Floor(num3)) * 60.0;
		string text2 = $"{num2,2:D2}:";
		string text3 = $"{(int)num3,2:D2}:";
		string text4 = ((!extraPrecision) ? $"{(int)num4,2:D2}" : num4.ToString("00.0000", CultureInfo.GetCultureInfo("en-us")));
		return text + text2 + text3 + text4;
	}

	public static string doubleToDms2(double f)
	{
		while (f >= 360.0)
		{
			f -= 360.0;
		}
		while (f < 0.0)
		{
			f += 360.0;
		}
		double num = f + 1E-05;
		int num2 = (int)Math.Floor(num);
		double num3 = (num - (double)num2) * 60.0;
		double num4 = (num3 - Math.Floor(num3)) * 60.0;
		string text = $"{num2,3:D3}:";
		string text2 = $"{(int)num3,2:D2}:";
		string text3 = $"{(int)num4,2:D2}";
		return text + text2 + text3;
	}

	public static string timeZoneToHM(double tz)
	{
		string text = "";
		double num = Math.Abs(tz - Math.Truncate(tz));
		text = $"{(int)Math.Abs(tz),2:D2}";
		if (Math.Abs(num - 0.5) < 1E-08)
		{
			text += ":30";
		}
		if (Math.Abs(num - 0.75) < 1E-08)
		{
			text += ":45";
		}
		if (tz < 0.0)
		{
			return "-" + text;
		}
		return "+" + text;
	}

	public static bool GetIPv4(string value, out string address, out int port)
	{
		int num = value.IndexOf(':');
		if (num >= 0)
		{
			port = Convert.ToInt32(value.Substring(num + 1));
		}
		else
		{
			port = 9999;
		}
		if (num < 0)
		{
			address = value;
		}
		else
		{
			address = value.Substring(0, num);
		}
		if (port < 0 || port > 65535)
		{
			return false;
		}
		if (!_IsIPv4(address))
		{
			return false;
		}
		return true;
	}

	public static bool IsIPv4(string value)
	{
		int num = value.IndexOf(':');
		int num2 = ((num < 0) ? 9999 : Convert.ToInt32(value.Substring(num + 1)));
		string value2 = ((num >= 0) ? value.Substring(0, num) : value);
		if (num2 < 0 || num2 > 65535)
		{
			return false;
		}
		if (!_IsIPv4(value2))
		{
			return false;
		}
		return true;
	}

	public static bool _IsIPv4(string value)
	{
		string[] array = value.Split('.');
		if (array.Length != 4)
		{
			return false;
		}
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!int.TryParse(text, out var result) || !result.ToString().Length.Equals(text.Length) || result < 0 || result > 255)
			{
				return false;
			}
		}
		return true;
	}

	public static string SendMessageIP(string deviceId, string message, bool raw, responding Responding, bool checksum)
	{
		lock (lockObject)
		{
			int num = 0;
			if (checksum)
			{
				message = EncodeChecksum(message);
			}
			if (message.Equals(""))
			{
				throw new InvalidOperationException("SendMessageIP failed: Improperly framed Command");
			}
			string text;
			NetworkStream stream;
			while (true)
			{
				if (cs != null)
				{
					cs.Close();
				}
				cs = new TcpClient();
				if (!GetIPv4(deviceId, out var address, out var port))
				{
					cs.Close();
					cs = null;
					throw new InvalidOperationException("SendMessageIP GetIPv4 failed: Invalid deviceID " + deviceId);
				}
				try
				{
					cs.Connect(address, port);
					cs.SendTimeout = timeoutIP;
					cs.ReceiveTimeout = timeoutIP;
				}
				catch
				{
					cs.Close();
					cs = null;
					throw new NotConnectedException("SendMessageIP failed: opening client socket to " + deviceId);
				}
				try
				{
					stream = cs.GetStream();
					stream.WriteTimeout = timeoutIP;
					stream.ReadTimeout = timeoutIP;
				}
				catch
				{
					cs.Close();
					cs = null;
					throw new NotConnectedException("SendMessageIP failed: opening network stream");
				}
				byte[] array = new byte[2000];
				byte[] bytes = Encoding.ASCII.GetBytes(message);
				try
				{
					stream.Write(bytes, 0, bytes.Length);
				}
				catch
				{
					cs.Close();
					cs = null;
					throw new NotConnectedException("SendMessageIP failed: writing to network stream");
				}
				text = "";
				try
				{
					text = "";
					if (checksum)
					{
						int length = stream.Read(array, 0, 40);
						text = Encoding.ASCII.GetString(array);
						text = text.Substring(0, length);
						text = DecodeChecksum(text);
						if (text.Equals(""))
						{
							throw new InvalidValueException("SendMessageIP failed: Recv. checksum");
						}
						if (text.Equals("SQ_FAIL#"))
						{
							throw new InvalidValueException("SendMessageIP failed: Recv. sequence");
						}
						if (text.Equals("CK_FAIL#"))
						{
							throw new InvalidValueException("SendMessageIP failed: Send checksum");
						}
						switch (Responding)
						{
						case responding.rString:
							text = text.TrimEnd('#');
							break;
						case responding.rOne:
							text = text.TrimEnd('#');
							if (text.Length != 1)
							{
								throw new ASCOM.Utilities.Exceptions.InvalidValueException("SendMessageIP failed: Length should be 1");
							}
							break;
						case responding.rNone:
							text = text.TrimEnd('#');
							if (text.Length != 0)
							{
								throw new InvalidValueException("SendMessageIP failed: Length should be 0");
							}
							break;
						}
						break;
					}
					switch (Responding)
					{
					case responding.rRawString:
					{
						int length2 = stream.Read(array, 0, 40);
						text = Encoding.ASCII.GetString(array);
						text = text.Substring(0, length2);
						if (text.IndexOf('#') < 0)
						{
							throw new InvalidValueException("SendMessageIP failed: No reply frame");
						}
						break;
					}
					case responding.rString:
					{
						int length3 = stream.Read(array, 0, 40);
						text = Encoding.ASCII.GetString(array);
						text = text.Substring(0, length3);
						if (text.IndexOf('#') < 0)
						{
							throw new InvalidValueException("SendMessageIP failed: No reply frame");
						}
						text = text.Substring(0, text.IndexOf('#'));
						break;
					}
					case responding.rOne:
					{
						int num2 = stream.Read(array, 0, 1);
						text = Encoding.ASCII.GetString(array).Substring(0, 1);
						if (num2 != 1)
						{
							throw new InvalidValueException("SendMessageIP failed: Length should be 1");
						}
						break;
					}
					case responding.rNone:
						Thread.Sleep(timeoutIP / 2);
						break;
					}
				}
				catch
				{
					stream.Close();
					stream = null;
					cs.Close();
					cs = null;
					if (num++ <= 3)
					{
						continue;
					}
					throw new InvalidOperationException("SendMessageIP failed: no command/response after 3 attempts");
				}
				break;
			}
			stream.Close();
			return text;
		}
	}
		/// <summary>SendMessage over HID — mirrors the semantics of the original serial SendMessage (raw/checksum/responding).</summary>
		private static string SendMessageHID(string deviceId, string message, bool raw, responding Responding, bool checksum, int timeoutMs)
		{
			lock (lockObject)
			{
				string text = "";
				if (!raw)
				{
					message = ":" + message + "#";
				}
				if (checksum)
				{
					message = EncodeChecksum(message);
				}
				HidTransport.Write(deviceId, Encoding.ASCII.GetBytes(message));
				if (checksum)
				{
					text = Encoding.ASCII.GetString(HidTransport.ReadUntil(deviceId, (byte)'#', timeoutMs));
					if (text.IndexOf('#') < 0)
					{
						throw new InvalidValueException("SendMessage, failed: No reply frame");
					}
					text = DecodeChecksum(text);
					if (text.Equals(""))
					{
						throw new InvalidValueException("SendMessage, failed: Recv. checksum");
					}
					if (text.Equals("SQ_FAIL#"))
					{
						throw new InvalidValueException("SendMessage, failed: Recv. sequence");
					}
					if (text.Equals("CK_FAIL#"))
					{
						throw new InvalidValueException("SendMessage, failed: Send checksum");
					}
					switch (Responding)
					{
					case responding.rString:
						text = text.TrimEnd('#');
						break;
					case responding.rOne:
						text = text.TrimEnd('#');
						if (text.Length != 1)
						{
							throw new InvalidValueException("SendMessage, failed: Length should be 1");
						}
						break;
					case responding.rNone:
						text = text.TrimEnd('#');
						if (text.Length != 0)
						{
							throw new InvalidValueException("SendMessage, failed: Length should be 0");
						}
						break;
					}
				}
				else
				{
					if (Responding == responding.rOne)
					{
						text = Encoding.ASCII.GetString(HidTransport.ReadCounted(deviceId, 1, timeoutMs));
					}
					if (Responding == responding.rString)
					{
						text = Encoding.ASCII.GetString(HidTransport.ReadUntil(deviceId, (byte)'#', timeoutMs));
						text = text.TrimEnd('#');
					}
					if (Responding == responding.rRawString)
					{
						text = Encoding.ASCII.GetString(HidTransport.ReadUntil(deviceId, (byte)'#', timeoutMs));
					}
				}
				return text;
			}
		}
	}
