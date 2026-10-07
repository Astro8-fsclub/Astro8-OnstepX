using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCOM.OnStep.Properties;
using ASCOM.Utilities;

namespace ASCOM.OnStep;

[ComVisible(false)]
public class SetupDialogForm : Form
{
	private TraceLogger tl;

	private string currentComPort = "";

	private bool currentEcp;

	private bool blockConnections = true;

	private string OrigLat = "";

	private string OrigLong = "";

	private int MaxRateCountdownToRefresh;

	private bool blockGotoRateChange;

	private bool cancelClose;

	private IContainer components;

	private Button cmdOK;

	private Button cmdCancel;

	private PictureBox picASCOM;

	private CheckBox chkTrace;

	private ComboBox comboBoxComPort;

	private PictureBox pictureBox1;

	private PictureBox pictureBox2;

	private Label TimeoutLabel;

	private TrackBar TimeoutTrackBar;

	private Label label12;

	private TextBox IPAddress;

	private Label label17;

	private TextBox HidVID;

	private Label label18;

	private TextBox HidPID;

	private Label label15;

	private TextBox HidSN;

	private Label label16;

	private Label label11;

	private GroupBox MaxRateGroupBox;

	private Label label10;

	private Label currentDegSec;

	private Label currentMaxRate;

	private TrackBar gotoRate;

	private GroupBox LimitGroupBox;

	private Label labelMWD;

	private TextBox LimitMeridianW;

	private Label labelMW;

	private Label labelMED;

	private TextBox LimitMeridianE;

	private Label labelME;

	private Label label6;

	private Label label7;

	private TextBox LimitOverhead;

	private Label label8;

	private TextBox LimitHorizon;

	private Label label9;

	private Label DriverVersion;

	private GroupBox BacklashGroupBox;

	private Label label5;

	private Label label4;

	private TextBox BacklashDec;

	private Label label13;

	private TextBox BacklashRA;

	private Label label14;

	private GroupBox OpticsGroupBox;

	private TextBox FocalLength;

	private TextBox Area;

	private TextBox Aperature;

	private Label FL_Label;

	private Label Area_Label;

	private Label Aperature_Label;

	private TextBox connectedDevice;

	private Label Connect_Label;

	private Label RS232_Label;

	private GroupBox DateTimeGroupBox;

	private CheckBox DateTimeCheckBox;

	private Label UTC;

	private Label UTC_Label;

	private Label LST;

	private Label LST_Label;

	private Label LMT;

	private Label Date;

	private Label LMT_Label;

	private Label Date_Label;

	private GroupBox SiteGroupBox;

	private TextBox Offset;

	private TextBox Lat;

	private TextBox Long;

	private Label Offset_Label;

	private TextBox Elevation;

	private Label Ele_Label;

	private Label Long_Label;

	private Label Lat_Label;

	private System.Windows.Forms.Timer refreshTimer;

	private System.Windows.Forms.Timer startupTimer;

	private CheckBox ChecksumCheckBox;

	private CheckBox EnableDTRcheckBox;

	public SetupDialogForm(TraceLogger tlDriver)
	{
		InitializeComponent();
		tl = tlDriver;
	}

	private void BrowseToAscom(object sender, EventArgs e)
	{
		try
		{
			Process.Start("http://ascom-standards.org/");
		}
		catch (Win32Exception ex)
		{
			if (ex.ErrorCode == -2147467259)
			{
				MessageBox.Show(ex.Message);
			}
		}
		catch (Exception ex2)
		{
			MessageBox.Show(ex2.Message);
		}
	}

	private void InitUI()
	{
		chkTrace.Checked = tl.Enabled;
		comboBoxComPort.Items.Clear();
		comboBoxComPort.Items.Add("");
		ComboBox.ObjectCollection items = comboBoxComPort.Items;
		object[] portNames = SerialPort.GetPortNames();
		items.AddRange(portNames);
		comboBoxComPort.Items.Add("IP Address");
		comboBoxComPort.Items.Add("HID Device");
		IPAddress.Text = Telescope.ipAddress;
		HidVID.Text = Telescope.hidVid;
		HidPID.Text = Telescope.hidPid;
		HidSN.Text = Telescope.hidSerial;
		HidVID.Visible = false;
		HidPID.Visible = false;
		HidSN.Visible = false;
		label17.Visible = false;
		label18.Visible = false;
		label15.Visible = false;
		label16.Visible = false;
		TimeoutTrackBar.Value = Telescope.readTimeout * 100;
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		string text = string.Format(CultureInfo.InvariantCulture, "v{0}.{1}", version.Major, version.Minor);
		DriverVersion.Text = text + Environment.NewLine + "Built: " + GetBuildDateTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		blockConnections = false;
		if (comboBoxComPort.Items.Contains(Telescope.comPort))
		{
			comboBoxComPort.SelectedItem = Telescope.comPort;
		}
		if (Telescope.comPort != null && Telescope.comPort.StartsWith("HID:", StringComparison.OrdinalIgnoreCase))
		{
			comboBoxComPort.SelectedItem = "HID Device";
		}
	}

	private static DateTime GetBuildDateTime()
	{
		try
		{
			string path = Assembly.GetExecutingAssembly().Location;
			using (System.IO.FileStream fs = System.IO.File.OpenRead(path))
			using (System.IO.BinaryReader br = new System.IO.BinaryReader(fs))
			{
				fs.Position = 0x3C;
				int peOffset = br.ReadInt32();
				fs.Position = peOffset + 8;
				uint timestamp = br.ReadUInt32();
				return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime();
			}
		}
		catch
		{
			return System.IO.File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location);
		}
	}

	private void EnableDTRcheckBox_CheckedChanged(object sender, EventArgs e)
	{
		comboBoxComPort_TextChanged(sender, e);
	}

	private void EnableRTScheckBox_CheckedChanged(object sender, EventArgs e)
	{
	}

	private void ChecksumCheckBox_CheckedChanged(object sender, EventArgs e)
	{
		comboBoxComPort_TextChanged(sender, e);
	}

	private string BuildHidDeviceId()
	{
		string vid = HidVID.Text.Trim();
		string pid = HidPID.Text.Trim();
		string sn = HidSN.Text.Trim();
		if (vid == "") vid = "1A86";
		if (pid == "") pid = "55D4";
		return "HID:" + vid + ":" + pid + (sn == "" ? "" : (":" + sn));
	}

	private void comboBoxComPort_TextChanged(object sender, EventArgs e)
	{
		if (blockConnections)
		{
			return;
		}
		refreshTimer.Enabled = false;
		if (currentComPort != "")
		{
			SharedResources.Disconnect(currentComPort);
			currentComPort = "";
		}
		clearAndLockControls();
		currentComPort = (string)comboBoxComPort.SelectedItem;
		if (currentComPort == "IP Address")
		{
			currentComPort = IPAddress.Text;
			IPAddress.Visible = true;
			label12.Visible = true;
		}
		else if (currentComPort == "HID Device")
		{
			currentComPort = BuildHidDeviceId();
			HidVID.Visible = true;
			HidPID.Visible = true;
			HidSN.Visible = true;
			label17.Visible = true;
			label18.Visible = true;
			label15.Visible = true;
			label16.Visible = true;
		}
		else
		{
			IPAddress.Visible = false;
			label12.Visible = false;
			HidVID.Visible = false;
			HidPID.Visible = false;
			HidSN.Visible = false;
			label17.Visible = false;
			label18.Visible = false;
			label15.Visible = false;
			label16.Visible = false;
		}
		currentEcp = ChecksumCheckBox.Checked;
		if (!(currentComPort != ""))
		{
			return;
		}
		try
		{
			if (!SharedResources.Connect(currentComPort, (SerialSpeed)Telescope.baudRate, TimeoutTrackBar.Value / 100, EnableDTRcheckBox.Checked))
			{
				currentComPort = "";
				return;
			}
			connectedDevice.Text = "OnStep " + SharedResources.versionOnStepMajor + "." + SharedResources.versionOnStepMinor + SharedResources.versionOnStepPatch;
			PreloadControls();
			refreshTimer.Enabled = true;
		}
		catch (Exception ex)
		{
			SharedResources.Disconnect(currentComPort);
			connectedDevice.Text = "None";
			currentComPort = "";
			MessageBox.Show(ex.Message);
		}
	}

	private void PreloadControls()
	{
		cmdOK.Enabled = true;
		SiteGroupBox.Enabled = true;
		DateTimeGroupBox.Enabled = true;
		OpticsGroupBox.Enabled = true;
		BacklashGroupBox.Enabled = true;
		LimitGroupBox.Enabled = true;
		MaxRateGroupBox.Enabled = true;
		string text3;
		string text;
		if (SharedResources.versionOnStep > 415)
		{
			text = SharedResources.SendMessage(currentComPort, ":GtH#", raw: true, SharedResources.responding.rString, currentEcp);
			if (text.Length == 13)
			{
				string text2 = ((int)Convert.ToDouble(text.Substring(7), CultureInfo.GetCultureInfo("en-us"))).ToString();
				if (text2.Length == 1)
				{
					text2 = "0" + text2;
				}
				text = text.Substring(0, 7) + text2;
			}
			text3 = SharedResources.SendMessage(currentComPort, ":GgH#", raw: true, SharedResources.responding.rString, currentEcp);
			if (text3.Length == 14)
			{
				string text4 = ((int)Convert.ToDouble(text3.Substring(8), CultureInfo.GetCultureInfo("en-us"))).ToString();
				if (text4.Length == 1)
				{
					text4 = "0" + text4;
				}
				text3 = text3.Substring(0, 8) + text4;
			}
		}
		else
		{
			text = SharedResources.SendMessage(currentComPort, ":Gt#", raw: true, SharedResources.responding.rString, currentEcp);
			text3 = SharedResources.SendMessage(currentComPort, ":Gg#", raw: true, SharedResources.responding.rString, currentEcp);
		}
		if (text.Substring(1, 1) == "0")
		{
			text = text.Remove(1, 1);
		}
		Lat.Text = text;
		if (text3.Substring(1, 1) == "0")
		{
			text3 = text3.Remove(1, 1);
		}
		if (text3.Substring(1, 1) == "0")
		{
			text3 = text3.Remove(1, 1);
		}
		Long.Text = text3;
		OrigLat = Lat.Text;
		OrigLong = Long.Text;
		Elevation.Text = Telescope.siteElevation.ToString(CultureInfo.GetCultureInfo("en-us"));
		text = SharedResources.SendMessage(currentComPort, ":GG#", raw: true, SharedResources.responding.rString, currentEcp);
		if (text.Substring(1, 1) == "0")
		{
			text = text.Remove(1, 1);
		}
		Offset.Text = text;
		BacklashRA.Text = GetBacklashRA();
		BacklashDec.Text = GetBacklashDec();
		DateTimeCheckBox.Checked = Telescope.autoDateTime;
		text = SharedResources.SendMessage(currentComPort, ":Gh#", raw: true, SharedResources.responding.rString, currentEcp);
		LimitHorizon.Text = text.Substring(0, text.Length - 1);
		text = SharedResources.SendMessage(currentComPort, ":Go#", raw: true, SharedResources.responding.rString, currentEcp);
		LimitOverhead.Text = text.Substring(0, text.Length - 1);
		if (SharedResources.mountTypeGEM)
		{
			LimitMeridianE.Text = GetLimitMeridianE();
			LimitMeridianE.Enabled = true;
			labelME.Enabled = true;
			labelMED.Enabled = true;
			LimitMeridianW.Text = GetLimitMeridianW();
			LimitMeridianW.Enabled = true;
			labelMW.Enabled = true;
			labelMWD.Enabled = true;
		}
		Aperature.Text = Telescope.apertureDiameter.ToString(CultureInfo.GetCultureInfo("en-us"));
		Area.Text = Telescope.apertureArea.ToString(CultureInfo.GetCultureInfo("en-us"));
		FocalLength.Text = Telescope.focalLength.ToString(CultureInfo.GetCultureInfo("en-us"));
		GetMaxRate();
	}

	private string GetBacklashRA()
	{
		string text = SharedResources.SendMessage(currentComPort, ":%BR#", raw: true, SharedResources.responding.rString, currentEcp);
		if (text.Length == 0)
		{
			text = "0";
		}
		while (text.Length > 1 && text[0] == '0')
		{
			text = text.Substring(1);
		}
		return text;
	}

	private string GetBacklashDec()
	{
		string text = SharedResources.SendMessage(currentComPort, ":%BD#", raw: true, SharedResources.responding.rString, currentEcp);
		if (text.Length == 0)
		{
			text = "0";
		}
		while (text.Length > 1 && text[0] == '0')
		{
			text = text.Substring(1);
		}
		return text;
	}

	private string GetLimitMeridianE()
	{
		string value = SharedResources.SendMessage(currentComPort, ":GXE9#", raw: true, SharedResources.responding.rString, currentEcp);
		double num;
		try
		{
			num = Convert.ToDouble(value, CultureInfo.GetCultureInfo("en-us"));
		}
		catch
		{
			num = 0.0;
		}
		return Math.Round(num * 15.0 / 60.0).ToString(CultureInfo.GetCultureInfo("en-us"));
	}

	private string GetLimitMeridianW()
	{
		string value = SharedResources.SendMessage(currentComPort, ":GXEA#", raw: true, SharedResources.responding.rString, currentEcp);
		double num;
		try
		{
			num = Convert.ToDouble(value, CultureInfo.GetCultureInfo("en-us"));
		}
		catch
		{
			num = 0.0;
		}
		return Math.Round(num * 15.0 / 60.0).ToString(CultureInfo.GetCultureInfo("en-us"));
	}

	private void gotoRate_ValueChanged(object sender, EventArgs e)
	{
		if (!blockGotoRateChange)
		{
			MaxRateCountdownToRefresh = 2;
		}
	}

	private void GetMaxRate()
	{
		try
		{
			string text = SharedResources.SendMessage(currentComPort, ":GX93#", raw: true, SharedResources.responding.rString, currentEcp);
			blockGotoRateChange = true;
			if (text == "0#" || text == "0")
			{
				gotoRate.Enabled = false;
			}
			else
			{
				gotoRate.Minimum = (int)(Convert.ToDouble(text.Substring(0, text.Length), CultureInfo.GetCultureInfo("en-us")) * 16.0) / 2;
				gotoRate.Maximum = (int)(Convert.ToDouble(text.Substring(0, text.Length), CultureInfo.GetCultureInfo("en-us")) * 16.0) * 2;
				int num = gotoRate.Maximum - gotoRate.Minimum;
				if (num < 160)
				{
					gotoRate.TickFrequency = 8;
				}
				else if (num < 1600)
				{
					gotoRate.TickFrequency = 80;
				}
				else if (num < 16000)
				{
					gotoRate.TickFrequency = 800;
				}
				else if (num < 160000)
				{
					gotoRate.TickFrequency = 8000;
				}
				gotoRate.Enabled = true;
			}
		}
		catch
		{
			gotoRate.Enabled = false;
		}
		if (gotoRate.Enabled)
		{
			try
			{
				string text = SharedResources.SendMessage(currentComPort, ":GX92#", raw: true, SharedResources.responding.rString, currentEcp);
				blockGotoRateChange = true;
				if (text == "0#" || text == "0")
				{
					gotoRate.Value = gotoRate.Maximum;
					gotoRate.Enabled = false;
				}
				else
				{
					int num2 = (int)(Convert.ToDouble(text, CultureInfo.GetCultureInfo("en-us")) * 16.0);
					if (num2 < gotoRate.Minimum)
					{
						num2 = gotoRate.Minimum;
					}
					if (num2 > gotoRate.Maximum)
					{
						num2 = gotoRate.Maximum;
					}
					gotoRate.Value = num2;
					gotoRate.Enabled = true;
				}
			}
			catch
			{
				gotoRate.Value = gotoRate.Maximum;
				gotoRate.Enabled = false;
			}
		}
		currentMaxRate.Text = $"{(double)gotoRate.Value / 16.0:0.##}";
		if (SharedResources.stepsPerSecond > 0.0)
		{
			currentDegSec.Text = "(" + $"{1.0 / ((double)gotoRate.Value / 16.0 / 1000000.0) / SharedResources.stepsPerSecond / 240.0:0.##}" + " deg/sec)";
		}
		else
		{
			currentDegSec.Text = "(Unknown deg/sec)";
		}
		blockGotoRateChange = false;
	}

	private void TimeoutTrackBar_ValueChanged(object sender, EventArgs e)
	{
		int num = TimeoutTrackBar.Value * 10;
		if (num < 1000)
		{
			num = 1000;
		}
		if (num > 5000)
		{
			num = 5000;
		}
		TimeoutLabel.Text = "Retry Timeout (" + num + "ms)";
	}

	private void refreshTimer_Tick(object sender, EventArgs e)
	{
		if (currentComPort == "")
		{
			return;
		}
		string text = SharedResources.SendMessage(currentComPort, ":GC#", raw: true, SharedResources.responding.rString, currentEcp);
		Date.Text = text;
		text = SharedResources.SendMessage(currentComPort, ":GL#", raw: true, SharedResources.responding.rString, currentEcp);
		LMT.Text = text;
		double num = 0.0;
		try
		{
			num = Convert.ToInt32(LMT.Text.Substring(0, 2));
			num += (double)Convert.ToInt32(LMT.Text.Substring(3, 2)) / 60.0;
			num += (double)Convert.ToInt32(LMT.Text.Substring(6, 2)) / 3600.0;
		}
		catch
		{
			num = 0.0;
		}
		double num3;
		try
		{
			int num2 = Offset.Text.IndexOf(":");
			if (Offset.Text.Length > 3 || num2 > 0)
			{
				num3 = Convert.ToDouble(Offset.Text.Substring(0, num2), CultureInfo.GetCultureInfo("en-us"));
				double num4 = Convert.ToDouble(Offset.Text.Substring(num2 + 1, 2), CultureInfo.GetCultureInfo("en-us")) / 60.0;
				num3 = ((!(num3 < 0.0)) ? (num3 + num4) : (num3 - num4));
				if (Offset.Text.Substring(num2 + 1, 2) != "45" && Offset.Text.Substring(num2 + 1, 2) != "30")
				{
					num3 = 0.0;
				}
			}
			else
			{
				num3 = Convert.ToDouble(Offset.Text, CultureInfo.GetCultureInfo("en-us"));
			}
		}
		catch
		{
			num3 = 0.0;
		}
		if (num3 < -14.0 || num3 > 12.0)
		{
			num3 = 0.0;
		}
		num = num + num3 + 1E-07;
		if (num >= 24.0)
		{
			num -= 24.0;
		}
		if (num < 0.0)
		{
			num += 24.0;
		}
		double num5 = (num - Math.Floor(num)) * 60.0;
		double d = (num5 - Math.Floor(num5)) * 60.0;
		text = Convert.ToInt16(Math.Floor(num)).ToString("D2") + ":" + Convert.ToInt16(Math.Floor(num5)).ToString("D2") + ":" + Convert.ToInt16(Math.Floor(d)).ToString("D2");
		UTC.Text = text;
		text = SharedResources.SendMessage(currentComPort, ":GS#", raw: true, SharedResources.responding.rString, currentEcp);
		LST.Text = text;
		if (MaxRateCountdownToRefresh <= 0)
		{
			return;
		}
		MaxRateCountdownToRefresh--;
		if (MaxRateCountdownToRefresh == 0)
		{
			text = SharedResources.SendMessage(currentComPort, ":SX92," + Convert.ToString((double)gotoRate.Value / 16.0, CultureInfo.GetCultureInfo("en-us")) + "#", raw: true, SharedResources.responding.rOne, currentEcp);
			if (text != "1")
			{
				GetMaxRate();
			}
			GetMaxRate();
			currentMaxRate.Text = $"{(double)gotoRate.Value / 16.0:0.##}";
			if (SharedResources.stepsPerSecond > 0.0)
			{
				currentDegSec.Text = "(" + $"{1.0 / ((double)gotoRate.Value / 16.0 / 1000000.0) / SharedResources.stepsPerSecond / 240.0:0.##}" + " deg/sec)";
			}
			else
			{
				currentDegSec.Text = "(Unknown deg/sec)";
			}
		}
	}

	private void startupTimer_Tick(object sender, EventArgs e)
	{
		startupTimer.Enabled = false;
		InitUI();
	}

	private void clearAndLockControls()
	{
		connectedDevice.Text = "None";
		cmdOK.Enabled = false;
		Lat.Text = "";
		OrigLat = "";
		Long.Text = "";
		OrigLong = "";
		Elevation.Text = "";
		Offset.Text = "";
		BacklashRA.Text = "";
		BacklashDec.Text = "";
		Date.Text = "-";
		UTC.Text = "-";
		LMT.Text = "-";
		LST.Text = "-";
		DateTimeCheckBox.Checked = false;
		LimitHorizon.Text = "";
		LimitOverhead.Text = "";
		LimitMeridianE.Text = "";
		LimitMeridianE.Enabled = false;
		labelME.Enabled = false;
		labelMED.Enabled = false;
		LimitMeridianW.Text = "";
		LimitMeridianW.Enabled = false;
		labelMW.Enabled = false;
		labelMWD.Enabled = false;
		Aperature.Text = "";
		Area.Text = "";
		LimitOverhead.Text = "";
		currentMaxRate.Text = "-";
		currentDegSec.Text = "";
		gotoRate.Minimum = 2;
		gotoRate.Maximum = 100;
		gotoRate.Value = 64;
		SiteGroupBox.Enabled = false;
		DateTimeGroupBox.Enabled = false;
		OpticsGroupBox.Enabled = false;
		BacklashGroupBox.Enabled = false;
		LimitGroupBox.Enabled = false;
		MaxRateGroupBox.Enabled = false;
	}

	private void SetupDialogForm_Shown(object sender, EventArgs e)
	{
		comboBoxComPort.Focus();
		base.WindowState = FormWindowState.Minimized;
		Show();
		base.WindowState = FormWindowState.Normal;
		EnableDTRcheckBox.Checked = Telescope.dtrControl;
		ChecksumCheckBox.Checked = Telescope.errorCorrection;
		clearAndLockControls();
		startupTimer.Enabled = true;
	}

	private void SetupDialogForm_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (!cancelClose)
		{
			refreshTimer.Enabled = false;
			if (currentComPort != "")
			{
				SharedResources.Disconnect(currentComPort);
				currentComPort = "";
			}
			tl.Enabled = chkTrace.Checked;
		}
		e.Cancel = cancelClose;
	}

	private void cmdOK_Click(object sender, EventArgs e)
	{
		cancelClose = true;
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		double num7 = 0.0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		int num11 = 0;
		int num12 = 0;
		int num13 = 0;
		Util util = new Util();
		try
		{
			num = util.DMSToDegrees(Lat.Text);
		}
		catch
		{
			num = -9999.0;
		}
		if (num < -90.0 || num > 90.0)
		{
			MessageBox.Show("Latitude is invalid, use +DD:MM:SS between -90 and 90 (degrees, + is North)");
			util.Dispose();
			return;
		}
		try
		{
			num2 = util.DMSToDegrees(Long.Text);
		}
		catch
		{
			num2 = -9999.0;
		}
		if (num2 < -180.0 || num2 > 180.0)
		{
			MessageBox.Show("Longitude is invalid, use +DDD:MM:SS between -180 and 180 (degrees, + is West)");
			util.Dispose();
			return;
		}
		try
		{
			num3 = util.HMSToHours(Offset.Text);
		}
		catch
		{
			num3 = -9999.0;
		}
		if (num3 < -14.0 || num3 > 12.0)
		{
			MessageBox.Show("Offset format is invalid, use +HH:MM between -14 and 12 (hours, opposite of Time Zone value)");
			util.Dispose();
			return;
		}
		try
		{
			num4 = Convert.ToDouble(Elevation.Text, CultureInfo.GetCultureInfo("en-us"));
		}
		catch
		{
			num4 = -9999.0;
		}
		if (num4 < -300.0 || num4 > 10000.0)
		{
			MessageBox.Show("Elevation is invalid, use a number between 0 and 10000 (meters)");
			util.Dispose();
			return;
		}
		try
		{
			num8 = Convert.ToInt16(BacklashRA.Text);
		}
		catch
		{
			num8 = -9999;
		}
		if (num8 < 0 || num8 > 999)
		{
			MessageBox.Show("Backlash RA/Azm is invalid, use a number between 0 and 999 (arc-seconds)");
			util.Dispose();
			return;
		}
		try
		{
			num9 = Convert.ToInt16(BacklashDec.Text);
		}
		catch
		{
			num9 = -9999;
		}
		if (num9 < 0 || num9 > 999)
		{
			MessageBox.Show("Backlash Dec/Alt is invalid, use a number between 0 and 999 (arc-seconds)");
			util.Dispose();
			return;
		}
		try
		{
			num10 = Convert.ToInt16(LimitOverhead.Text);
		}
		catch
		{
			num10 = -9999;
		}
		if (num10 < 60 || num10 > 90)
		{
			MessageBox.Show("Overhead Limit is invalid, use a number between 60 and 90 (degrees)");
			util.Dispose();
			return;
		}
		try
		{
			num11 = Convert.ToInt16(LimitHorizon.Text);
		}
		catch
		{
			num11 = -9999;
		}
		if (num11 < -30 || num11 > 30)
		{
			MessageBox.Show("Horizon Limit is invalid, use a number between -30 and 30 (degrees)");
			util.Dispose();
			return;
		}
		if (SharedResources.mountTypeGEM)
		{
			try
			{
				num12 = Convert.ToInt16(LimitMeridianE.Text);
			}
			catch
			{
				num12 = -9999;
			}
			if (num12 < -360 || num12 > 360)
			{
				MessageBox.Show("Meridian Limit East is invalid, use a number between -360 and 360 (degrees)");
				util.Dispose();
				return;
			}
			try
			{
				num13 = Convert.ToInt16(LimitMeridianW.Text);
			}
			catch
			{
				num13 = -9999;
			}
			if (num13 < -360 || num13 > 360)
			{
				MessageBox.Show("Meridian Limit West is invalid, use a number between -360 and 360 (degrees)");
				util.Dispose();
				return;
			}
		}
		try
		{
			num5 = Convert.ToDouble(Aperature.Text, CultureInfo.GetCultureInfo("en-us"));
		}
		catch
		{
			num5 = -9999.0;
		}
		if (num5 < 0.0 || num5 > 100.0)
		{
			MessageBox.Show("Aperature is invalid, use a number between 0 and 100 (meters)");
			util.Dispose();
			return;
		}
		try
		{
			num6 = Convert.ToDouble(Area.Text, CultureInfo.GetCultureInfo("en-us"));
		}
		catch
		{
			num6 = -9999.0;
		}
		if (num6 < 0.0 || num6 > 10.0)
		{
			MessageBox.Show("Area is invalid, use a number between 0 and 10 (sq meters)");
			util.Dispose();
			return;
		}
		try
		{
			num7 = Convert.ToDouble(FocalLength.Text, CultureInfo.GetCultureInfo("en-us"));
		}
		catch
		{
			num7 = -9999.0;
		}
		if (num7 < 0.0 || num7 > 10000.0)
		{
			MessageBox.Show("Focal Length is invalid, use a number between 0 and 1000 (meters)");
			util.Dispose();
			return;
		}
		Telescope.readTimeout = (int)Math.Round((double)TimeoutTrackBar.Value / 100.0);
		Telescope.comPort = (string)comboBoxComPort.SelectedItem;
		currentComPort = Telescope.comPort;
		if (currentComPort == "IP Address")
		{
			currentComPort = IPAddress.Text;
			Telescope.comPort = currentComPort;
		}
		else if (currentComPort == "HID Device")
		{
			currentComPort = BuildHidDeviceId();
			Telescope.comPort = currentComPort;
			Telescope.hidVid = HidVID.Text.Trim();
			Telescope.hidPid = HidPID.Text.Trim();
			Telescope.hidSerial = HidSN.Text.Trim();
		}
		Telescope.ipAddress = IPAddress.Text;
		Telescope.errorCorrection = ChecksumCheckBox.Checked;
		Telescope.dtrControl = EnableDTRcheckBox.Checked;
		Telescope.autoDateTime = DateTimeCheckBox.Checked;
		string text;
		if (OrigLat != Lat.Text)
		{
			text = ((SharedResources.versionOnStep <= 415) ? SharedResources.doubleToDm(num) : SharedResources.doubleToDms(num));
			if (SharedResources.SendMessage(currentComPort, ":St" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
			{
				MessageBox.Show("Latitude upload to OnStep failed: " + text);
				util.Dispose();
				return;
			}
		}
		if (OrigLong != Long.Text)
		{
			if (SharedResources.versionOnStep > 415)
			{
				text = SharedResources.doubleToDms2(Math.Abs(num2));
				text = ((!(num2 < 0.0)) ? ("+" + text) : ("-" + text));
			}
			else
			{
				text = SharedResources.doubleToDm2(num2);
			}
			if (SharedResources.SendMessage(currentComPort, ":Sg" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
			{
				MessageBox.Show("Longitude upload to OnStep failed: " + text);
				util.Dispose();
				return;
			}
		}
		text = SharedResources.timeZoneToHM(num3);
		if (SharedResources.SendMessage(currentComPort, ":SG" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
		{
			MessageBox.Show("Offset upload to OnStep failed: " + text);
			util.Dispose();
			return;
		}
		text = num8.ToString(CultureInfo.GetCultureInfo("en-us"));
		if (SharedResources.SendMessage(currentComPort, ":$BR" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
		{
			MessageBox.Show("Backlash RA/Azm upload to OnStep failed: " + text);
			util.Dispose();
			return;
		}
		text = num9.ToString(CultureInfo.GetCultureInfo("en-us"));
		if (SharedResources.SendMessage(currentComPort, ":$BD" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
		{
			MessageBox.Show("Backlash Dec/Alt upload to OnStep failed: " + text);
			util.Dispose();
			return;
		}
		text = num11.ToString(CultureInfo.GetCultureInfo("en-us"));
		if (SharedResources.SendMessage(currentComPort, ":Sh" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
		{
			MessageBox.Show("Horizon Limit upload to OnStep failed: " + text);
			util.Dispose();
			return;
		}
		text = num10.ToString(CultureInfo.GetCultureInfo("en-us"));
		if (SharedResources.SendMessage(currentComPort, ":So" + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
		{
			MessageBox.Show("Overhead Limit upload to OnStep failed: " + text);
			util.Dispose();
			return;
		}
		if (SharedResources.mountTypeGEM)
		{
			text = (num12 * 4).ToString(CultureInfo.GetCultureInfo("en-us"));
			if (SharedResources.SendMessage(currentComPort, ":SXE9," + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
			{
				MessageBox.Show("Meridian Limit East upload to OnStep failed: " + text);
				util.Dispose();
				return;
			}
			text = (num13 * 4).ToString(CultureInfo.GetCultureInfo("en-us"));
			if (SharedResources.SendMessage(currentComPort, ":SXEA," + text + "#", raw: true, SharedResources.responding.rOne, currentEcp) != "1")
			{
				MessageBox.Show("Meridian Limit West upload to OnStep failed");
				util.Dispose();
				return;
			}
		}
		Telescope.siteElevation = num4;
		Telescope.apertureDiameter = Convert.ToDouble(Aperature.Text, CultureInfo.GetCultureInfo("en-us"));
		Telescope.apertureArea = Convert.ToDouble(Area.Text, CultureInfo.GetCultureInfo("en-us"));
		Telescope.focalLength = Convert.ToDouble(FocalLength.Text, CultureInfo.GetCultureInfo("en-us"));
		util.Dispose();
		cancelClose = false;
	}

	private void cmdCancel_Click(object sender, EventArgs e)
	{
		cancelClose = false;
		Close();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ASCOM.OnStep.SetupDialogForm));
		this.cmdOK = new System.Windows.Forms.Button();
		this.cmdCancel = new System.Windows.Forms.Button();
		this.picASCOM = new System.Windows.Forms.PictureBox();
		this.chkTrace = new System.Windows.Forms.CheckBox();
		this.comboBoxComPort = new System.Windows.Forms.ComboBox();
		this.pictureBox1 = new System.Windows.Forms.PictureBox();
		this.pictureBox2 = new System.Windows.Forms.PictureBox();
		this.TimeoutLabel = new System.Windows.Forms.Label();
		this.TimeoutTrackBar = new System.Windows.Forms.TrackBar();
		this.label12 = new System.Windows.Forms.Label();
		this.IPAddress = new System.Windows.Forms.TextBox();
		this.label17 = new System.Windows.Forms.Label();
		this.HidVID = new System.Windows.Forms.TextBox();
		this.label18 = new System.Windows.Forms.Label();
		this.HidPID = new System.Windows.Forms.TextBox();
		this.label15 = new System.Windows.Forms.Label();
		this.HidSN = new System.Windows.Forms.TextBox();
		this.label16 = new System.Windows.Forms.Label();
		this.label11 = new System.Windows.Forms.Label();
		this.MaxRateGroupBox = new System.Windows.Forms.GroupBox();
		this.label10 = new System.Windows.Forms.Label();
		this.currentDegSec = new System.Windows.Forms.Label();
		this.currentMaxRate = new System.Windows.Forms.Label();
		this.gotoRate = new System.Windows.Forms.TrackBar();
		this.LimitGroupBox = new System.Windows.Forms.GroupBox();
		this.labelMWD = new System.Windows.Forms.Label();
		this.LimitMeridianW = new System.Windows.Forms.TextBox();
		this.labelMW = new System.Windows.Forms.Label();
		this.labelMED = new System.Windows.Forms.Label();
		this.LimitMeridianE = new System.Windows.Forms.TextBox();
		this.labelME = new System.Windows.Forms.Label();
		this.label6 = new System.Windows.Forms.Label();
		this.label7 = new System.Windows.Forms.Label();
		this.LimitOverhead = new System.Windows.Forms.TextBox();
		this.label8 = new System.Windows.Forms.Label();
		this.LimitHorizon = new System.Windows.Forms.TextBox();
		this.label9 = new System.Windows.Forms.Label();
		this.DriverVersion = new System.Windows.Forms.Label();
		this.BacklashGroupBox = new System.Windows.Forms.GroupBox();
		this.label5 = new System.Windows.Forms.Label();
		this.label4 = new System.Windows.Forms.Label();
		this.BacklashDec = new System.Windows.Forms.TextBox();
		this.label13 = new System.Windows.Forms.Label();
		this.BacklashRA = new System.Windows.Forms.TextBox();
		this.label14 = new System.Windows.Forms.Label();
		this.OpticsGroupBox = new System.Windows.Forms.GroupBox();
		this.FocalLength = new System.Windows.Forms.TextBox();
		this.Area = new System.Windows.Forms.TextBox();
		this.Aperature = new System.Windows.Forms.TextBox();
		this.FL_Label = new System.Windows.Forms.Label();
		this.Area_Label = new System.Windows.Forms.Label();
		this.Aperature_Label = new System.Windows.Forms.Label();
		this.connectedDevice = new System.Windows.Forms.TextBox();
		this.Connect_Label = new System.Windows.Forms.Label();
		this.RS232_Label = new System.Windows.Forms.Label();
		this.DateTimeGroupBox = new System.Windows.Forms.GroupBox();
		this.DateTimeCheckBox = new System.Windows.Forms.CheckBox();
		this.UTC = new System.Windows.Forms.Label();
		this.UTC_Label = new System.Windows.Forms.Label();
		this.LST = new System.Windows.Forms.Label();
		this.LST_Label = new System.Windows.Forms.Label();
		this.LMT = new System.Windows.Forms.Label();
		this.Date = new System.Windows.Forms.Label();
		this.LMT_Label = new System.Windows.Forms.Label();
		this.Date_Label = new System.Windows.Forms.Label();
		this.SiteGroupBox = new System.Windows.Forms.GroupBox();
		this.Offset = new System.Windows.Forms.TextBox();
		this.Lat = new System.Windows.Forms.TextBox();
		this.Long = new System.Windows.Forms.TextBox();
		this.Offset_Label = new System.Windows.Forms.Label();
		this.Elevation = new System.Windows.Forms.TextBox();
		this.Ele_Label = new System.Windows.Forms.Label();
		this.Long_Label = new System.Windows.Forms.Label();
		this.Lat_Label = new System.Windows.Forms.Label();
		this.refreshTimer = new System.Windows.Forms.Timer(this.components);
		this.startupTimer = new System.Windows.Forms.Timer(this.components);
		this.ChecksumCheckBox = new System.Windows.Forms.CheckBox();
		this.EnableDTRcheckBox = new System.Windows.Forms.CheckBox();
		((System.ComponentModel.ISupportInitialize)this.picASCOM).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.pictureBox2).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.TimeoutTrackBar).BeginInit();
		this.MaxRateGroupBox.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.gotoRate).BeginInit();
		this.LimitGroupBox.SuspendLayout();
		this.BacklashGroupBox.SuspendLayout();
		this.OpticsGroupBox.SuspendLayout();
		this.DateTimeGroupBox.SuspendLayout();
		this.SiteGroupBox.SuspendLayout();
		base.SuspendLayout();
		this.cmdOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.cmdOK.Location = new System.Drawing.Point(566, 352);
		this.cmdOK.Name = "cmdOK";
		this.cmdOK.Size = new System.Drawing.Size(59, 25);
		this.cmdOK.TabIndex = 0;
		this.cmdOK.Text = "OK";
		this.cmdOK.UseVisualStyleBackColor = true;
		this.cmdOK.Click += new System.EventHandler(cmdOK_Click);
		this.cmdCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.cmdCancel.Location = new System.Drawing.Point(631, 352);
		this.cmdCancel.Name = "cmdCancel";
		this.cmdCancel.Size = new System.Drawing.Size(59, 25);
		this.cmdCancel.TabIndex = 1;
		this.cmdCancel.Text = "Cancel";
		this.cmdCancel.UseVisualStyleBackColor = true;
		this.cmdCancel.Click += new System.EventHandler(cmdCancel_Click);
		this.picASCOM.Cursor = System.Windows.Forms.Cursors.Hand;
		this.picASCOM.Image = ASCOM.OnStep.Properties.Resources.ASCOM;
		this.picASCOM.Location = new System.Drawing.Point(12, 11);
		this.picASCOM.Name = "picASCOM";
		this.picASCOM.Size = new System.Drawing.Size(48, 56);
		this.picASCOM.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
		this.picASCOM.TabIndex = 3;
		this.picASCOM.TabStop = false;
		this.picASCOM.Click += new System.EventHandler(BrowseToAscom);
		this.picASCOM.DoubleClick += new System.EventHandler(BrowseToAscom);
		this.chkTrace.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		this.chkTrace.AutoSize = true;
		this.chkTrace.Location = new System.Drawing.Point(477, 356);
		this.chkTrace.Name = "chkTrace";
		this.chkTrace.Size = new System.Drawing.Size(69, 17);
		this.chkTrace.TabIndex = 6;
		this.chkTrace.Text = "Trace on";
		this.chkTrace.UseVisualStyleBackColor = true;
		this.comboBoxComPort.FormattingEnabled = true;
		this.comboBoxComPort.Location = new System.Drawing.Point(388, 16);
		this.comboBoxComPort.Name = "comboBoxComPort";
		this.comboBoxComPort.Size = new System.Drawing.Size(90, 21);
		this.comboBoxComPort.TabIndex = 7;
		this.comboBoxComPort.TextChanged += new System.EventHandler(comboBoxComPort_TextChanged);
		this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pictureBox1.Image = (System.Drawing.Image)resources.GetObject("pictureBox1.Image");
		this.pictureBox1.Location = new System.Drawing.Point(67, 11);
		this.pictureBox1.Name = "pictureBox1";
		this.pictureBox1.Size = new System.Drawing.Size(273, 57);
		this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
		this.pictureBox1.TabIndex = 58;
		this.pictureBox1.TabStop = false;
		this.pictureBox1.Visible = false;
		this.pictureBox2.BackColor = System.Drawing.Color.White;
		this.pictureBox2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pictureBox2.Image = (System.Drawing.Image)resources.GetObject("pictureBox2.Image");
		this.pictureBox2.Location = new System.Drawing.Point(67, 11);
		this.pictureBox2.Name = "pictureBox2";
		this.pictureBox2.Size = new System.Drawing.Size(273, 57);
		this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
		this.pictureBox2.TabIndex = 60;
		this.pictureBox2.TabStop = false;
		this.TimeoutLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.TimeoutLabel.Location = new System.Drawing.Point(348, 50);
		this.TimeoutLabel.Name = "TimeoutLabel";
		this.TimeoutLabel.Size = new System.Drawing.Size(128, 18);
		this.TimeoutLabel.TabIndex = 56;
		this.TimeoutLabel.Text = "Retry Timeout (3000ms):";
		this.TimeoutLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.TimeoutTrackBar.AutoSize = false;
		this.TimeoutTrackBar.Location = new System.Drawing.Point(473, 48);
		this.TimeoutTrackBar.Maximum = 500;
		this.TimeoutTrackBar.Minimum = 100;
		this.TimeoutTrackBar.Name = "TimeoutTrackBar";
		this.TimeoutTrackBar.Size = new System.Drawing.Size(216, 20);
		this.TimeoutTrackBar.TabIndex = 55;
		this.TimeoutTrackBar.TickFrequency = 50;
		this.TimeoutTrackBar.Value = 300;
		this.TimeoutTrackBar.ValueChanged += new System.EventHandler(TimeoutTrackBar_ValueChanged);
		this.label12.AutoSize = true;
		this.label12.Location = new System.Drawing.Point(512, 19);
		this.label12.Name = "label12";
		this.label12.Size = new System.Drawing.Size(61, 13);
		this.label12.TabIndex = 54;
		this.label12.Text = "IP Address:";
		this.IPAddress.Location = new System.Drawing.Point(579, 16);
		this.IPAddress.Name = "IPAddress";
		this.IPAddress.Size = new System.Drawing.Size(107, 20);
		this.IPAddress.TabIndex = 53;
		this.label17.AutoSize = true;
		this.label17.Location = new System.Drawing.Point(505, 41);
		this.label17.Name = "label17";
		this.label17.Size = new System.Drawing.Size(28, 13);
		this.label17.TabIndex = 61;
		this.label17.Text = "VID:";
		this.HidVID.Location = new System.Drawing.Point(535, 38);
		this.HidVID.Name = "HidVID";
		this.HidVID.Size = new System.Drawing.Size(48, 20);
		this.HidVID.TabIndex = 62;
		this.HidVID.Text = "1A86";
		this.HidVID.TextChanged += new System.EventHandler(comboBoxComPort_TextChanged);
		this.label18.AutoSize = true;
		this.label18.Location = new System.Drawing.Point(585, 41);
		this.label18.Name = "label18";
		this.label18.Size = new System.Drawing.Size(28, 13);
		this.label18.TabIndex = 63;
		this.label18.Text = "PID:";
		this.HidPID.Location = new System.Drawing.Point(615, 38);
		this.HidPID.Name = "HidPID";
		this.HidPID.Size = new System.Drawing.Size(48, 20);
		this.HidPID.TabIndex = 64;
		this.HidPID.Text = "55D4";
		this.HidPID.TextChanged += new System.EventHandler(comboBoxComPort_TextChanged);
		this.label15.AutoSize = true;
		this.label15.Location = new System.Drawing.Point(665, 41);
		this.label15.Name = "label15";
		this.label15.Size = new System.Drawing.Size(28, 13);
		this.label15.TabIndex = 65;
		this.label15.Text = "SN:";
		this.HidSN.Location = new System.Drawing.Point(695, 38);
		this.HidSN.Name = "HidSN";
		this.HidSN.Size = new System.Drawing.Size(90, 20);
		this.HidSN.TabIndex = 66;
		this.HidSN.Text = "A8-0001";
		this.HidSN.TextChanged += new System.EventHandler(comboBoxComPort_TextChanged);
		this.label16.AutoSize = true;
		this.label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5f, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, 0);
		this.label16.Location = new System.Drawing.Point(505, 62);
		this.label16.Name = "label16";
		this.label16.Size = new System.Drawing.Size(280, 12);
		this.label16.TabIndex = 67;
		this.label16.Text = "HID: VID/PID/SN (hex VID/PID; SN blank = any)";
		this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5f, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, 0);
		this.label11.Location = new System.Drawing.Point(15, 249);
		this.label11.Name = "label11";
		this.label11.Size = new System.Drawing.Size(208, 46);
		this.label11.TabIndex = 52;
		this.label11.Text = "NOTE: OnStep never uses Daylight Savings Time internally, so all time related values are based on Standard Time.";
		this.MaxRateGroupBox.Controls.Add(this.label10);
		this.MaxRateGroupBox.Controls.Add(this.currentDegSec);
		this.MaxRateGroupBox.Controls.Add(this.currentMaxRate);
		this.MaxRateGroupBox.Controls.Add(this.gotoRate);
		this.MaxRateGroupBox.Location = new System.Drawing.Point(469, 253);
		this.MaxRateGroupBox.Name = "MaxRateGroupBox";
		this.MaxRateGroupBox.Size = new System.Drawing.Size(211, 86);
		this.MaxRateGroupBox.TabIndex = 51;
		this.MaxRateGroupBox.TabStop = false;
		this.MaxRateGroupBox.Text = "Max. Goto Rate";
		this.label10.AutoSize = true;
		this.label10.Location = new System.Drawing.Point(170, 33);
		this.label10.Name = "label10";
		this.label10.Size = new System.Drawing.Size(18, 13);
		this.label10.TabIndex = 3;
		this.label10.Text = "us";
		this.currentDegSec.Location = new System.Drawing.Point(13, 57);
		this.currentDegSec.Name = "currentDegSec";
		this.currentDegSec.Size = new System.Drawing.Size(187, 23);
		this.currentDegSec.TabIndex = 2;
		this.currentDegSec.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.currentMaxRate.AutoSize = true;
		this.currentMaxRate.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.currentMaxRate.Location = new System.Drawing.Point(169, 22);
		this.currentMaxRate.Name = "currentMaxRate";
		this.currentMaxRate.Size = new System.Drawing.Size(10, 13);
		this.currentMaxRate.TabIndex = 1;
		this.currentMaxRate.Text = "-";
		this.gotoRate.AutoSize = false;
		this.gotoRate.LargeChange = 20;
		this.gotoRate.Location = new System.Drawing.Point(12, 18);
		this.gotoRate.Maximum = 100;
		this.gotoRate.Minimum = 2;
		this.gotoRate.Name = "gotoRate";
		this.gotoRate.Size = new System.Drawing.Size(152, 35);
		this.gotoRate.TabIndex = 0;
		this.gotoRate.TickFrequency = 10;
		this.gotoRate.Value = 64;
		this.gotoRate.ValueChanged += new System.EventHandler(gotoRate_ValueChanged);
		this.LimitGroupBox.Controls.Add(this.labelMWD);
		this.LimitGroupBox.Controls.Add(this.LimitMeridianW);
		this.LimitGroupBox.Controls.Add(this.labelMW);
		this.LimitGroupBox.Controls.Add(this.labelMED);
		this.LimitGroupBox.Controls.Add(this.LimitMeridianE);
		this.LimitGroupBox.Controls.Add(this.labelME);
		this.LimitGroupBox.Controls.Add(this.label6);
		this.LimitGroupBox.Controls.Add(this.label7);
		this.LimitGroupBox.Controls.Add(this.LimitOverhead);
		this.LimitGroupBox.Controls.Add(this.label8);
		this.LimitGroupBox.Controls.Add(this.LimitHorizon);
		this.LimitGroupBox.Controls.Add(this.label9);
		this.LimitGroupBox.Location = new System.Drawing.Point(239, 253);
		this.LimitGroupBox.Name = "LimitGroupBox";
		this.LimitGroupBox.Size = new System.Drawing.Size(210, 125);
		this.LimitGroupBox.TabIndex = 49;
		this.LimitGroupBox.TabStop = false;
		this.LimitGroupBox.Text = "Limits";
		this.labelMWD.AutoSize = true;
		this.labelMWD.Location = new System.Drawing.Point(161, 100);
		this.labelMWD.Name = "labelMWD";
		this.labelMWD.Size = new System.Drawing.Size(31, 13);
		this.labelMWD.TabIndex = 17;
		this.labelMWD.Text = "(deg)";
		this.LimitMeridianW.Location = new System.Drawing.Point(84, 97);
		this.LimitMeridianW.Name = "LimitMeridianW";
		this.LimitMeridianW.Size = new System.Drawing.Size(71, 20);
		this.LimitMeridianW.TabIndex = 16;
		this.labelMW.AutoSize = true;
		this.labelMW.Location = new System.Drawing.Point(17, 100);
		this.labelMW.Name = "labelMW";
		this.labelMW.Size = new System.Drawing.Size(64, 13);
		this.labelMW.TabIndex = 15;
		this.labelMW.Text = "Meridian W:";
		this.labelMED.AutoSize = true;
		this.labelMED.Location = new System.Drawing.Point(161, 75);
		this.labelMED.Name = "labelMED";
		this.labelMED.Size = new System.Drawing.Size(31, 13);
		this.labelMED.TabIndex = 14;
		this.labelMED.Text = "(deg)";
		this.LimitMeridianE.Location = new System.Drawing.Point(84, 72);
		this.LimitMeridianE.Name = "LimitMeridianE";
		this.LimitMeridianE.Size = new System.Drawing.Size(71, 20);
		this.LimitMeridianE.TabIndex = 13;
		this.labelME.AutoSize = true;
		this.labelME.Location = new System.Drawing.Point(17, 75);
		this.labelME.Name = "labelME";
		this.labelME.Size = new System.Drawing.Size(60, 13);
		this.labelME.TabIndex = 12;
		this.labelME.Text = "Meridian E:";
		this.label6.AutoSize = true;
		this.label6.Location = new System.Drawing.Point(161, 50);
		this.label6.Name = "label6";
		this.label6.Size = new System.Drawing.Size(31, 13);
		this.label6.TabIndex = 11;
		this.label6.Text = "(deg)";
		this.label7.AutoSize = true;
		this.label7.Location = new System.Drawing.Point(161, 25);
		this.label7.Name = "label7";
		this.label7.Size = new System.Drawing.Size(31, 13);
		this.label7.TabIndex = 10;
		this.label7.Text = "(deg)";
		this.LimitOverhead.Location = new System.Drawing.Point(84, 47);
		this.LimitOverhead.Name = "LimitOverhead";
		this.LimitOverhead.Size = new System.Drawing.Size(71, 20);
		this.LimitOverhead.TabIndex = 9;
		this.label8.AutoSize = true;
		this.label8.Location = new System.Drawing.Point(17, 50);
		this.label8.Name = "label8";
		this.label8.Size = new System.Drawing.Size(57, 13);
		this.label8.TabIndex = 7;
		this.label8.Text = "Overhead:";
		this.LimitHorizon.Location = new System.Drawing.Point(84, 22);
		this.LimitHorizon.Name = "LimitHorizon";
		this.LimitHorizon.Size = new System.Drawing.Size(71, 20);
		this.LimitHorizon.TabIndex = 8;
		this.label9.AutoSize = true;
		this.label9.Location = new System.Drawing.Point(17, 25);
		this.label9.Name = "label9";
		this.label9.Size = new System.Drawing.Size(46, 13);
		this.label9.TabIndex = 6;
		this.label9.Text = "Horizon:";
		this.DriverVersion.AutoSize = true;
		this.DriverVersion.BackColor = System.Drawing.Color.White;
		this.DriverVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.DriverVersion.Location = new System.Drawing.Point(69, 13);
		this.DriverVersion.Name = "DriverVersion";
		this.DriverVersion.Size = new System.Drawing.Size(11, 15);
		this.DriverVersion.TabIndex = 50;
		this.DriverVersion.Text = "-";
		this.BacklashGroupBox.Controls.Add(this.label5);
		this.BacklashGroupBox.Controls.Add(this.label4);
		this.BacklashGroupBox.Controls.Add(this.BacklashDec);
		this.BacklashGroupBox.Controls.Add(this.label13);
		this.BacklashGroupBox.Controls.Add(this.BacklashRA);
		this.BacklashGroupBox.Controls.Add(this.label14);
		this.BacklashGroupBox.Location = new System.Drawing.Point(17, 298);
		this.BacklashGroupBox.Name = "BacklashGroupBox";
		this.BacklashGroupBox.Size = new System.Drawing.Size(200, 77);
		this.BacklashGroupBox.TabIndex = 48;
		this.BacklashGroupBox.TabStop = false;
		this.BacklashGroupBox.Text = "Backlash";
		this.label5.AutoSize = true;
		this.label5.Location = new System.Drawing.Point(136, 52);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(48, 13);
		this.label5.TabIndex = 5;
		this.label5.Text = "(arc-sec)";
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(136, 28);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(48, 13);
		this.label4.TabIndex = 4;
		this.label4.Text = "(arc-sec)";
		this.BacklashDec.Location = new System.Drawing.Point(74, 47);
		this.BacklashDec.Name = "BacklashDec";
		this.BacklashDec.Size = new System.Drawing.Size(56, 20);
		this.BacklashDec.TabIndex = 3;
		this.label13.AutoSize = true;
		this.label13.Location = new System.Drawing.Point(19, 50);
		this.label13.Name = "label13";
		this.label13.Size = new System.Drawing.Size(47, 13);
		this.label13.TabIndex = 1;
		this.label13.Text = "Dec/Alt:";
		this.BacklashRA.Location = new System.Drawing.Point(74, 22);
		this.BacklashRA.Name = "BacklashRA";
		this.BacklashRA.Size = new System.Drawing.Size(56, 20);
		this.BacklashRA.TabIndex = 2;
		this.label14.AutoSize = true;
		this.label14.Location = new System.Drawing.Point(19, 28);
		this.label14.Name = "label14";
		this.label14.Size = new System.Drawing.Size(50, 13);
		this.label14.TabIndex = 0;
		this.label14.Text = "RA/Azm:";
		this.OpticsGroupBox.Controls.Add(this.FocalLength);
		this.OpticsGroupBox.Controls.Add(this.Area);
		this.OpticsGroupBox.Controls.Add(this.Aperature);
		this.OpticsGroupBox.Controls.Add(this.FL_Label);
		this.OpticsGroupBox.Controls.Add(this.Area_Label);
		this.OpticsGroupBox.Controls.Add(this.Aperature_Label);
		this.OpticsGroupBox.Location = new System.Drawing.Point(469, 118);
		this.OpticsGroupBox.Name = "OpticsGroupBox";
		this.OpticsGroupBox.Size = new System.Drawing.Size(211, 128);
		this.OpticsGroupBox.TabIndex = 47;
		this.OpticsGroupBox.TabStop = false;
		this.OpticsGroupBox.Text = "Optics";
		this.FocalLength.Location = new System.Drawing.Point(126, 92);
		this.FocalLength.Name = "FocalLength";
		this.FocalLength.Size = new System.Drawing.Size(68, 20);
		this.FocalLength.TabIndex = 5;
		this.Area.Location = new System.Drawing.Point(126, 57);
		this.Area.Name = "Area";
		this.Area.Size = new System.Drawing.Size(68, 20);
		this.Area.TabIndex = 4;
		this.Aperature.Location = new System.Drawing.Point(126, 22);
		this.Aperature.Name = "Aperature";
		this.Aperature.Size = new System.Drawing.Size(68, 20);
		this.Aperature.TabIndex = 3;
		this.FL_Label.AutoSize = true;
		this.FL_Label.Location = new System.Drawing.Point(7, 95);
		this.FL_Label.Name = "FL_Label";
		this.FL_Label.Size = new System.Drawing.Size(89, 13);
		this.FL_Label.TabIndex = 2;
		this.FL_Label.Text = "Focal Length (m):";
		this.Area_Label.AutoSize = true;
		this.Area_Label.Location = new System.Drawing.Point(7, 60);
		this.Area_Label.Name = "Area_Label";
		this.Area_Label.Size = new System.Drawing.Size(104, 13);
		this.Area_Label.TabIndex = 1;
		this.Area_Label.Text = "Aperture Area (m^2):";
		this.Aperature_Label.AutoSize = true;
		this.Aperature_Label.Location = new System.Drawing.Point(7, 25);
		this.Aperature_Label.Name = "Aperature_Label";
		this.Aperature_Label.Size = new System.Drawing.Size(67, 13);
		this.Aperature_Label.TabIndex = 0;
		this.Aperature_Label.Text = "Aperture (m):";
		this.connectedDevice.Location = new System.Drawing.Point(152, 80);
		this.connectedDevice.Name = "connectedDevice";
		this.connectedDevice.ReadOnly = true;
		this.connectedDevice.Size = new System.Drawing.Size(152, 20);
		this.connectedDevice.TabIndex = 44;
		this.connectedDevice.Text = "None";
		this.connectedDevice.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
		this.Connect_Label.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.Connect_Label.Location = new System.Drawing.Point(22, 83);
		this.Connect_Label.Name = "Connect_Label";
		this.Connect_Label.Size = new System.Drawing.Size(125, 18);
		this.Connect_Label.TabIndex = 43;
		this.Connect_Label.Text = "Currently connected to:";
		this.Connect_Label.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.RS232_Label.AutoSize = true;
		this.RS232_Label.Location = new System.Drawing.Point(353, 19);
		this.RS232_Label.Name = "RS232_Label";
		this.RS232_Label.Size = new System.Drawing.Size(29, 13);
		this.RS232_Label.TabIndex = 41;
		this.RS232_Label.Text = "Port:";
		this.DateTimeGroupBox.Controls.Add(this.DateTimeCheckBox);
		this.DateTimeGroupBox.Controls.Add(this.UTC);
		this.DateTimeGroupBox.Controls.Add(this.UTC_Label);
		this.DateTimeGroupBox.Controls.Add(this.LST);
		this.DateTimeGroupBox.Controls.Add(this.LST_Label);
		this.DateTimeGroupBox.Controls.Add(this.LMT);
		this.DateTimeGroupBox.Controls.Add(this.Date);
		this.DateTimeGroupBox.Controls.Add(this.LMT_Label);
		this.DateTimeGroupBox.Controls.Add(this.Date_Label);
		this.DateTimeGroupBox.Location = new System.Drawing.Point(238, 118);
		this.DateTimeGroupBox.Name = "DateTimeGroupBox";
		this.DateTimeGroupBox.Size = new System.Drawing.Size(211, 128);
		this.DateTimeGroupBox.TabIndex = 46;
		this.DateTimeGroupBox.TabStop = false;
		this.DateTimeGroupBox.Text = "Date/Time";
		this.DateTimeCheckBox.AutoSize = true;
		this.DateTimeCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.DateTimeCheckBox.Location = new System.Drawing.Point(17, 105);
		this.DateTimeCheckBox.Name = "DateTimeCheckBox";
		this.DateTimeCheckBox.Size = new System.Drawing.Size(172, 17);
		this.DateTimeCheckBox.TabIndex = 8;
		this.DateTimeCheckBox.Text = "Set Date/Time on Connect:     ";
		this.DateTimeCheckBox.UseVisualStyleBackColor = true;
		this.UTC.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.UTC.Location = new System.Drawing.Point(110, 65);
		this.UTC.Name = "UTC";
		this.UTC.Size = new System.Drawing.Size(79, 13);
		this.UTC.TabIndex = 0;
		this.UTC.Text = "-";
		this.UTC.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.UTC_Label.AutoSize = true;
		this.UTC_Label.Location = new System.Drawing.Point(17, 65);
		this.UTC_Label.Name = "UTC_Label";
		this.UTC_Label.Size = new System.Drawing.Size(63, 13);
		this.UTC_Label.TabIndex = 4;
		this.UTC_Label.Text = "Time (UT1):";
		this.LST.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.LST.Location = new System.Drawing.Point(110, 85);
		this.LST.Name = "LST";
		this.LST.Size = new System.Drawing.Size(79, 13);
		this.LST.TabIndex = 1;
		this.LST.Text = "-";
		this.LST.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.LST_Label.AutoSize = true;
		this.LST_Label.Location = new System.Drawing.Point(17, 85);
		this.LST_Label.Name = "LST_Label";
		this.LST_Label.Size = new System.Drawing.Size(62, 13);
		this.LST_Label.TabIndex = 5;
		this.LST_Label.Text = "Time (LST):";
		this.LMT.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.LMT.Location = new System.Drawing.Point(110, 45);
		this.LMT.Name = "LMT";
		this.LMT.Size = new System.Drawing.Size(79, 13);
		this.LMT.TabIndex = 7;
		this.LMT.Text = "-";
		this.LMT.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.Date.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.Date.Location = new System.Drawing.Point(110, 25);
		this.Date.Name = "Date";
		this.Date.Size = new System.Drawing.Size(79, 13);
		this.Date.TabIndex = 6;
		this.Date.Text = "-";
		this.Date.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.LMT_Label.AutoSize = true;
		this.LMT_Label.Location = new System.Drawing.Point(17, 45);
		this.LMT_Label.Name = "LMT_Label";
		this.LMT_Label.Size = new System.Drawing.Size(79, 13);
		this.LMT_Label.TabIndex = 3;
		this.LMT_Label.Text = "Standard Time:";
		this.Date_Label.AutoSize = true;
		this.Date_Label.Location = new System.Drawing.Point(17, 25);
		this.Date_Label.Name = "Date_Label";
		this.Date_Label.Size = new System.Drawing.Size(33, 13);
		this.Date_Label.TabIndex = 2;
		this.Date_Label.Text = "Date:";
		this.SiteGroupBox.Controls.Add(this.Offset);
		this.SiteGroupBox.Controls.Add(this.Lat);
		this.SiteGroupBox.Controls.Add(this.Long);
		this.SiteGroupBox.Controls.Add(this.Offset_Label);
		this.SiteGroupBox.Controls.Add(this.Elevation);
		this.SiteGroupBox.Controls.Add(this.Ele_Label);
		this.SiteGroupBox.Controls.Add(this.Long_Label);
		this.SiteGroupBox.Controls.Add(this.Lat_Label);
		this.SiteGroupBox.Location = new System.Drawing.Point(17, 118);
		this.SiteGroupBox.Name = "SiteGroupBox";
		this.SiteGroupBox.Size = new System.Drawing.Size(200, 128);
		this.SiteGroupBox.TabIndex = 45;
		this.SiteGroupBox.TabStop = false;
		this.SiteGroupBox.Text = "Site Information";
		this.Offset.Location = new System.Drawing.Point(121, 99);
		this.Offset.Name = "Offset";
		this.Offset.Size = new System.Drawing.Size(68, 20);
		this.Offset.TabIndex = 7;
		this.Lat.Location = new System.Drawing.Point(121, 21);
		this.Lat.Name = "Lat";
		this.Lat.Size = new System.Drawing.Size(68, 20);
		this.Lat.TabIndex = 4;
		this.Long.Location = new System.Drawing.Point(121, 45);
		this.Long.Name = "Long";
		this.Long.Size = new System.Drawing.Size(68, 20);
		this.Long.TabIndex = 5;
		this.Offset_Label.Location = new System.Drawing.Point(7, 94);
		this.Offset_Label.Name = "Offset_Label";
		this.Offset_Label.Size = new System.Drawing.Size(110, 26);
		this.Offset_Label.TabIndex = 3;
		this.Offset_Label.Text = "UTC Offset (opposite of a time-zone value):";
		this.Elevation.Location = new System.Drawing.Point(121, 69);
		this.Elevation.Name = "Elevation";
		this.Elevation.Size = new System.Drawing.Size(68, 20);
		this.Elevation.TabIndex = 6;
		this.Ele_Label.AutoSize = true;
		this.Ele_Label.Location = new System.Drawing.Point(7, 73);
		this.Ele_Label.Name = "Ele_Label";
		this.Ele_Label.Size = new System.Drawing.Size(71, 13);
		this.Ele_Label.TabIndex = 2;
		this.Ele_Label.Text = "Elevation (m):";
		this.Long_Label.AutoSize = true;
		this.Long_Label.Location = new System.Drawing.Point(7, 49);
		this.Long_Label.Name = "Long_Label";
		this.Long_Label.Size = new System.Drawing.Size(96, 13);
		this.Long_Label.TabIndex = 1;
		this.Long_Label.Text = "Longitude (W is +):";
		this.Lat_Label.AutoSize = true;
		this.Lat_Label.Location = new System.Drawing.Point(7, 25);
		this.Lat_Label.Name = "Lat_Label";
		this.Lat_Label.Size = new System.Drawing.Size(84, 13);
		this.Lat_Label.TabIndex = 0;
		this.Lat_Label.Text = "Latitude (N is +):";
		this.refreshTimer.Interval = 1000;
		this.refreshTimer.Tick += new System.EventHandler(refreshTimer_Tick);
		this.startupTimer.Interval = 500;
		this.startupTimer.Tick += new System.EventHandler(startupTimer_Tick);
		this.ChecksumCheckBox.AutoSize = true;
		this.ChecksumCheckBox.Location = new System.Drawing.Point(526, 83);
		this.ChecksumCheckBox.Name = "ChecksumCheckBox";
		this.ChecksumCheckBox.Size = new System.Drawing.Size(163, 17);
		this.ChecksumCheckBox.TabIndex = 75;
		this.ChecksumCheckBox.Text = "Use Error Correction Protocol";
		this.ChecksumCheckBox.UseVisualStyleBackColor = true;
		this.EnableDTRcheckBox.AutoSize = true;
		this.EnableDTRcheckBox.Checked = true;
		this.EnableDTRcheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
		this.EnableDTRcheckBox.Location = new System.Drawing.Point(334, 83);
		this.EnableDTRcheckBox.Name = "EnableDTRcheckBox";
		this.EnableDTRcheckBox.Size = new System.Drawing.Size(172, 17);
		this.EnableDTRcheckBox.TabIndex = 74;
		this.EnableDTRcheckBox.Text = "Enable Serial Port DTR Control";
		this.EnableDTRcheckBox.UseVisualStyleBackColor = true;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(702, 389);
		base.Controls.Add(this.ChecksumCheckBox);
		base.Controls.Add(this.EnableDTRcheckBox);
		base.Controls.Add(this.DriverVersion);
		base.Controls.Add(this.pictureBox2);
		base.Controls.Add(this.pictureBox1);
		base.Controls.Add(this.TimeoutLabel);
		base.Controls.Add(this.TimeoutTrackBar);
		base.Controls.Add(this.label12);
		base.Controls.Add(this.IPAddress);
		base.Controls.Add(this.label17);
		base.Controls.Add(this.HidVID);
		base.Controls.Add(this.label18);
		base.Controls.Add(this.HidPID);
		base.Controls.Add(this.label15);
		base.Controls.Add(this.HidSN);
		base.Controls.Add(this.label16);
		base.Controls.Add(this.label11);
		base.Controls.Add(this.MaxRateGroupBox);
		base.Controls.Add(this.LimitGroupBox);
		base.Controls.Add(this.BacklashGroupBox);
		base.Controls.Add(this.OpticsGroupBox);
		base.Controls.Add(this.connectedDevice);
		base.Controls.Add(this.Connect_Label);
		base.Controls.Add(this.RS232_Label);
		base.Controls.Add(this.DateTimeGroupBox);
		base.Controls.Add(this.SiteGroupBox);
		base.Controls.Add(this.comboBoxComPort);
		base.Controls.Add(this.chkTrace);
		base.Controls.Add(this.picASCOM);
		base.Controls.Add(this.cmdCancel);
		base.Controls.Add(this.cmdOK);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "SetupDialogForm";
		base.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "ASTRO8-OnstepHIDascom Setup";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(SetupDialogForm_FormClosing);
		base.Shown += new System.EventHandler(SetupDialogForm_Shown);
		((System.ComponentModel.ISupportInitialize)this.picASCOM).EndInit();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
		((System.ComponentModel.ISupportInitialize)this.pictureBox2).EndInit();
		((System.ComponentModel.ISupportInitialize)this.TimeoutTrackBar).EndInit();
		this.MaxRateGroupBox.ResumeLayout(false);
		this.MaxRateGroupBox.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.gotoRate).EndInit();
		this.LimitGroupBox.ResumeLayout(false);
		this.LimitGroupBox.PerformLayout();
		this.BacklashGroupBox.ResumeLayout(false);
		this.BacklashGroupBox.PerformLayout();
		this.OpticsGroupBox.ResumeLayout(false);
		this.OpticsGroupBox.PerformLayout();
		this.DateTimeGroupBox.ResumeLayout(false);
		this.DateTimeGroupBox.PerformLayout();
		this.SiteGroupBox.ResumeLayout(false);
		this.SiteGroupBox.PerformLayout();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
