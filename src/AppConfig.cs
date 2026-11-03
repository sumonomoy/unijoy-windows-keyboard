using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace UnijoyKeyboard
{
    public sealed class AppConfig
    {
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "UnijoyKeyboard";

        // Typing & behavior options
        public bool StartInBengaliMode { get; set; }
        public bool SmartVowelForming { get; set; }
        public bool BengaliNumpad { get; set; }
        public bool LeftAltAsAltGr { get; set; }
        public string CustomLayoutPath { get; set; }

        public AppConfig()
        {
            StartInBengaliMode = false;
            SmartVowelForming = true;
            BengaliNumpad = true;
            LeftAltAsAltGr = false;
            CustomLayoutPath = "";
        }

        private static string GetConfigFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "UnijoyKeyboard");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "config.ini");
        }

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    return cfg;
                }

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    bool parsedBool;

                    switch (key)
                    {
                        case "StartInBengaliMode":
                            if (bool.TryParse(val, out parsedBool))
                                cfg.StartInBengaliMode = parsedBool;
                            break;
                        case "SmartVowelForming":
                            if (bool.TryParse(val, out parsedBool))
                                cfg.SmartVowelForming = parsedBool;
                            break;
                        case "BengaliNumpad":
                            if (bool.TryParse(val, out parsedBool))
                                cfg.BengaliNumpad = parsedBool;
                            break;
                        case "LeftAltAsAltGr":
                            if (bool.TryParse(val, out parsedBool))
                                cfg.LeftAltAsAltGr = parsedBool;
                            break;
                        case "CustomLayoutPath":
                            cfg.CustomLayoutPath = val;
                            break;
                    }
                }
            }
            catch
            {
                // Ignore config read errors and use defaults
            }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                var sb = new StringBuilder();
                sb.AppendLine("# Unijoy Keyboard Configuration");
                sb.AppendLine("StartInBengaliMode=" + StartInBengaliMode);
                sb.AppendLine("SmartVowelForming=" + SmartVowelForming);
                sb.AppendLine("BengaliNumpad=" + BengaliNumpad);
                sb.AppendLine("LeftAltAsAltGr=" + LeftAltAsAltGr);
                sb.AppendLine("CustomLayoutPath=" + (CustomLayoutPath ?? ""));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Ignore save errors
            }
        }

        public string GetHotkeyDisplayString()
        {
            return "Ctrl+Space";
        }

        public string GetPrimaryHotkeyString()
        {
            return "Ctrl + Space";
        }

        public static bool IsStartWithWindowsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(AppRegistryName);
                    return val != null && !string.IsNullOrEmpty(val.ToString());
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = InstallerManager.IsInstalled()
                            ? InstallerManager.GetInstalledExePath()
                            : Application.ExecutablePath;
                        key.SetValue(AppRegistryName, "\"" + exePath + "\"");
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
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to update Windows startup registry:\n" + ex.Message,
                    "Unijoy Keyboard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
