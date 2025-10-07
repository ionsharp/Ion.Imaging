using System;
using System.Windows.Media.Imaging;

namespace Ion.Imaging;

public enum BitmapEncoders
{
    JPG, PNG,
}

public static class XBitmapEncoder
{
    public static BitmapEncoder GetEncoder(this BitmapEncoders e)
        => e == BitmapEncoders.JPG ? new JpegBitmapEncoder() : e == BitmapEncoders.PNG ? new PngBitmapEncoder() : throw new NotSupportedException();
}