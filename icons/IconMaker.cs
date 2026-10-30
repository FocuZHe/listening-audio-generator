using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconMaker
{
    public static int Main(string[] args)
    {
        Make(args[0], args[1]);
        return 0;
    }

    static void Make(string src, string dest)
    {
        using (var original = new Bitmap(src))
        {
            int minX = original.Width, minY = original.Height, maxX = 0, maxY = 0;
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    if (c.R < 245 || c.G < 245 || c.B < 245)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            var rect = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            using (var cropped = original.Clone(rect, PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < cropped.Height; y++)
                {
                    for (int x = 0; x < cropped.Width; x++)
                    {
                        Color c = cropped.GetPixel(x, y);
                        if (c.R > 248 && c.G > 248 && c.B > 248)
                            cropped.SetPixel(x, y, Color.FromArgb(0, c));
                    }
                }
                int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
                var pngs = new byte[sizes.Length][];
                for (int i = 0; i < sizes.Length; i++)
                {
                    using (var scaled = new Bitmap(sizes[i], sizes[i], PixelFormat.Format32bppArgb))
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(cropped, 0, 0, sizes[i], sizes[i]);
                        using (var ms = new MemoryStream())
                        {
                            scaled.Save(ms, ImageFormat.Png);
                            pngs[i] = ms.ToArray();
                        }
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                using (var fs = File.Create(dest))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write((ushort)0);
                    w.Write((ushort)1);
                    w.Write((ushort)sizes.Length);
                    int offset = 6 + (16 * sizes.Length);
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        int s = sizes[i];
                        w.Write((byte)(s >= 256 ? 0 : s));
                        w.Write((byte)(s >= 256 ? 0 : s));
                        w.Write((byte)0);
                        w.Write((byte)0);
                        w.Write((ushort)1);
                        w.Write((ushort)32);
                        w.Write(pngs[i].Length);
                        w.Write(offset);
                        offset += pngs[i].Length;
                    }
                    for (int i = 0; i < sizes.Length; i++) w.Write(pngs[i]);
                }
            }
        }
    }
}
