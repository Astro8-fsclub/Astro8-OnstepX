using System;
using System.IO;
using System.Reflection;

namespace ASCOM.OnStep;

/// <summary>
/// Compatibility shim for this build (GPLv3-derived from the official OnStep driver).
/// The official SetupDialog resources were generated with GenerateResourceUsePreserializedResources=true,
/// which requires System.Resources.Extensions at assembly version 4.0.0.0, while NuGet resolves 5.0.0.0.
/// This redirects the 4.0.0.0 request to the 5.0.0.0 assembly shipped next to the driver DLL.
/// Original driver logic is untouched.
/// </summary>
internal static class Compatibility
{
    private static readonly object _lock = new object();
    private static bool _registered;

    internal static void Ensure()
    {
        lock (_lock)
        {
            if (_registered) return;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _registered = true;
        }
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        try
        {
            var name = new AssemblyName(args.Name);
            if (string.Equals(name.Name, "System.Resources.Extensions", StringComparison.OrdinalIgnoreCase))
            {
                string dir = Path.GetDirectoryName(typeof(Compatibility).Assembly.Location);
                if (string.IsNullOrEmpty(dir)) return null;
                string path = Path.Combine(dir, "System.Resources.Extensions.dll");
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }
        }
        catch
        {
            // never throw from AssemblyResolve
        }
        return null;
    }
}
