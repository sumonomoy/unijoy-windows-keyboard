using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Unijoy Keyboard")]
[assembly: AssemblyDescription("Lightweight Windows System Tray Bengali (bn-BD) Keyboard for Unijoy (.avrolayout)")]
[assembly: AssemblyCompany("Unijoy")]
[assembly: AssemblyProduct("Unijoy Keyboard")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace UnijoyKeyboard
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string firstArg = (args != null && args.Length > 0) ? args[0].Trim().ToLowerInvariant() : "";
            string exeName = Path.GetFileNameWithoutExtension(Application.ExecutablePath) ?? "";

            if (firstArg == "--self-test")
            {
                return RunSelfTest();
            }

            if (firstArg == "--install-silent" || firstArg == "/s" || firstArg == "/silent")
            {
                InstallerManager.Install(true, true, true, true);
                return 0;
            }

            if (firstArg == "--uninstall")
            {
                InstallerManager.Uninstall(false);
                return 0;
            }

            if (firstArg == "--uninstall-silent")
            {
                InstallerManager.Uninstall(true);
                return 0;
            }

            if (firstArg == "--install" ||
                exeName.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                exeName.IndexOf("Installer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                using (Icon appIcon = TrayIconGenerator.CreateModeIcon(true))
                using (var installerForm = new InstallerForm(appIcon))
                {
                    Application.Run(installerForm);
                }
                return 0;
            }

            // If already installed in %LOCALAPPDATA%\Programs\UnijoyKeyboard and user launches from project folder,
            // update the installed executable and launch from the installed location
            if (InstallerManager.IsInstalled() && !InstallerManager.IsRunningFromInstallDir())
            {
                try
                {
                    InstallerManager.Install(
                        AppConfig.IsStartWithWindowsEnabled(),
                        false,
                        true,
                        true);
                    return 0;
                }
                catch
                {
                }
            }

            // Ensure any previous running instance is cleanly shut down before starting
            InstallerManager.StopOtherRunningInstances();

            bool createdNew;
            using (var mutex = new Mutex(true, "Local\\UnijoyKeyboard_SingleInstance_Mutex", out createdNew))
            {
                using (var context = new TrayApplicationContext())
                {
                    Application.Run(context);
                }
                return 0;
            }
        }

        private static int RunSelfTest()
        {
            try
            {
                AvroLayout layout = AvroLayoutParser.LoadDefaultOrCustom("");
                Console.WriteLine("Loaded Layout: " + layout.LayoutName + " v" + layout.LayoutVersion);
                Console.WriteLine("Source: " + layout.FilePath);
                Console.WriteLine("Total Mapped Keys: " + layout.KeyMap.Count);
                Console.WriteLine("Win32 INPUT struct size: " + KeyboardHookEngine.GetInputStructSize() + " bytes (PtrSize=" + IntPtr.Size + ")");

                int expectedInputSize = (IntPtr.Size == 8) ? 40 : 28;
                if (KeyboardHookEngine.GetInputStructSize() != expectedInputSize)
                {
                    throw new Exception("Unexpected Win32 INPUT struct size: " + KeyboardHookEngine.GetInputStructSize());
                }

                // Verify key mappings from Unijoy.avrolayout
                AssertKey(layout, Keys.J, "ক", "খ", "", "");
                AssertKey(layout, Keys.F, "া", "অ", "আ", "অ");
                AssertKey(layout, Keys.D, "ি", "ী", "ই", "ঈ");
                AssertKey(layout, Keys.S, "ু", "ূ", "উ", "ঊ");
                AssertKey(layout, Keys.A, "ৃ", "র্", "ঋ", "");
                AssertKey(layout, Keys.G, "্", "।", "", "");
                AssertKey(layout, Keys.Z, "্র", "্য", "", "");
                AssertKey(layout, Keys.X, "ো", "ৌ", "ও", "ঔ");
                AssertKey(layout, Keys.C, "ে", "ৈ", "এ", "ঐ");
                AssertKey(layout, Keys.D1, "১", "!", "", "");
                AssertKey(layout, Keys.D4, "৪", "৳", "", "");
                AssertKey(layout, Keys.D7, "৭", "ৎ", "", "");
                AssertKey(layout, Keys.Oemtilde, "\u200c", "\u200d", "", "");

                using (var img1 = layout.CreateNormalShiftImage())
                using (var img2 = layout.CreateAltGrShiftImage())
                {
                    if (img1 == null || img2 == null)
                        throw new Exception("Embedded layout images failed to decode.");
                    Console.WriteLine("Decoded Images: Normal=" + img1.Width + "x" + img1.Height + ", AltGr=" + img2.Width + "x" + img2.Height);
                }

                using (var icEn = TrayIconGenerator.CreateModeIcon(false))
                using (var icBn = TrayIconGenerator.CreateModeIcon(true))
                {
                    if (icEn == null || icBn == null)
                        throw new Exception("Failed to generate EN / বাং tray icons.");
                    Console.WriteLine("Generated simple text tray icons (EN / বাং): " + icEn.Width + "x" + icEn.Height);
                }

                using (var testEngine = new KeyboardHookEngine(new AppConfig(), layout))
                {
                    testEngine.Start();
                    Console.WriteLine("HookStarted=" + testEngine.IsHookInstalled + ", RawInputRegistered=" + testEngine.IsRawInputRegistered);
                    if (!testEngine.IsHookInstalled || !testEngine.IsRawInputRegistered)
                        throw new Exception("Hook or RawInput failed to register.");
                    testEngine.Stop();
                }

                Console.WriteLine("SELF-TEST PASSED.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF-TEST FAILED: " + ex);
                return 1;
            }
        }

        private static void AssertKey(AvroLayout layout, Keys vk, string expectedNormal, string expectedShift, string expectedAltGr, string expectedShiftAltGr)
        {
            KeyMappingEntry entry;
            if (!layout.KeyMap.TryGetValue((int)vk, out entry))
                throw new Exception("Missing key in layout: " + vk);

            if (entry.Normal != expectedNormal)
                throw new Exception(string.Format("Key {0} Normal mismatch: expected '{1}', got '{2}'", vk, expectedNormal, entry.Normal));
            if (entry.Shift != expectedShift)
                throw new Exception(string.Format("Key {0} Shift mismatch: expected '{1}', got '{2}'", vk, expectedShift, entry.Shift));
            if (entry.AltGr != expectedAltGr)
                throw new Exception(string.Format("Key {0} AltGr mismatch: expected '{1}', got '{2}'", vk, expectedAltGr, entry.AltGr));
            if (entry.ShiftAltGr != expectedShiftAltGr)
                throw new Exception(string.Format("Key {0} ShiftAltGr mismatch: expected '{1}', got '{2}'", vk, expectedShiftAltGr, entry.ShiftAltGr));
        }
    }
}
