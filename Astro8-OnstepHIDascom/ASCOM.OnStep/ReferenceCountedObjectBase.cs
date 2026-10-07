using System.Runtime.InteropServices;

namespace ASCOM.OnStep;

[ComVisible(false)]
public class ReferenceCountedObjectBase
{
	public ReferenceCountedObjectBase()
	{
		Server.CountObject();
	}

	~ReferenceCountedObjectBase()
	{
		Server.UncountObject();
		Server.ExitIf();
	}
}
