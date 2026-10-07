using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using ASCOM.Utilities;
using Microsoft.Win32;

namespace ASCOM.OnStep;

public static class Server
{
	[Flags]
	private enum CLSCTX : uint
	{
		CLSCTX_INPROC_SERVER = 1u,
		CLSCTX_INPROC_HANDLER = 2u,
		CLSCTX_LOCAL_SERVER = 4u,
		CLSCTX_INPROC_SERVER16 = 8u,
		CLSCTX_REMOTE_SERVER = 0x10u,
		CLSCTX_INPROC_HANDLER16 = 0x20u,
		CLSCTX_RESERVED1 = 0x40u,
		CLSCTX_RESERVED2 = 0x80u,
		CLSCTX_RESERVED3 = 0x100u,
		CLSCTX_RESERVED4 = 0x200u,
		CLSCTX_NO_CODE_DOWNLOAD = 0x400u,
		CLSCTX_RESERVED5 = 0x800u,
		CLSCTX_NO_CUSTOM_MARSHAL = 0x1000u,
		CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000u,
		CLSCTX_NO_FAILURE_LOG = 0x4000u,
		CLSCTX_DISABLE_AAA = 0x8000u,
		CLSCTX_ENABLE_AAA = 0x10000u,
		CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000u,
		CLSCTX_INPROC = 3u,
		CLSCTX_SERVER = 0x15u,
		CLSCTX_ALL = 0x17u
	}

	[Flags]
	private enum COINIT : uint
	{
		COINIT_MULTITHREADED = 0u,
		COINIT_APARTMENTTHREADED = 2u,
		COINIT_DISABLE_OLE1DDE = 4u,
		COINIT_SPEED_OVER_MEMORY = 8u
	}

	[Flags]
	private enum REGCLS : uint
	{
		REGCLS_SINGLEUSE = 0u,
		REGCLS_MULTIPLEUSE = 1u,
		REGCLS_MULTI_SEPARATE = 2u,
		REGCLS_SUSPENDED = 4u,
		REGCLS_SURROGATE = 8u
	}

	private static int objsInUse;

	private static int serverLocks;

	private static frmMain s_MainForm = null;

	private static ArrayList s_ComObjectAssys;

	private static ArrayList s_ComObjectTypes;

	private static ArrayList s_ClassFactories;

	private static string s_appId = "{23e810f1-c867-4ade-881b-9fae5476d2ce}";

	private static readonly object lockObject = new object();

	public static uint MainThreadId { get; private set; }

	public static bool StartedByCOM { get; private set; }

	public static int ObjectsCount
	{
		get
		{
			lock (lockObject)
			{
				return objsInUse;
			}
		}
	}

	public static int ServerLockCount
	{
		get
		{
			lock (lockObject)
			{
				return serverLocks;
			}
		}
	}

	private static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

	[DllImport("ole32.dll")]
	private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

	[DllImport("ole32.dll")]
	private static extern void CoUninitialize();

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint idThread, uint Msg, UIntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	public static int CountObject()
	{
		return Interlocked.Increment(ref objsInUse);
	}

	public static int UncountObject()
	{
		return Interlocked.Decrement(ref objsInUse);
	}

	public static int CountLock()
	{
		return Interlocked.Increment(ref serverLocks);
	}

	public static int UncountLock()
	{
		return Interlocked.Decrement(ref serverLocks);
	}

	public static void ExitIf()
	{
		lock (lockObject)
		{
			if (ObjectsCount <= 0 && ServerLockCount <= 0 && StartedByCOM)
			{
				PostThreadMessage(wParam: new UIntPtr(0u), lParam: new IntPtr(0), idThread: MainThreadId, Msg: 18u);
			}
		}
	}

	private static bool LoadComObjectAssemblies()
	{
		s_ComObjectAssys = new ArrayList();
		s_ComObjectTypes = new ArrayList();
		FileInfo[] files = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).GetFiles("*.dll");
		foreach (FileInfo fileInfo in files)
		{
			string fullName = fileInfo.FullName;
			try
			{
				Assembly assembly = Assembly.LoadFrom(fullName);
				Type[] types = assembly.GetTypes();
				foreach (Type type in types)
				{
					if (type.GetCustomAttributes(typeof(ServedClassNameAttribute), inherit: false).Length != 0)
					{
						s_ComObjectTypes.Add(type);
						s_ComObjectAssys.Add(assembly);
					}
				}
			}
			catch (BadImageFormatException)
			{
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Failed to load served COM class assembly " + fileInfo.Name + " - " + ex2.Message, "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return false;
			}
		}
		return true;
	}

	private static void ElevateSelf(string arg)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.Arguments = arg;
		processStartInfo.WorkingDirectory = Environment.CurrentDirectory;
		processStartInfo.FileName = Application.ExecutablePath;
		processStartInfo.Verb = "runas";
		try
		{
			Process.Start(processStartInfo);
		}
		catch (Win32Exception)
		{
			MessageBox.Show("The OnStep was not " + ((arg == "/register") ? "registered" : "unregistered") + " because you did not allow it.", "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		catch (Exception ex2)
		{
			MessageBox.Show(ex2.ToString(), "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private static void RegisterObjects()
	{
		if (!IsAdministrator)
		{
			ElevateSelf("/register");
			return;
		}
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string title = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(executingAssembly, typeof(AssemblyTitleAttribute))).Title;
		string description = ((AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(executingAssembly, typeof(AssemblyDescriptionAttribute))).Description;
		try
		{
			using (RegistryKey registryKey = Registry.ClassesRoot.CreateSubKey("APPID\\" + s_appId))
			{
				registryKey.SetValue(null, description);
				registryKey.SetValue("AppID", s_appId);
				registryKey.SetValue("AuthenticationLevel", 1, RegistryValueKind.DWord);
				registryKey.SetValue("RunAs", "Interactive User", RegistryValueKind.String);
			}
			using RegistryKey registryKey2 = Registry.ClassesRoot.CreateSubKey($"APPID\\{Application.ExecutablePath.Substring(Application.ExecutablePath.LastIndexOf('\\') + 1)}");
			registryKey2.SetValue("AppID", s_appId);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error while registering the server:\n" + ex.ToString(), "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		foreach (Type s_ComObjectType in s_ComObjectTypes)
		{
			bool flag = false;
			try
			{
				string text = Marshal.GenerateGuidForType(s_ComObjectType).ToString("B");
				string text2 = Marshal.GenerateProgIdForType(s_ComObjectType);
				string name = s_ComObjectType.Name;
				using (RegistryKey registryKey3 = Registry.ClassesRoot.CreateSubKey($"CLSID\\{text}"))
				{
					registryKey3.SetValue(null, text2);
					registryKey3.SetValue("AppId", s_appId);
					using (RegistryKey registryKey4 = registryKey3.CreateSubKey("Implemented Categories"))
					{
						registryKey4.CreateSubKey("{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}");
					}
					using (RegistryKey registryKey5 = registryKey3.CreateSubKey("ProgId"))
					{
						registryKey5.SetValue(null, text2);
					}
					registryKey3.CreateSubKey("Programmable");
					using RegistryKey registryKey6 = registryKey3.CreateSubKey("LocalServer32");
					registryKey6.SetValue(null, Application.ExecutablePath);
				}
				using (RegistryKey registryKey7 = Registry.ClassesRoot.CreateSubKey(text2))
				{
					registryKey7.SetValue(null, title);
					using RegistryKey registryKey8 = registryKey7.CreateSubKey("CLSID");
					registryKey8.SetValue(null, text);
				}
				_ = s_ComObjectType.Assembly;
				string descriptiveName = ((ServedClassNameAttribute)Attribute.GetCustomAttribute(s_ComObjectType, typeof(ServedClassNameAttribute))).DisplayName ?? "MultiServer";
				using Profile profile = new Profile();
				profile.DeviceType = name;
				profile.Register(text2, descriptiveName);
			}
			catch (Exception ex2)
			{
				MessageBox.Show("Error while registering the server:\n" + ex2.ToString(), "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				flag = true;
			}
			if (flag)
			{
				break;
			}
		}
	}

	private static void UnregisterObjects()
	{
		if (!IsAdministrator)
		{
			ElevateSelf("/unregister");
			return;
		}
		Registry.ClassesRoot.DeleteSubKey($"APPID\\{s_appId}", throwOnMissingSubKey: false);
		Registry.ClassesRoot.DeleteSubKey($"APPID\\{Application.ExecutablePath.Substring(Application.ExecutablePath.LastIndexOf('\\') + 1)}", throwOnMissingSubKey: false);
		foreach (Type s_ComObjectType in s_ComObjectTypes)
		{
			string arg = Marshal.GenerateGuidForType(s_ComObjectType).ToString("B");
			string text = Marshal.GenerateProgIdForType(s_ComObjectType);
			string name = s_ComObjectType.Name;
			Registry.ClassesRoot.DeleteSubKey($"{text}\\CLSID", throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey(text, throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey($"CLSID\\{arg}\\Implemented Categories\\{{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}}", throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey($"CLSID\\{arg}\\Implemented Categories", throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey($"CLSID\\{arg}\\ProgId", throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey($"CLSID\\{arg}\\LocalServer32", throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey($"CLSID\\{arg}\\Programmable", throwOnMissingSubKey: false);
			Registry.ClassesRoot.DeleteSubKey($"CLSID\\{arg}", throwOnMissingSubKey: false);
			try
			{
				using Profile profile = new Profile();
				profile.DeviceType = name;
				profile.Unregister(text);
			}
			catch (Exception)
			{
			}
		}
	}

	private static bool RegisterClassFactories()
	{
		s_ClassFactories = new ArrayList();
		foreach (Type s_ComObjectType in s_ComObjectTypes)
		{
			ClassFactory classFactory = new ClassFactory(s_ComObjectType);
			s_ClassFactories.Add(classFactory);
			if (!classFactory.RegisterClassObject())
			{
				MessageBox.Show("Failed to register class factory for " + s_ComObjectType.Name, "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return false;
			}
		}
		ClassFactory.ResumeClassObjects();
		return true;
	}

	private static void RevokeClassFactories()
	{
		ClassFactory.SuspendClassObjects();
		foreach (ClassFactory s_ClassFactory in s_ClassFactories)
		{
			s_ClassFactory.RevokeClassObject();
		}
	}

	private static bool ProcessArguments(string[] args)
	{
		bool result = true;
		if (args.Length != 0)
		{
			switch (args[0].ToLower())
			{
			case "-embedding":
				StartedByCOM = true;
				break;
			case "-register":
			case "/register":
			case "-regserver":
			case "/regserver":
				RegisterObjects();
				result = false;
				break;
			case "-unregister":
			case "/unregister":
			case "-unregserver":
			case "/unregserver":
				UnregisterObjects();
				result = false;
				break;
			default:
				MessageBox.Show("Unknown argument: " + args[0] + "\nValid are : -register, -unregister and -embedding", "OnStep", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				break;
			}
		}
		else
		{
			StartedByCOM = false;
		}
		return result;
	}

	[STAThread]
	private static void Main(string[] args)
	{
		if (!LoadComObjectAssemblies() || !ProcessArguments(args))
		{
			return;
		}
		objsInUse = 0;
		serverLocks = 0;
		MainThreadId = GetCurrentThreadId();
		Thread.CurrentThread.Name = "Main Thread";
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		s_MainForm = new frmMain();
		if (StartedByCOM)
		{
			s_MainForm.WindowState = FormWindowState.Minimized;
		}
		RegisterClassFactories();
		GarbageCollection garbageCollection = new GarbageCollection(1000);
		Thread thread = new Thread(garbageCollection.GCWatch);
		thread.Name = "Garbage Collection Thread";
		thread.Start();
		try
		{
			Application.Run(s_MainForm);
		}
		finally
		{
			RevokeClassFactories();
			garbageCollection.StopThread();
			garbageCollection.WaitForThreadToStop();
		}
	}
}
