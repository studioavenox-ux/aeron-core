using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aeron.Desktop.Tray;

/// <summary>
/// Renders AERON's minimal geometric brand emblem for both the native Win32 system tray icon
/// and the WPF window titlebar without external binary asset bloat or GDI handle leaks.
/// </summary>
internal static class BrandIconFactory
{
    public static ImageSource CreateWindowIcon()
    {
        const int size = 32;
        var pixels = RenderEmblemBgra(size);
        var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, palette: null);
        bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride: size * 4, offset: 0);
        bitmap.Freeze();
        return bitmap;
    }

    public static nint CreateTrayHIcon()
    {
        const int size = 32;
        var pixels = RenderEmblemBgra(size);

        var bmi = new NativeTrayMethods.BITMAPV5HEADER
        {
            bV5Size = (uint)Marshal.SizeOf<NativeTrayMethods.BITMAPV5HEADER>(),
            bV5Width = size,
            bV5Height = -size, // Top-down DIB
            bV5Planes = 1,
            bV5BitCount = 32,
            bV5Compression = NativeTrayMethods.BI_BITFIELDS,
            bV5RedMask = 0x00FF0000,
            bV5GreenMask = 0x0000FF00,
            bV5BlueMask = 0x000000FF,
            bV5AlphaMask = 0xFF000000
        };

        var screenDc = NativeTrayMethods.GetDC(nint.Zero);
        var colorBitmap = nint.Zero;
        var maskBitmap = nint.Zero;

        try
        {
            colorBitmap = NativeTrayMethods.CreateDIBSection(
                screenDc,
                ref bmi,
                NativeTrayMethods.DIB_RGB_COLORS,
                out var bitsPtr,
                nint.Zero,
                0);

            if (colorBitmap == nint.Zero || bitsPtr == nint.Zero)
            {
                return nint.Zero;
            }

            Marshal.Copy(pixels, 0, bitsPtr, pixels.Length);

            // Zero-initialized 1-bpp AND mask (32x32 = 128 bytes; WORD-aligned scanlines)
            // ensures clean per-pixel alpha blending across all Windows 11 tray DPI scales.
            var zeroMask = new byte[(size / 8) * size];
            maskBitmap = NativeTrayMethods.CreateBitmap(size, size, 1, 1, zeroMask);
            if (maskBitmap == nint.Zero)
            {
                return nint.Zero;
            }

            var iconInfo = new NativeTrayMethods.ICONINFO
            {
                fIcon = true,
                xHotspot = 0,
                yHotspot = 0,
                hbmMask = maskBitmap,
                hbmColor = colorBitmap
            };

            return NativeTrayMethods.CreateIconIndirect(ref iconInfo);
        }
        finally
        {
            if (maskBitmap != nint.Zero)
            {
                _ = NativeTrayMethods.DeleteObject(maskBitmap);
            }

            if (colorBitmap != nint.Zero)
            {
                _ = NativeTrayMethods.DeleteObject(colorBitmap);
            }

            if (screenDc != nint.Zero)
            {
                _ = NativeTrayMethods.ReleaseDC(nint.Zero, screenDc);
            }
        }
    }

    private static byte[] RenderEmblemBgra(int size)
    {
        var buffer = new byte[size * size * 4];
        var center = (size - 1) / 2.0;
        var cornerRadius = 6.0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var idx = ((y * size) + x) * 4;
                if (!InsideRoundedRect(x, y, size, cornerRadius))
                {
                    continue;
                }

                var isBorder = !InsideRoundedRect(x - 1, y - 1, size - 2, cornerRadius - 1.0);

                // Background: deep slate #0F172A; border: #1E3A5F
                byte r = isBorder ? (byte)0x1E : (byte)0x0F;
                byte g = isBorder ? (byte)0x3A : (byte)0x17;
                byte b = isBorder ? (byte)0x5F : (byte)0x2A;
                const byte a = 0xFF;

                // Crisp geometric "A" chevron glyph in cyan #38BDF8
                if (IsGlyphPixel(x, y, center))
                {
                    r = 0x38;
                    g = 0xBD;
                    b = 0xF8;
                }

                buffer[idx] = b;
                buffer[idx + 1] = g;
                buffer[idx + 2] = r;
                buffer[idx + 3] = a;
            }
        }

        return buffer;
    }

    private static bool InsideRoundedRect(int x, int y, int size, double radius)
    {
        if (x < 0 || y < 0 || x >= size || y >= size)
        {
            return false;
        }

        var dx = Math.Max(0.0, Math.Max(radius - x, x - (size - 1 - radius)));
        var dy = Math.Max(0.0, Math.Max(radius - y, y - (size - 1 - radius)));
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static bool IsGlyphPixel(int x, int y, double center)
    {
        if (y is < 6 or > 25)
        {
            return false;
        }

        var progress = (y - 6) / 19.0; // 0 at apex, 1 at base
        var halfSpan = progress * 8.5;
        var distanceFromCenter = Math.Abs(x - center);

        // Two angled legs of the "A"
        var onLeg = Math.Abs(distanceFromCenter - halfSpan) <= 1.65;

        // Horizontal crossbar
        var onCrossbar = y is >= 18 and <= 20 && distanceFromCenter <= halfSpan - 0.5;

        return onLeg || onCrossbar;
    }
}
