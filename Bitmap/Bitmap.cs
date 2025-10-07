using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ion.Imaging;

[Extend<Bitmap>]
public static partial class XBitmap
{
    /// <see cref="Region.Method"/>

    public static Bitmap Convert(BitmapImage i, BitmapEncoders e = BitmapEncoders.JPG)
    {
        using var outStream = new MemoryStream();

        var encoder = e.GetEncoder();
        encoder.Frames.Add(BitmapFrame.Create(i));
        encoder.Save(outStream);

        var result = new Bitmap(outStream);
        return new Bitmap(result);
    }

    public static Bitmap Convert(BitmapSource i, BitmapEncoders e = BitmapEncoders.JPG)
    {
        if (i is null)
            return null;

        Bitmap result;
        using (var outStream = new MemoryStream())
        {
            var encoder = e.GetEncoder();

            encoder.Frames.Add(BitmapFrame.Create(i));
            encoder.Save(outStream);
            result = new Bitmap(outStream);
        }
        return result;
    }

    public static Bitmap Convert(ImageSource i, BitmapEncoders e = BitmapEncoders.JPG) => Convert(i as BitmapSource, e);

    public static Bitmap Convert<T>(this WriteableBitmap i) where T : BitmapEncoder, new()
    {
        Bitmap result = default;
        using (var stream = new MemoryStream())
        {
            T encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(i));
            encoder.Save(stream);
            result = new(stream);
        }
        return result;
    }

    public static Bitmap New(Size i) => New(i.Height, i.Width);

    public static Bitmap New(int height, int width) => new(width, height);

    [Convert<byte[], Bitmap>]
    public static unsafe byte[] ToBytes(this Bitmap i)
    {
        //Lock the bitmap's bits. 
        Rectangle rect = new(0, 0, i.Width, i.Height);
        var data = i.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, i.PixelFormat);

        //Get the address of the first line.
        var pointer = data.Scan0;

        //Declare an array to hold the bytes of the bitmap.
        int byteCount
            = data.Stride * i.Height;
        byte[] result
            = new byte[byteCount];

        // Copy the RGB values into the array.
        System.Runtime.InteropServices.Marshal.Copy(pointer, result, 0, byteCount);
        i.UnlockBits(data);

        return result;
    }

    /// <see cref="Region.Method.Import"/>

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect([In] ref ICONINFO piconinfo);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}