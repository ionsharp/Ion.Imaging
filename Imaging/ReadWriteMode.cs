namespace Ion.Imaging;

/// <summary>Read Write Mode for the BitmapContext.</summary>
public enum ReadWriteMode
{
    /// <summary>On Dispose of a BitmapContext, do not Invalidate</summary>
    ReadOnly,
    /// <summary>On Dispose of a BitmapContext, invalidate the bitmap</summary>
    ReadWrite
}