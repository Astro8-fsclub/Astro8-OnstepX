using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace ASCOM.OnStep;

public class ClassFactory : IClassFactory
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
	private enum REGCLS : uint
	{
		REGCLS_SINGLEUSE = 0u,
		REGCLS_MULTIPLEUSE = 1u,
		REGCLS_MULTI_SEPARATE = 2u,
		REGCLS_SUSPENDED = 4u,
		REGCLS_SURROGATE = 8u
	}

	public static Guid IID_IUnknown = new Guid("{00000000-0000-0000-C000-000000000046}");

	public static Guid IID_IDispatch = new Guid("{00020400-0000-0000-C000-000000000046}");

	protected Type m_ClassType;

	protected Guid m_ClassId;

	protected ArrayList m_InterfaceTypes;

	protected uint m_ClassContext;

	protected uint m_Flags;

	protected uint m_locked;

	protected uint m_Cookie;

	protected string m_progid;

	public uint ClassContext
	{
		get
		{
			return m_ClassContext;
		}
		set
		{
			m_ClassContext = value;
		}
	}

	public Guid ClassId
	{
		get
		{
			return m_ClassId;
		}
		set
		{
			m_ClassId = value;
		}
	}

	public uint Flags
	{
		get
		{
			return m_Flags;
		}
		set
		{
			m_Flags = value;
		}
	}

	[DllImport("ole32.dll")]
	private static extern int CoRegisterClassObject([In] ref Guid rclsid, [MarshalAs(UnmanagedType.IUnknown)] object pUnk, uint dwClsContext, uint flags, out uint lpdwRegister);

	[DllImport("ole32.dll")]
	private static extern int CoResumeClassObjects();

	[DllImport("ole32.dll")]
	private static extern int CoSuspendClassObjects();

	[DllImport("ole32.dll")]
	private static extern int CoRevokeClassObject(uint dwRegister);

	public ClassFactory(Type type)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		m_ClassType = type;
		m_progid = Marshal.GenerateProgIdForType(type);
		m_ClassId = Marshal.GenerateGuidForType(type);
		m_ClassContext = 4u;
		m_Flags = 5u;
		m_InterfaceTypes = new ArrayList();
		Type[] interfaces = type.GetInterfaces();
		foreach (Type value in interfaces)
		{
			m_InterfaceTypes.Add(value);
		}
	}

	public bool RegisterClassObject()
	{
		return CoRegisterClassObject(ref m_ClassId, this, m_ClassContext, m_Flags, out m_Cookie) == 0;
	}

	public bool RevokeClassObject()
	{
		return CoRevokeClassObject(m_Cookie) == 0;
	}

	public static bool ResumeClassObjects()
	{
		return CoResumeClassObjects() == 0;
	}

	public static bool SuspendClassObjects()
	{
		return CoSuspendClassObjects() == 0;
	}

	void IClassFactory.CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
	{
		IntPtr intPtr = new IntPtr(0);
		ppvObject = intPtr;
		foreach (Type interfaceType in m_InterfaceTypes)
		{
			if (riid == Marshal.GenerateGuidForType(interfaceType))
			{
				ppvObject = Marshal.GetComInterfaceForObject(Activator.CreateInstance(m_ClassType), interfaceType);
				return;
			}
		}
		if (riid == IID_IDispatch)
		{
			ppvObject = Marshal.GetIDispatchForObject(Activator.CreateInstance(m_ClassType));
			return;
		}
		if (riid == IID_IUnknown)
		{
			ppvObject = Marshal.GetIUnknownForObject(Activator.CreateInstance(m_ClassType));
			return;
		}
		throw new COMException("No interface", -2147467262);
	}

	void IClassFactory.LockServer(bool bLock)
	{
		if (bLock)
		{
			Server.CountLock();
		}
		else
		{
			Server.UncountLock();
		}
		Server.ExitIf();
	}
}
