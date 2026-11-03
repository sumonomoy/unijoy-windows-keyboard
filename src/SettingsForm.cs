using System;
using System.Drawing;
using System.Windows.Forms;

namespace UniBijoyKeyboard
{
    public sealed class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private readonly KeyboardHookEngine _engine;

        private CheckBox _chkStartWithWindows;
        private CheckBox _chkStartInBengali;
        private CheckBox _chkSmartVowel;
        private CheckBox _chkBengaliNumpad;
        private CheckBox _chkLeftAltAsAltGr;
        private TextBox _testTypingBox;
        private Label _lblLiveMode;

        public SettingsForm(AppConfig config, KeyboardHookEngine engine, Icon appIcon)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (engine == null) throw new ArgumentNullException("engine");
            _config = config;
            _engine = engine;

            Text = "UniBijoy Keyboard — Settings";
            Size = new Size(560, 520);
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
            _engine.ModeChanged += OnEngineModeChanged;
        }

        private void BuildUi()
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            var headerTitle = new Label
            {
                Text = "UniBijoy Keyboard Settings",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 13)
            };
            headerPanel.Controls.Add(headerTitle);

            // Group 1: Global Hotkey Info
            var grpHotkey = new GroupBox
            {
                Text = "Language Toggle Shortcut (EN <-> বাং)",
                Location = new Point(16, 64),
                Size = new Size(512, 82),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            var lblShortcut = new Label
            {
                Text = "Toggle Hotkey:   Ctrl + Space",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(4, 120, 87),
                AutoSize = true,
                Location = new Point(16, 26)
            };

            var lblTrayClickHint = new Label
            {
                Text = "Press Ctrl + Space or left-click the System Tray icon (EN / বাং) to switch language.",
                Font = new Font("Segoe UI", 8.75f),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Location = new Point(16, 52)
            };

            grpHotkey.Controls.Add(lblShortcut);
            grpHotkey.Controls.Add(lblTrayClickHint);

            // Group 2: Typing & Startup Options
            var grpOptions = new GroupBox
            {
                Text = "Startup && UniBijoy Typing Preferences",
                Location = new Point(16, 156),
                Size = new Size(512, 178),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            _chkStartWithWindows = new CheckBox
            {
                Text = "Start UniBijoy Keyboard automatically when Windows starts",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(16, 28),
                Checked = AppConfig.IsStartWithWindowsEnabled()
            };

            _chkStartInBengali = new CheckBox
            {
                Text = "Start in Bengali (বাং UniBijoy) mode by default on launch",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(16, 55),
                Checked = _config.StartInBengaliMode
            };

            _chkSmartVowel = new CheckBox
            {
                Text = "Smart Independent Vowels: Hasanta (্ [G]) + Vowel Kar (া, ি, ী...) -> স্বরবর্ণ (আ, ই, ঈ...)",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(16, 82),
                Checked = _config.SmartVowelForming
            };

            _chkBengaliNumpad = new CheckBox
            {
                Text = "Type Bengali digits (০-৯) on Numpad when NumLock is on",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(16, 109),
                Checked = _config.BengaliNumpad
            };

            _chkLeftAltAsAltGr = new CheckBox
            {
                Text = "Allow Left Alt key (in addition to Right Alt / Ctrl+Alt) as AltGr for vowels",
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Location = new Point(16, 136),
                Checked = _config.LeftAltAsAltGr
            };

            grpOptions.Controls.Add(_chkStartWithWindows);
            grpOptions.Controls.Add(_chkStartInBengali);
            grpOptions.Controls.Add(_chkSmartVowel);
            grpOptions.Controls.Add(_chkBengaliNumpad);
            grpOptions.Controls.Add(_chkLeftAltAsAltGr);

            // Group 3: Live Test Box
            var grpTest = new GroupBox
            {
                Text = "Test Typing Area (Strict Unicode Order)",
                Location = new Point(16, 344),
                Size = new Size(512, 86),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            _lblLiveMode = new Label
            {
                AutoSize = true,
                Location = new Point(14, 22),
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
            };
            UpdateLiveModeLabel();

            var btnQuickToggle = new Button
            {
                Text = "Switch Mode",
                Location = new Point(404, 17),
                Size = new Size(94, 24),
                Font = new Font("Segoe UI", 8.5f),
                UseVisualStyleBackColor = true
            };
            btnQuickToggle.Click += (s, e) =>
            {
                _engine.ToggleMode();
                _testTypingBox.Focus();
            };

            _testTypingBox = new TextBox
            {
                Location = new Point(14, 46),
                Size = new Size(484, 28),
                Font = new Font("Nirmala UI", 11f)
            };

            grpTest.Controls.Add(_lblLiveMode);
            grpTest.Controls.Add(btnQuickToggle);
            grpTest.Controls.Add(_testTypingBox);

            // Bottom buttons
            var btnSave = new Button
            {
                Text = "Save && Apply",
                Location = new Point(332, 440),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(5, 150, 105),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += OnSaveClick;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(440, 440),
                Size = new Size(88, 32),
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += (s, e) => Close();

            Controls.Add(headerPanel);
            Controls.Add(grpHotkey);
            Controls.Add(grpOptions);
            Controls.Add(grpTest);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
        }

        private void OnEngineModeChanged(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateLiveModeLabel));
            }
            else
            {
                UpdateLiveModeLabel();
            }
        }

        private void UpdateLiveModeLabel()
        {
            if (_engine.IsBengaliMode)
            {
                _lblLiveMode.Text = "Active Mode: বাং (UniBijoy) — Try typing: f (া), j+f (কা), j+g+j (ক্ক), g+f (আ)";
                _lblLiveMode.ForeColor = Color.FromArgb(4, 120, 87);
            }
            else
            {
                _lblLiveMode.Text = "Active Mode: EN (English) — Press Ctrl + Space to type বাংলা";
                _lblLiveMode.ForeColor = Color.FromArgb(51, 65, 85);
            }
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            _config.StartInBengaliMode = _chkStartInBengali.Checked;
            _config.SmartVowelForming = _chkSmartVowel.Checked;
            _config.BengaliNumpad = _chkBengaliNumpad.Checked;
            _config.LeftAltAsAltGr = _chkLeftAltAsAltGr.Checked;

            _config.Save();
            AppConfig.SetStartWithWindows(_chkStartWithWindows.Checked);
            _engine.UpdateConfig(_config);

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _engine.ModeChanged -= OnEngineModeChanged;
            base.OnFormClosed(e);
        }
    }
}
