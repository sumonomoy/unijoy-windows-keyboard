using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace UnijoyKeyboard
{
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly AppConfig _config;
        private AvroLayout _layout;
        private readonly KeyboardHookEngine _engine;

        private readonly NotifyIcon _notifyIcon;
        private Icon _iconEnglish;
        private Icon _iconBengali;

        private ToolStripMenuItem _menuToggleHeader;
        private ToolStripMenuItem _menuModeBengali;
        private ToolStripMenuItem _menuModeEnglish;
        private ToolStripMenuItem _menuSmartVowel;
        private ToolStripMenuItem _menuStartWithWindows;
        private ToolStripMenuItem _menuCurrentLayoutInfo;
        private ToolStripMenuItem _menuInstallApp;

        private SettingsForm _openSettingsForm;
        private LayoutViewerForm _openLayoutForm;
        private InstallerForm _openInstallerForm;

        public TrayApplicationContext()
        {
            _config = AppConfig.Load();
            _layout = AvroLayoutParser.LoadDefaultOrCustom(_config.CustomLayoutPath);
            _engine = new KeyboardHookEngine(_config, _layout);

            _iconEnglish = TrayIconGenerator.CreateModeIcon(false);
            _iconBengali = TrayIconGenerator.CreateModeIcon(true);

            ContextMenuStrip menu = BuildContextMenu();

            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Visible = true
            };

            // Use MouseUp for Left-Click so every single left click (even rapid clicks within double-click time) toggles EN <-> বাং
            _notifyIcon.MouseUp += OnNotifyIconMouseUp;
            _engine.ModeChanged += OnEngineModeChanged;
            _engine.ShutdownRequested += (s, e) => ExitApplication();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            _engine.Start();
            UpdateTrayState();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Icon oldEn = _iconEnglish;
            Icon oldBn = _iconBengali;
            _iconEnglish = TrayIconGenerator.CreateModeIcon(false);
            _iconBengali = TrayIconGenerator.CreateModeIcon(true);
            UpdateTrayState();
            if (oldEn != null) oldEn.Dispose();
            if (oldBn != null) oldBn.Dispose();
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI", 9.25f)
            };

            _menuToggleHeader = new ToolStripMenuItem
            {
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };
            _menuToggleHeader.Click += (s, e) => _engine.ToggleMode();

            _menuModeBengali = new ToolStripMenuItem("বাং — বাংলা (" + _layout.LayoutName + ")");
            _menuModeBengali.Click += (s, e) => _engine.IsBengaliMode = true;

            _menuModeEnglish = new ToolStripMenuItem("EN — English (System Default)");
            _menuModeEnglish.Click += (s, e) => _engine.IsBengaliMode = false;

            var itemViewLayout = new ToolStripMenuItem("View Keyboard Layout...");
            itemViewLayout.Click += (s, e) => ShowLayoutViewer();

            _menuCurrentLayoutInfo = new ToolStripMenuItem("Load Custom .avrolayout File...");
            _menuCurrentLayoutInfo.Click += (s, e) => PromptLoadCustomLayout();

            _menuSmartVowel = new ToolStripMenuItem("Smart Vowel Forming (্ + কার = স্বরবর্ণ)")
            {
                Checked = _config.SmartVowelForming
            };
            _menuSmartVowel.Click += (s, e) =>
            {
                _config.SmartVowelForming = !_config.SmartVowelForming;
                _menuSmartVowel.Checked = _config.SmartVowelForming;
                _config.Save();
                _engine.UpdateConfig(_config);
            };

            _menuStartWithWindows = new ToolStripMenuItem("Start with Windows")
            {
                Checked = AppConfig.IsStartWithWindowsEnabled()
            };
            _menuStartWithWindows.Click += (s, e) =>
            {
                bool target = !_menuStartWithWindows.Checked;
                AppConfig.SetStartWithWindows(target);
                _menuStartWithWindows.Checked = AppConfig.IsStartWithWindowsEnabled();
            };

            _menuInstallApp = new ToolStripMenuItem("Install / Manage Installation...");
            _menuInstallApp.Click += (s, e) => ShowInstaller();

            var itemSettings = new ToolStripMenuItem("Settings...");
            itemSettings.Click += (s, e) => ShowSettings();

            var itemExit = new ToolStripMenuItem("Exit");
            itemExit.Click += (s, e) => ExitApplication();

            menu.Opening += (s, e) => RefreshMenuState();

            menu.Items.Add(_menuToggleHeader);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_menuModeBengali);
            menu.Items.Add(_menuModeEnglish);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemViewLayout);
            menu.Items.Add(_menuCurrentLayoutInfo);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_menuSmartVowel);
            menu.Items.Add(_menuStartWithWindows);
            menu.Items.Add(itemSettings);
            menu.Items.Add(_menuInstallApp);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemExit);

            return menu;
        }

        private void OnNotifyIconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _engine.ToggleMode();
            }
        }

        private void OnEngineModeChanged(object sender, EventArgs e)
        {
            UpdateTrayState();
        }

        private void UpdateTrayState()
        {
            bool isBn = _engine.IsBengaliMode;
            _notifyIcon.Icon = isBn ? _iconBengali : _iconEnglish;

            string modeName = isBn ? ("বাং (" + _layout.LayoutName + ")") : "EN (English)";
            string tooltip = string.Format("Unijoy: {0} [Ctrl+Space]", modeName);
            if (tooltip.Length > 63)
            {
                tooltip = tooltip.Substring(0, 63);
            }
            _notifyIcon.Text = tooltip;

            RefreshMenuState();
        }

        private void RefreshMenuState()
        {
            bool isBn = _engine.IsBengaliMode;
            _menuToggleHeader.Text = isBn
                ? string.Format("Active: বাং ({0}) — [Ctrl + Space]", _layout.LayoutName)
                : "Active: EN (English) — [Ctrl + Space]";

            _menuModeBengali.Text = "বাং — বাংলা (" + _layout.LayoutName + ")";
            _menuModeBengali.Checked = isBn;
            _menuModeEnglish.Checked = !isBn;

            _menuSmartVowel.Checked = _config.SmartVowelForming;
            _menuStartWithWindows.Checked = AppConfig.IsStartWithWindowsEnabled();
            _menuInstallApp.Text = InstallerManager.IsInstalled()
                ? "Manage / Uninstall Installation..."
                : "Install Application to Windows...";
        }

        private void PromptLoadCustomLayout()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Select an Avro Keyboard Layout (.avrolayout)",
                Filter = "Avro Keyboard Layout (*.avrolayout)|*.avrolayout|All Files (*.*)|*.*",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        AvroLayout newLayout = AvroLayoutParser.LoadFromFile(dlg.FileName);
                        _layout = newLayout;
                        _engine.CurrentLayout = _layout;
                        _config.CustomLayoutPath = dlg.FileName;
                        _config.Save();
                        UpdateTrayState();

                        MessageBox.Show(
                            string.Format("Successfully loaded layout: {0} (v{1})", _layout.LayoutName, _layout.LayoutVersion),
                            "Unijoy Keyboard",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Failed to load .avrolayout file:\n" + ex.Message,
                            "Unijoy Keyboard",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ShowLayoutViewer()
        {
            if (_openLayoutForm != null && !_openLayoutForm.IsDisposed)
            {
                _openLayoutForm.BringToFront();
                _openLayoutForm.Activate();
                return;
            }

            _openLayoutForm = new LayoutViewerForm(_layout, _iconBengali);
            _openLayoutForm.Show();
        }

        private void ShowSettings()
        {
            if (_openSettingsForm != null && !_openSettingsForm.IsDisposed)
            {
                _openSettingsForm.BringToFront();
                _openSettingsForm.Activate();
                return;
            }

            _openSettingsForm = new SettingsForm(_config, _engine, _iconBengali);
            _openSettingsForm.FormClosed += (s, e) => UpdateTrayState();
            _openSettingsForm.Show();
        }

        private void ShowInstaller()
        {
            if (_openInstallerForm != null && !_openInstallerForm.IsDisposed)
            {
                _openInstallerForm.BringToFront();
                _openInstallerForm.Activate();
                return;
            }

            _openInstallerForm = new InstallerForm(_iconBengali);
            _openInstallerForm.FormClosed += (s, e) => UpdateTrayState();
            _openInstallerForm.Show();
        }

        private void ExitApplication()
        {
            _notifyIcon.Visible = false;
            _engine.Stop();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                if (_engine != null) _engine.Dispose();
                if (_notifyIcon != null) _notifyIcon.Dispose();
                if (_iconEnglish != null) _iconEnglish.Dispose();
                if (_iconBengali != null) _iconBengali.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
