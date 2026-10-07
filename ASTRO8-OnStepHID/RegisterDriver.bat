@echo off
setlocal
cd /d "%~dp0"

set "DLL=src\bin\Release\net48\ASTRO8-OnStepHID.dll"
if exist "%DLL%" goto :FOUND
set "DLL=src\bin\Debug\net48\ASTRO8-OnStepHID.dll"
if exist "%DLL%" goto :FOUND
echo [ERROR] ASTRO8-OnStepHID.dll not found. Build it first:
echo   dotnet build -c Release
echo   (or build Release in Visual Studio)
pause
exit /b 1

:FOUND
echo Using assembly: %~dp0%DLL%
echo.

echo [1/3] Registering 64-bit COM ...
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "%~dp0%DLL%"
if errorlevel 1 echo [WARN] 64-bit RegAsm failed (skip if this OS has no 64-bit .NET Framework).
echo.

echo [2/3] Registering 32-bit COM ...
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /codebase "%~dp0%DLL%"
if errorlevel 1 echo [WARN] 32-bit RegAsm failed.
echo.

echo [3/3] ASCOM Profile keys written by the driver itself:
echo   HKLM\SOFTWARE\WOW6432Node\ASCOM\Telescope Drivers\ASCOM.OnStepAstro8.Telescope
echo   HKLM\SOFTWARE\ASCOM\Telescope Drivers\ASCOM.OnStepAstro8.Telescope
echo.
echo Done. Open ASCOM Chooser / N.I.N.A. / Cartes du Ciel telescope settings,
echo pick "ASTRO8-OnstepX", click Setup to set VID/PID and test.
echo.
echo To uninstall, run as Administrator:
echo   "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /unregister "%~dp0%DLL%"
echo   "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /unregister "%~dp0%DLL%"
pause
