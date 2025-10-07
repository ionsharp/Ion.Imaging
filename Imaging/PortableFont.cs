namespace Ion.Imaging;

public class PortableFont(string name = "Arial", int emsize = 12, bool isbold = false, bool isitalic = false, bool cleartype = false)
{
    public readonly int EmSize = emsize;

    public readonly string FontName = name;

    public readonly bool IsBold = isbold;

    public readonly bool IsItalic = isitalic;

    public readonly bool IsClearType = cleartype;

    public override int GetHashCode()
    {
        unchecked
        {
            return FontName.GetHashCode() ^ EmSize.GetHashCode() ^ IsBold.GetHashCode() ^ IsItalic.GetHashCode() ^ IsClearType.GetHashCode();
        }
    }

    public override bool Equals(object obj)
    {
        if (obj is not PortableFont other) return false;
        return FontName == other.FontName && EmSize == other.EmSize && IsBold == other.IsBold && IsItalic == other.IsItalic && IsClearType == other.IsClearType;
    }
}
