using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace Ion.Imaging;

[Extend<ImageSource>]
public static class XImageSource
{
    public static ImageSource Convert(byte[] i, int width, int height, System.Drawing.Imaging.PixelFormat Format)
    {
        var bitmap = new Bitmap(width, height, Format);

        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, Format);
        Marshal.Copy(i, 0, bitmapData.Scan0, i.Length);

        bitmap.UnlockBits(bitmapData);
        return XBitmapSource.Convert(bitmap);
    }

    public static ImageSource Convert(Uri i) => Try.Get(() => (ImageSource)new ImageSourceConverter().ConvertFromString(i.OriginalString));
}