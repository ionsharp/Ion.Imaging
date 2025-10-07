using Ion.Numeral;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ion.Imaging;

public static class LetterGlyphTool
{
    public static readonly Dictionary<PortableFont, GlyphFont> FontCache = [];

    public static unsafe void DrawLetter(this BitmapContext context, int x0, int y0, Area<int> cliprect, System.Windows.Media.Color fontColor, GrayScaleLetterGlyph glyph)
    {
        if (glyph.Items is null) return;

        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;

        int fr = fontColor.R;
        int fg = fontColor.G;
        int fb = fontColor.B;

        int xmin = cliprect.X;
        int ymin = cliprect.Y;
        int xmax = cliprect.Right();
        int ymax = cliprect.Bottom();

        if (xmin < 0) xmin = 0;
        if (ymin < 0) ymin = 0;
        if (xmax >= w) xmax = w - 1;
        if (ymax >= h) ymax = h - 1;

        fixed (GrayScaleLetterGlyph.Item* items = glyph.Items)
        {
            int itemCount = glyph.Items.Length;
            GrayScaleLetterGlyph.Item* currentItem = items;
            for (int i = 0; i < itemCount; i++, currentItem++)
            {
                int x = x0 + currentItem->X;
                int y = y0 + currentItem->Y;
                int alpha = currentItem->Alpha;
                if (x < xmin || y < ymin || x > xmax || y > ymax) continue;

                int color = pixels[y * w + x];
                int r = ((color >> 16) & 0xFF);
                int g = ((color >> 8) & 0xFF);
                int b = ((color) & 0xFF);

                r = (((r << 12) + (fr - r) * alpha) >> 12) & 0xFF;
                g = (((g << 12) + (fg - g) * alpha) >> 12) & 0xFF;
                b = (((b << 12) + (fb - b) * alpha) >> 12) & 0xFF;

                pixels[y * w + x] = (0xFF << 24) | (r << 16) | (g << 8) | (b);
            }
        }
    }

    public static unsafe void DrawLetter(this BitmapContext context, int x0, int y0, Area<int> cliprect, ClearTypeLetterGlyph glyph)
    {
        //if (glyph.Instructions is null) return;
        if (glyph.Items is null) return;

        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;

        int xmin = cliprect.X;
        int ymin = cliprect.Y;
        int xmax = cliprect.Right();
        int ymax = cliprect.Bottom();

        if (xmin < 0) xmin = 0;
        if (ymin < 0) ymin = 0;
        if (xmax >= w) xmax = w - 1;
        if (ymax >= h) ymax = h - 1;

        fixed (ClearTypeLetterGlyph.Item* items = glyph.Items)
        {
            int itemCount = glyph.Items.Length;
            ClearTypeLetterGlyph.Item* currentItem = items;
            //if (x0 >= xmin && y0 >= ymin && x0 + glyph.Width < xmax && y0 + glyph.Height < ymax)
            //{
            //    for (int i = 0; i < itemCount; i++, currentItem++)
            //    {
            //        pixels[(y0 + currentItem->Y) * w + x0 + currentItem->X] = currentItem->Color;
            //    }
            //}
            //else
            //{
            for (int i = 0; i < itemCount; i++, currentItem++)
            {
                int x = x0 + currentItem->X;
                int y = y0 + currentItem->Y;
                int color = currentItem->Color;
                if (x < xmin || y < ymin || x > xmax || y > ymax) continue;

                pixels[y * w + x] = color;
            }
            //}
        }

        //fixed (int *instructions = glyph.Instructions)
        //{
        //    int* current = instructions;
        //    while (*current != -1)
        //    {
        //        int dy = *current++;
        //        int dx = *current++;
        //        int count0 = *current++;

        //        int y = y0 + dy;
        //        if (y >= ymin && y <= ymax)
        //        {
        //            int x = x0 + dx;
        //            int* dst = pixels + y*w + x;
        //            int* src = current;
        //            int count = count0;

        //            if (x < xmin)
        //            {
        //                int dmin = xmin - x;
        //                x += dmin;
        //                dst += dmin;
        //                src += dmin;
        //                count -= dmin;
        //            }

        //            if (x + count - 1 > xmax)
        //            {
        //                int dmax = x + count - 1 - xmax;
        //                count -= dmax;
        //            }

        //            if (count > 0)
        //            {
        //                NativeMethods.memcpy(dst, src, count * 4);

        //                //if (count < 10)
        //                //{
        //                //    while (count > 0)
        //                //    {
        //                //        *dst++ = *src++;
        //                //        count--;
        //                //    }
        //                //}
        //                //else
        //                //{
        //                //    NativeMethods.memcpy(dst, src, count*4);
        //                //}
        //            }
        //        }

        //        current += count0;
        //    }
        //}
    }

    public static int DrawString(this WriteableBitmap bmp, int x0, int y0, Area<int> cliprect, System.Windows.Media.Color fontColor, GlyphFont font, string text)
    {
        return DrawString(bmp, x0, y0, cliprect, fontColor, null, font, text);
    }

    public static int DrawString(this WriteableBitmap bmp, int x0, int y0, Area<int> cliprect, System.Windows.Media.Color fontColor, System.Windows.Media.Color? bgColor, GlyphFont font, string text)
    {
        if (text is null) return 0;
        int dx = 0, dy = 0;
        int textwi = 0;

        using (var context = XWriteableBitmap.GetContext(bmp))
        {
            foreach (char ch in text)
            {
                if (ch == '\n')
                {
                    if (dx > textwi) textwi = dx;
                    dx = 0;
                    dy += font.TextHeight;
                }
                if (x0 + dx <= cliprect.Right())
                {
                    if (font.IsClearType)
                    {
                        if (!bgColor.HasValue)
                            throw new Exception("Clear type fonts must have background specified");
                        var letter = font.GetClearTypeLetter(ch, fontColor, bgColor.Value);
                        if (letter is null) continue;
                        context.DrawLetter(x0 + dx, y0 + dy, cliprect, letter);
                        dx += letter.Width;
                    }
                    else
                    {
                        var letter = font.GetGrayScaleLetter(ch);
                        if (letter is null) continue;
                        context.DrawLetter(x0 + dx, y0 + dy, cliprect, fontColor, letter);
                        dx += letter.Width;
                    }
                }
            }
        }

        if (dx > textwi) textwi = dx;
        return textwi;
    }

    public static int DrawString(this WriteableBitmap bmp, int x0, int y0, Area<int> cliprect, System.Windows.Media.Color fontColor, PortableFont typeface, string text)
    {
        var font = GetFont(typeface);
        return bmp.DrawString(x0, y0, cliprect, fontColor, font, text);
    }

    public static int DrawString(this WriteableBitmap bmp, int x0, int y0, System.Windows.Media.Color fontColor, PortableFont typeface, string text)
    {
        var font = GetFont(typeface);
        return bmp.DrawString(x0, y0, new Area<int>(new Vector2<int>(0), new Size<int>(bmp.PixelHeight, bmp.PixelWidth)), fontColor, font, text);
    }

    public static int DrawString(this WriteableBitmap bmp, int x0, int y0, System.Windows.Media.Color fontColor, System.Windows.Media.Color? bgColor, PortableFont typeface, string text)
    {
        var font = GetFont(typeface);
        return bmp.DrawString(x0, y0, new Area<int>(new Vector2<int>(0), new Size<int>(bmp.PixelHeight, bmp.PixelWidth)), fontColor, bgColor, font, text);
    }

    public static GlyphFont GetFont(PortableFont typeface)
    {
        lock (FontCache)
        {
            if (FontCache.ContainsKey(typeface)) return FontCache[typeface];
        }
        var fontFlags = System.Drawing.FontStyle.Regular;
        if (typeface.IsItalic) fontFlags |= System.Drawing.FontStyle.Italic;
        if (typeface.IsBold) fontFlags |= System.Drawing.FontStyle.Bold;
        var font = new GlyphFont
        {
            Typeface = new Typeface(new System.Windows.Media.FontFamily(typeface.FontName),
                                        typeface.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                                        typeface.IsBold ? FontWeights.Bold : FontWeights.Normal,
                                        FontStretches.Normal),
            EmSize = typeface.EmSize,
            Font = new Font(typeface.FontName, typeface.EmSize * 76.0f / 92.0f, fontFlags),
            IsClearType = typeface.IsClearType,
        };
        font.Typeface.TryGetGlyphTypeface(out font.GlyphTypeface);
        lock (FontCache)
        {
            FontCache[typeface] = font;
        }
        return font;
    }
}

public struct ColorGlyphKey(System.Windows.Media.Color fontColor, System.Windows.Media.Color backgroundColor, char @char) : IEquatable<ColorGlyphKey>
{
    public int FontColor = (fontColor.A << 24) | (fontColor.R << 16) | (fontColor.G << 8) | fontColor.B;
    public int BackgroundColor = (backgroundColor.A << 24) | (backgroundColor.R << 16) | (backgroundColor.G << 8) | backgroundColor.B;
    public char Char = @char;

    public readonly bool Equals(ColorGlyphKey other)
    {
        return FontColor.Equals(other.FontColor) && BackgroundColor.Equals(other.BackgroundColor) && Char == other.Char;
    }

    public override readonly bool Equals(object obj)
    {
        if (obj is null) return false;
        return obj is ColorGlyphKey key && Equals(key);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(FontColor, BackgroundColor, Char);
    }

    public static bool operator ==(ColorGlyphKey left, ColorGlyphKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ColorGlyphKey left, ColorGlyphKey right)
    {
        return !(left == right);
    }
}

public class GlyphFont
{
    public Dictionary<char, GrayScaleLetterGlyph> Glyphs = [];
    public Dictionary<ColorGlyphKey, ClearTypeLetterGlyph> ColorGlyphs = [];
    public Typeface Typeface;
    public double EmSize;
    public GlyphTypeface GlyphTypeface;
    public System.Drawing.Font Font;
    public bool IsClearType;

    public GrayScaleLetterGlyph GetGrayScaleLetter(char ch)
    {
        lock (Glyphs)
        {
            if (!Glyphs.ContainsKey(ch))
            {
                Glyphs[ch] = GrayScaleLetterGlyph.CreateGlyph(Typeface, GlyphTypeface, EmSize, ch);
            }
            return Glyphs[ch];
        }
    }

    public ClearTypeLetterGlyph GetClearTypeLetter(char ch, System.Windows.Media.Color fontColor, System.Windows.Media.Color bgColor)
    {
        lock (ColorGlyphs)
        {
            var key = new ColorGlyphKey(fontColor, bgColor, ch);

            if (!ColorGlyphs.TryGetValue(key, out ClearTypeLetterGlyph glyph))
            {
                glyph = ClearTypeLetterGlyph.CreateGlyph(GlyphTypeface, Font, EmSize, ch, fontColor, bgColor);
                ColorGlyphs[key] = glyph;
            }
            return glyph;
        }
    }

    public int GetTextWidth(string text, int? maxSize = null)
    {
        int maxLineWidth = 0;
        int curLineWidth = 0;
        if (text is null) return 0;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                if (curLineWidth > maxLineWidth) maxLineWidth = curLineWidth;
                curLineWidth = 0;
            }

            if (IsClearType)
            {
                var letter = GetClearTypeLetter(ch, System.Windows.Media.Colors.Black, System.Windows.Media.Colors.White);
                if (letter is null) continue;
                curLineWidth += letter.Width;
            }
            else
            {
                var letter = GetGrayScaleLetter(ch);
                if (letter is null) continue;
                curLineWidth += letter.Width;
            }
            if (maxSize.HasValue && maxLineWidth >= maxSize.Value)
            {
                return maxSize.Value;
            }
            if (maxSize.HasValue && curLineWidth >= maxSize.Value)
            {
                return maxSize.Value;
            }
        }
        if (curLineWidth > maxLineWidth) maxLineWidth = curLineWidth;
        return maxLineWidth;
    }

    public int GetTextHeight(string text)
    {
        if (text is null) return 0;
        int lines = text.Count(x => x == '\n') + 1;
        return lines * TextHeight;
    }

    public int TextHeight
    {
        get { return (int)Math.Ceiling(GlyphTypeface.Height * EmSize * DpiDetector.DpiYKoef); }
    }
}
