using System;
using System.Drawing;
using System.Windows.Forms;

namespace UniBijoyKeyboard
{
    public sealed class LayoutViewerForm : Form
    {
        private readonly AvroLayout _layout;
        private PictureBox _picNormal;
        private PictureBox _picAltGr;
        private DataGridView _grid;
        private TextBox _searchBox;
        private Image _normalImage;
        private Image _altGrImage;

        public LayoutViewerForm(AvroLayout layout, Icon appIcon)
        {
            if (layout == null) throw new ArgumentNullException("layout");
            _layout = layout;

            Text = string.Format("{0} (v{1}) — Keyboard Layout Viewer", _layout.LayoutName, _layout.LayoutVersion);
            Size = new Size(920, 560);
            MinimumSize = new Size(720, 440);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            BuildUi();
            PopulateGrid("");
        }

        private void BuildUi()
        {
            // Header panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(16, 10, 16, 10)
            };

            var titleLabel = new Label
            {
                Text = string.Format("{0} Keyboard Layout (bn-BD Unicode)", _layout.LayoutName),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 8)
            };

            var subTitleLabel = new Label
            {
                Text = "Strict Unicode Order: Type Consonant (ব্যঞ্জনবর্ণ) first, then Vowel Sign (কার) or Hasanta (্) + Consonant for যুক্তবর্ণ",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(17, 32)
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subTitleLabel);

            // Tab control
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Padding = new Point(12, 6)
            };

            // Tab 1: Normal & Shift Layout Image
            var tabNormalImg = new TabPage("Normal && Shift Layout");
            tabNormalImg.BackColor = Color.White;
            _normalImage = _layout.CreateNormalShiftImage();
            if (_normalImage != null)
            {
                _picNormal = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = _normalImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White
                };
                tabNormalImg.Controls.Add(_picNormal);
            }
            else
            {
                tabNormalImg.Controls.Add(CreateNoImageLabel("No Normal/Shift layout image embedded in this .avrolayout file."));
            }

            // Tab 2: AltGr & Shift+AltGr Layout Image
            var tabAltGrImg = new TabPage("AltGr (Ctrl+Alt) Layout");
            tabAltGrImg.BackColor = Color.White;
            _altGrImage = _layout.CreateAltGrShiftImage();
            if (_altGrImage != null)
            {
                _picAltGr = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = _altGrImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White
                };
                tabAltGrImg.Controls.Add(_picAltGr);
            }
            else
            {
                tabAltGrImg.Controls.Add(CreateNoImageLabel("No AltGr layout image embedded in this .avrolayout file."));
            }

            // Tab 3: Searchable Key Mapping Table
            var tabTable = new TabPage("Key Mapping Reference");
            tabTable.BackColor = Color.White;
            tabTable.Padding = new Padding(10);

            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(0, 2, 0, 6)
            };

            var searchLabel = new Label
            {
                Text = "Filter by key or Bengali character:",
                AutoSize = true,
                Location = new Point(2, 7)
            };

            _searchBox = new TextBox
            {
                Location = new Point(215, 4),
                Width = 260,
                Font = new Font("Nirmala UI", 10f)
            };
            _searchBox.TextChanged += (s, e) => PopulateGrid(_searchBox.Text);

            var tipLabel = new Label
            {
                Text = "Tip: G (্) + Vowel Kar (া, ি, ী, ু, ূ, ৃ, ে, ৈ, ো, ৌ) or RightAlt/Ctrl+Alt produces Full Vowels (আ, ই, ঈ...)",
                AutoSize = true,
                ForeColor = Color.FromArgb(4, 120, 87),
                Location = new Point(490, 7)
            };

            searchPanel.Controls.Add(searchLabel);
            searchPanel.Controls.Add(_searchBox);
            searchPanel.Controls.Add(tipLabel);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(226, 232, 240),
                RowTemplate = { Height = 32 },
                Font = new Font("Nirmala UI", 11.5f)
            };

            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _grid.ColumnHeadersHeight = 34;
            _grid.EnableHeadersVisualStyles = false;

            _grid.Columns.Add("Key", "Physical Key");
            _grid.Columns.Add("Normal", "Normal");
            _grid.Columns.Add("Shift", "Shift");
            _grid.Columns.Add("AltGr", "AltGr (RightAlt / Ctrl+Alt)");
            _grid.Columns.Add("ShiftAltGr", "Shift + AltGr");

            tabTable.Controls.Add(_grid);
            tabTable.Controls.Add(searchPanel);

            tabs.TabPages.Add(tabNormalImg);
            tabs.TabPages.Add(tabAltGrImg);
            tabs.TabPages.Add(tabTable);

            Controls.Add(tabs);
            Controls.Add(headerPanel);
        }

        private static Label CreateNoImageLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
        }

        private void PopulateGrid(string filter)
        {
            _grid.Rows.Clear();
            string q = (filter ?? "").Trim();

            foreach (KeyMappingEntry entry in _layout.OrderedEntries)
            {
                string normalDisp = FormatCharForDisplay(entry.Normal);
                string shiftDisp = FormatCharForDisplay(entry.Shift);
                string altGrDisp = FormatCharForDisplay(entry.AltGr);
                string shiftAltGrDisp = FormatCharForDisplay(entry.ShiftAltGr);

                if (!string.IsNullOrEmpty(q))
                {
                    bool match =
                        entry.DisplayKeyLabel.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.AvroKeyName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        normalDisp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shiftDisp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        altGrDisp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shiftAltGrDisp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!match)
                        continue;
                }

                _grid.Rows.Add(
                    entry.DisplayKeyLabel,
                    normalDisp,
                    shiftDisp,
                    altGrDisp,
                    shiftAltGrDisp);
            }
        }

        private static string FormatCharForDisplay(string val)
        {
            if (string.IsNullOrEmpty(val))
                return "—";
            if (val == "\u200C")
                return "ZWNJ (Zero Width Non-Joiner)";
            if (val == "\u200D")
                return "ZWJ (Zero Width Joiner)";
            return val;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_normalImage != null) _normalImage.Dispose();
                if (_altGrImage != null) _altGrImage.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
