using Microsoft.Win32.SafeHandles;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Ion.Imaging;

[Extend<System.Windows.Forms.Cursor>]
public static class XCursor
{
    public static System.Windows.Forms.Cursor Convert(Bitmap i, int hotX, int hotY)
    {
        var handle = i.GetHicon();
        GetIconInfo(handle, out ICONINFO info);

        info.fIcon = false;
        info.xHotspot = hotX;
        info.yHotspot = hotY;

        var h = CreateIconIndirect(ref info);
        return new System.Windows.Forms.Cursor(h);
    }

    public static System.Windows.Input.Cursor Convert(this System.Windows.Forms.Cursor i)
    {
        SafeFileHandle h = new(i.Handle, false);
        return System.Windows.Interop.CursorInteropHelper.Create(h);
    }

    /// <summary>Get the color at the current position of the <see cref="System.Windows.Forms.Cursor"/>.</summary>
    public static Color GetColor()
    {
        var location = new Point();
        GetCursorPos(ref location);

        Bitmap screenPixel = new(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics gdest = Graphics.FromImage(screenPixel))
        {
            using Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr hSrcDC = gsrc.GetHdc();
            IntPtr hDC = gdest.GetHdc();
            int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int)CopyPixelOperation.SourceCopy);
            gdest.ReleaseHdc();
            gsrc.ReleaseHdc();
        }

        return screenPixel.GetPixel(0, 0);
    }

    /// <summary>Get the current position of the <see cref="System.Windows.Forms.Cursor"/>.</summary>
    public static Point GetPosition()
    {
        var result = new Point();
        GetCursorPos(ref result);
        return result;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
    private static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect([In] ref ICONINFO piconinfo);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(ref Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
}