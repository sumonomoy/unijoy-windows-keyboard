using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UniBijoyKeyboard
{
    public static class TrayIconGenerator
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateModeIcon(bool isBengali)
        {
            bool lightTaskbar = IsSystemTaskbarLightTheme();
            Color textColor = lightTaskbar
                ? Color.FromArgb(20, 20, 20)
                : Color.White;

            using (Bitmap bmp = CreateSimpleTextBitmap(isBengali, 32, textColor))
            {
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        return (Icon)tempIcon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        public static Bitmap CreateSimpleTextBitmap(bool isBengali, int size, Color textColor)
        {
            var bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // AntiAliasGridFit produces clean alpha-blended edges on transparent HICONs
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                using (var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                using (var textBrush = new SolidBrush(textColor))
                {
                    if (isBengali)
                    {
                        // Simple text "বাং"
                        float fontSize = size * 0.50f;
                        using (var font = new Font("Nirmala UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            g.DrawString("বাং", font, textBrush, new RectangleF(-2f, 0f, size + 4f, size), sf);
                        }
                    }
                    else
                    {
                        // Simple text "EN"
                        float fontSize = size * 0.52f;
                        using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            g.DrawString("EN", font, textBrush, new RectangleF(-1f, 0f, size + 2f, size), sf);
                        }
                    }
                }
            }
            return bmp;
        }

        private static bool IsSystemTaskbarLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("SystemUsesLightTheme");
                        if (val != null)
                        {
                            return Convert.ToInt32(val) == 1;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
