# Draws the NotchPrompter icon (dark tile, black notch with a green camera dot, white text lines)
# and writes a multi-size .ico. Small sizes are stored as 32-bit DIBs, 256px as PNG.
param([string]$Out = (Join-Path $PSScriptRoot '..\assets\app.ico'))

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class NotchIcon
{
    static GraphicsPath Rounded(RectangleF r, float rad, bool top, bool bottom)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        if (top) { p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); }
        else { p.AddLine(r.X, r.Y, r.Right, r.Y); }
        if (bottom) { p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); }
        else { p.AddLine(r.Right, r.Bottom, r.X, r.Bottom); }
        p.CloseFigure();
        return p;
    }

    public static Bitmap Draw(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float pad = Math.Max(0.5f, s * 0.04f);
            var tile = new RectangleF(pad, pad, s - 2 * pad, s - 2 * pad);
            using (var path = Rounded(tile, s * 0.22f, true, true))
            using (var fill = new LinearGradientBrush(tile, Color.FromArgb(255, 58, 62, 78), Color.FromArgb(255, 20, 21, 28), 90f))
                g.FillPath(fill, path);

            // Notch hanging from the top edge of the tile.
            float nw = s * 0.50f, nh = s * 0.20f;
            var notch = new RectangleF((s - nw) / 2f, pad, nw, nh);
            using (var np = Rounded(notch, nh * 0.5f, false, true))
                g.FillPath(Brushes.Black, np);
            float dot = Math.Max(1.6f, s * 0.07f);
            using (var green = new SolidBrush(Color.FromArgb(255, 48, 209, 88)))
                g.FillEllipse(green, notch.Right - nh * 0.55f - dot / 2f, notch.Y + nh * 0.48f - dot / 2f, dot, dot);

            // Script lines.
            float th = Math.Max(1.2f, s * 0.075f);
            float[] widths = { 0.60f, 0.46f, 0.54f };
            float[] ys = { 0.47f, 0.62f, 0.77f };
            int[] alpha = { 255, 190, 120 };
            for (int i = 0; i < 3; i++)
            {
                if (s <= 20 && i == 2) break;
                float w = s * widths[i];
                var line = new RectangleF((s - w) / 2f, s * ys[i] - th / 2f, w, th);
                using (var lp = Rounded(line, th / 2f, true, true))
                using (var b = new SolidBrush(i == 0 ? Color.FromArgb(255, 255, 214, 10) : Color.FromArgb(alpha[i], 255, 255, 255)))
                    g.FillPath(b, lp);
            }
        }
        return bmp;
    }

    static byte[] Dib(Bitmap bmp)
    {
        int s = bmp.Width;
        var data = bmp.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new byte[s * s * 4];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
        bmp.UnlockBits(data);

        int maskStride = ((s + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--) w.Write(px, y * s * 4, s * 4);   // bottom-up BGRA
            for (int y = s - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < s; x++)
                    if (px[(y * s + x) * 4 + 3] < 128) row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            return ms.ToArray();
        }
    }

    public static void Save(string path, int[] sizes)
    {
        var images = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (var bmp = Draw(s))
            {
                if (s >= 256)
                {
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); images.Add(ms.ToArray()); }
                }
                else images.Add(Dib(bmp));
            }
        }
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
    }

    public static void SavePreview(string path, int s)
    {
        using (var bmp = Draw(s)) bmp.Save(path, ImageFormat.Png);
    }
}
'@

$dir = Split-Path -Parent $Out
New-Item -ItemType Directory -Force $dir | Out-Null
$full = [System.IO.Path]::GetFullPath($Out)
[NotchIcon]::Save($full, [int[]](16, 20, 24, 32, 40, 48, 64, 256))
[NotchIcon]::SavePreview((Join-Path $dir 'icon-preview.png'), 256)
"Wrote $full"
