using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TrishulSim.Scenarios
{
    /// <summary>
    /// Automated verification and smoke test suite for US-P2-B:
    /// 1. InnoSetup 6 compiler execution (ISCC.exe) on trishulsim.iss.
    /// 2. Verification of single-file installer binary at dist/TRISHUL-SIM-installer-YYYYMMDD.exe.
    /// 3. Clean-environment installation test into sandbox directory (Build/TestInstall).
    /// 4. Asset, configuration, and scenario payload verification in installed directory.
    /// 5. Execution of installed standalone TRISHUL-SIM.exe (>=60 FPS benchmark, NDJSON, Scorecard).
    /// 6. Clean sandbox environment teardown.
    /// </summary>
    public static class InstallerSmokeTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAll();
                Console.WriteLine("\n[PASS] All US-P2-B IL2CPP + InnoSetup installer tests passed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] US-P2-B installer smoke test failed: " + ex);
                return 1;
            }
        }
#endif

        public static void RunAll()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("    TRISHUL-SIM · INSTALLER & STANDALONE SMOKE TEST (US-P2-B)");
            Console.WriteLine("    Indian Ministry of Defence (MoD) Problem SIH26247");
            Console.WriteLine("================================================================================\n");

            string rootDir = Directory.GetCurrentDirectory();
            string issPath = Path.Combine(rootDir, "trishulsim.iss");
            if (!File.Exists(issPath))
            {
                throw new FileNotFoundException("trishulsim.iss script not found at " + issPath);
            }
            Console.WriteLine("[1/5] Verified trishulsim.iss presence at repo root.");

            // 1. Locate ISCC.exe
            string isccPath = LocateIscc();
            Console.WriteLine("[2/5] Located InnoSetup compiler: " + isccPath);

            // 2. Compile installer
            Console.WriteLine("\n--- COMPILING INSTALLER VIA INNO SETUP 6 (ISCC) ---");
            ProcessStartInfo isccPsi = new ProcessStartInfo
            {
                FileName = isccPath,
                Arguments = "\"" + issPath + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process isccProc = Process.Start(isccPsi))
            {
                string isccOut = isccProc.StandardOutput.ReadToEnd();
                string isccErr = isccProc.StandardError.ReadToEnd();
                isccProc.WaitForExit();
                if (isccProc.ExitCode != 0)
                {
                    throw new InvalidOperationException("ISCC compilation failed (exit code " + isccProc.ExitCode + "):\n" + isccOut + "\n" + isccErr);
                }
                Console.WriteLine(isccOut);
            }

            // 3. Verify single-file installer output
            string installerPath = Path.Combine(rootDir, "dist", "TRISHUL-SIM-installer-20261008.exe");
            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("Compiled installer not found at " + installerPath);
            }
            FileInfo installerInfo = new FileInfo(installerPath);
            Console.WriteLine(string.Format("[3/5] Verified compiled single-file installer: {0} ({1:N0} bytes)", 
                installerInfo.Name, installerInfo.Length));

            // 4. Test installation into clean sandbox directory
            string installSandbox = Path.Combine(rootDir, "Build", "TestInstall");
            if (Directory.Exists(installSandbox))
            {
                Directory.Delete(installSandbox, true);
            }

            Console.WriteLine("\n--- TESTING CLEAN INSTALLATION INTO SANDBOX ---");
            Console.WriteLine("Target Sandbox: " + installSandbox);
            ProcessStartInfo installPsi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=\"" + installSandbox + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process installProc = Process.Start(installPsi))
            {
                installProc.WaitForExit();
                if (installProc.ExitCode != 0)
                {
                    throw new InvalidOperationException("Silent installation failed with exit code " + installProc.ExitCode);
                }
            }

            // Verify installed files
            string installedExe = Path.Combine(installSandbox, "TRISHUL-SIM.exe");
            string installedData = Path.Combine(installSandbox, "TRISHUL-SIM_Data");
            string installedScenarios = Path.Combine(installedData, "StreamingAssets", "scenarios");

            Assert(File.Exists(installedExe), "Installed TRISHUL-SIM.exe must exist");
            Assert(Directory.Exists(installedScenarios), "Installed scenarios directory must exist");
            Assert(File.Exists(Path.Combine(installedScenarios, "punjab-fog-pincer-01.json")), "punjab scenario must be installed");
            Assert(File.Exists(Path.Combine(installedScenarios, "ladakh-lac-ridge-01.json")), "ladakh scenario must be installed");
            Assert(File.Exists(Path.Combine(installedScenarios, "loc-mountain-pass-01.json")), "loc scenario must be installed");
            Assert(File.Exists(Path.Combine(installedScenarios, "desert-fob-01.json")), "desert scenario must be installed");
            Assert(File.Exists(Path.Combine(installSandbox, "unins000.exe")), "InnoSetup uninstaller must exist");
            Console.WriteLine("[4/5] Installed file layout verified: Exe, 4 scenarios, Data/, and Uninstaller present.");

            // 5. Run installed application smoke test
            Console.WriteLine("\n--- EXECUTING INSTALLED TRISHUL-SIM.EXE FROM SANDBOX ---");
            ProcessStartInfo appPsi = new ProcessStartInfo
            {
                FileName = installedExe,
                WorkingDirectory = installSandbox,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process appProc = Process.Start(appPsi))
            {
                string appOut = appProc.StandardOutput.ReadToEnd();
                string appErr = appProc.StandardError.ReadToEnd();
                appProc.WaitForExit();
                Console.WriteLine(appOut);

                if (appProc.ExitCode != 0)
                {
                    throw new InvalidOperationException("Installed TRISHUL-SIM.exe failed (exit code " + appProc.ExitCode + "):\n" + appErr);
                }

                Assert(appOut.Contains("Discovered 4 tactical scenarios"), "Must discover all 4 scenarios");
                Assert(appOut.Contains("Dual-station link established"), "Must establish IPC link");
                Assert(appOut.Contains("Hardware graphics performance verified: FPS >= 60.0"), "Must verify >=60 FPS");
                Assert(appOut.Contains("DOCTRINE PASSED"), "Must pass doctrinal evaluation");
            }

            // Cleanup sandbox
            try
            {
                if (Directory.Exists(installSandbox))
                {
                    Directory.Delete(installSandbox, true);
                }
            }
            catch { }

            Console.WriteLine("\n[5/5] Clean sandbox cleanup complete.");
        }

        private static string LocateIscc()
        {
            string[] candidatePaths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Inno Setup 6", "ISCC.exe"),
                @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
                @"C:\Program Files\Inno Setup 6\ISCC.exe",
                @"C:\Tools\InnoSetup\ISCC.exe"
            };

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            throw new FileNotFoundException("ISCC.exe (Inno Setup 6 compiler) could not be located in standard paths.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }
    }
}
