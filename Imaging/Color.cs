using Ion.Numeral;
using System.Windows.Media;

namespace Ion.Imaging;

[Extend<System.Drawing.Color>, Extend<System.Windows.Media.Color>]
public static class XColor
{
    public static Color A(this Color color, byte a)
        => Color.FromArgb(a, color.R, color.G, color.B);

    ///

    public static void Convert(this System.Drawing.Color color, out Color result)
        => result = Color.FromArgb(color.A, color.R, color.G, color.B);

    public static void Convert(this Color input, out System.Drawing.Color result)
        => result = System.Drawing.Color.FromArgb(input.A, input.R, input.G, input.B);

    ///

    public static void Convert(this Color input, out ByteVector4 result)
        => result = new(input.R, input.G, input.B, input.A);

    public static Color Convert(ByteVector4 i) => Color.FromArgb(i.A, i.R, i.G, i.B);
}