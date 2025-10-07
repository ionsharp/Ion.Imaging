using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Ion.Imaging;

[Extend<BitmapImage>]
public static class XBitmapImage
{
    public static BitmapImage Convert(Bitmap i, BitmapEncoders e = BitmapEncoders.JPG)
        => Convert(XBitmapSource.Convert(i), e);

    public static BitmapImage Convert(BitmapSource i, BitmapEncoders e = BitmapEncoders.JPG)
    {
        if (i is null)
            return null;

        var encoder = e.GetEncoder();

        var stream = new MemoryStream();
        var result = new BitmapImage();

        encoder.Frames.Add(BitmapFrame.Create(i));
        encoder.Save(stream);

        result.BeginInit();
        result.StreamSource = new MemoryStream(stream.ToArray());
        result.EndInit();

        stream.Close();
        return result;
    }
}