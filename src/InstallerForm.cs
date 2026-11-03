using System;
using System.Drawing;
using System.Windows.Forms;

namespace UniBijoyKeyboard
{
    public sealed class InstallerForm : Form
    {
        private CheckBox _chkStartWithWindows;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkStartMenuShortcut;
        private CheckBox _chkLaunchNow;
        private TextBox _txtInstallPath;
        private Button _btnInstall;
        private Button _btnUninstall;

        public InstallerForm(Icon appIcon)
        {
            Text = "UniBijoy Keyboard (bn-BD) — Setup";
            Size = new Size(520, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            BuildUi();
        }

        private void BuildUi()
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            var lblTitle = new Label
            {
                Text = "Install UniBijoy Keyboard (bn-BD)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 10)
            };

            var lblSub = new Label
            {
                Text = "Installs to your system and automatically runs on Windows startup & restart",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Location = new Point(19, 36)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSub);

            var lblFolder = new Label
            {
                Text = "Installation Folder:",
                Font = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 80)
            };

            _txtInstallPath = new TextBox
            {
                Location = new Point(20, 102),
                Size = new Size(464, 26),
                ReadOnly = true,
                BackColor = Color.White,
                Text = InstallerManager.GetInstallDirectory()
            };

            var grpOptions = new GroupBox
            {
                Text = "Installation && Persistence Options",
                Location = new Point(20, 140),
                Size = new Size(464, 140),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            _chkStartWithWindows = new CheckBox
            {
                Text = "Start automatically when Windows starts (persists across shutdowns/restarts)",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(14, 28),
                Checked = true
            };

            _chkStartMenuShortcut = new CheckBox
            {
                Text = "Create Start Menu shortcuts && register in Windows Installed Apps",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(14, 56),
                Checked = true
            };

            _chkDesktopShortcut = new CheckBox
            {
                Text = "Create Desktop shortcut",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(14, 84),
                Checked = true
            };

            _chkLaunchNow = new CheckBox
            {
                Text = "Launch UniBijoy Keyboard in the System Tray after installation",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(14, 110),
                Checked = true
            };

            grpOptions.Controls.Add(_chkStartWithWindows);
            grpOptions.Controls.Add(_chkStartMenuShortcut);
            grpOptions.Controls.Add(_chkDesktopShortcut);
            grpOptions.Controls.Add(_chkLaunchNow);

            bool alreadyInstalled = InstallerManager.IsInstalled();

            _btnUninstall = new Button
            {
                Text = "Uninstall",
                Location = new Point(20, 294),
                Size = new Size(96, 34),
                UseVisualStyleBackColor = true,
                Visible = alreadyInstalled
            };
            _btnUninstall.Click += (s, e) =>
            {
                InstallerManager.Uninstall(false);
                DialogResult = DialogResult.Abort;
                Close();
            };

            _btnInstall = new Button
            {
                Text = alreadyInstalled ? "Reinstall / Update" : "Install Now",
                Location = new Point(256, 294),
                Size = new Size(134, 34),
                BackColor = Color.FromArgb(5, 150, 105),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += OnInstallClick;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(396, 294),
                Size = new Size(88, 34),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += (s, e) => Close();

            Controls.Add(headerPanel);
            Controls.Add(lblFolder);
            Controls.Add(_txtInstallPath);
            Controls.Add(grpOptions);
            Controls.Add(_btnUninstall);
            Controls.Add(_btnInstall);
            Controls.Add(btnCancel);
        }

        private void OnInstallClick(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _btnInstall.Enabled = false;

                InstallerManager.Install(
                    _chkStartWithWindows.Checked,
                    _chkDesktopShortcut.Checked,
                    _chkStartMenuShortcut.Checked,
                    _chkLaunchNow.Checked);

                Cursor = Cursors.Default;
                MessageBox.Show(
                    "UniBijoy Keyboard has been installed successfully!\n\n" +
                    "• Installed to: " + InstallerManager.GetInstallDirectory() + "\n" +
                    "• Auto-start on Windows boot/restart: " + (_chkStartWithWindows.Checked ? "Enabled" : "Disabled") + "\n" +
                    "• Tray Icons: 'EN' (English) / 'বাং' (Bengali UniBijoy)\n" +
                    "• Toggle Hotkey: Ctrl + Space (or click the tray icon)",
                    "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                _btnInstall.Enabled = true;
                MessageBox.Show(
                    "Installation failed:\n" + ex.Message,
                    "UniBijoy Keyboard Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
