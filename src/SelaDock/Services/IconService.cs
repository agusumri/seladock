using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SelaDock.Interop;

namespace SelaDock.Services;

/// <summary>
/// Produces sharp, high-resolution WPF icons for windows and executables, cached by key so each
/// icon is extracted at most once. Icons are taken from the shell's large/jumbo image list (up to
/// 256px) rather than the tiny 16px window icon, so they stay crisp when the dock magnifies them.
/// Frozen images are returned so they can be shared across the UI thread without re-decoding.
/// </summary>
public sealed class IconService
{
    private readonly Dictionary<string, ImageSource?> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<nint, ImageSource?> _byWindow = new();

    /// <summary>Icon for a window: prefer the sharp exe icon, fall back to the window's own icon.</summary>
    public ImageSource? ForWindow(nint hWnd, string processPath)
    {
        if (_byWindow.TryGetValue(hWnd, out var cached) && cached is not null) return cached;

        // The exe icon from the shell image list is high-resolution; prefer it.
        var icon = !string.IsNullOrEmpty(processPath) ? ForPath(processPath) : null;
        // Fall back to the window's small icon only if the exe had none.
        icon ??= FromWindowMessage(hWnd) ?? FromClassIcon(hWnd);

        _byWindow[hWnd] = icon;
        return icon;
    }

    /// <summary>Sharp icon for an executable path, used by pinned shortcuts and running apps.</summary>
    public ImageSource? ForPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_byPath.TryGetValue(path, out var cached)) return cached;

        var img = FromShellImageList(path, Native.SHIL_JUMBO)
                  ?? FromShellImageList(path, Native.SHIL_EXTRALARGE)
                  ?? FromExtractAssociated(path);

        _byPath[path] = img;
        return img;
    }

    public void ForgetWindow(nint hWnd) => _byWindow.Remove(hWnd);

    /// <summary>Pull the jumbo (256px) or extra-large (48px) icon for a file from the shell image list.</summary>
    private static ImageSource? FromShellImageList(string path, int size)
    {
        try
        {
            var info = new Native.SHFILEINFO();
            var flags = Native.SHGFI_SYSICONINDEX;
            if (!File.Exists(path)) flags |= Native.SHGFI_USEFILEATTRIBUTES;

            var r = Native.SHGetFileInfoW(path, 0x80 /*FILE_ATTRIBUTE_NORMAL*/, ref info,
                (uint)Marshal.SizeOf<Native.SHFILEINFO>(), flags);
            if (r == nint.Zero) return null;

            var guid = Native.IID_IImageList;
            if (Native.SHGetImageList(size, ref guid, out var list) != 0 || list is null) return null;

            if (list.GetIcon(info.iIcon, Native.ILD_TRANSPARENT, out var hIcon) != 0 || hIcon == nint.Zero)
                return null;
            try
            {
                return ToImageSource(hIcon, trim: size == Native.SHIL_JUMBO);
            }
            finally
            {
                Native.DestroyIcon(hIcon);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidCastException)
        {
            return null;
        }
    }

    private static ImageSource? FromExtractAssociated(string path)
    {
        try
        {
            using var sys = System.Drawing.Icon.ExtractAssociatedIcon(path);
            return sys is not null ? ToImageSource(sys.Handle) : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static ImageSource? FromWindowMessage(nint hWnd)
    {
        nint handle = Send(hWnd, Native.ICON_BIG);
        if (handle == nint.Zero) handle = Send(hWnd, Native.ICON_SMALL2);
        return handle != nint.Zero ? ToImageSource(handle) : null;

        static nint Send(nint h, nint type)
        {
            Native.SendMessageTimeoutW(h, Native.WM_GETICON, type, nint.Zero,
                Native.SMTO_ABORTIFHUNG, 200, out var result);
            return result;
        }
    }

    private static ImageSource? FromClassIcon(nint hWnd)
    {
        var handle = Native.GetClassLongPtrW(hWnd, Native.GCLP_HICON);
        if (handle == nint.Zero) handle = Native.GetClassLongPtrW(hWnd, Native.GCLP_HICONSM);
        return handle != nint.Zero ? ToImageSource(handle) : null;
    }

    /// <summary>
    /// Convert an HICON to a frozen BitmapSource. Jumbo icons from the 256px list are often a small
    /// glyph padded inside a 256px canvas; <paramref name="trim"/> crops the transparent margin so
    /// the icon fills the dock button instead of appearing tiny.
    /// </summary>
    private static ImageSource? ToImageSource(nint hIcon, bool trim = false)
    {
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            ImageSource result = trim ? TrimTransparent(src) : src;
            result.Freeze();
            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Crop the fully-transparent border of a bitmap so a small glyph in a big canvas fills the frame.</summary>
    private static BitmapSource TrimTransparent(BitmapSource src)
    {
        int w = src.PixelWidth, h = src.PixelHeight;
        if (src.Format != PixelFormats.Bgra32)
            src = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);

        int stride = w * 4;
        var px = new byte[h * stride];
        src.CopyPixels(px, stride, 0);

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * stride;
            for (int x = 0; x < w; x++)
            {
                if (px[row + x * 4 + 3] <= 8) continue; // near-transparent
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY) return src; // fully transparent: leave as-is

        // Keep it square with a hair of padding so magnification doesn't clip edges.
        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        int side = Math.Max(cw, ch);
        int pad = Math.Max(1, side / 24);
        side += pad * 2;
        int cx = Math.Clamp(minX + cw / 2 - side / 2, 0, Math.Max(0, w - side));
        int cy = Math.Clamp(minY + ch / 2 - side / 2, 0, Math.Max(0, h - side));
        side = Math.Min(side, Math.Min(w - cx, h - cy));
        if (side <= 0) return src;

        return new CroppedBitmap(src, new Int32Rect(cx, cy, side, side));
    }
}
