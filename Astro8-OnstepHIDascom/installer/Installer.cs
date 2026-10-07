using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

// Astro8-OnstepHIDascom installer (GPLv3, part of Astro8-OnstepX)
// Usage: place next to the driver DLLs (same folder), run as administrator.
// Copies DLLs to Program Files, registers the COM driver (64+32 bit) and adds the ASCOM Chooser entry.
internal static class Installer
{
    private const string ProgId = "ASCOM.Astro8OnstepHIDascom.Telescope";
    private const string DisplayName = "ASTRO8-OnstepX (HID/WiFi)";
    private const string AppDirName = "Astro8-OnstepHIDascom";

    private static int Main(string[] args)
    {
        try
        {
            if (!IsAdministrator())
            {
                RestartAsAdministrator();
                return 0;
            }

            string srcDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string dstDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppDirName);

            Console.WriteLine("Astro8-OnstepHIDascom installer");
            Console.WriteLine("Source : " + srcDir);
            Console.WriteLine("Install: " + dstDir);

            Directory.CreateDirectory(dstDir);
            int copied = 0;
            foreach (string f in Directory.GetFiles(srcDir, "*.dll"))
            {
                string name = Path.GetFileName(f);
                if (name.Equals(Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location), StringComparison.OrdinalIgnoreCase))
                    continue;
                File.Copy(f, Path.Combine(dstDir, name), true);
                copied++;
            }
            Console.WriteLine("Copied {0} DLL(s).", copied);

            string dllPath = Path.Combine(dstDir, "Astro8-OnstepHIDascom.Telescope.dll");
            RunRegAsm(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe", dllPath);
            RunRegAsm(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe", dllPath);

            using (RegistryKey k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\" + ProgId))
            {
                k.SetValue("Name", DisplayName);
                k.SetValue("Description", DisplayName + " - official OnStep driver + HID transport (GPLv3)");
            }
            Console.WriteLine("ASCOM Chooser entry registered.");

            Console.WriteLine();
            Console.WriteLine("Installation successful.");
            Console.WriteLine("Open N.I.N.A. (or any ASCOM client) and choose: " + DisplayName);
            Console.WriteLine("Press any key to exit...");
            if (args.Length == 0)
                Console.ReadKey();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Installation FAILED: " + ex.Message);
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return 1;
        }
    }

    private static bool IsAdministrator()
    {
        using (WindowsIdentity id = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private static void RestartAsAdministrator()
    {
        Console.WriteLine("This installer requires administrator privileges; restarting with UAC prompt...");
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
            UseShellExecute = true,
            Verb = "runas"
        };
        try
        {
            Process.Start(psi);
        }
        catch
        {
            Console.WriteLine("UAC declined - installation aborted.");
            Console.ReadKey();
        }
    }

    private static void RunRegAsm(string regAsmPath, string dllPath)
    {
        if (!File.Exists(regAsmPath))
        {
            Console.WriteLine("WARNING: RegAsm not found at " + regAsmPath);
            return;
        }
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = regAsmPath,
            Arguments = "\"" + dllPath + "\" /codebase",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (Process p = Process.Start(psi))
        {
            p.WaitForExit();
            string so = p.StandardOutput.ReadToEnd();
            string se = p.StandardError.ReadToEnd();
            if (p.ExitCode != 0)
                throw new InvalidOperationException("RegAsm failed (" + p.ExitCode + "): " + se.Trim() + so.Trim());
            Console.WriteLine("Registered (x" + (regAsmPath.Contains("Framework64") ? "64" : "86") + "): " + Path.GetFileName(regAsmPath));
        }
    }
}
