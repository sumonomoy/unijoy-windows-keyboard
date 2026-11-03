using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UnijoyKeyboard
{
    public sealed class KeyMappingEntry
    {
        public Keys VirtualKey { get; set; }
        public string AvroKeyName { get; set; }
        public string DisplayKeyLabel { get; set; }
        public string Normal { get; set; }
        public string Shift { get; set; }
        public string AltGr { get; set; }
        public string ShiftAltGr { get; set; }
        public bool IsNumpad { get; set; }

        public KeyMappingEntry()
        {
            Normal = "";
            Shift = "";
            AltGr = "";
            ShiftAltGr = "";
        }
    }

    public sealed class AvroLayout
    {
        public string FilePath { get; set; }
        public string AvroKeyboardVersion { get; set; }
        public string LayoutName { get; set; }
        public string LayoutVersion { get; set; }
        public string DeveloperName { get; set; }
        public string DeveloperComment { get; set; }
        public string ImageNormalShiftBase64 { get; set; }
        public string ImageAltGrShiftBase64 { get; set; }

        public Dictionary<int, KeyMappingEntry> KeyMap { get; private set; }
        public List<KeyMappingEntry> OrderedEntries { get; private set; }

        public AvroLayout()
        {
            FilePath = "";
            AvroKeyboardVersion = "4";
            LayoutName = "Unijoy";
            LayoutVersion = "4";
            DeveloperName = "";
            DeveloperComment = "";
            ImageNormalShiftBase64 = "";
            ImageAltGrShiftBase64 = "";
            KeyMap = new Dictionary<int, KeyMappingEntry>();
            OrderedEntries = new List<KeyMappingEntry>();
        }

        public Image CreateNormalShiftImage()
        {
            return DecodeBase64Image(ImageNormalShiftBase64);
        }

        public Image CreateAltGrShiftImage()
        {
            return DecodeBase64Image(ImageAltGrShiftBase64);
        }

        private static Image DecodeBase64Image(string b64)
        {
            if (string.IsNullOrWhiteSpace(b64))
                return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(b64.Trim());
                var ms = new MemoryStream(bytes);
                return Image.FromStream(ms);
            }
            catch
            {
                return null;
            }
        }
    }

    public static class AvroLayoutParser
    {
        private struct KeyDefinition
        {
            public string AvroName;
            public Keys Vk;
            public string Label;
            public bool IsNumpad;

            public KeyDefinition(string avroName, Keys vk, string label, bool isNumpad = false)
            {
                AvroName = avroName;
                Vk = vk;
                Label = label;
                IsNumpad = isNumpad;
            }
        }

        private static readonly KeyDefinition[] StandardKeys = new[]
        {
            // Number row
            new KeyDefinition("OEM3", Keys.Oemtilde, "` / ~"),
            new KeyDefinition("1", Keys.D1, "1 / !"),
            new KeyDefinition("2", Keys.D2, "2 / @"),
            new KeyDefinition("3", Keys.D3, "3 / #"),
            new KeyDefinition("4", Keys.D4, "4 / $"),
            new KeyDefinition("5", Keys.D5, "5 / %"),
            new KeyDefinition("6", Keys.D6, "6 / ^"),
            new KeyDefinition("7", Keys.D7, "7 / &"),
            new KeyDefinition("8", Keys.D8, "8 / *"),
            new KeyDefinition("9", Keys.D9, "9 / ("),
            new KeyDefinition("0", Keys.D0, "0 / )"),
            new KeyDefinition("MINUS", Keys.OemMinus, "- / _"),
            new KeyDefinition("PLUS", Keys.Oemplus, "= / +"),

            // Top letter row (QWERTY)
            new KeyDefinition("Q", Keys.Q, "Q"),
            new KeyDefinition("W", Keys.W, "W"),
            new KeyDefinition("E", Keys.E, "E"),
            new KeyDefinition("R", Keys.R, "R"),
            new KeyDefinition("T", Keys.T, "T"),
            new KeyDefinition("Y", Keys.Y, "Y"),
            new KeyDefinition("U", Keys.U, "U"),
            new KeyDefinition("I", Keys.I, "I"),
            new KeyDefinition("O", Keys.O, "O"),
            new KeyDefinition("P", Keys.P, "P"),
            new KeyDefinition("OEM4", Keys.OemOpenBrackets, "[ / {"),
            new KeyDefinition("OEM6", Keys.OemCloseBrackets, "] / }"),
            new KeyDefinition("OEM5", Keys.OemPipe, "\\ / |"),

            // Home letter row (ASDF)
            new KeyDefinition("A", Keys.A, "A"),
            new KeyDefinition("S", Keys.S, "S"),
            new KeyDefinition("D", Keys.D, "D"),
            new KeyDefinition("F", Keys.F, "F"),
            new KeyDefinition("G", Keys.G, "G"),
            new KeyDefinition("H", Keys.H, "H"),
            new KeyDefinition("J", Keys.J, "J"),
            new KeyDefinition("K", Keys.K, "K"),
            new KeyDefinition("L", Keys.L, "L"),
            new KeyDefinition("OEM1", Keys.OemSemicolon, "; / :"),
            new KeyDefinition("OEM7", Keys.OemQuotes, "' / \""),

            // Bottom letter row (ZXCV)
            new KeyDefinition("Z", Keys.Z, "Z"),
            new KeyDefinition("X", Keys.X, "X"),
            new KeyDefinition("C", Keys.C, "C"),
            new KeyDefinition("V", Keys.V, "V"),
            new KeyDefinition("B", Keys.B, "B"),
            new KeyDefinition("N", Keys.N, "N"),
            new KeyDefinition("M", Keys.M, "M"),
            new KeyDefinition("COMMA", Keys.Oemcomma, ", / <"),
            new KeyDefinition("PERIOD", Keys.OemPeriod, ". / >"),
            new KeyDefinition("OEM2", Keys.OemQuestion, "/ / ?")
        };

        private static readonly KeyDefinition[] NumpadKeys = new[]
        {
            new KeyDefinition("Num0", Keys.NumPad0, "Numpad 0", true),
            new KeyDefinition("Num1", Keys.NumPad1, "Numpad 1", true),
            new KeyDefinition("Num2", Keys.NumPad2, "Numpad 2", true),
            new KeyDefinition("Num3", Keys.NumPad3, "Numpad 3", true),
            new KeyDefinition("Num4", Keys.NumPad4, "Numpad 4", true),
            new KeyDefinition("Num5", Keys.NumPad5, "Numpad 5", true),
            new KeyDefinition("Num6", Keys.NumPad6, "Numpad 6", true),
            new KeyDefinition("Num7", Keys.NumPad7, "Numpad 7", true),
            new KeyDefinition("Num8", Keys.NumPad8, "Numpad 8", true),
            new KeyDefinition("Num9", Keys.NumPad9, "Numpad 9", true),
            new KeyDefinition("NumAdd", Keys.Add, "Numpad +", true),
            new KeyDefinition("NumSubtract", Keys.Subtract, "Numpad -", true),
            new KeyDefinition("NumMultiply", Keys.Multiply, "Numpad *", true),
            new KeyDefinition("NumDivide", Keys.Divide, "Numpad /", true),
            new KeyDefinition("NumDecimal", Keys.Decimal, "Numpad .", true)
        };

        private static readonly Regex TagRegex = new Regex(
            @"<([a-zA-Z0-9_]+)>(?:<!\[CDATA\[(.*?)\]\]>|([^<]*))</\1>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        public static AvroLayout LoadDefaultOrCustom(string customFilePath)
        {
            if (!string.IsNullOrWhiteSpace(customFilePath) && File.Exists(customFilePath))
            {
                try
                {
                    return LoadFromFile(customFilePath);
                }
                catch
                {
                }
            }

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(exeDir, "Unijoy.avrolayout");
            if (File.Exists(localPath))
            {
                try
                {
                    return LoadFromFile(localPath);
                }
                catch
                {
                }
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
                            using (var reader = new StreamReader(s, Encoding.UTF8))
                            {
                                string xml = reader.ReadToEnd();
                                var layout = ParseXmlContent(xml);
                                layout.FilePath = "(Built-in Unijoy.avrolayout)";
                                return layout;
                            }
                        }
                    }
                }
            }

            throw new FileNotFoundException("Could not find Unijoy.avrolayout in folder or embedded resources.");
        }

        public static AvroLayout LoadFromFile(string filePath)
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            AvroLayout layout = ParseXmlContent(content);
            layout.FilePath = Path.GetFullPath(filePath);
            return layout;
        }

        public static AvroLayout ParseXmlContent(string content)
        {
            var rawTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            MatchCollection matches = TagRegex.Matches(content);
            foreach (Match m in matches)
            {
                string tagName = m.Groups[1].Value;
                string val = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                rawTags[tagName] = val ?? "";
            }

            var layout = new AvroLayout
            {
                AvroKeyboardVersion = GetTag(rawTags, "AvroKeyboardVersion", "4").Trim(),
                LayoutName = GetTag(rawTags, "LayoutName", "Unijoy").Trim(),
                LayoutVersion = GetTag(rawTags, "LayoutVersion", "4").Trim(),
                DeveloperName = GetTag(rawTags, "DeveloperName", "").Trim(),
                DeveloperComment = GetTag(rawTags, "DeveloperComment", "").Trim(),
                ImageNormalShiftBase64 = GetTag(rawTags, "ImageNormalShift", ""),
                ImageAltGrShiftBase64 = GetTag(rawTags, "ImageAltGrShift", "")
            };

            foreach (KeyDefinition def in StandardKeys)
            {
                var entry = new KeyMappingEntry
                {
                    VirtualKey = def.Vk,
                    AvroKeyName = def.AvroName,
                    DisplayKeyLabel = def.Label,
                    Normal = GetTag(rawTags, def.AvroName + "_Normal", ""),
                    Shift = GetTag(rawTags, def.AvroName + "_Shift", ""),
                    AltGr = GetTag(rawTags, def.AvroName + "_AltGr", ""),
                    ShiftAltGr = GetTag(rawTags, def.AvroName + "_ShiftAltGr", ""),
                    IsNumpad = false
                };

                layout.KeyMap[(int)def.Vk] = entry;
                layout.OrderedEntries.Add(entry);
            }

            foreach (KeyDefinition def in NumpadKeys)
            {
                string numVal = GetTag(rawTags, def.AvroName, "");
                var entry = new KeyMappingEntry
                {
                    VirtualKey = def.Vk,
                    AvroKeyName = def.AvroName,
                    DisplayKeyLabel = def.Label,
                    Normal = numVal,
                    Shift = numVal,
                    AltGr = "",
                    ShiftAltGr = "",
                    IsNumpad = true
                };

                layout.KeyMap[(int)def.Vk] = entry;
                layout.OrderedEntries.Add(entry);
            }

            return layout;
        }

        private static string GetTag(Dictionary<string, string> dict, string key, string defaultValue)
        {
            string val;
            if (dict.TryGetValue(key, out val))
                return val;
            return defaultValue;
        }
    }
}
