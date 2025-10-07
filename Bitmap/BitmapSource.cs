using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ion.Imaging;

[Extend<BitmapSource>]
public static class XBitmapSource
{
    public static BitmapSource Convert(Bitmap i)
    {
        if (i is null)
            return null;

        var pointer
            = i.GetHbitmap();
        var result
            = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(pointer, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        DeleteObject(pointer);
        return result;
    }

    public static byte[] ToBytes(this BitmapSource i)
    {
        var stream = ((BitmapImage)(i as ImageSource)).StreamSource;
        byte[] buffer = null;
        if (stream != null && stream.Length > 0)
        {
            using var reader = new BinaryReader(stream);
            buffer = reader.ReadBytes((int)stream.Length);
        }
        return buffer;
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr hObject);
}