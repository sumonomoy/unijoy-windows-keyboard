using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace UnijoyKeyboard
{
    public static class InstallerManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint RegisterWindowMessage(string lpString);
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryUninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\UnijoyKeyboard";
        private const string AppRegistryName = "UnijoyKeyboard";

        public static string GetInstallDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", "UnijoyKeyboard");
        }

        public static string GetInstalledExePath()
        {
            return Path.Combine(GetInstallDirectory(), "UnijoyKeyboard.exe");
        }

        public static bool IsInstalled()
        {
            return File.Exists(GetInstalledExePath());
        }

        public static bool IsRunningFromInstallDir()
        {
            try
            {
                string currentExe = Path.GetFullPath(Application.ExecutablePath);
                string targetExe = Path.GetFullPath(GetInstalledExePath());
                return string.Equals(currentExe, targetExe, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void Install(
            bool startWithWindows,
            bool createDesktopShortcut,
            bool createStartMenuShortcuts,
            bool launchAfterInstall)
        {
            string installDir = GetInstallDirectory();
            string targetExe = GetInstalledExePath();
            string targetLayout = Path.Combine(installDir, "Unijoy.avrolayout");
            string currentExe = Path.GetFullPath(Application.ExecutablePath);

            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
            }

            // If we are not already running from targetExe, stop any running instance at targetExe before overwriting
            if (!string.Equals(currentExe, Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
            {
                StopOtherRunningInstances();
                File.Copy(currentExe, targetExe, true);
            }

            // Write Unijoy.avrolayout to installDir
            WriteLayoutFileToInstallDir(targetLayout);

            // Configure Start with Windows pointing to the installed executable
            SetStartupRegistry(startWithWindows ? targetExe : null);

            // Register in Windows Installed Apps (Add/Remove Programs)
            RegisterUninstallEntry(installDir, targetExe);

            // Create Start Menu shortcuts
            if (createStartMenuShortcuts)
            {
                string startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "Unijoy Keyboard");
                if (!Directory.Exists(startMenuDir))
                {
                    Directory.CreateDirectory(startMenuDir);
                }

                CreateShortcut(
                    Path.Combine(startMenuDir, "Unijoy Keyboard.lnk"),
                    targetExe,
                    "",
                    installDir,
                    "Unijoy Bengali (bn-BD) Keyboard");

                CreateShortcut(
                    Path.Combine(startMenuDir, "Uninstall Unijoy Keyboard.lnk"),
                    targetExe,
                    "--uninstall",
                    installDir,
                    "Uninstall Unijoy Keyboard");
            }

            // Create Desktop shortcut
            if (createDesktopShortcut)
            {
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(
                    Path.Combine(desktopDir, "Unijoy Keyboard.lnk"),
                    targetExe,
                    "",
                    installDir,
                    "Unijoy Bengali (bn-BD) Keyboard");
            }

            // Launch the installed application
            if (launchAfterInstall && !string.Equals(currentExe, Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });
            }
        }

        public static void Uninstall(bool silent)
        {
            if (!silent)
            {
                DialogResult res = MessageBox.Show(
                    "Are you sure you want to uninstall Unijoy Keyboard from your computer?",
                    "Uninstall Unijoy Keyboard",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (res != DialogResult.Yes)
                {
                    return;
                }
            }

            StopOtherRunningInstances();

            // 1. Remove Startup Registry entry
            SetStartupRegistry(null);

            // 2. Remove Uninstall Registry entry
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegistryUninstallPath, false);
            }
            catch
            {
            }

            // 3. Remove Desktop & Start Menu shortcuts
            try
            {
                string desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "Unijoy Keyboard.lnk");
                if (File.Exists(desktopShortcut))
                {
                    File.Delete(desktopShortcut);
                }

                string startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "Unijoy Keyboard");
                if (Directory.Exists(startMenuDir))
                {
                    Directory.Delete(startMenuDir, true);
                }
            }
            catch
            {
            }

            // 4. Delete install directory (using delayed cmd if currently running from installDir)
            string installDir = GetInstallDirectory();
            if (Directory.Exists(installDir))
            {
                try
                {
                    if (IsRunningFromInstallDir())
                    {
                        string cmdArgs = string.Format("/C timeout /t 2 /nobreak > NUL & rmdir /s /q \"{0}\"", installDir);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = cmdArgs,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        Directory.Delete(installDir, true);
                    }
                }
                catch
                {
                }
            }

            if (!silent)
            {
                MessageBox.Show(
                    "Unijoy Keyboard has been uninstalled.",
                    "Uninstall Unijoy Keyboard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        public static void StopOtherRunningInstances()
        {
            try
            {
                // First ask any running instance to cleanly remove its NotifyIcon from the taskbar and exit
                uint shutdownMsg = RegisterWindowMessage("UnijoyKeyboard_CleanShutdown_Msg");
                if (shutdownMsg != 0)
                {
                    PostMessage((IntPtr)0xFFFF, shutdownMsg, IntPtr.Zero, IntPtr.Zero);
                    Thread.Sleep(350);
                }

                int currentPid = Process.GetCurrentProcess().Id;
                string[] names = new[] { "UnijoyKeyboard", "UnijoyKeyboardSetup" };
                foreach (string procName in names)
                {
                    Process[] procs = Process.GetProcessesByName(procName);
                    foreach (Process p in procs)
                    {
                        if (p.Id != currentPid)
                        {
                            try
                            {
                                if (!p.HasExited)
                                {
                                    p.Kill();
                                    p.WaitForExit(2000);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                Thread.Sleep(150);
            }
            catch
            {
            }
        }

        private static void WriteLayoutFileToInstallDir(string targetLayoutPath)
        {
            string localLayout = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Unijoy.avrolayout");
            if (File.Exists(localLayout))
            {
                if (!string.Equals(Path.GetFullPath(localLayout), Path.GetFullPath(targetLayoutPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(localLayout, targetLayoutPath, true);
                }
                return;
            }

            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string resName in asm.GetManifestResourceNames())
            {
                if (resName.EndsWith("Unijoy.avrolayout", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream s = asm.GetManifestResourceStream(resName))
                    {
                        if (s != null)
                        {
                            using (FileStream fs = File.Create(targetLayoutPath))
                            {
                                s.CopyTo(fs);
                            }
                            return;
                        }
                    }
                }
            }
        }

        public static void SetStartupRegistry(string exePathOrNull)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true))
                {
                    if (key == null) return;
                    if (!string.IsNullOrEmpty(exePathOrNull))
                    {
                        key.SetValue(AppRegistryName, "\"" + exePathOrNull + "\"");
                    }
                    else
                    {
                        if (key.GetValue(AppRegistryName) != null)
                        {
                            key.DeleteValue(AppRegistryName, false);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void RegisterUninstallEntry(string installDir, string targetExe)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryUninstallPath))
                {
                    if (key == null) return;
                    key.SetValue("DisplayName", "Unijoy Keyboard (bn-BD)");
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", "Unijoy");
                    key.SetValue("DisplayIcon", targetExe + ",0");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", "\"" + targetExe + "\" --uninstall");
                    key.SetValue("QuietUninstallString", "\"" + targetExe + "\" --uninstall-silent");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("EstimatedSize", 512, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        private static void CreateShortcut(
            string shortcutPath,
            string targetPath,
            string arguments,
            string workingDirectory,
            string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                if (!string.IsNullOrEmpty(arguments))
                {
                    scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                }
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch
            {
            }
        }
    }
}
