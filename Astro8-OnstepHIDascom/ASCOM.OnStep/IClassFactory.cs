using System;
using System.Runtime.InteropServices;

namespace ASCOM.OnStep;

[ComImport]
[ComVisible(false)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000001-0000-0000-C000-000000000046")]
public interface IClassFactory
{
	void CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

	void LockServer(bool fLock);
}
