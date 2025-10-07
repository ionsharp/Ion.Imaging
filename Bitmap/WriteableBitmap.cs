using Ion.Collect;
using Ion.Colors;
using Ion.Numeral;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Math;

namespace Ion.Imaging;

[Extend<WriteableBitmap>]
public unsafe static class XWriteableBitmap
{
    public const int SizeOfArgb = 4;

    private static ByteVector4 GetColor(Color color)
        => new(color.R, color.G, color.B, color.A);

    #region Antialiasing

    private static readonly int[] leftEdgeX = new int[8192];
    private static readonly int[] rightEdgeX = new int[8192];

    private static void AALineQ1(int width, int height, BitmapContext context, int x1, int y1, int x2, int y2, int color, bool minEdge, bool leftEdge)
    {
        byte off = 0;

        if (minEdge) off = 0xff;

        if (x1 == x2) return;
        if (y1 == y2) return;

        var buffer = context.Pixels;

        if (y1 > y2)
        {
            Swap(ref x1, ref x2);
            Swap(ref y1, ref y2);
        }

        int deltax = (x2 - x1);
        int deltay = (y2 - y1);

        if (x1 > x2) deltax = (x1 - x2);

        int x = x1;
        int y = y1;

        ushort m = 0;

        if (deltax > deltay) m = (ushort)(((deltay << 16) / deltax));
        else m = (ushort)(((deltax << 16) / deltay));

        ushort e = 0;

        var a = (byte)((color & 0xff000000) >> 24);
        var r = (byte)((color & 0x00ff0000) >> 16);
        var g = (byte)((color & 0x0000ff00) >> 8);
        var b = (byte)((color & 0x000000ff) >> 0);

        byte rs, gs, bs;
        byte rd, gd, bd;

        int d;

        byte ta = a;

        e = 0;

        if (deltax >= deltay)
        {
            while (deltax-- != 0)
            {
                if ((ushort)(e + m) <= e) // Roll
                {
                    y++;
                }

                e += m;

                if (x1 < x2) x++;
                else x--;

                if (y < 0 || y >= height) continue;

                if (leftEdge) leftEdgeX[y] = Math.Max(x + 1, leftEdgeX[y]);
                else rightEdgeX[y] = Math.Min(x - 1, rightEdgeX[y]);

                if (x < 0 || x >= width) continue;

                //

                ta = (byte)((a * (ushort)(((((ushort)(e >> 8))) ^ off))) >> 8);

                rs = r;
                gs = g;
                bs = b;

                d = buffer[y * width + x];

                rd = (byte)((d & 0x00ff0000) >> 16);
                gd = (byte)((d & 0x0000ff00) >> 8);
                bd = (byte)((d & 0x000000ff) >> 0);

                rd = (byte)((rs * ta + rd * (0xff - ta)) >> 8);
                gd = (byte)((gs * ta + gd * (0xff - ta)) >> 8);
                bd = (byte)((bs * ta + bd * (0xff - ta)) >> 8);

                buffer[y * width + x] = (0xff << 24) | (rd << 16) | (gd << 8) | (bd << 0);

                //
            }
        }
        else
        {
            off ^= 0xff;

            while (--deltay != 0)
            {
                if ((ushort)(e + m) <= e) // Roll
                {
                    if (x1 < x2) x++;
                    else x--;
                }

                e += m;

                y++;

                if (x < 0 || x >= width) continue;
                if (y < 0 || y >= height) continue;

                //

                ta = (byte)((a * (ushort)(((((ushort)(e >> 8))) ^ off))) >> 8);

                rs = r;
                gs = g;
                bs = b;

                d = buffer[y * width + x];

                rd = (byte)((d & 0x00ff0000) >> 16);
                gd = (byte)((d & 0x0000ff00) >> 8);
                bd = (byte)((d & 0x000000ff) >> 0);

                rd = (byte)((rs * ta + rd * (0xff - ta)) >> 8);
                gd = (byte)((gs * ta + gd * (0xff - ta)) >> 8);
                bd = (byte)((bs * ta + bd * (0xff - ta)) >> 8);

                buffer[y * width + x] = (0xff << 24) | (rd << 16) | (gd << 8) | (bd << 0);

                if (leftEdge) leftEdgeX[y] = x + 1;
                else rightEdgeX[y] = x - 1;
            }
        }
    }

    private static void AAWidthLine(int width, int height, BitmapContext context, float x1, float y1, float x2, float y2, float lineWidth, int color, Rect? clipRect = null)
    {
        // Perform cohen-sutherland clipping if either point is out of the viewport
        if (!CohenSutherlandLineClip(clipRect ?? new Rect(0, 0, width, height), ref x1, ref y1, ref x2, ref y2)) return;

        if (lineWidth <= 0) return;

        var buffer = context.Pixels;

        if (y1 > y2)
        {
            Swap(ref x1, ref x2);
            Swap(ref y1, ref y2);
        }

        if (x1 == x2)
        {
            x1 -= (int)lineWidth / 2;
            x2 += (int)lineWidth / 2;

            if (x1 < 0)
                x1 = 0;
            if (x2 < 0)
                return;

            if (x1 >= width)
                return;
            if (x2 >= width)
                x2 = width - 1;

            if (y1 >= height || y2 < 0)
                return;

            if (y1 < 0)
                y1 = 0;
            if (y2 >= height)
                y2 = height - 1;

            for (var x = (int)x1; x <= x2; x++)
            {
                for (var y = (int)y1; y <= y2; y++)
                {
                    var a = (byte)((color & 0xff000000) >> 24);
                    var r = (byte)((color & 0x00ff0000) >> 16);
                    var g = (byte)((color & 0x0000ff00) >> 8);
                    var b = (byte)((color & 0x000000ff) >> 0);

                    byte rs, gs, bs;
                    byte rd, gd, bd;

                    int d;

                    rs = r;
                    gs = g;
                    bs = b;

                    d = buffer[y * width + x];

                    rd = (byte)((d & 0x00ff0000) >> 16);
                    gd = (byte)((d & 0x0000ff00) >> 8);
                    bd = (byte)((d & 0x000000ff) >> 0);

                    rd = (byte)((rs * a + rd * (0xff - a)) >> 8);
                    gd = (byte)((gs * a + gd * (0xff - a)) >> 8);
                    bd = (byte)((bs * a + bd * (0xff - a)) >> 8);

                    buffer[y * width + x] = (0xff << 24) | (rd << 16) | (gd << 8) | (bd << 0);
                }
            }

            return;
        }
        if (y1 == y2)
        {
            if (x1 > x2) Swap(ref x1, ref x2);

            y1 -= (int)lineWidth / 2;
            y2 += (int)lineWidth / 2;

            if (y1 < 0) y1 = 0;
            if (y2 < 0) return;

            if (y1 >= height) return;
            if (y2 >= height) y2 = height - 1;

            if (x1 >= width || y2 < 0) return;

            if (x1 < 0) x1 = 0;
            if (x2 >= width) x2 = width - 1;

            for (var x = (int)x1; x <= x2; x++)
            {
                for (var y = (int)y1; y <= y2; y++)
                {
                    var a = (byte)((color & 0xff000000) >> 24);
                    var r = (byte)((color & 0x00ff0000) >> 16);
                    var g = (byte)((color & 0x0000ff00) >> 8);
                    var b = (byte)((color & 0x000000ff) >> 0);

                    byte rs, gs, bs;
                    byte rd, gd, bd;

                    int d;

                    rs = r;
                    gs = g;
                    bs = b;

                    d = buffer[y * width + x];

                    rd = (byte)((d & 0x00ff0000) >> 16);
                    gd = (byte)((d & 0x0000ff00) >> 8);
                    bd = (byte)((d & 0x000000ff) >> 0);

                    rd = (byte)((rs * a + rd * (0xff - a)) >> 8);
                    gd = (byte)((gs * a + gd * (0xff - a)) >> 8);
                    bd = (byte)((bs * a + bd * (0xff - a)) >> 8);

                    buffer[y * width + x] = (0xff << 24) | (rd << 16) | (gd << 8) | (bd << 0);
                }
            }

            return;
        }

        y1 += 1;
        y2 += 1;

        float slope = (y2 - y1) / (x2 - x1);
        float islope = (x2 - x1) / (y2 - y1);

        float m = slope;
        float w = lineWidth;

        float dx = x2 - x1;
        float dy = y2 - y1;

        var xtot = (float)(w * dy / Math.Sqrt(dx * dx + dy * dy));
        var ytot = (float)(w * dx / Math.Sqrt(dx * dx + dy * dy));

        float sm = dx * dy / (dx * dx + dy * dy);

        // Center it.

        x1 += xtot / 2;
        y1 -= ytot / 2;
        x2 += xtot / 2;
        y2 -= ytot / 2;

        //
        //

        float sx = -xtot;
        float sy = +ytot;

        var ix1 = (int)x1;
        var iy1 = (int)y1;

        var ix2 = (int)x2;
        var iy2 = (int)y2;

        var ix3 = (int)(x1 + sx);
        var iy3 = (int)(y1 + sy);

        var ix4 = (int)(x2 + sx);
        var iy4 = (int)(y2 + sy);

        if (ix1 == ix2)
        {
            ix2++;
        }
        if (ix3 == ix4)
        {
            ix4++;
        }

        if (lineWidth == 2)
        {
            if (Math.Abs(dy) < Math.Abs(dx))
            {
                if (x1 < x2)
                {
                    iy3 = iy1 + 2;
                    iy4 = iy2 + 2;
                }
                else
                {
                    iy1 = iy3 + 2;
                    iy2 = iy4 + 2;
                }
            }
            else
            {
                ix1 = ix3 + 2;
                ix2 = ix4 + 2;
            }
        }

        int starty = Math.Min(Math.Min(iy1, iy2), Math.Min(iy3, iy4));
        int endy = Math.Max(Math.Max(iy1, iy2), Math.Max(iy3, iy4));

        if (starty < 0) starty = -1;
        if (endy >= height) endy = height + 1;

        for (int y = starty + 1; y < endy - 1; y++)
        {
            leftEdgeX[y] = -1 << 16;
            rightEdgeX[y] = 1 << 16 - 1;
        }


        AALineQ1(width, height, context, ix1, iy1, ix2, iy2, color, sy > 0, false);
        AALineQ1(width, height, context, ix3, iy3, ix4, iy4, color, sy < 0, true);

        if (lineWidth > 1)
        {
            AALineQ1(width, height, context, ix1, iy1, ix3, iy3, color, true, sy > 0);
            AALineQ1(width, height, context, ix2, iy2, ix4, iy4, color, false, sy < 0);
        }

        if (x1 < x2)
        {
            if (iy2 >= 0 && iy2 < height) rightEdgeX[iy2] = Math.Min(ix2, rightEdgeX[iy2]);
            if (iy3 >= 0 && iy3 < height) leftEdgeX[iy3] = Math.Max(ix3, leftEdgeX[iy3]);
        }
        else
        {
            if (iy1 >= 0 && iy1 < height) rightEdgeX[iy1] = Math.Min(ix1, rightEdgeX[iy1]);
            if (iy4 >= 0 && iy4 < height) leftEdgeX[iy4] = Math.Max(ix4, leftEdgeX[iy4]);
        }

        //return;

        for (int y = starty + 1; y < endy - 1; y++)
        {
            leftEdgeX[y] = Math.Max(leftEdgeX[y], 0);
            rightEdgeX[y] = Math.Min(rightEdgeX[y], width - 1);

            for (int x = leftEdgeX[y]; x <= rightEdgeX[y]; x++)
            {
                var a = (byte)((color & 0xff000000) >> 24);
                var r = (byte)((color & 0x00ff0000) >> 16);
                var g = (byte)((color & 0x0000ff00) >> 8);
                var b = (byte)((color & 0x000000ff) >> 0);

                byte rs, gs, bs;
                byte rd, gd, bd;

                int d;

                rs = r;
                gs = g;
                bs = b;

                d = buffer[y * width + x];

                rd = (byte)((d & 0x00ff0000) >> 16);
                gd = (byte)((d & 0x0000ff00) >> 8);
                bd = (byte)((d & 0x000000ff) >> 0);

                rd = (byte)((rs * a + rd * (0xff - a)) >> 8);
                gd = (byte)((gs * a + gd * (0xff - a)) >> 8);
                bd = (byte)((bs * a + bd * (0xff - a)) >> 8);

                buffer[y * width + x] = (0xff << 24) | (rd << 16) | (gd << 8) | (bd << 0);
            }
        }
    }

    private static void Swap<T>(ref T a, ref T b)
    {
        (b, a) = (a, b);
    }

    #endregion

    #region Blit

    private const int WhiteR = 255, WhiteG = 255, WhiteB = 255;

    public enum BlendMode
    {
        /// <summary>
        /// Alpha blending uses the alpha channel to combine the source and destination. 
        /// </summary>
        Alpha,
        /// <summary>
        /// Additive blending adds the colors of the source and the destination.
        /// </summary>
        Additive,
        /// <summary>
        /// Subtractive blending subtracts the source color from the destination.
        /// </summary>
        Subtractive,
        /// <summary>
        /// Uses the source color as a mask.
        /// </summary>
        Mask,
        /// <summary>
        /// Multiplies the source color with the destination color.
        /// </summary>
        Multiply,
        /// <summary>
        /// Ignores the specified Color
        /// </summary>
        ColorKeying,
        /// <summary>
        /// No blending just copies the pixels from the source.
        /// </summary>
        None
    }

    /// <summary>
    /// Copies (blits) the pixels from the WriteableBitmap source to the destination WriteableBitmap (this).
    /// </summary>
    /// <param name="bmp">The destination WriteableBitmap.</param>
    /// <param name="destRect">The rectangle that defines the destination region.</param>
    /// <param name="source">The source WriteableBitmap.</param>
    /// <param name="sourceRect">The rectangle that will be copied from the source to the destination.</param>
    /// <param name="blendMode">The blending mode <see cref="BlendMode"/>.</param>
    public static void Blit(this WriteableBitmap bmp, Rect destRect, WriteableBitmap source, Rect sourceRect, BlendMode blendMode)
    {
        Blit(bmp, destRect, source, sourceRect, System.Windows.Media.Colors.White, blendMode);
    }

    /// <summary>
    /// Copies (blits) the pixels from the WriteableBitmap source to the destination WriteableBitmap (this).
    /// </summary>
    /// <param name="bmp">The destination WriteableBitmap.</param>
    /// <param name="destRect">The rectangle that defines the destination region.</param>
    /// <param name="source">The source WriteableBitmap.</param>
    /// <param name="sourceRect">The rectangle that will be copied from the source to the destination.</param>
    public static void Blit(this WriteableBitmap bmp, Rect destRect, WriteableBitmap source, Rect sourceRect)
    {
        Blit(bmp, destRect, source, sourceRect, System.Windows.Media.Colors.White, BlendMode.Alpha);
    }

    /// <summary>
    /// Copies (blits) the pixels from the WriteableBitmap source to the destination WriteableBitmap (this).
    /// </summary>
    /// <param name="bmp">The destination WriteableBitmap.</param>
    /// <param name="destPosition">The destination position in the destination bitmap.</param>
    /// <param name="source">The source WriteableBitmap.</param>
    /// <param name="sourceRect">The rectangle that will be copied from the source to the destination.</param>
    /// <param name="color">If not Colors.White, will tint the source image. A partially transparent color and the image will be drawn partially transparent.</param>
    /// <param name="blendMode">The blending mode <see cref="BlendMode"/>.</param>
    public static void Blit(this WriteableBitmap bmp, Point destPosition, WriteableBitmap source, Rect sourceRect, Color color, BlendMode blendMode)
    {
        var destRect = new Rect(destPosition, new Size(sourceRect.Width, sourceRect.Height));
        Blit(bmp, destRect, source, sourceRect, color, blendMode);
    }

    /// <summary>
    /// Copies (blits) the pixels from the WriteableBitmap source to the destination WriteableBitmap (this).
    /// </summary>
    /// <param name="bmp">The destination WriteableBitmap.</param>
    /// <param name="destRect">The rectangle that defines the destination region.</param>
    /// <param name="source">The source WriteableBitmap.</param>
    /// <param name="sourceRect">The rectangle that will be copied from the source to the destination.</param>
    /// <param name="color">If not Colors.White, will tint the source image. A partially transparent color and the image will be drawn partially transparent. If the BlendMode is ColorKeying, this color will be used as color key to mask all pixels with this value out.</param>
    /// <param name="blendMode">The blending mode <see cref="BlendMode"/>.</param>
    internal static void Blit(this WriteableBitmap bmp, Rect destRect, WriteableBitmap source, Rect sourceRect, Color color, BlendMode blendMode)
    {
        if (color.A == 0)
        {
            return;
        }
        var dw = (int)destRect.Width;
        var dh = (int)destRect.Height;

        using var srcContext = source.GetContext(Imaging.ReadWriteMode.ReadOnly);
#if WPF
            var isPrgba = srcContext.Format == PixelFormats.Pbgra32 || srcContext.Format == PixelFormats.Prgba64 || srcContext.Format == PixelFormats.Prgba128Float;
#endif
        using var destContext = bmp.GetContext();
        var sourceWidth = srcContext.Width;
        var dpw = destContext.Width;
        var dph = destContext.Height;

        var intersect = new Rect(0, 0, dpw, dph);
        intersect.Intersect(destRect);
        if (intersect.IsEmpty)
        {
            return;
        }

        var sourcePixels = srcContext.Pixels;
        var destPixels = destContext.Pixels;
        var sourceLength = srcContext.Length;

        int sourceIdx = -1;
        int px = (int)destRect.X;
        int py = (int)destRect.Y;

        int x;
        int y;
        int idx;
        double ii;
        double jj;
        int sr = 0;
        int sg = 0;
        int sb = 0;
        int dr, dg, db;
        int sourcePixel;
        int sa = 0;
        int da;
        int ca = color.A;
        int cr = color.R;
        int cg = color.G;
        int cb = color.B;
        bool tinted = color != System.Windows.Media.Colors.White;
        var sw = (int)sourceRect.Width;
        var sdx = sourceRect.Width / destRect.Width;
        var sdy = sourceRect.Height / destRect.Height;
        int sourceStartX = (int)sourceRect.X;
        int sourceStartY = (int)sourceRect.Y;
        int lastii, lastjj;
        lastii = -1;
        lastjj = -1;
        jj = sourceStartY;
        y = py;
        for (int j = 0; j < dh; j++)
        {
            if (y >= 0 && y < dph)
            {
                ii = sourceStartX;
                idx = px + y * dpw;
                x = px;
                sourcePixel = sourcePixels[0];

                // Scanline BlockCopy is much faster (3.5x) if no tinting and blending is needed,
                // even for smaller sprites like the 32x32 particles. 
                if (blendMode == BlendMode.None && !tinted)
                {
                    sourceIdx = (int)ii + (int)jj * sourceWidth;
                    var offset = x < 0 ? -x : 0;
                    var xx = x + offset;
                    var wx = sourceWidth - offset;
                    var len = xx + wx < dpw ? wx : dpw - xx;
                    if (len > sw) len = sw;
                    if (len > dw) len = dw;
                    BitmapContext.BlockCopy(srcContext, (sourceIdx + offset) * 4, destContext, (idx + offset) * 4, len * 4);
                }

                // Pixel by pixel copying
                else
                {
                    for (int i = 0; i < dw; i++)
                    {
                        if (x >= 0 && x < dpw)
                        {
                            if ((int)ii != lastii || (int)jj != lastjj)
                            {
                                sourceIdx = (int)ii + (int)jj * sourceWidth;
                                if (sourceIdx >= 0 && sourceIdx < sourceLength)
                                {
                                    sourcePixel = sourcePixels[sourceIdx];
                                    sa = ((sourcePixel >> 24) & 0xff);
                                    sr = ((sourcePixel >> 16) & 0xff);
                                    sg = ((sourcePixel >> 8) & 0xff);
                                    sb = ((sourcePixel) & 0xff);
                                    if (tinted && sa != 0)
                                    {
                                        sa = (((sa * ca) * 0x8081) >> 23);
                                        sr = ((((((sr * cr) * 0x8081) >> 23) * ca) * 0x8081) >> 23);
                                        sg = ((((((sg * cg) * 0x8081) >> 23) * ca) * 0x8081) >> 23);
                                        sb = ((((((sb * cb) * 0x8081) >> 23) * ca) * 0x8081) >> 23);
                                        sourcePixel = (sa << 24) | (sr << 16) | (sg << 8) | sb;
                                    }
                                }
                                else
                                {
                                    sa = 0;
                                }
                            }
                            if (blendMode == BlendMode.None)
                            {
                                destPixels[idx] = sourcePixel;
                            }
                            else if (blendMode == BlendMode.ColorKeying)
                            {
                                sr = ((sourcePixel >> 16) & 0xff);
                                sg = ((sourcePixel >> 8) & 0xff);
                                sb = ((sourcePixel) & 0xff);

                                if (sr != color.R || sg != color.G || sb != color.B)
                                {
                                    destPixels[idx] = sourcePixel;
                                }

                            }
                            else if (blendMode == BlendMode.Mask)
                            {
                                int destPixel = destPixels[idx];
                                da = ((destPixel >> 24) & 0xff);
                                dr = ((destPixel >> 16) & 0xff);
                                dg = ((destPixel >> 8) & 0xff);
                                db = ((destPixel) & 0xff);
                                destPixel = ((((da * sa) * 0x8081) >> 23) << 24) |
                                            ((((dr * sa) * 0x8081) >> 23) << 16) |
                                            ((((dg * sa) * 0x8081) >> 23) << 8) |
                                            ((((db * sa) * 0x8081) >> 23));
                                destPixels[idx] = destPixel;
                            }
                            else if (sa > 0)
                            {
                                int destPixel = destPixels[idx];
                                da = ((destPixel >> 24) & 0xff);
                                if ((sa == 255 || da == 0) &&
                                               blendMode != BlendMode.Additive
                                               && blendMode != BlendMode.Subtractive
                                               && blendMode != BlendMode.Multiply
                                   )
                                {
                                    destPixels[idx] = sourcePixel;
                                }
                                else
                                {
                                    dr = ((destPixel >> 16) & 0xff);
                                    dg = ((destPixel >> 8) & 0xff);
                                    db = ((destPixel) & 0xff);
                                    if (blendMode == BlendMode.Alpha)
                                    {
                                        var isa = 255 - sa;
#if NETFX_CORE
                                                 // Special case for WinRT since it does not use pARGB (pre-multiplied alpha)
                                                 destPixel = ((da & 0xff) << 24) |
                                                             ((((sr * sa + isa * dr) >> 8) & 0xff) << 16) |
                                                             ((((sg * sa + isa * dg) >> 8) & 0xff) <<  8) |
                                                              (((sb * sa + isa * db) >> 8) & 0xff);
#elif WPF
                                                if (isPrgba)
                                                {
                                                    destPixel = ((da & 0xff) << 24) |
                                                                (((((sr << 8) + isa * dr) >> 8) & 0xff) << 16) |
                                                                (((((sg << 8) + isa * dg) >> 8) & 0xff) <<  8) |
                                                                 ((((sb << 8) + isa * db) >> 8) & 0xff);
                                                }
                                                else
                                                {
                                                    destPixel = ((da & 0xff) << 24) |
                                                                (((((sr * sa) + isa * dr) >> 8) & 0xff) << 16) |
                                                                (((((sg * sa) + isa * dg) >> 8) & 0xff) <<  8) |
                                                                 ((((sb * sa) + isa * db) >> 8) & 0xff);
                                                }
#else
                                        destPixel = ((da & 0xff) << 24) |
                                                    (((((sr << 8) + isa * dr) >> 8) & 0xff) << 16) |
                                                    (((((sg << 8) + isa * dg) >> 8) & 0xff) << 8) |
                                                     ((((sb << 8) + isa * db) >> 8) & 0xff);
#endif
                                    }
                                    else if (blendMode == BlendMode.Additive)
                                    {
                                        int a = (255 <= sa + da) ? 255 : (sa + da);
                                        destPixel = (a << 24) |
                                           (((a <= sr + dr) ? a : (sr + dr)) << 16) |
                                           (((a <= sg + dg) ? a : (sg + dg)) << 8) |
                                           (((a <= sb + db) ? a : (sb + db)));
                                    }
                                    else if (blendMode == BlendMode.Subtractive)
                                    {
                                        int a = da;
                                        destPixel = (a << 24) |
                                           (((sr >= dr) ? 0 : (sr - dr)) << 16) |
                                           (((sg >= dg) ? 0 : (sg - dg)) << 8) |
                                           (((sb >= db) ? 0 : (sb - db)));
                                    }
                                    else if (blendMode == BlendMode.Multiply)
                                    {
                                        // Faster than a division like (s * d) / 255 are 2 shifts and 2 adds
                                        int ta = (sa * da) + 128;
                                        int tr = (sr * dr) + 128;
                                        int tg = (sg * dg) + 128;
                                        int tb = (sb * db) + 128;

                                        int ba = ((ta >> 8) + ta) >> 8;
                                        int br = ((tr >> 8) + tr) >> 8;
                                        int bg = ((tg >> 8) + tg) >> 8;
                                        int bb = ((tb >> 8) + tb) >> 8;

                                        destPixel = (ba << 24) |
                                                    ((ba <= br ? ba : br) << 16) |
                                                    ((ba <= bg ? ba : bg) << 8) |
                                                    ((ba <= bb ? ba : bb));
                                    }

                                    destPixels[idx] = destPixel;
                                }
                            }
                        }
                        x++;
                        idx++;
                        ii += sdx;
                    }
                }
            }
            jj += sdy;
            y++;
        }
    }

    public static void Blit(BitmapContext destContext, int dpw, int dph, Rect destRect, BitmapContext srcContext, Rect sourceRect, int sourceWidth)
    {
        int dw = (int)destRect.Width;
        int dh = (int)destRect.Height;

        Rect intersect = new(0, 0, dpw, dph);
        intersect.Intersect(destRect);
        if (intersect.IsEmpty)
        {
            return;
        }
#if WPF
        var isPrgba = srcContext.Format == PixelFormats.Pbgra32 || srcContext.Format == PixelFormats.Prgba64 || srcContext.Format == PixelFormats.Prgba128Float;
#endif

        var sourcePixels = srcContext.Pixels;
        var destPixels = destContext.Pixels;
        int sourceLength = srcContext.Length;
        int sourceIdx = -1;
        int px = (int)destRect.X;
        int py = (int)destRect.Y;
        int x;
        int y;
        int idx;
        double ii;
        double jj;
        int sr = 0;
        int sg = 0;
        int sb = 0;
        int dr, dg, db;
        int sourcePixel;
        int sa = 0;
        int da;

        var sw = (int)sourceRect.Width;
        var sdx = sourceRect.Width / destRect.Width;
        var sdy = sourceRect.Height / destRect.Height;
        int sourceStartX = (int)sourceRect.X;
        int sourceStartY = (int)sourceRect.Y;
        int lastii, lastjj;
        lastii = -1;
        lastjj = -1;
        jj = sourceStartY;
        y = py;
        for (var j = 0; j < dh; j++)
        {
            if (y >= 0 && y < dph)
            {
                ii = sourceStartX;
                idx = px + y * dpw;
                x = px;
                sourcePixel = sourcePixels[0];

                // Pixel by pixel copying
                for (var i = 0; i < dw; i++)
                {
                    if (x >= 0 && x < dpw)
                    {
                        if ((int)ii != lastii || (int)jj != lastjj)
                        {
                            sourceIdx = (int)ii + (int)jj * sourceWidth;
                            if (sourceIdx >= 0 && sourceIdx < sourceLength)
                            {
                                sourcePixel = sourcePixels[sourceIdx];
                                sa = ((sourcePixel >> 24) & 0xff);
                                sr = ((sourcePixel >> 16) & 0xff);
                                sg = ((sourcePixel >> 8) & 0xff);
                                sb = ((sourcePixel) & 0xff);
                            }
                            else
                            {
                                sa = 0;
                            }
                        }

                        if (sa > 0)
                        {
                            int destPixel = destPixels[idx];
                            da = ((destPixel >> 24) & 0xff);
                            if ((sa == 255 || da == 0))
                            {
                                destPixels[idx] = sourcePixel;
                            }
                            else
                            {
                                dr = ((destPixel >> 16) & 0xff);
                                dg = ((destPixel >> 8) & 0xff);
                                db = ((destPixel) & 0xff);
                                var isa = 255 - sa;
#if NETFX_CORE
                                // Special case for WinRT since it does not use pARGB (pre-multiplied alpha)
                                destPixel = ((da & 0xff) << 24) |
                                            ((((sr * sa + isa * dr) >> 8) & 0xff) << 16) |
                                            ((((sg * sa + isa * dg) >> 8) & 0xff) <<  8) |
                                            (((sb * sa + isa * db) >> 8) & 0xff);
#elif WPF
                                if (isPrgba)
                                {
                                    destPixel = ((da & 0xff) << 24) |
                                                (((((sr << 8) + isa * dr) >> 8) & 0xff) << 16) |
                                                (((((sg << 8) + isa * dg) >> 8) & 0xff) << 8) |
                                                 ((((sb << 8) + isa * db) >> 8) & 0xff);
                                }
                                else
                                {
                                    destPixel = ((da & 0xff) << 24) |
                                                (((((sr * sa) + isa * dr) >> 8) & 0xff) << 16) |
                                                (((((sg * sa) + isa * dg) >> 8) & 0xff) << 8) |
                                                 ((((sb * sa) + isa * db) >> 8) & 0xff);
                                }
#else
                                destPixel = ((da & 0xff) << 24) |
                                            (((((sr << 8) + isa * dr) >> 8) & 0xff) << 16) |
                                            (((((sg << 8) + isa * dg) >> 8) & 0xff) << 8) |
                                             ((((sb << 8) + isa * db) >> 8) & 0xff);
#endif
                                destPixels[idx] = destPixel;
                            }
                        }
                    }
                    x++;
                    idx++;
                    ii += sdx;
                }
            }
            jj += sdy;
            y++;
        }

    }

    /// <summary>
    /// Renders a bitmap using any affine transformation and transparency into this bitmap
    /// Unlike Silverlight's Render() method, this one uses 2-3 times less memory, and is the same or better quality
    /// The algorithm is simple dx/dy (bresenham-like) step by step painting, optimized with fixed point and fast bilinear filtering
    /// It's used in Fantasia Painter for drawing stickers and 3D objects on screen
    /// </summary>
    /// <param name="bmp">Destination bitmap.</param>
    /// <param name="source">The source WriteableBitmap.</param>
    /// <param name="shouldClear">If true, the the destination bitmap will be set to all clear (0) before rendering.</param>
    /// <param name="opacity">opacity of the source bitmap to render, between 0 and 1 inclusive</param>
    /// <param name="transform">Transformation to apply</param>
    public static void BlitRender(this WriteableBitmap bmp, WriteableBitmap source, bool shouldClear = true, float opacity = 1f, GeneralTransform transform = null)
    {
        const int PRECISION_SHIFT = 10;
        const int PRECISION_VALUE = (1 << PRECISION_SHIFT);
        const int PRECISION_MASK = PRECISION_VALUE - 1;

        using BitmapContext destContext = bmp.GetContext();
        transform ??= new MatrixTransform();

        var destPixels = destContext.Pixels;
        int destWidth = destContext.Width;
        int destHeight = destContext.Height;
        var inverse = transform.Inverse;
        if (shouldClear) destContext.Clear();

        using BitmapContext sourceContext = source.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var sourcePixels = sourceContext.Pixels;
        int sourceWidth = sourceContext.Width;
        int sourceHeight = sourceContext.Height;

        Rect sourceRect = new(0, 0, sourceWidth, sourceHeight);
        Rect destRect = new(0, 0, destWidth, destHeight);
        Rect bounds = transform.TransformBounds(sourceRect);
        bounds.Intersect(destRect);

        int startX = (int)bounds.Left;
        int startY = (int)bounds.Top;
        int endX = (int)bounds.Right;
        int endY = (int)bounds.Bottom;

#if NETFX_CORE
                Point zeroZero = inverse.TransformPoint(new Point(startX, startY));
                Point oneZero = inverse.TransformPoint(new Point(startX + 1, startY));
                Point zeroOne = inverse.TransformPoint(new Point(startX, startY + 1));
#else
        Point zeroZero = inverse.Transform(new Point(startX, startY));
        Point oneZero = inverse.Transform(new Point(startX + 1, startY));
        Point zeroOne = inverse.Transform(new Point(startX, startY + 1));
#endif
        float sourceXf = ((float)zeroZero.X);
        float sourceYf = ((float)zeroZero.Y);
        int dxDx = (int)((((float)oneZero.X) - sourceXf) * PRECISION_VALUE); // for 1 unit in X coord, how much does X change in source texture?
        int dxDy = (int)((((float)oneZero.Y) - sourceYf) * PRECISION_VALUE); // for 1 unit in X coord, how much does Y change in source texture?
        int dyDx = (int)((((float)zeroOne.X) - sourceXf) * PRECISION_VALUE); // for 1 unit in Y coord, how much does X change in source texture?
        int dyDy = (int)((((float)zeroOne.Y) - sourceYf) * PRECISION_VALUE); // for 1 unit in Y coord, how much does Y change in source texture?

        int sourceX = (int)(((float)zeroZero.X) * PRECISION_VALUE);
        int sourceY = (int)(((float)zeroZero.Y) * PRECISION_VALUE);
        int sourceWidthFixed = sourceWidth << PRECISION_SHIFT;
        int sourceHeightFixed = sourceHeight << PRECISION_SHIFT;

        int opacityInt = (int)(opacity * 255);

        int index = 0;
        for (int destY = startY; destY < endY; destY++)
        {
            index = destY * destWidth + startX;
            int savedSourceX = sourceX;
            int savedSourceY = sourceY;

            for (int destX = startX; destX < endX; destX++)
            {
                if ((sourceX >= 0) && (sourceX < sourceWidthFixed) && (sourceY >= 0) && (sourceY < sourceHeightFixed))
                {
                    // bilinear filtering
                    int xFloor = sourceX >> PRECISION_SHIFT;
                    int yFloor = sourceY >> PRECISION_SHIFT;

                    if (xFloor < 0) xFloor = 0;
                    if (yFloor < 0) yFloor = 0;

                    int xCeil = xFloor + 1;
                    int yCeil = yFloor + 1;

                    if (xCeil >= sourceWidth)
                    {
                        xFloor = sourceWidth - 1;
                        xCeil = 0;
                    }
                    else
                    {
                        xCeil = 1;
                    }

                    if (yCeil >= sourceHeight)
                    {
                        yFloor = sourceHeight - 1;
                        yCeil = 0;
                    }
                    else
                    {
                        yCeil = sourceWidth;
                    }

                    int i1 = yFloor * sourceWidth + xFloor;
                    int p1 = sourcePixels[i1];
                    int p2 = sourcePixels[i1 + xCeil];
                    int p3 = sourcePixels[i1 + yCeil];
                    int p4 = sourcePixels[i1 + yCeil + xCeil];

                    int xFrac = sourceX & PRECISION_MASK;
                    int yFrac = sourceY & PRECISION_MASK;

                    // alpha
                    byte a1 = (byte)(p1 >> 24);
                    byte a2 = (byte)(p2 >> 24);
                    byte a3 = (byte)(p3 >> 24);
                    byte a4 = (byte)(p4 >> 24);

                    int comp1, comp2;
                    byte a;

                    if ((a1 == a2) && (a1 == a3) && (a1 == a4))
                    {
                        if (a1 == 0)
                        {
                            destPixels[index] = 0;

                            sourceX += dxDx;
                            sourceY += dxDy;
                            index++;
                            continue;
                        }

                        a = a1;
                    }
                    else
                    {
                        comp1 = a1 + ((xFrac * (a2 - a1)) >> PRECISION_SHIFT);
                        comp2 = a3 + ((xFrac * (a4 - a3)) >> PRECISION_SHIFT);
                        a = (byte)(comp1 + ((yFrac * (comp2 - comp1)) >> PRECISION_SHIFT));
                    }

                    // red
                    comp1 = ((byte)(p1 >> 16)) + ((xFrac * (((byte)(p2 >> 16)) - ((byte)(p1 >> 16)))) >> PRECISION_SHIFT);
                    comp2 = ((byte)(p3 >> 16)) + ((xFrac * (((byte)(p4 >> 16)) - ((byte)(p3 >> 16)))) >> PRECISION_SHIFT);
                    byte r = (byte)(comp1 + ((yFrac * (comp2 - comp1)) >> PRECISION_SHIFT));

                    // green
                    comp1 = ((byte)(p1 >> 8)) + ((xFrac * (((byte)(p2 >> 8)) - ((byte)(p1 >> 8)))) >> PRECISION_SHIFT);
                    comp2 = ((byte)(p3 >> 8)) + ((xFrac * (((byte)(p4 >> 8)) - ((byte)(p3 >> 8)))) >> PRECISION_SHIFT);
                    byte g = (byte)(comp1 + ((yFrac * (comp2 - comp1)) >> PRECISION_SHIFT));

                    // blue
                    comp1 = ((byte)p1) + ((xFrac * (((byte)p2) - ((byte)p1))) >> PRECISION_SHIFT);
                    comp2 = ((byte)p3) + ((xFrac * (((byte)p4) - ((byte)p3))) >> PRECISION_SHIFT);
                    byte b = (byte)(comp1 + ((yFrac * (comp2 - comp1)) >> PRECISION_SHIFT));

                    // save updated pixel
                    if (opacityInt != 255)
                    {
                        a = (byte)((a * opacityInt) >> 8);
                        r = (byte)((r * opacityInt) >> 8);
                        g = (byte)((g * opacityInt) >> 8);
                        b = (byte)((b * opacityInt) >> 8);
                    }
                    destPixels[index] = (a << 24) | (r << 16) | (g << 8) | b;
                }

                sourceX += dxDx;
                sourceY += dxDy;
                index++;
            }

            sourceX = savedSourceX + dyDx;
            sourceY = savedSourceY + dyDy;
        }
    }

    #endregion

    #region Clone

    public static WriteableBitmap Clone(this WriteableBitmap i)
    {
        using var source = i.GetContext(ReadWriteMode.ReadOnly);
        var result = XWriteableBitmap.New(source.Width, source.Height);
        using (var destination = result.GetContext())
        {
            BitmapContext.BlockCopy(source, 0, destination, 0, source.Length * SizeOfArgb);
        }
        return result;
    }

    #endregion

    #region Convert

    public static WriteableBitmap Convert(System.Drawing.Bitmap i)
    {
        var result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(i.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        return new(result);
    }

    public static WriteableBitmap Convert(BitmapSource i)
    {
        // Convert to Pbgra32 if it's a different format
        if (i.Format == PixelFormats.Pbgra32)
            return new WriteableBitmap(i);

        var formatedBitmapSource = new FormatConvertedBitmap();
        formatedBitmapSource.BeginInit();
        formatedBitmapSource.Source = i;
        formatedBitmapSource.DestinationFormat = PixelFormats.Pbgra32;
        formatedBitmapSource.EndInit();
        return new WriteableBitmap(formatedBitmapSource);
    }

    public static void Convert(this WriteableBitmap i, out ColorMatrix4 colors)
    {
        colors = default;
        if (i != null)
        {
            var result = new ByteVector4[i.PixelHeight][];
            for (var y = 0; y < i.PixelHeight; y++)
            {
                result[y] = new ByteVector4[i.PixelWidth];
                for (var x = 0; x < i.PixelWidth; x++)
                {
                    var color = i.GetPixel(x, y);
                    result[y][x] = new ByteVector4(color.R, color.G, color.B, color.A);
                }
            }
            colors = new ColorMatrix4(result);
        }
    }

    public static void Convert(this ColorMatrix4 i, out WriteableBitmap bitmap)
    {
        bitmap = null;
        if (i != null)
        {
            var result = New(i.Columns, i.Rows);
            i.ForEach((y, x, color) => result.SetPixel(x, y, Color.FromArgb(color.W, color.R, color.G, color.B)));
            bitmap = result;
        }
    }

    public static WriteableBitmap Convert(Stream stream)
    {
        var bmpi = new BitmapImage();
        bmpi.BeginInit();
        bmpi.CreateOptions = BitmapCreateOptions.None;
        bmpi.StreamSource = stream;
        bmpi.EndInit();
        var bmp = new WriteableBitmap(bmpi);
        bmpi.UriSource = null;
        return bmp;
    }

    public static WriteableBitmap Convert(string relativePath)
    {
        using var stream = Application.GetResourceStream(new Uri(relativePath, UriKind.Relative)).Stream;
        return Convert(stream);
    }

    #endregion

    #region Fill

    #region General

    /// <summary>
    /// Fills the whole WriteableBitmap with a color.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="color">The color used for filling.</param>
    public static void Clear(this WriteableBitmap bmp, Color color)
    {
        var col = GetColor(color).Encode();
        using var context = bmp.GetContext();
        var pixels = context.Pixels;
        var w = context.Width;
        var h = context.Height;
        var len = w * XWriteableBitmap.SizeOfArgb;

        // Fill first line
        for (var x = 0; x < w; x++)
        {
            pixels[x] = col;
        }

        // Copy first line
        var blockHeight = 1;
        var y = 1;
        while (y < h)
        {
            BitmapContext.BlockCopy(context, 0, context, y * len, blockHeight * len);
            y += blockHeight;
            blockHeight = Math.Min(2 * blockHeight, h - y);
        }
    }

    /// <summary>
    /// Fills the whole WriteableBitmap with an empty color (0).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    public static void Clear(this WriteableBitmap bmp)
    {
        using var context = bmp.GetContext();
        context.Clear();
    }

    #endregion

    #region Brightness

    /// <summary>
    /// Gets the brightness / luminance of the pixel at the x, y coordinate as byte.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate of the pixel.</param>
    /// <param name="y">The y coordinate of the pixel.</param>
    /// <returns>The brightness of the pixel at x, y.</returns>
    public static byte GetBrightness(this WriteableBitmap bmp, int x, int y)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        // Extract color components
        var c = context.Pixels[y * context.Width + x];
        var r = (byte)(c >> 16);
        var g = (byte)(c >> 8);
        var b = (byte)(c);

        // Convert to gray with constant factors 0.2126, 0.7152, 0.0722
        return (byte)((r * 6966 + g * 23436 + b * 2366) >> 15);
    }

    #endregion

    #region Beziér

    /// <summary>
    /// Draws a filled, cubic Beziér spline defined by start, end and two control points.
    /// </summary>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="cx1">The x-coordinate of the 1st control point.</param>
    /// <param name="cy1">The y-coordinate of the 1st control point.</param>
    /// <param name="cx2">The x-coordinate of the 2nd control point.</param>
    /// <param name="cy2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color.</param>
    /// <param name="context">The context with the pixels.</param>
    /// <param name="w">The width of the bitmap.</param>
    /// <param name="h">The height of the bitmap.</param> 
    [Obsolete("Obsolete, left for compatibility reasons. Please use List<int> ComputeBezierPoints(int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2) instead.")]
    private static List<int> ComputeBezierPoints(int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2, int color, BitmapContext context, int w, int h)
    {
        return ComputeBezierPoints(x1, y1, cx1, cy1, cx2, cy2, x2, y1);
    }

    /// <summary>
    /// Draws a filled, cubic Beziér spline defined by start, end and two control points.
    /// </summary>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="cx1">The x-coordinate of the 1st control point.</param>
    /// <param name="cy1">The y-coordinate of the 1st control point.</param>
    /// <param name="cx2">The x-coordinate of the 2nd control point.</param>
    /// <param name="cy2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    private static List<int> ComputeBezierPoints(int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2)
    {
        // Determine distances between controls points (bounding rect) to find the optimal stepsize
        var minX = Math.Min(x1, Math.Min(cx1, Math.Min(cx2, x2)));
        var minY = Math.Min(y1, Math.Min(cy1, Math.Min(cy2, y2)));
        var maxX = Math.Max(x1, Math.Max(cx1, Math.Max(cx2, x2)));
        var maxY = Math.Max(y1, Math.Max(cy1, Math.Max(cy2, y2)));

        // Get slope
        var lenx = maxX - minX;
        var len = maxY - minY;
        if (lenx > len)
        {
            len = lenx;
        }

        // Prevent division by zero
        var list = new List<int>();
        if (len != 0)
        {
            // Init vars
            var step = StepFactor / len;

            // Interpolate
            for (var t = 0f; t <= 1; t += step)
            {
                var tSq = t * t;
                var t1 = 1 - t;
                var t1Sq = t1 * t1;

                var tx = (int)(t1 * t1Sq * x1 + 3 * t * t1Sq * cx1 + 3 * t1 * tSq * cx2 + t * tSq * x2);
                var ty = (int)(t1 * t1Sq * y1 + 3 * t * t1Sq * cy1 + 3 * t1 * tSq * cy2 + t * tSq * y2);

                list.Add(tx);
                list.Add(ty);
            }

            // Prevent rounding gap
            list.Add(x2);
            list.Add(y2);
        }
        return list;
    }

    /// <summary>
    /// Draws a series of filled, cubic Beziér splines each defined by start, end and two control points. 
    /// The ending point of the previous curve is used as starting point for the next. 
    /// Therefore the initial curve needs four points and the subsequent 3 (2 control and 1 end point).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, cx1, cy1, cx2, cy2, x2, y2, cx3, cx4 ..., xn, yn).</param>
    /// <param name="color">The color for the spline.</param>
    public static void FillBeziers(this WriteableBitmap bmp, int[] points, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.FillBeziers(points, col);
    }

    /// <summary>
    /// Draws a series of filled, cubic Beziér splines each defined by start, end and two control points. 
    /// The ending point of the previous curve is used as starting point for the next. 
    /// Therefore the initial curve needs four points and the subsequent 3 (2 control and 1 end point).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, cx1, cy1, cx2, cy2, x2, y2, cx3, cx4 ..., xn, yn).</param>
    /// <param name="color">The color for the spline.</param>
    public static void FillBeziers(this WriteableBitmap bmp, int[] points, int color)
    {
        // Compute Bezier curve
        int x1 = points[0];
        int y1 = points[1];
        int x2, y2;
        var list = new List<int>();
        for (int i = 2; i + 5 < points.Length; i += 6)
        {
            x2 = points[i + 4];
            y2 = points[i + 5];
            list.AddRange(ComputeBezierPoints(x1, y1, points[i], points[i + 1], points[i + 2], points[i + 3], x2, y2));
            x1 = x2;
            y1 = y2;
        }

        // Fill
        bmp.FillPolygon([.. list], color);
    }

    #endregion

    #region Blend

    private static int Blend(BlendModes? mode, int target, int sa, int sr, int sg, int sb)
    {
        var ca = target.Decode();
        var cb = ((sa << 24) | (sr << 16) | (sg << 8) | sb).Decode();

        var va = ca;
        var vb = cb;
        var vc = va.Blend(vb, mode ?? BlendModes.Normal);
        return vc.Encode();
    }

    public static void Blend(this WriteableBitmap a, WriteableBitmap b, BlendModes blendMode = BlendModes.Normal)
    {
        a.ForEach((x, y, color) =>
        {
            if (x < b.PixelWidth && y < b.PixelHeight)
            {
                var ca = color;
                var cb = b.GetPixel(x, y);

                var va = GetColor(ca);
                var vb = GetColor(cb);
                var vc = va.Blend(vb, blendMode, 1);

                return Color.FromArgb(vc.A, vc.R, vc.G, vc.B);
            }

            return color;
        });
    }

    public static void Blend(this WriteableBitmap a, Matrix<byte> b, Vector2<int> point, Color color, BlendModes mode = BlendModes.Normal, double opacity = 1)
    {
        var colors = b.NewType((y, x, i) => Color.FromArgb(System.Convert.ToByte(i / 255 * opacity * 255), color.R, color.G, color.B));
        a.FillRectangle(point.X, point.Y, colors, mode);
    }

    ///

    public static void Blend(this WriteableBitmap a, BlendModes blendMode, Matrix<Color> b, int xOffset = 0, int yOffset = 0, double opacity = 1, bool everything = false)
    {
        a.ForEach((x, y, color) =>
        {
            if (everything || color.A > 0)
            {
                //To do: Fill empty pixels when OffsetX != 0 and OffsetY !=  0
                if (x + xOffset < b.Columns && y + yOffset < b.Rows)
                {
                    var ca = color;
                    var cb = b[(y + yOffset), (x + xOffset)];

                    var va = GetColor(ca);
                    var vb = GetColor(cb);
                    var vc = va.Blend(vb, blendMode, opacity);

                    return Color.FromArgb(vc.A, vc.R, vc.G, vc.B);
                }
            }
            return color;
        });
    }

    public static void Blend(this WriteableBitmap a, BlendModes blendMode, WriteableBitmap b, int xOffset = 0, int yOffset = 0, double opacity = 1, bool everything = false)
    {
        a.ForEach((x, y, color) =>
        {
            if (everything || color.A > 0)
            {
                //To do: Fill empty pixels when OffsetX != 0 and OffsetY !=  0
                if (x + xOffset < b.PixelWidth && y + yOffset < b.PixelHeight)
                {
                    var ca = color;
                    var cb = b.GetPixel(x + xOffset, y + yOffset);

                    var va = GetColor(ca);
                    var vb = GetColor(cb);
                    var vc = va.Blend(vb, blendMode, opacity);

                    return Color.FromArgb(vc.A, vc.R, vc.G, vc.B);
                }
            }
            return color;
        });
    }

    public static void Blend(this WriteableBitmap a, BlendModes blendMode, WriteableBitmap b, Point point, double opacity = 1)
    {
        a.ForEach((x0, y0, color) =>
        {
            if (x0 >= point.X && y0 >= point.Y)
            {
                var x1 = x0 - point.X;
                var y1 = y0 - point.Y;

                var x2 = System.Convert.ToInt32(Clamp(x1, 0, b.PixelWidth));
                var y2 = System.Convert.ToInt32(Clamp(y1, 0, b.PixelHeight));

                if (x1 < b.PixelWidth && y1 < b.PixelHeight)
                {
                    var ca = color;
                    var cb = b.GetPixel(x2, y2);

                    var va = GetColor(ca);
                    var vb = GetColor(cb);
                    var vc = va.Blend(vb, blendMode, opacity);

                    return Color.FromArgb(vc.A, vc.R, vc.G, vc.B);
                }
            }
            return color;
        });
    }

    private static bool Similar(Color a, Color b, byte tolerance)
    {
        if (tolerance == 0)
            return a == b;

        a.Convert(out System.Drawing.Color da);
        b.Convert(out System.Drawing.Color db);

        var ab = da.GetBrightness();
        var bb = db.GetBrightness();

        var cb = Abs(ab - bb) * 255f;
        return cb <= tolerance;
    }

    private static bool Similar(Color a, System.Drawing.Color b, byte tolerance)
    {
        b.Convert(out Color c);
        return Similar(a, c, tolerance);
    }

    public static void BlendAt(this WriteableBitmap input, BlendModes blendMode, System.Drawing.Point point, System.Drawing.Color fill, double opacity, byte tolerance)
    {

        input.GetPixel(point.X, point.Y).Convert(out System.Drawing.Color old);

        var height = System.Convert.ToUInt32(input.PixelHeight);
        var width = System.Convert.ToUInt32(input.PixelWidth);

        if (fill == old)
            return;

        old.Convert(out Color oldClone);

        if (point.X < 0 || point.X >= input.PixelWidth)
            return;

        if (point.Y < 0 || point.Y >= input.PixelHeight)
            return;

        if (input.GetPixel(point.X, point.Y) != oldClone)
            return;

        fill.Convert(out Color fillClone);

        Queue<System.Drawing.Point> queue = new();
        input.SetPixel(point.X, point.Y, fillClone);
        queue.Enqueue(point);

        while (queue.Count > 0)
        {
            var next = queue.Dequeue();

            //Left
            var p = new System.Drawing.Point(next.X - 1, next.Y);

            Color c = input.GetPixel(p.X, p.Y);
            if (p.X >= 0 && Similar(c, old, tolerance))
            {
                var ca = c;
                var cb = fillClone;

                var va = GetColor(ca);
                var vb = GetColor(cb);
                var vc = va.Blend(vb, blendMode, opacity);

                input.SetPixel(p.X, p.Y, Color.FromArgb(vc.A, vc.R, vc.G, vc.B));
                queue.Enqueue(p);
            }

            //Right
            p = new System.Drawing.Point(next.X + 1, next.Y);

            c = input.GetPixel(p.X, p.Y);
            if (p.X < width && Similar(c, old, tolerance))
            {
                var ca = c;
                var cb = fillClone;

                var va = GetColor(ca);
                var vb = GetColor(cb);
                var vc = va.Blend(vb, blendMode, opacity);

                input.SetPixel(p.X, p.Y, Color.FromArgb(vc.A, vc.R, vc.G, vc.B));
                queue.Enqueue(p);
            }

            //Top
            p = new System.Drawing.Point(next.X, next.Y + 1);

            c = input.GetPixel(p.X, p.Y);
            if (p.Y < height && Similar(c, old, tolerance))
            {
                var ca = c;
                var cb = fillClone;

                var va = GetColor(ca);
                var vb = GetColor(cb);
                var vc = va.Blend(vb, blendMode, opacity);

                input.SetPixel(p.X, p.Y, Color.FromArgb(vc.A, vc.R, vc.G, vc.B));
                queue.Enqueue(p);
            }

            //Bottom
            p = new System.Drawing.Point(next.X, next.Y - 1);

            c = input.GetPixel(p.X, p.Y);
            if (p.Y >= 0 && Similar(c, old, tolerance))
            {
                var ca = c;
                var cb = fillClone;

                var va = GetColor(ca);
                var vb = GetColor(cb);
                var vc = va.Blend(vb, blendMode, opacity);

                input.SetPixel(p.X, p.Y, Color.FromArgb(vc.A, vc.R, vc.G, vc.B));
                queue.Enqueue(p);
            }
        }
    }

    ///

    public static void Erase(this WriteableBitmap a, Matrix<byte> b, Vector2<int> point)
    {
        var result = b.NewType(i => Color.FromArgb(i, 0, 0, 0));
        a.ForEach((x0, y0, color) =>
        {
            if (x0 >= point.X && y0 >= point.Y)
            {
                var x1 = x0 - point.X;
                var y1 = y0 - point.Y;

                var x2 = Clamp(x1, 0, b.Columns);
                var y2 = Clamp(y1, 0, b.Rows);

                if (x1 < b.Columns && y1 < b.Rows)
                {
                    var alpha = System.Convert.ToByte((Clamp((color.A / 255) - (b[y2, x2] / 255), 0, 1) * 255));
                    return Color.FromArgb(alpha, color.R, color.G, color.B);
                }
            }
            return color;
        });
    }

    #endregion

    #region Cardinal

    /// <summary>
    /// Computes the discrete segment points of a Cardinal spline (cubic) defined by four control points.
    /// </summary>
    /// <param name="x1">The x-coordinate of the 1st control point.</param>
    /// <param name="y1">The y-coordinate of the 1st control point.</param>
    /// <param name="x2">The x-coordinate of the 2nd control point.</param>
    /// <param name="y2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x3">The x-coordinate of the 3rd control point.</param>
    /// <param name="y3">The y-coordinate of the 3rd control point.</param>
    /// <param name="x4">The x-coordinate of the 4th control point.</param>
    /// <param name="y4">The y-coordinate of the 4th control point.</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color.</param>
    /// <param name="context">The context with the pixels.</param>
    /// <param name="w">The width of the bitmap.</param>
    /// <param name="h">The height of the bitmap.</param> 
    [Obsolete("Obsolete, left for compatibility reasons. Please use List<int> ComputeSegmentPoints(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension) instead.")]
    private static List<int> ComputeSegmentPoints(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension, int color, BitmapContext context, int w, int h)
    {
        return ComputeSegmentPoints(x1, y1, x2, y2, x3, y3, x4, y4, tension);
    }

    /// <summary>
    /// Computes the discrete segment points of a Cardinal spline (cubic) defined by four control points.
    /// </summary>
    /// <param name="x1">The x-coordinate of the 1st control point.</param>
    /// <param name="y1">The y-coordinate of the 1st control point.</param>
    /// <param name="x2">The x-coordinate of the 2nd control point.</param>
    /// <param name="y2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x3">The x-coordinate of the 3rd control point.</param>
    /// <param name="y3">The y-coordinate of the 3rd control point.</param>
    /// <param name="x4">The x-coordinate of the 4th control point.</param>
    /// <param name="y4">The y-coordinate of the 4th control point.</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    private static List<int> ComputeSegmentPoints(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension)
    {
        // Determine distances between controls points (bounding rect) to find the optimal stepsize
        var minX = Math.Min(x1, Math.Min(x2, Math.Min(x3, x4)));
        var minY = Math.Min(y1, Math.Min(y2, Math.Min(y3, y4)));
        var maxX = Math.Max(x1, Math.Max(x2, Math.Max(x3, x4)));
        var maxY = Math.Max(y1, Math.Max(y2, Math.Max(y3, y4)));

        // Get slope
        var lenx = maxX - minX;
        var len = maxY - minY;
        if (lenx > len)
        {
            len = lenx;
        }

        // Prevent division by zero
        var list = new List<int>();
        if (len != 0)
        {
            // Init vars
            var step = StepFactor / len;

            // Calculate factors
            var sx1 = tension * (x3 - x1);
            var sy1 = tension * (y3 - y1);
            var sx2 = tension * (x4 - x2);
            var sy2 = tension * (y4 - y2);
            var ax = sx1 + sx2 + 2 * x2 - 2 * x3;
            var ay = sy1 + sy2 + 2 * y2 - 2 * y3;
            var bx = -2 * sx1 - sx2 - 3 * x2 + 3 * x3;
            var by = -2 * sy1 - sy2 - 3 * y2 + 3 * y3;

            // Interpolate
            for (var t = 0f; t <= 1; t += step)
            {
                var tSq = t * t;

                int tx = (int)(ax * tSq * t + bx * tSq + sx1 * t + x2);
                int ty = (int)(ay * tSq * t + by * tSq + sy1 * t + y2);

                list.Add(tx);
                list.Add(ty);
            }

            // Prevent rounding gap
            list.Add(x3);
            list.Add(y3);
        }
        return list;
    }

    /// <summary>
    /// Draws a filled Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void FillCurve(this WriteableBitmap bmp, int[] points, float tension, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.FillCurve(points, tension, col);
    }

    /// <summary>
    /// Draws a filled Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void FillCurve(this WriteableBitmap bmp, int[] points, float tension, int color)
    {
        // First segment
        var list = ComputeSegmentPoints(points[0], points[1], points[0], points[1], points[2], points[3], points[4],
            points[5], tension);

        // Middle segments
        int i;
        for (i = 2; i < points.Length - 4; i += 2)
        {
            list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2],
                points[i + 3], points[i + 4], points[i + 5], tension));
        }

        // Last segment
        list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2],
            points[i + 3], points[i + 2], points[i + 3], tension));

        // Fill
        bmp.FillPolygon([.. list], color);
    }

    /// <summary>
    /// Draws a filled, closed Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void FillCurveClosed(this WriteableBitmap bmp, int[] points, float tension, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.FillCurveClosed(points, tension, col);
    }

    /// <summary>
    /// Draws a filled, closed Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void FillCurveClosed(this WriteableBitmap bmp, int[] points, float tension, int color)
    {
        int pn = points.Length;

        // First segment
        var list = ComputeSegmentPoints(points[pn - 2], points[pn - 1], points[0], points[1], points[2], points[3],
            points[4], points[5], tension);

        // Middle segments
        int i;
        for (i = 2; i < pn - 4; i += 2)
        {
            list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1],
                points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension));
        }

        // Last segment
        list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2],
            points[i + 3], points[0], points[1], tension));

        // Last-to-First segment
        list.AddRange(ComputeSegmentPoints(points[i], points[i + 1], points[i + 2], points[i + 3], points[0],
            points[1], points[2], points[3], tension));

        // Fill
        bmp.FillPolygon([.. list], color);
    }

    #endregion

    #region Ellipse

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing filled ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf 
    /// x2 has to be greater than x1 and y2 has to be greater than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color for the line.</param>
    public static void FillEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, BlendModes? mode = BlendModes.Normal)
    {
        var col = GetColor(color).Encode();
        bmp.FillEllipse(x1, y1, x2, y2, col, mode);
    }

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing filled ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf 
    /// x2 has to be greater than x1 and y2 has to be greater than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color for the line.</param>
    public static void FillEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, BlendModes? mode = BlendModes.Normal)
    {
        // Calc center and radius
        int xr = (x2 - x1) >> 1;
        int yr = (y2 - y1) >> 1;
        int xc = x1 + xr;
        int yc = y1 + yr;
        bmp.FillEllipseCentered(xc, yc, xr, yr, color, mode);
    }

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing filled ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf 
    /// Uses a different parameter representation than DrawEllipse().
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="xc">The x-coordinate of the ellipses center.</param>
    /// <param name="yc">The y-coordinate of the ellipses center.</param>
    /// <param name="xr">The radius of the ellipse in x-direction.</param>
    /// <param name="yr">The radius of the ellipse in y-direction.</param>
    /// <param name="color">The color for the line.</param>
    public static void FillEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, Color color, BlendModes? mode = BlendModes.Normal)
    {
        var col = GetColor(color).Encode();
        bmp.FillEllipseCentered(xc, yc, xr, yr, col, mode);
    }

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing filled ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf  
    /// With or without alpha blending (default = false).
    /// Uses a different parameter representation than DrawEllipse().
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="xc">The x-coordinate of the ellipses center.</param>
    /// <param name="yc">The y-coordinate of the ellipses center.</param>
    /// <param name="xr">The radius of the ellipse in x-direction.</param>
    /// <param name="yr">The radius of the ellipse in y-direction.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="doAlphaBlend">True if alpha blending should be performed or false if not.</param>
    public static void FillEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, int color, BlendModes? mode = BlendModes.Normal)
    {
        // Use refs for faster access (really important!) speeds up a lot!
        using var context = bmp.GetContext();
        var pixels = context.Pixels;
        int w = context.Width;
        int h = context.Height;

        // Avoid endless loop
        if (xr < 1 || yr < 1)
        {
            return;
        }

        // Skip completly outside objects
        if (xc - xr >= w || xc + xr < 0 || yc - yr >= h || yc + yr < 0)
        {
            return;
        }

        // Init vars
        int uh, lh, uy, ly, lx, rx;
        int x = xr;
        int y = 0;
        int xrSqTwo = (xr * xr) << 1;
        int yrSqTwo = (yr * yr) << 1;
        int xChg = yr * yr * (1 - (xr << 1));
        int yChg = xr * xr;
        int err = 0;
        int xStopping = yrSqTwo * xr;
        int yStopping = 0;

        int sa = ((color >> 24) & 0xff);
        int sr = ((color >> 16) & 0xff);
        int sg = ((color >> 8) & 0xff);
        int sb = ((color) & 0xff);

        bool noBlending = mode is null;

        // Draw first set of points counter clockwise where tangent line slope > -1.
        while (xStopping >= yStopping)
        {
            // Draw 4 quadrant points at once
            // Upper half
            uy = yc + y;
            // Lower half
            ly = yc - y - 1;

            // Clip
            if (uy < 0) uy = 0;
            if (uy >= h) uy = h - 1;
            if (ly < 0) ly = 0;
            if (ly >= h) ly = h - 1;

            // Upper half
            uh = uy * w;
            // Lower half
            lh = ly * w;

            rx = xc + x;
            lx = xc - x;

            // Clip
            if (rx < 0) rx = 0;
            if (rx >= w) rx = w - 1;
            if (lx < 0) lx = 0;
            if (lx >= w) lx = w - 1;

            // Draw line
            if (noBlending)
            {
                for (int i = lx; i <= rx; i++)
                {
                    pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                    pixels[i + lh] = color; // Quadrant III to IV
                }
            }
            else
            {
                for (int i = lx; i <= rx; i++)
                {
                    // Quadrant II to I (Actually two octants)
                    pixels[i + uh] = Blend(mode, pixels[i + uh], sa, sr, sg, sb);

                    // Quadrant III to IV
                    pixels[i + lh] = Blend(mode, pixels[i + lh], sa, sr, sg, sb);
                }
            }

            y++;
            yStopping += xrSqTwo;
            err += yChg;
            yChg += xrSqTwo;
            if ((xChg + (err << 1)) > 0)
            {
                x--;
                xStopping -= yrSqTwo;
                err += xChg;
                xChg += yrSqTwo;
            }
        }

        // ReInit vars
        x = 0;
        y = yr;

        // Upper half
        uy = yc + y;
        // Lower half
        ly = yc - y;

        // Clip
        if (uy < 0) uy = 0;
        if (uy >= h) uy = h - 1;
        if (ly < 0) ly = 0;
        if (ly >= h) ly = h - 1;

        // Upper half
        uh = uy * w;
        // Lower half
        lh = ly * w;

        xChg = yr * yr;
        yChg = xr * xr * (1 - (yr << 1));
        err = 0;
        xStopping = 0;
        yStopping = xrSqTwo * yr;

        // Draw second set of points clockwise where tangent line slope < -1.
        while (xStopping <= yStopping)
        {
            // Draw 4 quadrant points at once
            rx = xc + x;
            lx = xc - x;

            // Clip
            if (rx < 0) rx = 0;
            if (rx >= w) rx = w - 1;
            if (lx < 0) lx = 0;
            if (lx >= w) lx = w - 1;

            // Draw line
            if (noBlending)
            {
                for (int i = lx; i <= rx; i++)
                {
                    pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                    pixels[i + lh] = color; // Quadrant III to IV
                }
            }
            else
            {
                for (int i = lx; i <= rx; i++)
                {
                    // Quadrant II to I (Actually two octants)
                    pixels[i + uh] = Blend(mode, pixels[i + uh], sa, sr, sg, sb);

                    // Quadrant III to IV
                    pixels[i + lh] = Blend(mode, pixels[i + lh], sa, sr, sg, sb);
                }
            }

            x++;
            xStopping += yrSqTwo;
            err += xChg;
            xChg += yrSqTwo;
            if ((yChg + (err << 1)) > 0)
            {
                y--;
                uy = yc + y; // Upper half
                ly = yc - y; // Lower half
                if (uy < 0) uy = 0; // Clip
                if (uy >= h) uy = h - 1; // ...
                if (ly < 0) ly = 0;
                if (ly >= h) ly = h - 1;
                uh = uy * w; // Upper half
                lh = ly * w; // Lower half
                yStopping -= xrSqTwo;
                err += yChg;
                yChg += xrSqTwo;
            }
        }
    }

    #endregion

    #region Rectangle

    /// <summary>
    /// Draws a filled rectangle.
    /// x2 has to be greater than x1 and y2 has to be greater than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color.</param>
    public static void FillRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, BlendModes? mode = BlendModes.Normal)
    {
        var col = GetColor(color).Encode();
        bmp.FillRectangle(x1, y1, x2, y2, col, mode);
    }

    /// <summary>
    /// Draws a filled rectangle with or without alpha blending (default = false).
    /// x2 has to be greater than x1 and y2 has to be greater than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color.</param>
    /// <param name="doAlphaBlend">True if alpha blending should be performed or false if not.</param>
    public static void FillRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, BlendModes? mode = BlendModes.Normal)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;

        int sa = ((color >> 24) & 0xff);
        int sr = ((color >> 16) & 0xff);
        int sg = ((color >> 8) & 0xff);
        int sb = ((color) & 0xff);

        var pixels = context.Pixels;

        // Check boundaries
        if ((x1 < 0 && x2 < 0) || (y1 < 0 && y2 < 0)
            || (x1 >= w && x2 >= w) || (y1 >= h && y2 >= h))
        {
            return;
        }

        // Clamp boundaries
        if (x1 < 0) { x1 = 0; }
        if (y1 < 0) { y1 = 0; }
        if (x2 < 0) { x2 = 0; }
        if (y2 < 0) { y2 = 0; }
        if (x1 > w) { x1 = w; }
        if (y1 > h) { y1 = h; }
        if (x2 > w) { x2 = w; }
        if (y2 > h) { y2 = h; }

        //swap values
        if (y1 > y2)
        {
            y2 -= y1;
            y1 += y2;
            y2 = (y1 - y2);
        }

        // Fill first line
        var startY = y1 * w;
        var startYPlusX1 = startY + x1;
        var endOffset = startY + x2;
        for (var idx = startYPlusX1; idx < endOffset; idx++)
        {
            pixels[idx] = Blend(mode, pixels[idx], sa, sr, sg, sb);
        }

        // Copy first line
        var len = (x2 - x1);
        var srcOffsetBytes = startYPlusX1 * XWriteableBitmap.SizeOfArgb;
        var offset2 = y2 * w + x1;
        for (var y = startYPlusX1 + w; y < offset2; y += w)
        {
            // Alpha blend line
            for (int i = 0; i < len; i++)
            {
                int idx = y + i;
                pixels[idx] = Blend(mode, pixels[idx], sa, sr, sg, sb);
            }
        }
    }

    public static void FillRectangle(this WriteableBitmap bmp, int x1, int y1, Matrix<Color> colors, BlendModes? mode = BlendModes.Normal)
    {
        var x2 = x1 + System.Convert.ToInt32(colors.Columns);
        var y2 = y1 + System.Convert.ToInt32(colors.Rows);

        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;

        var pixels = context.Pixels;

        // Check boundaries
        if ((x1 < 0 && x2 < 0) || (y1 < 0 && y2 < 0)
            || (x1 >= w && x2 >= w) || (y1 >= h && y2 >= h))
        {
            return;
        }

        // Clamp boundaries
        if (x1 < 0) { x1 = 0; }
        if (y1 < 0) { y1 = 0; }
        if (x2 < 0) { x2 = 0; }
        if (y2 < 0) { y2 = 0; }
        if (x1 > w) { x1 = w; }
        if (y1 > h) { y1 = h; }
        if (x2 > w) { x2 = w; }
        if (y2 > h) { y2 = h; }

        //swap values
        if (y1 > y2)
        {
            y2 -= y1;
            y1 += y2;
            y2 = (y1 - y2);
        }

        int sa = 0, sr = 0, sg = 0, sb = 0;

        // Fill first line
        var startY = y1 * w;
        var startYPlusX1 = startY + x1;
        var endOffset = startY + x2;

        int wxy = 0;
        for (var idx = startYPlusX1; idx < endOffset; idx++, wxy++)
        {
            var j = GetColor(colors[0, wxy]).Encode();
            sa = ((j >> 24) & 0xff);
            sr = ((j >> 16) & 0xff);
            sg = ((j >> 8) & 0xff);
            sb = ((j) & 0xff);
            pixels[idx] = Blend(mode, pixels[idx], sa, sr, sg, sb);
        }

        // Copy first line
        var len = (x2 - x1);
        var srcOffsetBytes = startYPlusX1 * XWriteableBitmap.SizeOfArgb;
        var offset2 = y2 * w + x1;

        int jkd = 0;
        for (var y = startYPlusX1 + w; y < offset2; y += w, jkd++)
        {
            // Alpha blend line
            for (int i = 0; i < len; i++)
            {
                int idx = y + i;

                var j = GetColor(colors[jkd, i]).Encode();
                sa = ((j >> 24) & 0xff);
                sr = ((j >> 16) & 0xff);
                sg = ((j >> 8) & 0xff);
                sb = ((j) & 0xff);

                pixels[idx] = Blend(mode, pixels[idx], sa, sr, sg, sb);
            }
        }
    }

    #endregion

    #region Polygon, Triangle, Quad

    /// <summary>
    /// Draws a filled polygon. Add the first point also at the end of the array if the line should be closed.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points of the polygon in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).</param>
    /// <param name="color">The color for the line.</param>
    public static void FillPolygon(this WriteableBitmap bmp, int[] points, Color color, BlendModes? mode = BlendModes.Normal)
    {
        var col = GetColor(color).Encode();
        bmp.FillPolygon(points, col, mode);
    }

    /// <summary>
    /// Draws a filled polygon with or without alpha blending (default = false). 
    /// Add the first point also at the end of the array if the line should be closed.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points of the polygon in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="doAlphaBlend">True if alpha blending should be performed or false if not.</param>
    public static void FillPolygon(this WriteableBitmap bmp, int[] points, int color, BlendModes? mode = BlendModes.Normal)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;

        int sa = ((color >> 24) & 0xff);
        int sr = ((color >> 16) & 0xff);
        int sg = ((color >> 8) & 0xff);
        int sb = ((color) & 0xff);

        bool noBlending = mode is null;

        var pixels = context.Pixels;
        int pn = points.Length;
        int pnh = points.Length >> 1;
        int[] intersectionsX = new int[pnh];

        // Find y min and max (slightly faster than scanning from 0 to height)
        int yMin = h;
        int yMax = 0;
        for (int i = 1; i < pn; i += 2)
        {
            int py = points[i];
            if (py < yMin) yMin = py;
            if (py > yMax) yMax = py;
        }
        if (yMin < 0) yMin = 0;
        if (yMax >= h) yMax = h - 1;


        // Scan line from min to max
        for (int y = yMin; y <= yMax; y++)
        {
            // Initial point x, y
            float vxi = points[0];
            float vyi = points[1];

            // Find all intersections
            // Based on http://alienryderflex.com/polygon_fill/
            int intersectionCount = 0;
            for (int i = 2; i < pn; i += 2)
            {
                // Next point x, y
                float vxj = points[i];
                float vyj = points[i + 1];

                // Is the scanline between the two points
                if (vyi < y && vyj >= y
                    || vyj < y && vyi >= y)
                {
                    // Compute the intersection of the scanline with the edge (line between two points)
                    intersectionsX[intersectionCount++] = (int)(vxi + (y - vyi) / (vyj - vyi) * (vxj - vxi));
                }
                vxi = vxj;
                vyi = vyj;
            }

            // Sort the intersections from left to right using Insertion sort 
            // It's faster than Array.Sort for this small data set
            int t, j;
            for (int i = 1; i < intersectionCount; i++)
            {
                t = intersectionsX[i];
                j = i;
                while (j > 0 && intersectionsX[j - 1] > t)
                {
                    intersectionsX[j] = intersectionsX[j - 1];
                    j--;
                }
                intersectionsX[j] = t;
            }

            // Fill the pixels between the intersections
            for (int i = 0; i < intersectionCount - 1; i += 2)
            {
                int x0 = intersectionsX[i];
                int x1 = intersectionsX[i + 1];

                // Check boundary
                if (x1 > 0 && x0 < w)
                {
                    if (x0 < 0) x0 = 0;
                    if (x1 >= w) x1 = w - 1;

                    // Fill the pixels
                    for (int x = x0; x <= x1; x++)
                    {
                        int idx = y * w + x;

                        pixels[idx] = noBlending ? color : Blend(mode, pixels[idx], sa, sr, sg, sb);
                    }
                }
            }
        }
    }

    #region Multiple (possibly nested) Polygons
    /// <summary>
    /// Helper class for storing the data of an edge.
    /// </summary>
    /// <remarks>
    /// The following is always true: 
    /// <code>edge.StartY &lt; edge.EndY</code>
    /// </remarks>
    private class Edge : IComparable<Edge>
    {
        /// <summary>
        /// X coordinate of starting point of edge.
        /// </summary>
        public readonly int StartX;
        /// <summary>
        /// Y coordinate of starting point of edge.
        /// </summary>
        public readonly int StartY;
        /// <summary>
        /// X coordinate of ending point of edge.
        /// </summary>
        public readonly int EndX;
        /// <summary>
        /// Y coordinate of ending point of edge.
        /// </summary>
        public readonly int EndY;
        /// <summary>
        /// The slope of the edge.
        /// </summary>
        public readonly float Sloap;

        /// <summary>
        /// Initializes a new instance of the <see cref="Edge"/> class.
        /// </summary>
        /// <remarks>
        /// The constructor may swap start and end point to fulfill the guarantees of this class.
        /// </remarks>
        /// <param name="startX">The X coordinate of the start point of the edge.</param>
        /// <param name="startY">The Y coordinate of the start point of the edge.</param>
        /// <param name="endX">The X coordinate of the end point of the edge.</param>
        /// <param name="endY">The Y coordinate of the end point of the edge.</param>
        public Edge(int startX, int startY, int endX, int endY)
        {
            if (startY > endY)
            {
                // swap direction
                StartX = endX;
                StartY = endY;
                EndX = startX;
                EndY = startY;
            }
            else
            {
                StartX = startX;
                StartY = startY;
                EndX = endX;
                EndY = endY;
            }
            Sloap = (EndX - StartX) / (float)(EndY - StartY);
        }

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> parameter.Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public int CompareTo(Edge other)
        {
            return StartY == other.StartY
                ? StartX.CompareTo(other.StartX)
                : StartY.CompareTo(other.StartY);
        }
    }

    /// <summary>
    /// Draws filled polygons using even-odd filling, therefore allowing for holes.
    /// </summary>
    /// <remarks>
    /// Polygons are implicitly closed if necessary.
    /// </remarks>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="polygons">Array of polygons. 
    /// The different polygons are identified by the first index, 
    /// while the points of each polygon are in x and y pairs indexed by the second index, 
    /// therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).
    /// </param>
    /// <param name="color">The color for the polygon.</param>
    public static void FillPolygonsEvenOdd(this WriteableBitmap bmp, int[][] polygons, Color color)
    {
        var col = GetColor(color).Encode();
        FillPolygonsEvenOdd(bmp, polygons, col);
    }

    /// <summary>
    /// Draws filled polygons using even-odd filling, therefore allowing for holes.
    /// </summary>
    /// <remarks>
    /// Polygons are implicitly closed if necessary.
    /// </remarks>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="polygons">Array of polygons. 
    /// The different polygons are identified by the first index, 
    /// while the points of each polygon are in x and y pairs indexed by the second index, 
    /// therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).
    /// </param>
    /// <param name="color">The color for the polygon.</param>
    public static void FillPolygonsEvenOdd(this WriteableBitmap bmp, int[][] polygons, int color)
    {
        #region Algorithm

        // Algorithm:
        // This is using a scanline algorithm which is kept similar to the one the FillPolygon() method is using,
        // but it is only comparing the edges with the scanline which are currently intersecting the line.
        // To be able to do this it first builds a list of edges (var edges) from the polygons, which is then 
        // sorted via by their minimal y coordinate. During the scanline run only the edges which can intersect 
        // the current scanline are intersected to get the X coordinate of the intersection. These edges are kept 
        // in the list named currentEdges.
        // Especially for larger sane(*) polygons this is a lot faster then the algorithm used in the FillPolygon() 
        // method which is always comparing all edges with the scan line.
        // And sorry: the constraint to explicitly make the polygon close before using the FillPolygon() method is 
        // stupid, as filling an unclosed polygon is not very useful.
        //
        // (*) sane: the polygons in the FillSample speed test are not sane, because they contain a lot of very long 
        //     nearly vertical lines. A sane example would be a letter 'o', in which case the currentEdges list is 
        //     containing no more than 4 edges at any moment, regardless of the smoothness of the rendering of the 
        //     letter into two polygons.

        #endregion

        int polygonCount = polygons.Length;
        if (polygonCount == 0)
        {
            return;
        }
        // could use single polygon fill if count is 1, but it the algorithm used there is slower (at least for larger polygons)


        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;

        // Register edges, and find y max
        List<Edge> edges = [];
        int yMax = 0;
        foreach (int[] points in polygons)
        {
            int pn = points.Length;
            if (pn < 6)
            {
                // sanity check: don't care for lines or points or empty polygons
                continue;
            }
            int lastX;
            int lastY;
            int start;
            if (points[0] != points[pn - 2]
                || points[1] != points[pn - 1])
            {
                start = 0;
                lastX = points[pn - 2];
                lastY = points[pn - 1];
            }
            else
            {
                start = 2;
                lastX = points[0];
                lastY = points[1];
            }
            for (int i = start; i < pn; i += 2)
            {
                int px = points[i];
                int py = points[i + 1];
                if (py != lastY)
                {
                    Edge edge = new(lastX, lastY, px, py);
                    if (edge.StartY < h && edge.EndY >= 0)
                    {
                        if (edge.EndY > yMax) yMax = edge.EndY;
                        edges.Add(edge);
                    }
                }
                lastX = px;
                lastY = py;
            }
        }
        if (edges.Count == 0)
        {
            // sanity check
            return;
        }

        if (yMax >= h) yMax = h - 1;

        edges.Sort();
        int yMin = edges[0].StartY;
        if (yMin < 0) yMin = 0;

        int[] intersectionsX = new int[edges.Count];

        LinkedList<Edge> currentEdges = new();
        int e = 0;

        // Scan line from min to max
        for (int y = yMin; y <= yMax; y++)
        {
            // Remove edges no longer intersecting
            LinkedListNode<Edge> node = currentEdges.First;
            while (node != null)
            {
                LinkedListNode<Edge> nextNode = node.Next;
                Edge edge = node.Value;
                if (edge.EndY <= y)
                {
                    // using = here because the connecting edge will be added next
                    // remove edge
                    currentEdges.Remove(node);
                }
                node = nextNode;
            }
            // Add edges starting to intersect
            while (e < edges.Count &&
                    edges[e].StartY <= y)
            {
                currentEdges.AddLast(edges[e]);
                ++e;
            }
            // Calculate intersections
            int intersectionCount = 0;
            foreach (Edge currentEdge in currentEdges)
            {
                intersectionsX[intersectionCount++] =
                    (int)(currentEdge.StartX + (y - currentEdge.StartY) * currentEdge.Sloap);
            }

            // Sort the intersections from left to right using Insertion sort 
            // It's faster than Array.Sort for this small data set
            for (int i = 1; i < intersectionCount; i++)
            {
                int t = intersectionsX[i];
                int j = i;
                while (j > 0 && intersectionsX[j - 1] > t)
                {
                    intersectionsX[j] = intersectionsX[j - 1];
                    j--;
                }
                intersectionsX[j] = t;
            }

            // Fill the pixels between the intersections
            for (int i = 0; i < intersectionCount - 1; i += 2)
            {
                int x0 = intersectionsX[i];
                int x1 = intersectionsX[i + 1];

                if (x0 < 0) x0 = 0;
                if (x1 >= w) x1 = w - 1;
                if (x1 < x0)
                {
                    continue;
                }

                // Fill the pixels
                int index = y * w + x0;
                for (int x = x0; x <= x1; x++)
                {
                    pixels[index++] = color;
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// Draws a filled quad.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="x4">The x-coordinate of the 4th point.</param>
    /// <param name="y4">The y-coordinate of the 4th point.</param>
    /// <param name="color">The color.</param>
    public static void FillQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.FillQuad(x1, y1, x2, y2, x3, y3, x4, y4, col);
    }

    /// <summary>
    /// Draws a filled quad.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="x4">The x-coordinate of the 4th point.</param>
    /// <param name="y4">The y-coordinate of the 4th point.</param>
    /// <param name="color">The color.</param>
    public static void FillQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, int color)
    {
        bmp.FillPolygon([x1, y1, x2, y2, x3, y3, x4, y4, x1, y1], color);
    }

    /// <summary>
    /// Draws a filled triangle.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="color">The color.</param>
    public static void FillTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.FillTriangle(x1, y1, x2, y2, x3, y3, col);
    }

    /// <summary>
    /// Draws a filled triangle.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="color">The color.</param>
    public static void FillTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int color)
    {
        bmp.FillPolygon([x1, y1, x2, y2, x3, y3, x1, y1], color);
    }

    #endregion

    #endregion

    #region Filter

    #region Kernels

    /// <summary>
    /// Gaussian blur kernel with the size 5x5
    /// </summary>
    public static readonly int[,] KernelGaussianBlur5x5 =
    {
        {1,  4,  7,  4, 1},
        {4, 16, 26, 16, 4},
        {7, 26, 41, 26, 7},
        {4, 16, 26, 16, 4},
        {1,  4,  7,  4, 1}
    };

    /// <summary>
    /// Gaussian blur kernel with the size 3x3
    /// </summary>
    public static readonly int[,] KernelGaussianBlur3x3 =
    {
        {16, 26, 16},
        {26, 41, 26},
        {16, 26, 16}
    };

    /// <summary>
    /// Sharpen kernel with the size 3x3
    /// </summary>
    public static readonly int[,] KernelSharpen3x3 =
    {
        { 0, -2,  0},
        {-2, 11, -2},
        { 0, -2,  0}
    };

    #endregion

    #region Methods

    #region Convolute

    /// <summary>
    /// Creates a new filtered WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="kernel">The kernel used for convolution.</param>
    /// <returns>A new WriteableBitmap that is a filtered version of the input.</returns>
    public static WriteableBitmap Convolute(this WriteableBitmap bmp, int[,] kernel)
    {
        var kernelFactorSum = 0;
        foreach (var b in kernel)
        {
            kernelFactorSum += b;
        }
        return bmp.Convolute(kernel, kernelFactorSum, 0);
    }

    /// <summary>
    /// Creates a new filtered WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="kernel">The kernel used for convolution.</param>
    /// <param name="kernelFactorSum">The factor used for the kernel summing.</param>
    /// <param name="kernelOffsetSum">The offset used for the kernel summing.</param>
    /// <returns>A new WriteableBitmap that is a filtered version of the input.</returns>
    public static WriteableBitmap Convolute(this WriteableBitmap bmp, int[,] kernel, int kernelFactorSum, int kernelOffsetSum)
    {
        var kh = kernel.GetUpperBound(0) + 1;
        var kw = kernel.GetUpperBound(1) + 1;

        if ((kw & 1) == 0)
        {
            throw new InvalidOperationException("Kernel width must be odd!");
        }
        if ((kh & 1) == 0)
        {
            throw new InvalidOperationException("Kernel height must be odd!");
        }

        using var srcContext = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var w = srcContext.Width;
        var h = srcContext.Height;
        var result = XWriteableBitmap.New(w, h);

        using var resultContext = result.GetContext();
        var pixels = srcContext.Pixels;
        var resultPixels = resultContext.Pixels;
        var index = 0;
        var kwh = kw >> 1;
        var khh = kh >> 1;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var a = 0;
                var r = 0;
                var g = 0;
                var b = 0;

                for (var kx = -kwh; kx <= kwh; kx++)
                {
                    var px = kx + x;
                    // Repeat pixels at borders
                    if (px < 0)
                    {
                        px = 0;
                    }
                    else if (px >= w)
                    {
                        px = w - 1;
                    }

                    for (var ky = -khh; ky <= khh; ky++)
                    {
                        var py = ky + y;
                        // Repeat pixels at borders
                        if (py < 0)
                        {
                            py = 0;
                        }
                        else if (py >= h)
                        {
                            py = h - 1;
                        }

                        var col = pixels[py * w + px];
                        var k = kernel[ky + kwh, kx + khh];
                        a += ((col >> 24) & 0x000000FF) * k;
                        r += ((col >> 16) & 0x000000FF) * k;
                        g += ((col >> 8) & 0x000000FF) * k;
                        b += ((col) & 0x000000FF) * k;
                    }
                }

                var ta = ((a / kernelFactorSum) + kernelOffsetSum);
                var tr = ((r / kernelFactorSum) + kernelOffsetSum);
                var tg = ((g / kernelFactorSum) + kernelOffsetSum);
                var tb = ((b / kernelFactorSum) + kernelOffsetSum);

                // Clamp to byte boundaries
                var ba = (byte)((ta > 255) ? 255 : ((ta < 0) ? 0 : ta));
                var br = (byte)((tr > 255) ? 255 : ((tr < 0) ? 0 : tr));
                var bg = (byte)((tg > 255) ? 255 : ((tg < 0) ? 0 : tg));
                var bb = (byte)((tb > 255) ? 255 : ((tb < 0) ? 0 : tb));

                resultPixels[index++] = (ba << 24) | (br << 16) | (bg << 8) | (bb);
            }
        }
        return result;
    }

    #endregion

    #region Invert

    /// <summary>
    /// Creates a new inverted WriteableBitmap and returns it.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <returns>The new inverted WriteableBitmap.</returns>
    public static WriteableBitmap Invert(this WriteableBitmap bmp)
    {
        using var srcContext = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var result = XWriteableBitmap.New(srcContext.Width, srcContext.Height);
        using var resultContext = result.GetContext();
        var rp = resultContext.Pixels;
        var p = srcContext.Pixels;
        var length = srcContext.Length;

        for (var i = 0; i < length; i++)
        {
            // Extract
            var c = p[i];
            var a = (c >> 24) & 0x000000FF;
            var r = (c >> 16) & 0x000000FF;
            var g = (c >> 8) & 0x000000FF;
            var b = (c) & 0x000000FF;

            // Invert
            r = 255 - r;
            g = 255 - g;
            b = 255 - b;

            // Set
            rp[i] = (a << 24) | (r << 16) | (g << 8) | b;
        }

        return result;
    }

    #endregion

    #region Color transformations

    /// <summary>
    /// Creates a new WriteableBitmap which is the grayscaled version of this one and returns it. The gray values are equal to the brightness values. 
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <returns>The new gray WriteableBitmap.</returns>
    public static WriteableBitmap Gray(this WriteableBitmap bmp)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var nWidth = context.Width;
        var nHeight = context.Height;
        var px = context.Pixels;
        var result = XWriteableBitmap.New(nWidth, nHeight);

        using (var dest = result.GetContext())
        {
            var rp = dest.Pixels;
            var len = context.Length;
            for (var i = 0; i < len; i++)
            {
                // Extract
                var c = px[i];
                var a = (c >> 24) & 0x000000FF;
                var r = (c >> 16) & 0x000000FF;
                var g = (c >> 8) & 0x000000FF;
                var b = (c) & 0x000000FF;

                // Convert to gray with constant factors 0.2126, 0.7152, 0.0722
                var gray = (r * 6966 + g * 23436 + b * 2366) >> 15;
                r = g = b = gray;

                // Set
                rp[i] = (a << 24) | (r << 16) | (g << 8) | b;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a new WriteableBitmap which is contrast adjusted version of this one and returns it.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="level">Level of contrast as double. [-255.0, 255.0] </param>
    /// <returns>The new WriteableBitmap.</returns>
    public static WriteableBitmap AdjustContrast(this WriteableBitmap bmp, double level)
    {
        var factor = (int)((259.0 * (level + 255.0)) / (255.0 * (259.0 - level)) * 255.0);

        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var nWidth = context.Width;
        var nHeight = context.Height;
        var px = context.Pixels;
        var result = XWriteableBitmap.New(nWidth, nHeight);

        using (var dest = result.GetContext())
        {
            var rp = dest.Pixels;
            var len = context.Length;
            for (var i = 0; i < len; i++)
            {
                // Extract
                var c = px[i];
                var a = (c >> 24) & 0x000000FF;
                var r = (c >> 16) & 0x000000FF;
                var g = (c >> 8) & 0x000000FF;
                var b = (c) & 0x000000FF;

                // Adjust contrast based on computed factor
                r = ((factor * (r - 128)) >> 8) + 128;
                g = ((factor * (g - 128)) >> 8) + 128;
                b = ((factor * (b - 128)) >> 8) + 128;

                // Clamp
                r = r < 0 ? 0 : r > 255 ? 255 : r;
                g = g < 0 ? 0 : g > 255 ? 255 : g;
                b = b < 0 ? 0 : b > 255 ? 255 : b;

                // Set
                rp[i] = (a << 24) | (r << 16) | (g << 8) | b;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a new WriteableBitmap which is brightness adjusted version of this one and returns it.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="nLevel">Level of contrast as double. [-255.0, 255.0] </param>
    /// <returns>The new WriteableBitmap.</returns>
    public static WriteableBitmap AdjustBrightness(this WriteableBitmap bmp, int nLevel)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var nWidth = context.Width;
        var nHeight = context.Height;
        var px = context.Pixels;
        var result = XWriteableBitmap.New(nWidth, nHeight);

        using (var dest = result.GetContext())
        {
            var rp = dest.Pixels;
            var len = context.Length;
            for (var i = 0; i < len; i++)
            {
                // Extract
                var c = px[i];
                var a = (c >> 24) & 0x000000FF;
                var r = (c >> 16) & 0x000000FF;
                var g = (c >> 8) & 0x000000FF;
                var b = (c) & 0x000000FF;

                // Brightness adjustment
                r += nLevel;
                g += nLevel;
                b += nLevel;

                // Clamp
                r = r < 0 ? 0 : r > 255 ? 255 : r;
                g = g < 0 ? 0 : g > 255 ? 255 : g;
                b = b < 0 ? 0 : b > 255 ? 255 : b;

                // Set
                rp[i] = (a << 24) | (r << 16) | (g << 8) | b;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a new WriteableBitmap which is gamma adjusted version of this one and returns it.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="value">Value of gamma for adjustment. Original is 1.0.</param>
    /// <returns>The new WriteableBitmap.</returns>
    public static WriteableBitmap AdjustGamma(this WriteableBitmap bmp, double value)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var nWidth = context.Width;
        var nHeight = context.Height;
        var px = context.Pixels;
        var result = XWriteableBitmap.New(nWidth, nHeight);

        using (var dest = result.GetContext())
        {
            var rp = dest.Pixels;
            var gammaCorrection = 1.0 / value;
            var len = context.Length;
            for (var i = 0; i < len; i++)
            {
                // Extract
                var c = px[i];
                var a = (c >> 24) & 0x000000FF;
                var r = (c >> 16) & 0x000000FF;
                var g = (c >> 8) & 0x000000FF;
                var b = (c) & 0x000000FF;

                // Gamma adjustment
                r = (int)(255.0 * Math.Pow((r / 255.0), gammaCorrection));
                g = (int)(255.0 * Math.Pow((g / 255.0), gammaCorrection));
                b = (int)(255.0 * Math.Pow((b / 255.0), gammaCorrection));

                // Clamps
                r = r < 0 ? 0 : r > 255 ? 255 : r;
                g = g < 0 ? 0 : g > 255 ? 255 : g;
                b = b < 0 ? 0 : b > 255 ? 255 : b;

                // Set
                rp[i] = (a << 24) | (r << 16) | (g << 8) | b;
            }
        }

        return result;
    }

    #endregion

    #endregion

    #endregion

    #region ForEach

    /// <summary>
    /// Applies the given function to all the pixels of the bitmap in 
    /// order to set their color.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="func">The function to apply. With parameters x, y and a color as a result</param>
    unsafe public static void ForEach(this WriteableBitmap bmp, Func<int, int, System.Windows.Media.Color> func)
    {
        using var context = bmp.GetContext();
        var pixels = context.Pixels;
        int w = context.Width;
        int h = context.Height;
        int index = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var color = func(x, y);
                pixels[index++] = GetColor(color).Encode();
            }
        }
    }

    /// <summary>
    /// Applies the given function to all the pixels of the bitmap in 
    /// order to set their color.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="func">The function to apply. With parameters x, y, source color and a color as a result</param>
    unsafe public static void ForEach(this WriteableBitmap bmp, Func<int, int, System.Windows.Media.Color, System.Windows.Media.Color> func)
    {
        using var context = bmp.GetContext();
        var pixels = context.Pixels;
        var w = context.Width;
        var h = context.Height;
        var index = 0;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var c = pixels[index];

                // Premultiplied Alpha!
                var a = (byte)(c >> 24);
                // Prevent division by zero
                int ai = a;
                if (ai == 0)
                {
                    ai = 1;
                }
                // Scale inverse alpha to use cheap integer mul bit shift
                ai = ((255 << 8) / ai);
                var srcColor = System.Windows.Media.Color.FromArgb(a,
                                                (byte)((((c >> 16) & 0xFF) * ai) >> 8),
                                                (byte)((((c >> 8) & 0xFF) * ai) >> 8),
                                                (byte)((((c & 0xFF) * ai) >> 8)));

                var color = func(x, y, srcColor);
                pixels[index++] = GetColor(color).Encode();
            }
        }
    }

    #endregion

    #region From
#if !NETFX_CORE
    /// <summary>
    /// Loads an image from the applications resource file and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="relativePath">Only the relative path to the resource file. The assembly name is retrieved automatically.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromResource instead of this FromResource method.")]
    public static WriteableBitmap FromResource(this WriteableBitmap bmp, string relativePath)
    {
        return XWriteableBitmap.Convert(relativePath);
    }
#endif

#if NETFX_CORE
    /// <summary>
    /// Loads an image from the applications content and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="uri">The URI to the content file.</param>
    /// <param name="pixelFormat">The pixel format of the stream data. If Unknown is provided as param, the default format of the BitmapDecoder is used.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromContent instead of this FromContent method.")]
    public static Task<WriteableBitmap> FromContent(this WriteableBitmap bmp, Uri uri, BitmapPixelFormat pixelFormat = BitmapPixelFormat.Unknown)
    {
        return BitmapFactory.FromContent(uri, pixelFormat);
    }

    /// <summary>
    /// Loads the data from an image stream and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="stream">The stream with the image data.</param>
    /// <param name="pixelFormat">The pixel format of the stream data. If Unknown is provided as param, the default format of the BitmapDecoder is used.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromStream instead of this FromStream method.")]
    public static Task<WriteableBitmap> FromStream(this WriteableBitmap bmp, Stream stream, BitmapPixelFormat pixelFormat = BitmapPixelFormat.Unknown)
    {
        return BitmapFactory.FromStream(stream, pixelFormat);
    }

    /// <summary>
    /// Loads the data from an image stream and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="stream">The stream with the image data.</param>
    /// <param name="pixelFormat">The pixel format of the stream data. If Unknown is provided as param, the default format of the BitmapDecoder is used.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromStream instead of this FromStream method.")]
    public static Task<WriteableBitmap> FromStream(this WriteableBitmap bmp, IRandomAccessStream stream, BitmapPixelFormat pixelFormat = BitmapPixelFormat.Unknown)
    {
        return BitmapFactory.FromStream(stream, pixelFormat);
    }

    /// <summary>
    /// Encodes the data from a WriteableBitmap into a stream.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="destinationStream">The stream which will take the image data.</param>
    /// <param name="encoderId">The encoder GUID to use like BitmapEncoder.JpegEncoderId etc.</param>
    public static async Task ToStream(this WriteableBitmap bmp, IRandomAccessStream destinationStream, Guid encoderId)
    {
        // Copy buffer to pixels
        byte[] pixels;
        using (var stream = bmp.PixelBuffer.AsStream())
        {
            pixels = new byte[(uint)stream.Length];
            await stream.ReadAsync(pixels, 0, pixels.Length);
        }

        // Encode pixels into stream
        var encoder = await BitmapEncoder.CreateAsync(encoderId, destinationStream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bmp.PixelWidth, (uint)bmp.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }

    /// <summary>
    /// Encodes the data from a WriteableBitmap as JPEG into a stream.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="destinationStream">The stream which will take the JPEG image data.</param>
    public static async Task ToStreamAsJpeg(this WriteableBitmap bmp, IRandomAccessStream destinationStream)
    {
        await ToStream(bmp, destinationStream, BitmapEncoder.JpegEncoderId);
    }

    /// <summary>
    /// Loads the data from a pixel buffer like the RenderTargetBitmap provides and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="pixelBuffer">The source pixel buffer with the image data.</param>
    /// <param name="width">The width of the image data.</param>
    /// <param name="height">The height of the image data.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromPixelBuffer instead of this FromPixelBuffer method.")]
    public static Task<WriteableBitmap> FromPixelBuffer(this WriteableBitmap bmp, IBuffer pixelBuffer, int width, int height)
    {
        return BitmapFactory.FromPixelBuffer(pixelBuffer, width, height);
    }
#else
    /// <summary>
    /// Loads an image from the applications content and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="relativePath">Only the relative path to the content file.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromContent instead of this FromContent method.")]
    public static WriteableBitmap FromContent(this WriteableBitmap bmp, string relativePath)
    {
        return XWriteableBitmap.Convert(relativePath);
    }

    /// <summary>
    /// Loads the data from an image stream and returns a new WriteableBitmap. The passed WriteableBitmap is not used.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="stream">The stream with the image data.</param>
    /// <returns>A new WriteableBitmap containing the pixel data.</returns>
    [Obsolete("Please use BitmapFactory.FromStream instead of this FromStream method.")]
    public static WriteableBitmap FromStream(this WriteableBitmap bmp, Stream stream)
    {
        return XWriteableBitmap.Convert(stream);
    }
#endif

    #endregion

    #region From/ToByteArray

    /// <summary>
    /// Copies color information from an ARGB byte array into this WriteableBitmap starting at a specific buffer index.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="offset">The starting index in the buffer.</param>
    /// <param name="count">The number of bytes to copy from the buffer.</param>
    /// <param name="buffer">The color buffer as byte ARGB values.</param>
    /// <returns>The WriteableBitmap that was passed as parameter.</returns>
    public static WriteableBitmap FromByteArray(this WriteableBitmap bmp, byte[] buffer, int offset, int count)
    {
        using var context = bmp.GetContext();
        BitmapContext.BlockCopy(buffer, offset, context, 0, count);
        return bmp;
    }

    /// <summary>
    /// Copies color information from an ARGB byte array into this WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="count">The number of bytes to copy from the buffer.</param>
    /// <param name="buffer">The color buffer as byte ARGB values.</param>
    /// <returns>The WriteableBitmap that was passed as parameter.</returns>
    public static WriteableBitmap FromByteArray(this WriteableBitmap bmp, byte[] buffer, int count)
    {
        return bmp.FromByteArray(buffer, 0, count);
    }

    /// <summary>
    /// Copies all the color information from an ARGB byte array into this WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="buffer">The color buffer as byte ARGB values.</param>
    /// <returns>The WriteableBitmap that was passed as parameter.</returns>
    public static WriteableBitmap FromByteArray(this WriteableBitmap bmp, byte[] buffer)
    {
        return bmp.FromByteArray(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Copies the Pixels from the WriteableBitmap into a ARGB byte array starting at a specific Pixels index.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="offset">The starting Pixels index.</param>
    /// <param name="count">The number of Pixels to copy, -1 for all</param>
    /// <returns>The color buffer as byte ARGB values.</returns>
    public static byte[] ToByteArray(this WriteableBitmap bmp, int offset, int count)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        if (count == -1)
        {
            // Copy all to byte array
            count = context.Length;
        }

        var len = count * XWriteableBitmap.SizeOfArgb;
        var result = new byte[len]; // ARGB
        BitmapContext.BlockCopy(context, offset, result, 0, len);
        return result;
    }

    /// <summary>
    /// Copies the Pixels from the WriteableBitmap into a ARGB byte array.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="count">The number of pixels to copy.</param>
    /// <returns>The color buffer as byte ARGB values.</returns>
    public static byte[] ToByteArray(this WriteableBitmap bmp, int count)
    {
        return bmp.ToByteArray(0, count);
    }

    /// <summary>
    /// Copies all the Pixels from the WriteableBitmap into a ARGB byte array.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <returns>The color buffer as byte ARGB values.</returns>
    public static byte[] ToByteArray(this WriteableBitmap bmp)
    {
        return bmp.ToByteArray(0, -1);
    }

    #endregion

    #region Get

    /// <summary>Gets a BitmapContext within which to perform nested IO operations on the bitmap.</summary>
    /// <remarks>For WPF the BitmapContext will lock the bitmap. Call Dispose on the context to unlock</remarks>
    /// <param name="i"></param>
    /// <returns></returns>
    public static BitmapContext GetContext(this WriteableBitmap i) => new(i);

    /// <summary>Gets a BitmapContext within which to perform nested IO operations on the bitmap.</summary>
    /// <remarks>For WPF the BitmapContext will lock the bitmap. Call Dispose on the context to unlock</remarks>
    /// <param name="i">The bitmap.</param>
    /// <param name="mode">The ReadWriteMode. If set to ReadOnly, the bitmap will not be invalidated on dispose of the context, else it will</param>
    /// <returns></returns>
    public static BitmapContext GetContext(this WriteableBitmap i, ReadWriteMode mode) => new(i, mode);

    /// <summary>
    /// Gets the color of the pixel at the x, y coordinate as integer.  
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate of the pixel.</param>
    /// <param name="y">The y coordinate of the pixel.</param>
    /// <returns>The color of the pixel at x, y.</returns>
    unsafe public static int GetPixeli(this WriteableBitmap bmp, int x, int y)
    {
        using var context = bmp.GetContext(ReadWriteMode.ReadOnly);
        return context.Pixels[y * context.Width + x];
    }

    /// <summary>
    /// Gets the color of the pixel at the x, y coordinate as a Color struct.  
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate of the pixel.</param>
    /// <param name="y">The y coordinate of the pixel.</param>
    /// <returns>The color of the pixel at x, y as a Color struct.</returns>
    unsafe public static System.Windows.Media.Color GetPixel(this WriteableBitmap bmp, int x, int y)
    {
        using var context = bmp.GetContext(ReadWriteMode.ReadOnly);
        var c = context.Pixels[y * context.Width + x];
        var a = (byte)(c >> 24);

        // Prevent division by zero
        int ai = a;
        if (ai == 0)
        {
            ai = 1;
        }

        // Scale inverse alpha to use cheap integer mul bit shift
        ai = ((255 << 8) / ai);
        return System.Windows.Media.Color.FromArgb(a,
            (byte)((((c >> 16) & 0xFF) * ai) >> 8),
            (byte)((((c >> 8) & 0xFF) * ai) >> 8),
            (byte)((((c & 0xFF) * ai) >> 8)));
    }

    public static List<System.Windows.Media.Color> GetUnique(this WriteableBitmap i)
    {
        var result = new List<System.Windows.Media.Color>();
        i.ForEach((x, y, color) =>
        {
            if (!result.Contains(color))
                result.Add(color);

            return color;
        });
        return result;
    }

    #endregion

    #region Line

    #region Normal line

    /// <summary>
    /// Draws a colored line by connecting two points using the Bresenham algorithm.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLineBresenham(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
    {
        var col = GetColor(color).Encode();
        bmp.DrawLineBresenham(x1, y1, x2, y2, col, clipRect);
    }

    /// <summary>
    /// Draws a colored line by connecting two points using the Bresenham algorithm.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLineBresenham(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;

        // Get clip coordinates
        int clipX1 = 0;
        int clipX2 = w;
        int clipY1 = 0;
        int clipY2 = h;
        if (clipRect.HasValue)
        {
            var c = clipRect.Value;
            clipX1 = (int)c.X;
            clipX2 = (int)(c.X + c.Width);
            clipY1 = (int)c.Y;
            clipY2 = (int)(c.Y + c.Height);
        }

        // Distance start and end point
        int dx = x2 - x1;
        int dy = y2 - y1;

        // Determine sign for direction x
        int incx = 0;
        if (dx < 0)
        {
            dx = -dx;
            incx = -1;
        }
        else if (dx > 0)
        {
            incx = 1;
        }

        // Determine sign for direction y
        int incy = 0;
        if (dy < 0)
        {
            dy = -dy;
            incy = -1;
        }
        else if (dy > 0)
        {
            incy = 1;
        }

        // Which gradient is larger
        int pdx, pdy, odx, ody, es, el;
        if (dx > dy)
        {
            pdx = incx;
            pdy = 0;
            odx = incx;
            ody = incy;
            es = dy;
            el = dx;
        }
        else
        {
            pdx = 0;
            pdy = incy;
            odx = incx;
            ody = incy;
            es = dx;
            el = dy;
        }

        // Init start
        int x = x1;
        int y = y1;
        int error = el >> 1;
        if (y < clipY2 && y >= clipY1 && x < clipX2 && x >= clipX1)
        {
            pixels[y * w + x] = color;
        }

        // Walk the line!
        for (int i = 0; i < el; i++)
        {
            // Update error term
            error -= es;

            // Decide which coord to use
            if (error < 0)
            {
                error += el;
                x += odx;
                y += ody;
            }
            else
            {
                x += pdx;
                y += pdy;
            }

            // Set pixel
            if (y < clipY2 && y >= clipY1 && x < clipX2 && x >= clipX1)
            {
                pixels[y * w + x] = color;
            }
        }
    }

    /// <summary>
    /// Draws a colored line by connecting two points using a DDA algorithm (Digital Differential Analyzer).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLineDDA(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
    {
        var col = GetColor(color).Encode();
        bmp.DrawLineDDA(x1, y1, x2, y2, col, clipRect);
    }

    /// <summary>
    /// Draws a colored line by connecting two points using a DDA algorithm (Digital Differential Analyzer).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLineDDA(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;

        // Get clip coordinates
        int clipX1 = 0;
        int clipX2 = w;
        int clipY1 = 0;
        int clipY2 = h;
        if (clipRect.HasValue)
        {
            var c = clipRect.Value;
            clipX1 = (int)c.X;
            clipX2 = (int)(c.X + c.Width);
            clipY1 = (int)c.Y;
            clipY2 = (int)(c.Y + c.Height);
        }

        // Distance start and end point
        int dx = x2 - x1;
        int dy = y2 - y1;

        // Determine slope (absolute value)
        int len = dy >= 0 ? dy : -dy;
        int lenx = dx >= 0 ? dx : -dx;
        if (lenx > len)
        {
            len = lenx;
        }

        // Prevent division by zero
        if (len != 0)
        {
            // Init steps and start
            float incx = dx / (float)len;
            float incy = dy / (float)len;
            float x = x1;
            float y = y1;

            // Walk the line!
            for (int i = 0; i < len; i++)
            {
                if (y < clipY2 && y >= clipY1 && x < clipX2 && x >= clipX1)
                {
                    pixels[(int)y * w + (int)x] = color;
                }
                x += incx;
                y += incy;
            }
        }
    }

    /// <summary>
    /// Draws a colored line by connecting two points using an optimized DDA.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLine(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
    {
        var col = GetColor(color).Encode();
        bmp.DrawLine(x1, y1, x2, y2, col, clipRect);
    }

    /// <summary>
    /// Draws a colored line by connecting two points using an optimized DDA.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLine(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        DrawLine(context, context.Width, context.Height, x1, y1, x2, y2, color, clipRect);
    }

    /// <summary>
    /// Draws a colored line by connecting two points using an optimized DDA. 
    /// Uses the pixels array and the width directly for best performance.
    /// </summary>
    /// <param name="context">The context containing the pixels as int RGBA value.</param>
    /// <param name="pixelWidth">The width of one scanline in the pixels array.</param>
    /// <param name="pixelHeight">The height of the bitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLine(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
    {
        // Get clip coordinates
        int clipX1 = 0;
        int clipX2 = pixelWidth;
        int clipY1 = 0;
        int clipY2 = pixelHeight;
        if (clipRect.HasValue)
        {
            var c = clipRect.Value;
            clipX1 = (int)c.X;
            clipX2 = (int)(c.X + c.Width);
            clipY1 = (int)c.Y;
            clipY2 = (int)(c.Y + c.Height);
        }

        // Perform cohen-sutherland clipping if either point is out of the viewport
        if (!CohenSutherlandLineClip(new Rect(clipX1, clipY1, clipX2 - clipX1, clipY2 - clipY1), ref x1, ref y1, ref x2, ref y2)) return;

        var pixels = context.Pixels;

        // Distance start and end point
        int dx = x2 - x1;
        int dy = y2 - y1;

        const int PRECISION_SHIFT = 8;

        // Determine slope (absolute value)
        int lenX, lenY;
        if (dy >= 0)
        {
            lenY = dy;
        }
        else
        {
            lenY = -dy;
        }

        if (dx >= 0)
        {
            lenX = dx;
        }
        else
        {
            lenX = -dx;
        }

        if (lenX > lenY)
        { // x increases by +/- 1
            if (dx < 0)
            {
                (x2, x1) = (x1, x2);
                (y2, y1) = (y1, y2);
            }

            // Init steps and start
            int incy = (dy << PRECISION_SHIFT) / dx;

            int y1s = y1 << PRECISION_SHIFT;
            int y2s = y2 << PRECISION_SHIFT;
            int hs = pixelHeight << PRECISION_SHIFT;

            if (y1 < y2)
            {
                if (y1 >= clipY2 || y2 < clipY1)
                {
                    return;
                }
                if (y1s < 0)
                {
                    if (incy == 0)
                    {
                        return;
                    }
                    int oldy1s = y1s;
                    // Find lowest y1s that is greater or equal than 0.
                    y1s = incy - 1 + ((y1s + 1) % incy);
                    x1 += (y1s - oldy1s) / incy;
                }
                if (y2s >= hs)
                {
                    if (incy != 0)
                    {
                        // Find highest y2s that is less or equal than ws - 1.
                        // y2s = y1s + n * incy. Find n.
                        y2s = hs - 1 - (hs - 1 - y1s) % incy;
                        x2 = x1 + (y2s - y1s) / incy;
                    }
                }
            }
            else
            {
                if (y2 >= clipY2 || y1 < clipY1)
                {
                    return;
                }
                if (y1s >= hs)
                {
                    if (incy == 0)
                    {
                        return;
                    }
                    int oldy1s = y1s;
                    // Find highest y1s that is less or equal than ws - 1.
                    // y1s = oldy1s + n * incy. Find n.
                    y1s = hs - 1 + (incy - (hs - 1 - oldy1s) % incy);
                    x1 += (y1s - oldy1s) / incy;
                }
                if (y2s < 0)
                {
                    if (incy != 0)
                    {
                        // Find lowest y2s that is greater or equal than 0.
                        // y2s = y1s + n * incy. Find n.
                        y2s = y1s % incy;
                        x2 = x1 + (y2s - y1s) / incy;
                    }
                }
            }

            if (x1 < 0)
            {
                y1s -= incy * x1;
                x1 = 0;
            }
            if (x2 >= pixelWidth)
            {
                x2 = pixelWidth - 1;
            }

            int ys = y1s;

            // Walk the line!
            int y = ys >> PRECISION_SHIFT;
            int previousY = y;
            int index = x1 + y * pixelWidth;
            int k = incy < 0 ? 1 - pixelWidth : 1 + pixelWidth;
            for (int x = x1; x <= x2; ++x)
            {
                pixels[index] = color;
                ys += incy;
                y = ys >> PRECISION_SHIFT;
                if (y != previousY)
                {
                    previousY = y;
                    index += k;
                }
                else
                {
                    ++index;
                }
            }
        }
        else
        {
            // Prevent division by zero
            if (lenY == 0)
            {
                return;
            }
            if (dy < 0)
            {
                (x2, x1) = (x1, x2);
                (y2, y1) = (y1, y2);
            }

            // Init steps and start
            int x1s = x1 << PRECISION_SHIFT;
            int x2s = x2 << PRECISION_SHIFT;
            int ws = pixelWidth << PRECISION_SHIFT;

            int incx = (dx << PRECISION_SHIFT) / dy;

            if (x1 < x2)
            {
                if (x1 >= clipX2 || x2 < clipX1)
                {
                    return;
                }
                if (x1s < 0)
                {
                    if (incx == 0)
                    {
                        return;
                    }
                    int oldx1s = x1s;
                    // Find lowest x1s that is greater or equal than 0.
                    x1s = incx - 1 + ((x1s + 1) % incx);
                    y1 += (x1s - oldx1s) / incx;
                }
                if (x2s >= ws)
                {
                    if (incx != 0)
                    {
                        // Find highest x2s that is less or equal than ws - 1.
                        // x2s = x1s + n * incx. Find n.
                        x2s = ws - 1 - (ws - 1 - x1s) % incx;
                        y2 = y1 + (x2s - x1s) / incx;
                    }
                }
            }
            else
            {
                if (x2 >= clipX2 || x1 < clipX1)
                {
                    return;
                }
                if (x1s >= ws)
                {
                    if (incx == 0)
                    {
                        return;
                    }
                    int oldx1s = x1s;
                    // Find highest x1s that is less or equal than ws - 1.
                    // x1s = oldx1s + n * incx. Find n.
                    x1s = ws - 1 + (incx - (ws - 1 - oldx1s) % incx);
                    y1 += (x1s - oldx1s) / incx;
                }
                if (x2s < 0)
                {
                    if (incx != 0)
                    {
                        // Find lowest x2s that is greater or equal than 0.
                        // x2s = x1s + n * incx. Find n.
                        x2s = x1s % incx;
                        y2 = y1 + (x2s - x1s) / incx;
                    }
                }
            }

            if (y1 < 0)
            {
                x1s -= incx * y1;
                y1 = 0;
            }
            if (y2 >= pixelHeight)
            {
                y2 = pixelHeight - 1;
            }

            int index = x1s;
            int indexBaseValue = y1 * pixelWidth;

            // Walk the line!
            var inc = (pixelWidth << PRECISION_SHIFT) + incx;
            for (int y = y1; y <= y2; ++y)
            {
                pixels[indexBaseValue + (index >> PRECISION_SHIFT)] = color;
                index += inc;
            }
        }
    }
    #endregion

    #region Penned line

    /// <summary>
    /// Bitfields used to partition the space into 9 regions
    /// </summary>
    private const byte INSIDE = 0; // 0000
    private const byte LEFT = 1;   // 0001
    private const byte RIGHT = 2;  // 0010
    private const byte BOTTOM = 4; // 0100
    private const byte TOP = 8;    // 1000

    /// <summary>
    /// Draws a line using a pen / stamp for the line 
    /// </summary>
    /// <param name="bmp">The WriteableBitmap containing the pixels as int RGBA value.</param>
    /// <param name="w">The width of one scanline in the pixels array.</param>
    /// <param name="h">The height of the bitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="penBmp">The pen bitmap.</param>
    public static void DrawLinePenned(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, WriteableBitmap penBmp, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        using var penContext = penBmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        DrawLinePenned(context, context.Width, context.Height, x1, y1, x2, y2, penContext, clipRect);
    }

    /// <summary>
    /// Draws a line using a pen / stamp for the line 
    /// </summary>
    /// <param name="context">The context containing the pixels as int RGBA value.</param>
    /// <param name="w">The width of one scanline in the pixels array.</param>
    /// <param name="h">The height of the bitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="pen">The pen context.</param>
    public static void DrawLinePenned(BitmapContext context, int w, int h, int x1, int y1, int x2, int y2, BitmapContext pen, Rect? clipRect = null)
    {
        // Edge case where lines that went out of vertical bounds clipped instead of disappearing
        if ((y1 < 0 && y2 < 0) || (y1 > h && y2 > h))
            return;

        if (x1 == x2 && y1 == y2)
            return;

        // Perform cohen-sutherland clipping if either point is out of the viewport
        if (!CohenSutherlandLineClip(clipRect ?? new Rect(0, 0, w, h), ref x1, ref y1, ref x2, ref y2)) return;

        int size = pen.Width;
        int pw = size;
        var srcRect = new Rect(0, 0, size, size);

        // Distance start and end point
        int dx = x2 - x1;
        int dy = y2 - y1;

        // Determine sign for direction x
        int incx = 0;
        if (dx < 0)
        {
            dx = -dx;
            incx = -1;
        }
        else if (dx > 0)
        {
            incx = 1;
        }

        // Determine sign for direction y
        int incy = 0;
        if (dy < 0)
        {
            dy = -dy;
            incy = -1;
        }
        else if (dy > 0)
        {
            incy = 1;
        }

        // Which gradient is larger
        int pdx, pdy, odx, ody, es, el;
        if (dx > dy)
        {
            pdx = incx;
            pdy = 0;
            odx = incx;
            ody = incy;
            es = dy;
            el = dx;
        }
        else
        {
            pdx = 0;
            pdy = incy;
            odx = incx;
            ody = incy;
            es = dx;
            el = dy;
        }

        // Init start
        int x = x1;
        int y = y1;
        int error = el >> 1;

        var destRect = new Rect(x, y, size, size);

        if (y < h && y >= 0 && x < w && x >= 0)
        {
            //Blit(context.WriteableBitmap, new Rect(x,y,3,3), pen.WriteableBitmap, new Rect(0,0,3,3));
            Blit(context, w, h, destRect, pen, srcRect, pw);
            //pixels[y * w + x] = color;
        }

        // Walk the line!
        for (int i = 0; i < el; i++)
        {
            // Update error term
            error -= es;

            // Decide which coord to use
            if (error < 0)
            {
                error += el;
                x += odx;
                y += ody;
            }
            else
            {
                x += pdx;
                y += pdy;
            }

            // Set pixel
            if (y < h && y >= 0 && x < w && x >= 0)
            {
                //Blit(context, w, h, destRect, pen, srcRect, pw);
                Blit(context, w, h, new Rect(x, y, size, size), pen, srcRect, pw);
                //Blit(context.WriteableBitmap, destRect, pen.WriteableBitmap, srcRect);
                //pixels[y * w + x] = color;
            }
        }
    }

    /// <summary>
    /// Compute the bit code for a point (x, y) using the clip rectangle
    /// bounded diagonally by (xmin, ymin), and (xmax, ymax)
    /// ASSUME THAT xmax , xmin , ymax and ymin are global constants.
    /// </summary>
    /// <param name="extents">The extents.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns></returns>
    private static byte ComputeOutCode(Rect extents, double x, double y)
    {
        // initialized as being inside of clip window
        byte code = INSIDE;

        if (x < extents.Left)           // to the left of clip window
            code |= LEFT;
        else if (x > extents.Right)     // to the right of clip window
            code |= RIGHT;
        if (y > extents.Bottom)         // below the clip window
            code |= BOTTOM;
        else if (y < extents.Top)       // above the clip window
            code |= TOP;

        return code;
    }

    #endregion

    #region Dotted Line
    /// <summary>
    /// Draws a colored dotted line
    /// </summary>
    /// <param name="bmp">The WriteableBitmap</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="dotSpace">length of space between each line segment</param>
    /// <param name="dotLength">length of each line segment</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawLineDotted(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int dotSpace, int dotLength, Color color)
    {
        var c = GetColor(color).Encode();
        DrawLineDotted(bmp, x1, y1, x2, y2, dotSpace, dotLength, c);
    }
    /// <summary>
    /// Draws a colored dotted line
    /// </summary>
    /// <param name="bmp">The WriteableBitmap</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="dotSpace">length of space between each line segment</param>
    /// <param name="dotLength">length of each line segment</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawLineDotted(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int dotSpace, int dotLength, int color)
    {
        //if (x1 == 0) {
        //    x1 = 1;
        //}
        //if (x2 == 0) {
        //    x2 = 1;
        //}
        //if (y1 == 0) {
        //    y1 = 1;
        //}
        //if (y2 == 0) {
        //    y2 = 1;
        //}
        //if (x1 < 1 || x2 < 1 || y1 < 1 || y2 < 1 || dotSpace < 1 || dotLength < 1) {
        //    throw new ArgumentOutOfRangeException("Value must be larger than 0");
        //}
        // vertically and horizontally checks by themselves if coords are out of bounds, otherwise CohenSutherlandCLip is used

        // vertically?
        using var context = bmp.GetContext();
        if (x1 == x2)
        {
            SwapHorV(ref y1, ref y2);
            DrawVertically(context, x1, y1, y2, dotSpace, dotLength, color);
        }
        // horizontally?
        else if (y1 == y2)
        {
            SwapHorV(ref x1, ref x2);
            DrawHorizontally(context, x1, x2, y1, dotSpace, dotLength, color);
        }
        else
        {
            Draw(context, x1, y1, x2, y2, dotSpace, dotLength, color);
        }
    }

    private static void DrawVertically(BitmapContext context, int x, int y1, int y2, int dotSpace, int dotLength, int color)
    {
        int width = context.Width;
        int height = context.Height;

        if (x < 0 || x > width)
        {
            return;
        }

        var pixels = context.Pixels;
        bool on = true;
        int spaceCnt = 0;
        for (int i = y1; i <= y2; i++)
        {
            if (i < 1)
            {
                continue;
            }
            if (i >= height)
            {
                break;
            }

            if (on)
            {
                //bmp.SetPixel(x, i, color);
                //var idx = GetIndex(x, i, width);
                var idx = (i - 1) * width + x;
                pixels[idx] = color;
                on = i % dotLength != 0;
                spaceCnt = 0;
            }
            else
            {
                spaceCnt++;
                on = spaceCnt % dotSpace == 0;
            }
        }
    }

    private static void DrawHorizontally(BitmapContext context, int x1, int x2, int y, int dotSpace, int dotLength, int color)
    {
        int width = context.Width;
        int height = context.Height;

        if (y < 0 || y > height)
        {
            return;
        }

        var pixels = context.Pixels;
        bool on = true;
        int spaceCnt = 0;
        for (int i = x1; i <= x2; i++)
        {
            if (i < 1)
            {
                continue;
            }
            if (i >= width)
            {
                break;
            }
            if (y >= height)
            {
                break;
            }

            if (on)
            {
                //bmp.SetPixel(i, y, color);
                //var idx = GetIndex(i, y, width);
                var idx = y * width + i - 1;
                pixels[idx] = color;
                on = i % dotLength != 0;
                spaceCnt = 0;
            }
            else
            {
                spaceCnt++;
                on = spaceCnt % dotSpace == 0;
            }
        }
    }

    private static void Draw(BitmapContext context, int x1, int y1, int x2, int y2, int dotSpace, int dotLength, int color)
    {
        // y = m * x + n
        // y - m * x = n

        int width = context.Width;
        int height = context.Height;

        // Perform cohen-sutherland clipping if either point is out of the viewport
        if (!CohenSutherlandLineClip(new Rect(0, 0, width, height), ref x1, ref y1, ref x2, ref y2))
        {
            return;
        }
        Swap(ref x1, ref x2, ref y1, ref y2);
        float m = (y2 - y1) / (float)(x2 - x1);
        float n = y1 - m * x1;
        var pixels = context.Pixels;

        bool on = true;
        int spaceCnt = 0;
        for (int i = x1; i <= width; i++)
        {
            if (i == 0)
            {
                continue;
            }
            int y = (int)(m * i + n);
            if (y <= 0)
            {
                continue;
            }
            if (y >= height || i >= x2)
            {
                continue;
            }
            if (on)
            {
                //bmp.SetPixel(i, y, color);
                //var idx = GetIndex(i, y, width);
                var idx = (y - 1) * width + i - 1;
                pixels[idx] = color;
                spaceCnt = 0;
                on = i % dotLength != 0;
            }
            else
            {
                spaceCnt++;
                on = spaceCnt % dotSpace == 0;
            }
        }
    }

    private static void Swap(ref int x1, ref int x2, ref int y1, ref int y2)
    {
        // always draw from left to right
        // or from top to bottom
        if (x2 < x1)
        {
            int tmpx1 = x1;
            int tmpx2 = x2;
            int tmpy1 = y1;
            int tmpy2 = y2;
            x1 = tmpx2;
            y1 = tmpy2;
            x2 = tmpx1;
            y2 = tmpy1;
        }
    }

    private static void SwapHorV(ref int a1, ref int a2)
    {
        int x1 = 0; // dummy
        int x2 = -1; // dummy
        if (a2 < a1)
        {
            Swap(ref x1, ref x2, ref a1, ref a2);
        }
    }

    // inlined
    //private static int GetIndex(int x, int y, int width) {
    //    var idx = (y - 1) * width + x;
    //    return idx - 1;
    //}
    #endregion

    #region Anti-alias line

    /// <summary>
    /// Draws an anti-aliased, alpha blended, colored line by connecting two points using Wu's antialiasing algorithm
    /// Uses the pixels array and the width directly for best performance.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x0.</param>
    /// <param name="y1">The y0.</param>
    /// <param name="x2">The x1.</param>
    /// <param name="y2">The y1.</param>
    /// <param name="sa">Alpha color component</param>
    /// <param name="sr">Premultiplied red color component</param>
    /// <param name="sg">Premultiplied green color component</param>
    /// <param name="sb">Premultiplied blue color component</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLineWu(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int sa, int sr, int sg, int sb, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        DrawLineWu(context, context.Width, context.Height, x1, y1, x2, y2, sa, sr, sg, sb, clipRect);
    }

    /// <summary>
    /// Draws an anti-aliased, alpha-blended, colored line by connecting two points using Wu's antialiasing algorithm
    /// Uses the pixels array and the width directly for best performance.
    /// </summary>
    /// <param name="context">An array containing the pixels as int RGBA value.</param>
    /// <param name="pixelWidth">The width of one scanline in the pixels array.</param>
    /// <param name="pixelHeight">The height of the bitmap.</param>
    /// <param name="x1">The x0.</param>
    /// <param name="y1">The y0.</param>
    /// <param name="x2">The x1.</param>
    /// <param name="y2">The y1.</param>
    /// <param name="sa">Alpha color component</param>
    /// <param name="sr">Premultiplied red color component</param>
    /// <param name="sg">Premultiplied green color component</param>
    /// <param name="sb">Premultiplied blue color component</param>
    /// <param name="clipRect">The region in the image to restrict drawing to.</param>
    public static void DrawLineWu(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int sa, int sr, int sg, int sb, Rect? clipRect = null)
    {
        // Perform cohen-sutherland clipping if either point is out of the viewport
        if (!CohenSutherlandLineClip(clipRect ?? new Rect(0, 0, pixelWidth, pixelHeight), ref x1, ref y1, ref x2, ref y2)) return;

        var pixels = context.Pixels;

        const ushort INTENSITY_BITS = 8;
        const short NUM_LEVELS = 1 << INTENSITY_BITS; // 256
        // mask used to compute 1-value by doing (value XOR mask)
        const ushort WEIGHT_COMPLEMENT_MASK = NUM_LEVELS - 1; // 255
        // # of bits by which to shift ErrorAcc to get intensity level 
        const ushort INTENSITY_SHIFT = (ushort)(16 - INTENSITY_BITS); // 8

        ushort ErrorAdj, ErrorAcc;
        ushort ErrorAccTemp, Weighting;
        short DeltaX, DeltaY, XDir;
        int tmp;
        // ensure line runs from top to bottom
        if (y1 > y2)
        {
            tmp = y1; y1 = y2; y2 = tmp;
            tmp = x1; x1 = x2; x2 = tmp;
        }

        // draw initial pixel, which is always intersected by line to it's at 100% intensity
        pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
        //bitmap.SetPixel(X0, Y0, BaseColor);

        DeltaX = (short)(x2 - x1);
        if (DeltaX >= 0)
        {
            XDir = 1;
        }
        else
        {
            XDir = -1;
            DeltaX = (short)-DeltaX; /* make DeltaX positive */
        }

        // Special-case horizontal, vertical, and diagonal lines, which
        // require no weighting because they go right through the center of
        // every pixel; also avoids division by zero later
        DeltaY = (short)(y2 - y1);
        if (DeltaY == 0) // if horizontal line
        {
            while (DeltaX-- != 0)
            {
                x1 += XDir;
                pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
            }
            return;
        }

        if (DeltaX == 0) // if vertical line 
        {
            do
            {
                y1++;
                pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
            } while (--DeltaY != 0);
            return;
        }

        if (DeltaX == DeltaY) // diagonal line
        {
            do
            {
                x1 += XDir;
                y1++;
                pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
            } while (--DeltaY != 0);
            return;
        }

        // Line is not horizontal, diagonal, or vertical
        ErrorAcc = 0;  // initialize the line error accumulator to 0

        // Is this an X-major or Y-major line? 
        if (DeltaY > DeltaX)
        {
            // Y-major line; calculate 16-bit fixed-point fractional part of a
            // pixel that X advances each time Y advances 1 pixel, truncating the
            // result so that we won't overrun the endpoint along the X axis 
            ErrorAdj = (ushort)(((ulong)DeltaX << 16) / (ulong)DeltaY);

            // Draw all pixels other than the first and last 
            while (--DeltaY != 0)
            {
                ErrorAccTemp = ErrorAcc;   // remember current accumulated error 
                ErrorAcc += ErrorAdj;      // calculate error for next pixel 
                if (ErrorAcc <= ErrorAccTemp)
                {
                    // The error accumulator turned over, so advance the X coord */
                    x1 += XDir;
                }
                y1++; /* Y-major, so always advance Y */
                // The IntensityBits most significant bits of ErrorAcc give us the
                // intensity weighting for this pixel, and the complement of the
                // weighting for the paired pixel 
                Weighting = (ushort)(ErrorAcc >> INTENSITY_SHIFT);

                int weight = Weighting ^ WEIGHT_COMPLEMENT_MASK;
                pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, (sr * weight) >> 8, (sg * weight) >> 8, (sb * weight) >> 8, pixels[y1 * pixelWidth + x1]);

                weight = Weighting;
                pixels[y1 * pixelWidth + x1 + XDir] = AlphaBlend(sa, (sr * weight) >> 8, (sg * weight) >> 8, (sb * weight) >> 8, pixels[y1 * pixelWidth + x1 + XDir]);

                //bitmap.SetPixel(X0, Y0, 255 - (BaseColor + Weighting));
                //bitmap.SetPixel(X0 + XDir, Y0, 255 - (BaseColor + (Weighting ^ WeightingComplementMask)));
            }

            // Draw the final pixel, which is always exactly intersected by the line and so needs no weighting
            pixels[y2 * pixelWidth + x2] = AlphaBlend(sa, sr, sg, sb, pixels[y2 * pixelWidth + x2]);
            //bitmap.SetPixel(X1, Y1, BaseColor);
            return;
        }
        // It's an X-major line; calculate 16-bit fixed-point fractional part of a
        // pixel that Y advances each time X advances 1 pixel, truncating the
        // result to avoid overrunning the endpoint along the X axis */
        ErrorAdj = (ushort)(((ulong)DeltaY << 16) / (ulong)DeltaX);

        // Draw all pixels other than the first and last 
        while (--DeltaX != 0)
        {
            ErrorAccTemp = ErrorAcc;   // remember current accumulated error 
            ErrorAcc += ErrorAdj;      // calculate error for next pixel 
            if (ErrorAcc <= ErrorAccTemp) // if error accumulator turned over
            {
                // advance the Y coord
                y1++;
            }
            x1 += XDir; // X-major, so always advance X 
            // The IntensityBits most significant bits of ErrorAcc give us the
            // intensity weighting for this pixel, and the complement of the
            // weighting for the paired pixel 
            Weighting = (ushort)(ErrorAcc >> INTENSITY_SHIFT);

            int weight = Weighting ^ WEIGHT_COMPLEMENT_MASK;
            pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, (sr * weight) >> 8, (sg * weight) >> 8, (sb * weight) >> 8, pixels[y1 * pixelWidth + x1]);

            weight = Weighting;
            pixels[(y1 + 1) * pixelWidth + x1] = AlphaBlend(sa, (sr * weight) >> 8, (sg * weight) >> 8, (sb * weight) >> 8, pixels[(y1 + 1) * pixelWidth + x1]);

            //bitmap.SetPixel(X0, Y0, 255 - (BaseColor + Weighting));
            //bitmap.SetPixel(X0, Y0 + 1,
            //      255 - (BaseColor + (Weighting ^ WeightingComplementMask)));
        }
        // Draw the final pixel, which is always exactly intersected by the line and thus needs no weighting 
        pixels[y2 * pixelWidth + x2] = AlphaBlend(sa, sr, sg, sb, pixels[y2 * pixelWidth + x2]);
        //bitmap.SetPixel(X1, Y1, BaseColor);
    }

    /// <summary> 
    /// Draws an anti-aliased line with a desired stroke thickness
    /// <param name="context">The context containing the pixels as int RGBA value.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="strokeThickness">The stroke thickness of the line.</param>
    /// </summary>
    public static void DrawLineAa(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int color, int strokeThickness, Rect? clipRect = null)
    {
        AAWidthLine(pixelWidth, pixelHeight, context, x1, y1, x2, y2, strokeThickness, color, clipRect);
    }

    /// <summary> 
    /// Draws an anti-aliased line with a desired stroke thickness
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="strokeThickness">The stroke thickness of the line.</param>
    /// </summary>
    public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, int strokeThickness, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        AAWidthLine(context.Width, context.Height, context, x1, y1, x2, y2, strokeThickness, color, clipRect);
    }

    /// <summary> 
    /// Draws an anti-aliased line with a desired stroke thickness
    /// <param name="context">The context containing the pixels as int RGBA value.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="strokeThickness">The stroke thickness of the line.</param>
    /// </summary>
    public static void DrawLineAa(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, Color color, int strokeThickness, Rect? clipRect = null)
    {
        var col = GetColor(color).Encode();
        AAWidthLine(pixelWidth, pixelHeight, context, x1, y1, x2, y2, strokeThickness, col, clipRect);
    }

    /// <summary> 
    /// Draws an anti-aliased line with a desired stroke thickness
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// <param name="strokeThickness">The stroke thickness of the line.</param>
    /// </summary>
    public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, int strokeThickness, Rect? clipRect = null)
    {
        var col = GetColor(color).Encode();
        using var context = bmp.GetContext();
        AAWidthLine(context.Width, context.Height, context, x1, y1, x2, y2, strokeThickness, col, clipRect);
    }

    /// <summary> 
    /// Draws an anti-aliased line, using an optimized version of Gupta-Sproull algorithm 
    /// From http://nokola.com/blog/post/2010/10/14/Anti-aliased-Lines-And-Optimizing-Code-for-Windows-Phone-7e28093First-Look.aspx
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// </summary> 
    public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
    {
        var col = GetColor(color).Encode();
        bmp.DrawLineAa(x1, y1, x2, y2, col, clipRect);
    }

    /// <summary> 
    /// Draws an anti-aliased line, using an optimized version of Gupta-Sproull algorithm 
    /// From http://nokola.com/blog/post/2010/10/14/Anti-aliased-Lines-And-Optimizing-Code-for-Windows-Phone-7e28093First-Look.aspx
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// </summary> 
    public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
    {
        using var context = bmp.GetContext();
        DrawLineAa(context, context.Width, context.Height, x1, y1, x2, y2, color, clipRect);
    }

    /// <summary> 
    /// Draws an anti-aliased line, using an optimized version of Gupta-Sproull algorithm 
    /// From http://nokola.com/blog/post/2010/10/14/Anti-aliased-Lines-And-Optimizing-Code-for-Windows-Phone-7e28093First-Look.aspx
    /// <param name="context">The context containing the pixels as int RGBA value.</param>
    /// <param name="pixelWidth">The width of one scanline in the pixels array.</param>
    /// <param name="pixelHeight">The height of the bitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color for the line.</param>
    /// </summary> 
    public static void DrawLineAa(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
    {
        if ((x1 == x2) && (y1 == y2)) return; // edge case causing invDFloat to overflow, found by Shai Rubinshtein

        // Perform cohen-sutherland clipping if either point is out of the viewport
        if (!CohenSutherlandLineClip(clipRect ?? new Rect(0, 0, pixelWidth, pixelHeight), ref x1, ref y1, ref x2, ref y2)) return;

        if (x1 < 1) x1 = 1;
        if (x1 > pixelWidth - 2) x1 = pixelWidth - 2;
        if (y1 < 1) y1 = 1;
        if (y1 > pixelHeight - 2) y1 = pixelHeight - 2;

        if (x2 < 1) x2 = 1;
        if (x2 > pixelWidth - 2) x2 = pixelWidth - 2;
        if (y2 < 1) y2 = 1;
        if (y2 > pixelHeight - 2) y2 = pixelHeight - 2;

        var addr = y1 * pixelWidth + x1;
        var dx = x2 - x1;
        var dy = y2 - y1;

        int du;
        int dv;
        int u;
        int v;
        int uincr;
        int vincr;

        // Extract color
        var a = (color >> 24) & 0xFF;
        var srb = (uint)(color & 0x00FF00FF);
        var sg = (uint)((color >> 8) & 0xFF);

        // By switching to (u,v), we combine all eight octants 
        int adx = dx, ady = dy;
        if (dx < 0) adx = -dx;
        if (dy < 0) ady = -dy;

        if (adx > ady)
        {
            du = adx;
            dv = ady;
            u = x2;
            v = y2;
            uincr = 1;
            vincr = pixelWidth;
            if (dx < 0) uincr = -uincr;
            if (dy < 0) vincr = -vincr;
        }
        else
        {
            du = ady;
            dv = adx;
            u = y2;
            v = x2;
            uincr = pixelWidth;
            vincr = 1;
            if (dy < 0) uincr = -uincr;
            if (dx < 0) vincr = -vincr;
        }

        var uend = u + du;
        var d = (dv << 1) - du;        // Initial value as in Bresenham's 
        var incrS = dv << 1;    // &#916;d for straight increments 
        var incrD = (dv - du) << 1;    // &#916;d for diagonal increments

        var invDFloat = 1.0 / (4.0 * Math.Sqrt(du * du + dv * dv));   // Precomputed inverse denominator 
        var invD2DuFloat = 0.75 - 2.0 * (du * invDFloat);   // Precomputed constant

        const int PRECISION_SHIFT = 10; // result distance should be from 0 to 1 << PRECISION_SHIFT, mapping to a range of 0..1 
        const int PRECISION_MULTIPLIER = 1 << PRECISION_SHIFT;
        var invD = (int)(invDFloat * PRECISION_MULTIPLIER);
        var invD2Du = (int)(invD2DuFloat * PRECISION_MULTIPLIER * a);
        var zeroDot75 = (int)(0.75 * PRECISION_MULTIPLIER * a);

        var invDMulAlpha = invD * a;
        var duMulInvD = du * invDMulAlpha; // used to help optimize twovdu * invD 
        var dMulInvD = d * invDMulAlpha; // used to help optimize twovdu * invD 
        //int twovdu = 0;    // Numerator of distance; starts at 0 
        var twovduMulInvD = 0; // since twovdu == 0 
        var incrSMulInvD = incrS * invDMulAlpha;
        var incrDMulInvD = incrD * invDMulAlpha;

        do
        {
            AlphaBlendNormalOnPremultiplied(context, addr, (zeroDot75 - twovduMulInvD) >> PRECISION_SHIFT, srb, sg);
            AlphaBlendNormalOnPremultiplied(context, addr + vincr, (invD2Du + twovduMulInvD) >> PRECISION_SHIFT, srb, sg);
            AlphaBlendNormalOnPremultiplied(context, addr - vincr, (invD2Du - twovduMulInvD) >> PRECISION_SHIFT, srb, sg);

            if (d < 0)
            {
                // choose straight (u direction) 
                twovduMulInvD = dMulInvD + duMulInvD;
                d += incrS;
                dMulInvD += incrSMulInvD;
            }
            else
            {
                // choose diagonal (u+v direction) 
                twovduMulInvD = dMulInvD - duMulInvD;
                d += incrD;
                dMulInvD += incrDMulInvD;
                v++;
                addr += vincr;
            }
            u++;
            addr += uincr;
        } while (u <= uend);
    }

    /// <summary> 
    /// Blends a specific source color on top of a destination premultiplied color 
    /// </summary> 
    /// <param name="context">Array containing destination color</param> 
    /// <param name="index">Index of destination pixel</param> 
    /// <param name="sa">Source alpha (0..255)</param> 
    /// <param name="srb">Source non-premultiplied red and blue component in the format 0x00rr00bb</param> 
    /// <param name="sg">Source green component (0..255)</param> 
    private static void AlphaBlendNormalOnPremultiplied(BitmapContext context, int index, int sa, uint srb, uint sg)
    {
        var pixels = context.Pixels;
        var destPixel = (uint)pixels[index];

        var da = (destPixel >> 24);
        var dg = ((destPixel >> 8) & 0xff);
        var drb = destPixel & 0x00FF00FF;

        // blend with high-quality alpha and lower quality but faster 1-off RGBs 
        pixels[index] = (int)(
           ((sa + ((da * (255 - sa) * 0x8081) >> 23)) << 24) | // alpha 
           (((sg - dg) * sa + (dg << 8)) & 0xFFFFFF00) | // green 
           (((((srb - drb) * sa) >> 8) + drb) & 0x00FF00FF) // red and blue 
        );
    }

    #endregion

    #region Helper

    internal static bool CohenSutherlandLineClipWithViewPortOffset(Rect viewPort, ref float xi0, ref float yi0, ref float xi1, ref float yi1, int offset)
    {
        var viewPortWithOffset = new Rect(viewPort.X - offset, viewPort.Y - offset, viewPort.Width + 2 * offset, viewPort.Height + 2 * offset);

        return CohenSutherlandLineClip(viewPortWithOffset, ref xi0, ref yi0, ref xi1, ref yi1);
    }

    internal static bool CohenSutherlandLineClip(Rect extents, ref float xi0, ref float yi0, ref float xi1, ref float yi1)
    {
        // Fix #SC-1555: Log(0) issue
        // CohenSuzerland line clipping algorithm returns NaN when point has infinity value
        double x0 = ClipToInt(xi0);
        double y0 = ClipToInt(yi0);
        double x1 = ClipToInt(xi1);
        double y1 = ClipToInt(yi1);

        var isValid = CohenSutherlandLineClip(extents, ref x0, ref y0, ref x1, ref y1);

        // Update the clipped line
        xi0 = (float)x0;
        yi0 = (float)y0;
        xi1 = (float)x1;
        yi1 = (float)y1;

        return isValid;
    }

    private static float ClipToInt(float d)
    {
        if (d > int.MaxValue)
            return int.MaxValue;

        if (d < int.MinValue)
            return int.MinValue;

        return d;
    }

    internal static bool CohenSutherlandLineClip(Rect extents, ref int xi0, ref int yi0, ref int xi1, ref int yi1)
    {
        double x0 = xi0;
        double y0 = yi0;
        double x1 = xi1;
        double y1 = yi1;

        var isValid = CohenSutherlandLineClip(extents, ref x0, ref y0, ref x1, ref y1);

        // Update the clipped line
        xi0 = (int)x0;
        yi0 = (int)y0;
        xi1 = (int)x1;
        yi1 = (int)y1;

        return isValid;
    }

    /// <summary>
    /// Cohen–Sutherland clipping algorithm clips a line from
    /// P0 = (x0, y0) to P1 = (x1, y1) against a rectangle with 
    /// diagonal from (xmin, ymin) to (xmax, ymax).
    /// </summary>
    /// <remarks>See http://en.wikipedia.org/wiki/Cohen%E2%80%93Sutherland_algorithm for details</remarks>
    /// <returns>a list of two points in the resulting clipped line, or zero</returns>
    internal static bool CohenSutherlandLineClip(Rect extents, ref double x0, ref double y0, ref double x1, ref double y1)
    {
        // compute outcodes for P0, P1, and whatever point lies outside the clip rectangle
        byte outcode0 = ComputeOutCode(extents, x0, y0);
        byte outcode1 = ComputeOutCode(extents, x1, y1);

        // No clipping if both points lie inside viewport
        if (outcode0 == INSIDE && outcode1 == INSIDE)
            return true;

        bool isValid = false;

        while (true)
        {
            // Bitwise OR is 0. Trivially accept and get out of loop
            if ((outcode0 | outcode1) == 0)
            {
                isValid = true;
                break;
            }
            // Bitwise AND is not 0. Trivially reject and get out of loop
            else if ((outcode0 & outcode1) != 0)
            {
                break;
            }
            else
            {
                // failed both tests, so calculate the line segment to clip
                // from an outside point to an intersection with clip edge
                double x, y;

                // At least one endpoint is outside the clip rectangle; pick it.
                byte outcodeOut = (outcode0 != 0) ? outcode0 : outcode1;

                // Now find the intersection point;
                // use formulas y = y0 + slope * (x - x0), x = x0 + (1 / slope) * (y - y0)
                if ((outcodeOut & TOP) != 0)
                {   // point is above the clip rectangle
                    x = x0 + (x1 - x0) * (extents.Top - y0) / (y1 - y0);
                    y = extents.Top;
                }
                else if ((outcodeOut & BOTTOM) != 0)
                { // point is below the clip rectangle
                    x = x0 + (x1 - x0) * (extents.Bottom - y0) / (y1 - y0);
                    y = extents.Bottom;
                }
                else if ((outcodeOut & RIGHT) != 0)
                {  // point is to the right of clip rectangle
                    y = y0 + (y1 - y0) * (extents.Right - x0) / (x1 - x0);
                    x = extents.Right;
                }
                else if ((outcodeOut & LEFT) != 0)
                {   // point is to the left of clip rectangle
                    y = y0 + (y1 - y0) * (extents.Left - x0) / (x1 - x0);
                    x = extents.Left;
                }
                else
                {
                    x = double.NaN;
                    y = double.NaN;
                }

                // Now we move outside point to intersection point to clip
                // and get ready for next pass.
                if (outcodeOut == outcode0)
                {
                    x0 = x;
                    y0 = y;
                    outcode0 = ComputeOutCode(extents, x0, y0);
                }
                else
                {
                    x1 = x;
                    y1 = y;
                    outcode1 = ComputeOutCode(extents, x1, y1);
                }
            }
        }

        return isValid;
    }

    /// <summary>
    /// Alpha blends 2 premultiplied colors with each other
    /// </summary>
    /// <param name="sa">Source alpha color component</param>
    /// <param name="sr">Premultiplied source red color component</param>
    /// <param name="sg">Premultiplied source green color component</param>
    /// <param name="sb">Premultiplied source blue color component</param>
    /// <param name="destPixel">Premultiplied destination color</param>
    /// <returns>Premultiplied blended color value</returns>
    public static int AlphaBlend(int sa, int sr, int sg, int sb, int destPixel)
    {
        int dr, dg, db;
        int da;
        da = ((destPixel >> 24) & 0xff);
        dr = ((destPixel >> 16) & 0xff);
        dg = ((destPixel >> 8) & 0xff);
        db = ((destPixel) & 0xff);

        destPixel = ((sa + (((da * (255 - sa)) * 0x8081) >> 23)) << 24) |
           ((sr + (((dr * (255 - sa)) * 0x8081) >> 23)) << 16) |
           ((sg + (((dg * (255 - sa)) * 0x8081) >> 23)) << 8) |
           ((sb + (((db * (255 - sa)) * 0x8081) >> 23)));

        return destPixel;
    }

    #endregion

    #endregion

    #region New

    public static WriteableBitmap New(int pixelWidth, int pixelHeight, System.Drawing.Color color = default) => New(new System.Drawing.Size(pixelWidth, pixelHeight), color);

    public static WriteableBitmap New(System.Drawing.Size size, System.Drawing.Color color = default)
    {
        var result = new WriteableBitmap(size.Width < 1 ? 1 : size.Width, size.Height < 1 ? 1 : size.Height, 96.0, 96.0, PixelFormats.Pbgra32, null);
        if (color != default)
        {
            XColor.Convert(color, out System.Windows.Media.Color c);
            result.Clear(c);
        }

        return result;
    }

    #endregion

    #region Resize

    public static WriteableBitmap Resize(this WriteableBitmap i, double scale)
    {
        var s = new System.Windows.Media.ScaleTransform(scale, scale);

        var result = new TransformedBitmap(i, s);

        static WriteableBitmap a(BitmapSource b)
        {
            // Calculate stride of source
            int stride = b.PixelWidth * (b.Format.BitsPerPixel / 8);

            // Create data array to hold source pixel data
            byte[] data = new byte[stride * b.PixelHeight];

            // Copy source image pixels to the data array
            b.CopyPixels(data, stride, 0);

            // Create WriteableBitmap to copy the pixel data to.      
            WriteableBitmap target = new(b.PixelWidth, b.PixelHeight, b.DpiX, b.DpiY, b.Format, null);

            // Write the pixel data to the WriteableBitmap.
            target.WritePixels(new Int32Rect(0, 0, b.PixelWidth, b.PixelHeight), data, stride, 0);

            return target;
        }

        return a(result);
    }

    #endregion

    #region Scrolling Tool

    /// <summary>Scrolls content of given rectangle.</summary>
    /// <param name="bmp"></param>
    /// <param name="dy">if greater than 0, scrolls down, else scrolls up</param>
    /// <param name="rect"></param>
    public static unsafe void ScrollY(this WriteableBitmap bmp, int dy, Area<int> rect, Color? background = null)
    {
        int bgcolor = GetColor(background ?? System.Windows.Media.Colors.White).Encode();

        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;
        int xmin = rect.X;
        int ymin = rect.Y;
        int xmax = rect.Right();
        int ymax = rect.Bottom();

        if (xmin < 0) xmin = 0;
        if (ymin < 0) ymin = 0;
        if (xmax >= w) xmax = w - 1;
        if (ymax >= h) ymax = h - 1;
        int xcnt = xmax - xmin + 1;
        int ycnt = ymax - ymin + 1;
        if (xcnt <= 0) return;

        if (dy > 0)
        {
            for (int y = ymax; y >= ymin + dy; y--)
            {
                int ydstidex = y;
                int ysrcindex = y - dy;
                if (ysrcindex < ymin || ysrcindex > ymax) continue;

                BitmapContext.memcpy(pixels + ydstidex * w + xmin, pixels + ysrcindex * w + xmin, xcnt * 4);
            }
        }
        if (dy < 0)
        {
            for (int y = ymin; y <= ymax - dy; y++)
            {
                int ysrcindex = y - dy;
                int ydstidex = y;
                if (ysrcindex < ymin || ysrcindex > ymax) continue;
                BitmapContext.memcpy(pixels + ydstidex * w + xmin, pixels + ysrcindex * w + xmin, xcnt * 4);
            }
        }

        if (dy < 0)
        {
            bmp.FillRectangle(xmin, ymax + dy + 1, xmax, ymax, bgcolor);
        }
        if (dy > 0)
        {
            bmp.FillRectangle(xmin, ymin, xmax, ymin + dy - 1, bgcolor);
        }
    }

    /// <summary>Scrolls content of given rectangle.</summary>
    /// <param name="bmp"></param>
    /// <param name="dx">if greater than 0, scrolls right, else scrolls left</param>
    /// <param name="rect"></param>
    public static unsafe void ScrollX(this WriteableBitmap bmp, int dx, Area<int> rect, Color? background = null)
    {
        int bgcolor = GetColor(background ?? System.Windows.Media.Colors.White).Encode();
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;
        var pixels = context.Pixels;
        int xmin = rect.X;
        int ymin = rect.Y;
        int xmax = rect.Right();
        int ymax = rect.Bottom();

        if (xmin < 0) xmin = 0;
        if (ymin < 0) ymin = 0;
        if (xmax >= w) xmax = w - 1;
        if (ymax >= h) ymax = h - 1;
        int xcnt = xmax - xmin + 1;
        int ycnt = ymax - ymin + 1;

        int srcx = xmin, dstx = xmin;
        if (dx < 0)
        {
            xcnt += dx;
            dstx = xmin;
            srcx = xmin - dx;
        }
        if (dx > 0)
        {
            xcnt -= dx;
            srcx = xmin;
            dstx = xmin + dx;
        }

        if (xcnt <= 0) return;

        int* yptr = pixels + w * ymin;
        for (int y = ymin; y <= ymax; y++, yptr += w)
        {
            BitmapContext.memcpy(yptr + dstx, yptr + srcx, xcnt * 4);
        }

        if (dx < 0)
        {
            bmp.FillRectangle(xmax + dx + 1, ymin, xmax, ymax, bgcolor);
        }
        if (dx > 0)
        {
            bmp.FillRectangle(xmin, ymin, xmin + dx - 1, ymax, bgcolor);
        }
    }

    #endregion

    #region Set

    #region Without alpha

    /// <summary>
    /// Sets the color of the pixel using a precalculated index (faster). 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="index">The coordinate index.</param>
    /// <param name="r">The red value of the color.</param>
    /// <param name="g">The green value of the color.</param>
    /// <param name="b">The blue value of the color.</param>
    unsafe public static void SetPixeli(this WriteableBitmap bmp, int index, byte r, byte g, byte b)
    {
        using var context = bmp.GetContext();
        context.Pixels[index] = (255 << 24) | (r << 16) | (g << 8) | b;
    }

    /// <summary>
    /// Sets the color of the pixel. 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate (row).</param>
    /// <param name="y">The y coordinate (column).</param>
    /// <param name="r">The red value of the color.</param>
    /// <param name="g">The green value of the color.</param>
    /// <param name="b">The blue value of the color.</param>
    unsafe public static void SetPixel(this WriteableBitmap bmp, int x, int y, byte r, byte g, byte b)
    {
        using var context = bmp.GetContext();
        context.Pixels[y * context.Width + x] = (255 << 24) | (r << 16) | (g << 8) | b;
    }

    #endregion

    #region With alpha

    /// <summary>
    /// Sets the color of the pixel including the alpha value and using a precalculated index (faster). 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="index">The coordinate index.</param>
    /// <param name="a">The alpha value of the color.</param>
    /// <param name="r">The red value of the color.</param>
    /// <param name="g">The green value of the color.</param>
    /// <param name="b">The blue value of the color.</param>
    unsafe public static void SetPixeli(this WriteableBitmap bmp, int index, byte a, byte r, byte g, byte b)
    {
        using var context = bmp.GetContext();
        context.Pixels[index] = (a << 24) | (r << 16) | (g << 8) | b;
    }

    /// <summary>
    /// Sets the color of the pixel including the alpha value. 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate (row).</param>
    /// <param name="y">The y coordinate (column).</param>
    /// <param name="a">The alpha value of the color.</param>
    /// <param name="r">The red value of the color.</param>
    /// <param name="g">The green value of the color.</param>
    /// <param name="b">The blue value of the color.</param>
    unsafe public static void SetPixel(this WriteableBitmap bmp, int x, int y, byte a, byte r, byte g, byte b)
    {
        using var context = bmp.GetContext();
        context.Pixels[y * context.Width + x] = (a << 24) | (r << 16) | (g << 8) | b;
    }

    #endregion

    #region With System.Windows.Media.System.Windows.Media.Color

    /// <summary>
    /// Sets the color of the pixel using a precalculated index (faster). 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="index">The coordinate index.</param>
    /// <param name="color">The color.</param>
    unsafe public static void SetPixeli(this WriteableBitmap bmp, int index, System.Windows.Media.Color color)
    {
        using var context = bmp.GetContext();
        context.Pixels[index] = GetColor(color).Encode();
    }

    /// <summary>
    /// Sets the color of the pixel. 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate (row).</param>
    /// <param name="y">The y coordinate (column).</param>
    /// <param name="color">The color.</param>
    unsafe public static void SetPixel(this WriteableBitmap bmp, int x, int y, System.Windows.Media.Color color)
    {
        using var context = bmp.GetContext();
        context.Pixels[y * context.Width + x] = GetColor(color).Encode();
    }

    /// <summary>
    /// Sets the color of the pixel using an extra alpha value and a precalculated index (faster). 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="index">The coordinate index.</param>
    /// <param name="a">The alpha value of the color.</param>
    /// <param name="color">The color.</param>
    unsafe public static void SetPixeli(this WriteableBitmap bmp, int index, byte a, System.Windows.Media.Color color)
    {
        using var context = bmp.GetContext();
        // Add one to use mul and cheap bit shift for multiplicaltion
        var ai = a + 1;
        context.Pixels[index] = (a << 24)
                    | ((byte)((color.R * ai) >> 8) << 16)
                    | ((byte)((color.G * ai) >> 8) << 8)
                    | ((byte)((color.B * ai) >> 8));
    }

    /// <summary>
    /// Sets the color of the pixel using an extra alpha value. 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate (row).</param>
    /// <param name="y">The y coordinate (column).</param>
    /// <param name="a">The alpha value of the color.</param>
    /// <param name="color">The color.</param>
    unsafe public static void SetPixel(this WriteableBitmap bmp, int x, int y, byte a, System.Windows.Media.Color color)
    {
        using var context = bmp.GetContext();
        // Add one to use mul and cheap bit shift for multiplicaltion
        var ai = a + 1;
        context.Pixels[y * context.Width + x] = (a << 24)
                                        | ((byte)((color.R * ai) >> 8) << 16)
                                        | ((byte)((color.G * ai) >> 8) << 8)
                                        | ((byte)((color.B * ai) >> 8));
    }

    /// <summary>
    /// Sets the color of the pixel using a precalculated index (faster).  
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="index">The coordinate index.</param>
    /// <param name="color">The color.</param>
    unsafe public static void SetPixeli(this WriteableBitmap bmp, int index, int color)
    {
        using var context = bmp.GetContext();
        context.Pixels[index] = color;
    }

    /// <summary>
    /// Sets the color of the pixel. 
    /// For best performance this method should not be used in iterative real-time scenarios. Implement the code directly inside a loop.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate (row).</param>
    /// <param name="y">The y coordinate (column).</param>
    /// <param name="color">The color.</param>
    unsafe public static void SetPixel(this WriteableBitmap bmp, int x, int y, int color)
    {
        using var context = bmp.GetContext();
        context.Pixels[y * context.Width + x] = color;
    }

    #endregion

    #endregion

    #region Shape

    #region Polyline, Triangle, Quad

    /// <summary>
    /// Draws a polyline. Add the first point also at the end of the array if the line should be closed.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points of the polyline in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawPolyline(this WriteableBitmap bmp, int[] points, Color color, int strokeThickness)
    {
        var col = GetColor(color).Encode();
        bmp.DrawPolyline(points, col, strokeThickness);
    }

    /// <summary>
    /// Draws a polyline anti-aliased. Add the first point also at the end of the array if the line should be closed.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points of the polyline in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawPolyline(this WriteableBitmap bmp, int[] points, int color, int strokeThickness)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;
        var x1 = points[0];
        var y1 = points[1];

        for (var i = 2; i < points.Length; i += 2)
        {
            var x2 = points[i];
            var y2 = points[i + 1];

            DrawLineAa(context, w, h, x1, y1, x2, y2, color, strokeThickness);
            x1 = x2;
            y1 = y2;
        }
    }

    /// <summary>
    /// Draws a polyline. Add the first point also at the end of the array if the line should be closed.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points of the polyline in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawPolylineAa(this WriteableBitmap bmp, int[] points, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawPolylineAa(points, col);
    }

    /// <summary>
    /// Draws a polyline anti-aliased. Add the first point also at the end of the array if the line should be closed.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points of the polyline in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, ..., xn, yn).</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawPolylineAa(this WriteableBitmap bmp, int[] points, int color)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;
        var x1 = points[0];
        var y1 = points[1];

        for (var i = 2; i < points.Length; i += 2)
        {
            var x2 = points[i];
            var y2 = points[i + 1];

            DrawLineAa(context, w, h, x1, y1, x2, y2, color);
            x1 = x2;
            y1 = y2;
        }
    }

    /// <summary>
    /// Draws a triangle.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="color">The color.</param>
    public static void DrawTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawTriangle(x1, y1, x2, y2, x3, y3, col);
    }

    /// <summary>
    /// Draws a triangle.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="color">The color.</param>
    public static void DrawTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int color)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;

        DrawLine(context, w, h, x1, y1, x2, y2, color);
        DrawLine(context, w, h, x2, y2, x3, y3, color);
        DrawLine(context, w, h, x3, y3, x1, y1, color);
    }

    /// <summary>
    /// Draws a quad.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="x4">The x-coordinate of the 4th point.</param>
    /// <param name="y4">The y-coordinate of the 4th point.</param>
    /// <param name="color">The color.</param>
    public static void DrawQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawQuad(x1, y1, x2, y2, x3, y3, x4, y4, col);
    }

    /// <summary>
    /// Draws a quad.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the 1st point.</param>
    /// <param name="y1">The y-coordinate of the 1st point.</param>
    /// <param name="x2">The x-coordinate of the 2nd point.</param>
    /// <param name="y2">The y-coordinate of the 2nd point.</param>
    /// <param name="x3">The x-coordinate of the 3rd point.</param>
    /// <param name="y3">The y-coordinate of the 3rd point.</param>
    /// <param name="x4">The x-coordinate of the 4th point.</param>
    /// <param name="y4">The y-coordinate of the 4th point.</param>
    /// <param name="color">The color.</param>
    public static void DrawQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, int color)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;

        DrawLine(context, w, h, x1, y1, x2, y2, color);
        DrawLine(context, w, h, x2, y2, x3, y3, color);
        DrawLine(context, w, h, x3, y3, x4, y4, color);
        DrawLine(context, w, h, x4, y4, x1, y1, color);
    }

    #endregion

    #region Rectangle

    /// <summary>
    /// Draws a rectangle.
    /// x2 has to be greater than x1 and y2 has to be greater than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color.</param>
    public static void DrawRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawRectangle(x1, y1, x2, y2, col);
    }

    /// <summary>
    /// Draws a rectangle.
    /// x2 has to be greater than x1 and y2 has to be greater than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color.</param>
    public static void DrawRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;
        var pixels = context.Pixels;

        // Check boundaries
        if ((x1 < 0 && x2 < 0) || (y1 < 0 && y2 < 0)
         || (x1 >= w && x2 >= w) || (y1 >= h && y2 >= h))
        {
            return;
        }

        // Clamp boundaries
        if (x1 < 0) { x1 = 0; }
        if (y1 < 0) { y1 = 0; }
        if (x2 < 0) { x2 = 0; }
        if (y2 < 0) { y2 = 0; }
        if (x1 >= w) { x1 = w - 1; }
        if (y1 >= h) { y1 = h - 1; }
        if (x2 >= w) { x2 = w - 1; }
        if (y2 >= h) { y2 = h - 1; }

        var startY = y1 * w;
        var endY = y2 * w;

        var offset2 = endY + x1;
        var endOffset = startY + x2;
        var startYPlusX1 = startY + x1;

        // top and bottom horizontal scanlines
        for (var x = startYPlusX1; x <= endOffset; x++)
        {
            pixels[x] = color; // top horizontal line
            pixels[offset2] = color; // bottom horizontal line
            offset2++;
        }

        // offset2 == endY + x2

        // vertical scanlines
        endOffset = startYPlusX1 + w;
        offset2 -= w;

        for (var y = startY + x2 + w; y <= offset2; y += w)
        {
            pixels[y] = color; // right vertical line
            pixels[endOffset] = color; // left vertical line
            endOffset += w;
        }
    }

    #endregion

    #region Ellipse

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing Ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf 
    /// x2 has to be greater than x1 and y2 has to be less than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawEllipse(x1, y1, x2, y2, col);
    }

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing Ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf 
    /// x2 has to be greater than x1 and y2 has to be less than y1.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the bounding rectangle's left side.</param>
    /// <param name="y1">The y-coordinate of the bounding rectangle's top side.</param>
    /// <param name="x2">The x-coordinate of the bounding rectangle's right side.</param>
    /// <param name="y2">The y-coordinate of the bounding rectangle's bottom side.</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color)
    {
        // Calc center and radius
        int xr = (x2 - x1) >> 1;
        int yr = (y2 - y1) >> 1;
        int xc = x1 + xr;
        int yc = y1 + yr;
        bmp.DrawEllipseCentered(xc, yc, xr, yr, color);
    }

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing Ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf
    /// Uses a different parameter representation than DrawEllipse().
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="xc">The x-coordinate of the ellipses center.</param>
    /// <param name="yc">The y-coordinate of the ellipses center.</param>
    /// <param name="xr">The radius of the ellipse in x-direction.</param>
    /// <param name="yr">The radius of the ellipse in y-direction.</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawEllipseCentered(xc, yc, xr, yr, col);
    }

    /// <summary>
    /// A Fast Bresenham Type Algorithm For Drawing Ellipses http://homepage.smc.edu/kennedy_john/belipse.pdf 
    /// Uses a different parameter representation than DrawEllipse().
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="xc">The x-coordinate of the ellipses center.</param>
    /// <param name="yc">The y-coordinate of the ellipses center.</param>
    /// <param name="xr">The radius of the ellipse in x-direction.</param>
    /// <param name="yr">The radius of the ellipse in y-direction.</param>
    /// <param name="color">The color for the line.</param>
    public static void DrawEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, int color)
    {
        // Use refs for faster access (really important!) speeds up a lot!
        using var context = bmp.GetContext();

        var pixels = context.Pixels;
        var w = context.Width;
        var h = context.Height;

        // Avoid endless loop
        if (xr < 1 || yr < 1)
        {
            return;
        }

        // Init vars
        int uh, lh, uy, ly, lx, rx;
        int x = xr;
        int y = 0;
        int xrSqTwo = (xr * xr) << 1;
        int yrSqTwo = (yr * yr) << 1;
        int xChg = yr * yr * (1 - (xr << 1));
        int yChg = xr * xr;
        int err = 0;
        int xStopping = yrSqTwo * xr;
        int yStopping = 0;

        // Draw first set of points counter clockwise where tangent line slope > -1.
        while (xStopping >= yStopping)
        {
            // Draw 4 quadrant points at once
            uy = yc + y;                  // Upper half
            ly = yc - y;                  // Lower half

            rx = xc + x;
            lx = xc - x;

            if (0 <= uy && uy < h)
            {
                uh = uy * w;              // Upper half
                if (0 <= rx && rx < w) pixels[rx + uh] = color;      // Quadrant I (Actually an octant)
                if (0 <= lx && lx < w) pixels[lx + uh] = color;      // Quadrant II
            }

            if (0 <= ly && ly < h)
            {
                lh = ly * w;              // Lower half
                if (0 <= lx && lx < w) pixels[lx + lh] = color;      // Quadrant III
                if (0 <= rx && rx < w) pixels[rx + lh] = color;      // Quadrant IV
            }

            y++;
            yStopping += xrSqTwo;
            err += yChg;
            yChg += xrSqTwo;
            if ((xChg + (err << 1)) > 0)
            {
                x--;
                xStopping -= yrSqTwo;
                err += xChg;
                xChg += yrSqTwo;
            }
        }

        // ReInit vars
        x = 0;
        y = yr;
        uy = yc + y;                  // Upper half
        ly = yc - y;                  // Lower half
        uh = uy * w;                  // Upper half
        lh = ly * w;                  // Lower half
        xChg = yr * yr;
        yChg = xr * xr * (1 - (yr << 1));
        err = 0;
        xStopping = 0;
        yStopping = xrSqTwo * yr;

        // Draw second set of points clockwise where tangent line slope < -1.
        while (xStopping <= yStopping)
        {
            // Draw 4 quadrant points at once
            rx = xc + x;
            if (0 <= rx && rx < w)
            {
                if (0 <= uy && uy < h) pixels[rx + uh] = color;      // Quadrant I (Actually an octant)
                if (0 <= ly && ly < h) pixels[rx + lh] = color;      // Quadrant IV
            }

            lx = xc - x;
            if (0 <= lx && lx < w)
            {
                if (0 <= uy && uy < h) pixels[lx + uh] = color;      // Quadrant II
                if (0 <= ly && ly < h) pixels[lx + lh] = color;      // Quadrant III
            }

            x++;
            xStopping += yrSqTwo;
            err += xChg;
            xChg += yrSqTwo;
            if ((yChg + (err << 1)) > 0)
            {
                y--;
                uy = yc + y;                  // Upper half
                ly = yc - y;                  // Lower half
                uh = uy * w;                  // Upper half
                lh = ly * w;                  // Lower half
                yStopping -= xrSqTwo;
                err += yChg;
                yChg += xrSqTwo;
            }
        }
    }

    #endregion

    #endregion

    #region Spline

    private const float StepFactor = 2f;

    #region Beziér

    /// <summary>
    /// Draws a cubic Beziér spline defined by start, end and two control points.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="cx1">The x-coordinate of the 1st control point.</param>
    /// <param name="cy1">The y-coordinate of the 1st control point.</param>
    /// <param name="cx2">The x-coordinate of the 2nd control point.</param>
    /// <param name="cy2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color.</param>
    public static void DrawBezier(this WriteableBitmap bmp, int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawBezier(x1, y1, cx1, cy1, cx2, cy2, x2, y2, col);
    }

    /// <summary>
    /// Draws a cubic Beziér spline defined by start, end and two control points.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x1">The x-coordinate of the start point.</param>
    /// <param name="y1">The y-coordinate of the start point.</param>
    /// <param name="cx1">The x-coordinate of the 1st control point.</param>
    /// <param name="cy1">The y-coordinate of the 1st control point.</param>
    /// <param name="cx2">The x-coordinate of the 2nd control point.</param>
    /// <param name="cy2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x2">The x-coordinate of the end point.</param>
    /// <param name="y2">The y-coordinate of the end point.</param>
    /// <param name="color">The color.</param>
    public static void DrawBezier(this WriteableBitmap bmp, int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2, int color)
    {
        // Determine distances between controls points (bounding rect) to find the optimal stepsize
        var minX = Math.Min(x1, Math.Min(cx1, Math.Min(cx2, x2)));
        var minY = Math.Min(y1, Math.Min(cy1, Math.Min(cy2, y2)));
        var maxX = Math.Max(x1, Math.Max(cx1, Math.Max(cx2, x2)));
        var maxY = Math.Max(y1, Math.Max(cy1, Math.Max(cy2, y2)));

        // Get slope
        var lenx = maxX - minX;
        var len = maxY - minY;
        if (lenx > len)
        {
            len = lenx;
        }

        // Prevent division by zero
        if (len != 0)
        {
            using var context = bmp.GetContext();
            // Use refs for faster access (really important!) speeds up a lot!
            int w = context.Width;
            int h = context.Height;

            // Init vars
            var step = StepFactor / len;
            int tx1 = x1;
            int ty1 = y1;
            int tx2, ty2;

            // Interpolate
            for (var t = step; t <= 1; t += step)
            {
                var tSq = t * t;
                var t1 = 1 - t;
                var t1Sq = t1 * t1;

                tx2 = (int)(t1 * t1Sq * x1 + 3 * t * t1Sq * cx1 + 3 * t1 * tSq * cx2 + t * tSq * x2);
                ty2 = (int)(t1 * t1Sq * y1 + 3 * t * t1Sq * cy1 + 3 * t1 * tSq * cy2 + t * tSq * y2);

                // Draw line
                DrawLine(context, w, h, tx1, ty1, tx2, ty2, color);
                tx1 = tx2;
                ty1 = ty2;
            }

            // Prevent rounding gap
            DrawLine(context, w, h, tx1, ty1, x2, y2, color);
        }
    }

    /// <summary>
    /// Draws a series of cubic Beziér splines each defined by start, end and two control points. 
    /// The ending point of the previous curve is used as starting point for the next. 
    /// Therefore the initial curve needs four points and the subsequent 3 (2 control and 1 end point).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, cx1, cy1, cx2, cy2, x2, y2, cx3, cx4 ..., xn, yn).</param>
    /// <param name="color">The color for the spline.</param>
    public static void DrawBeziers(this WriteableBitmap bmp, int[] points, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawBeziers(points, col);
    }

    /// <summary>
    /// Draws a series of cubic Beziér splines each defined by start, end and two control points. 
    /// The ending point of the previous curve is used as starting point for the next. 
    /// Therefore the initial curve needs four points and the subsequent 3 (2 control and 1 end point).
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, cx1, cy1, cx2, cy2, x2, y2, cx3, cx4 ..., xn, yn).</param>
    /// <param name="color">The color for the spline.</param>
    public static void DrawBeziers(this WriteableBitmap bmp, int[] points, int color)
    {
        int x1 = points[0];
        int y1 = points[1];
        int x2, y2;
        for (int i = 2; i + 5 < points.Length; i += 6)
        {
            x2 = points[i + 4];
            y2 = points[i + 5];
            bmp.DrawBezier(x1, y1, points[i], points[i + 1], points[i + 2], points[i + 3], x2, y2, color);
            x1 = x2;
            y1 = y2;
        }
    }

    #endregion

    #region Cardinal

    /// <summary>
    /// Draws a segment of a Cardinal spline (cubic) defined by four control points.
    /// </summary>
    /// <param name="x1">The x-coordinate of the 1st control point.</param>
    /// <param name="y1">The y-coordinate of the 1st control point.</param>
    /// <param name="x2">The x-coordinate of the 2nd control point.</param>
    /// <param name="y2">The y-coordinate of the 2nd control point.</param>
    /// <param name="x3">The x-coordinate of the 3rd control point.</param>
    /// <param name="y3">The y-coordinate of the 3rd control point.</param>
    /// <param name="x4">The x-coordinate of the 4th control point.</param>
    /// <param name="y4">The y-coordinate of the 4th control point.</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color.</param>
    /// <param name="context">The pixel context.</param>
    /// <param name="w">The width of the bitmap.</param>
    /// <param name="h">The height of the bitmap.</param> 
    private static void DrawCurveSegment(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension, int color, BitmapContext context, int w, int h)
    {
        // Determine distances between controls points (bounding rect) to find the optimal stepsize
        var minX = Math.Min(x1, Math.Min(x2, Math.Min(x3, x4)));
        var minY = Math.Min(y1, Math.Min(y2, Math.Min(y3, y4)));
        var maxX = Math.Max(x1, Math.Max(x2, Math.Max(x3, x4)));
        var maxY = Math.Max(y1, Math.Max(y2, Math.Max(y3, y4)));

        // Get slope
        var lenx = maxX - minX;
        var len = maxY - minY;
        if (lenx > len)
        {
            len = lenx;
        }

        // Prevent division by zero
        if (len != 0)
        {
            // Init vars
            var step = StepFactor / len;
            int tx1 = x2;
            int ty1 = y2;
            int tx2, ty2;

            // Calculate factors
            var sx1 = tension * (x3 - x1);
            var sy1 = tension * (y3 - y1);
            var sx2 = tension * (x4 - x2);
            var sy2 = tension * (y4 - y2);
            var ax = sx1 + sx2 + 2 * x2 - 2 * x3;
            var ay = sy1 + sy2 + 2 * y2 - 2 * y3;
            var bx = -2 * sx1 - sx2 - 3 * x2 + 3 * x3;
            var by = -2 * sy1 - sy2 - 3 * y2 + 3 * y3;

            // Interpolate
            for (var t = step; t <= 1; t += step)
            {
                var tSq = t * t;

                tx2 = (int)(ax * tSq * t + bx * tSq + sx1 * t + x2);
                ty2 = (int)(ay * tSq * t + by * tSq + sy1 * t + y2);

                // Draw line
                DrawLine(context, w, h, tx1, ty1, tx2, ty2, color);
                tx1 = tx2;
                ty1 = ty2;
            }

            // Prevent rounding gap
            DrawLine(context, w, h, tx1, ty1, x3, y3, color);
        }
    }

    /// <summary>
    /// Draws a Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void DrawCurve(this WriteableBitmap bmp, int[] points, float tension, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawCurve(points, tension, col);
    }

    /// <summary>
    /// Draws a Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void DrawCurve(this WriteableBitmap bmp, int[] points, float tension, int color)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;

        // First segment
        DrawCurveSegment(points[0], points[1], points[0], points[1], points[2], points[3], points[4], points[5], tension, color, context, w, h);

        // Middle segments
        int i;
        for (i = 2; i < points.Length - 4; i += 2)
        {
            DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension, color, context, w, h);
        }

        // Last segment
        DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 2], points[i + 3], tension, color, context, w, h);
    }

    /// <summary>
    /// Draws a closed Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void DrawCurveClosed(this WriteableBitmap bmp, int[] points, float tension, Color color)
    {
        var col = GetColor(color).Encode();
        bmp.DrawCurveClosed(points, tension, col);
    }

    /// <summary>
    /// Draws a closed Cardinal spline (cubic) defined by a point collection. 
    /// The cardinal spline passes through each point in the collection.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="points">The points for the curve in x and y pairs, therefore the array is interpreted as (x1, y1, x2, y2, x3, y3, x4, y4, x1, x2 ..., xn, yn).</param>
    /// <param name="tension">The tension of the curve defines the shape. Usually between 0 and 1. 0 would be a straight line.</param>
    /// <param name="color">The color for the spline.</param>
    public static void DrawCurveClosed(this WriteableBitmap bmp, int[] points, float tension, int color)
    {
        using var context = bmp.GetContext();
        // Use refs for faster access (really important!) speeds up a lot!
        int w = context.Width;
        int h = context.Height;

        int pn = points.Length;

        // First segment
        DrawCurveSegment(points[pn - 2], points[pn - 1], points[0], points[1], points[2], points[3], points[4], points[5], tension, color, context, w, h);

        // Middle segments
        int i;
        for (i = 2; i < pn - 4; i += 2)
        {
            DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension, color, context, w, h);
        }

        // Last segment
        DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[0], points[1], tension, color, context, w, h);

        // Last-to-First segment
        DrawCurveSegment(points[i], points[i + 1], points[i + 2], points[i + 3], points[0], points[1], points[2], points[3], tension, color, context, w, h);
    }

    #endregion

    #endregion

    #region Transform

    /// <summary>The mode for flipping.</summary>
    public enum FlipMode
    {
        /// <summary>
        /// Flips the image vertical (around the center of the y-axis).
        /// </summary>
        Vertical,
        /// <summary>
        /// Flips the image horizontal (around the center of the x-axis).
        /// </summary>
        Horizontal
    }

    #region Crop

    /// <summary>
    /// Creates a new cropped WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="x">The x coordinate of the rectangle that defines the crop region.</param>
    /// <param name="y">The y coordinate of the rectangle that defines the crop region.</param>
    /// <param name="width">The width of the rectangle that defines the crop region.</param>
    /// <param name="height">The height of the rectangle that defines the crop region.</param>
    /// <returns>A new WriteableBitmap that is a cropped version of the input.</returns>
    public static WriteableBitmap Crop(this WriteableBitmap bmp, int x, int y, int width, int height)
    {
        using var srcContext = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var srcWidth = srcContext.Width;
        var srcHeight = srcContext.Height;

        // If the rectangle is completely out of the bitmap
        if (x > srcWidth || y > srcHeight)
        {
            return XWriteableBitmap.New(0, 0);
        }

        // Clamp to boundaries
        if (x < 0) x = 0;
        if (x + width > srcWidth) width = srcWidth - x;
        if (y < 0) y = 0;
        if (y + height > srcHeight) height = srcHeight - y;

        // Copy the pixels line by line using fast BlockCopy
        var result = XWriteableBitmap.New(width, height);
        using var destContext = result.GetContext();
        for (var line = 0; line < height; line++)
        {
            var srcOff = ((y + line) * srcWidth + x) * XWriteableBitmap.SizeOfArgb;
            var dstOff = line * width * XWriteableBitmap.SizeOfArgb;
            BitmapContext.BlockCopy(srcContext, srcOff, destContext, dstOff, width * XWriteableBitmap.SizeOfArgb);
        }

        return result;
    }
    /// <summary>
    /// Creates a new cropped WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="region">The rectangle that defines the crop region.</param>
    /// <returns>A new WriteableBitmap that is a cropped version of the input.</returns>
    public static WriteableBitmap Crop(this WriteableBitmap bmp, Rect region)
    {
        return bmp.Crop((int)region.X, (int)region.Y, (int)region.Width, (int)region.Height);
    }

    #endregion

    #region Resize

    /// <summary>
    /// Creates a new resized WriteableBitmap.
    /// </summary>
    /// <param name="input">The WriteableBitmap.</param>
    /// <param name="newWidth">The new desired width.</param>
    /// <param name="newHeight">The new desired height.</param>
    /// <param name="interpolation">The interpolation method that should be used.</param>
    /// <returns>A new WriteableBitmap that is a resized version of the input.</returns>
    public static WriteableBitmap Resize(this WriteableBitmap input, int newWidth, int newHeight, ColorInterpolation interpolation)
    {
        using var srcContext = input.GetContext(Imaging.ReadWriteMode.ReadOnly);
        var pd = Resize(srcContext, srcContext.Width, srcContext.Height, newWidth, newHeight, interpolation);

        var result = XWriteableBitmap.New(newWidth, newHeight);
        using (var dstContext = result.GetContext())
        {
            BitmapContext.BlockCopy(pd, 0, dstContext, 0, XWriteableBitmap.SizeOfArgb * pd.Length);
        }
        return result;
    }

    /// <summary>
    /// Creates a new resized bitmap.
    /// </summary>
    /// <param name="input">The source context.</param>
    /// <param name="oldWidth">The width of the source pixels.</param>
    /// <param name="oldHeight">The height of the source pixels.</param>
    /// <param name="newWidth">The new desired width.</param>
    /// <param name="newHeight">The new desired height.</param>
    /// <param name="interpolation">The interpolation method that should be used.</param>
    /// <returns>A new bitmap that is a resized version of the input.</returns>
    public static int[] Resize(BitmapContext input, int oldWidth, int oldHeight, int newWidth, int newHeight, ColorInterpolation interpolation)
    {
        return Resize(input.Pixels, oldWidth, oldHeight, newWidth, newHeight, interpolation);
    }

    /// <summary>
    /// Creates a new resized bitmap.
    /// </summary>
    /// <param name="input">The source pixels.</param>
    /// <param name="oldWidth">The width of the source pixels.</param>
    /// <param name="oldHeight">The height of the source pixels.</param>
    /// <param name="newWidth">The new desired width.</param>
    /// <param name="newHeight">The new desired height.</param>
    /// <param name="interpolation">The interpolation method that should be used.</param>
    /// <returns>A new bitmap that is a resized version of the input.</returns>
    public static int[] Resize(int* input, int oldWidth, int oldHeight, int newWidth, int newHeight, ColorInterpolation interpolation)
    {
        var pd = new int[newWidth * newHeight];
        var xs = (float)oldWidth / newWidth;
        var ys = (float)oldHeight / newHeight;

        float fracx, fracy, ifracx, ifracy, sx, sy, l0, l1, rf, gf, bf;
        int c, x0, x1, y0, y1;
        byte c1a, c1r, c1g, c1b, c2a, c2r, c2g, c2b, c3a, c3r, c3g, c3b, c4a, c4r, c4g, c4b;
        byte a, r, g, b;

        // Nearest Neighbor
        if (interpolation == ColorInterpolation.NearestNeighbor)
        {
            var srcIdx = 0;
            for (var y = 0; y < newHeight; y++)
            {
                for (var x = 0; x < newWidth; x++)
                {
                    sx = x * xs;
                    sy = y * ys;
                    x0 = (int)sx;
                    y0 = (int)sy;

                    pd[srcIdx++] = input[y0 * oldWidth + x0];
                }
            }
        }
        //Bilinear
        else if (interpolation == ColorInterpolation.Bilinear)
        {
            var srcIdx = 0;
            for (var y = 0; y < newHeight; y++)
            {
                for (var x = 0; x < newWidth; x++)
                {
                    sx = x * xs;
                    sy = y * ys;
                    x0 = (int)sx;
                    y0 = (int)sy;

                    // Calculate coordinates of the 4 interpolation points
                    fracx = sx - x0;
                    fracy = sy - y0;
                    ifracx = 1f - fracx;
                    ifracy = 1f - fracy;
                    x1 = x0 + 1;
                    if (x1 >= oldWidth)
                    {
                        x1 = x0;
                    }
                    y1 = y0 + 1;
                    if (y1 >= oldHeight)
                    {
                        y1 = y0;
                    }


                    // Read source color
                    c = input[y0 * oldWidth + x0];
                    c1a = (byte)(c >> 24);
                    c1r = (byte)(c >> 16);
                    c1g = (byte)(c >> 8);
                    c1b = (byte)(c);

                    c = input[y0 * oldWidth + x1];
                    c2a = (byte)(c >> 24);
                    c2r = (byte)(c >> 16);
                    c2g = (byte)(c >> 8);
                    c2b = (byte)(c);

                    c = input[y1 * oldWidth + x0];
                    c3a = (byte)(c >> 24);
                    c3r = (byte)(c >> 16);
                    c3g = (byte)(c >> 8);
                    c3b = (byte)(c);

                    c = input[y1 * oldWidth + x1];
                    c4a = (byte)(c >> 24);
                    c4r = (byte)(c >> 16);
                    c4g = (byte)(c >> 8);
                    c4b = (byte)(c);


                    // Calculate colors
                    // Alpha
                    l0 = ifracx * c1a + fracx * c2a;
                    l1 = ifracx * c3a + fracx * c4a;
                    a = (byte)(ifracy * l0 + fracy * l1);

                    // Red
                    l0 = ifracx * c1r + fracx * c2r;
                    l1 = ifracx * c3r + fracx * c4r;
                    rf = ifracy * l0 + fracy * l1;

                    // Green
                    l0 = ifracx * c1g + fracx * c2g;
                    l1 = ifracx * c3g + fracx * c4g;
                    gf = ifracy * l0 + fracy * l1;

                    // Blue
                    l0 = ifracx * c1b + fracx * c2b;
                    l1 = ifracx * c3b + fracx * c4b;
                    bf = ifracy * l0 + fracy * l1;

                    // Cast to byte
                    r = (byte)rf;
                    g = (byte)gf;
                    b = (byte)bf;

                    // Write destination
                    pd[srcIdx++] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }
        return pd;
    }

    #endregion

    #region Rotate

    /// <summary>
    /// Rotates the bitmap in 90° steps clockwise and returns a new rotated WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="angle">The angle in degrees the bitmap should be rotated in 90° steps clockwise.</param>
    /// <returns>A new WriteableBitmap that is a rotated version of the input.</returns>
    public static WriteableBitmap Rotate(this WriteableBitmap bmp, int angle)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;
        var p = context.Pixels;
        var i = 0;
        WriteableBitmap result = null;
        angle %= 360;

        if (angle > 0 && angle <= 90)
        {
            result = XWriteableBitmap.New(h, w);
            using var destContext = result.GetContext();
            var rp = destContext.Pixels;
            for (var x = 0; x < w; x++)
            {
                for (var y = h - 1; y >= 0; y--)
                {
                    var srcInd = y * w + x;
                    rp[i] = p[srcInd];
                    i++;
                }
            }
        }
        else if (angle > 90 && angle <= 180)
        {
            result = XWriteableBitmap.New(w, h);
            using var destContext = result.GetContext();
            var rp = destContext.Pixels;
            for (var y = h - 1; y >= 0; y--)
            {
                for (var x = w - 1; x >= 0; x--)
                {
                    var srcInd = y * w + x;
                    rp[i] = p[srcInd];
                    i++;
                }
            }
        }
        else if (angle > 180 && angle <= 270)
        {
            result = XWriteableBitmap.New(h, w);
            using var destContext = result.GetContext();
            var rp = destContext.Pixels;
            for (var x = w - 1; x >= 0; x--)
            {
                for (var y = 0; y < h; y++)
                {
                    var srcInd = y * w + x;
                    rp[i] = p[srcInd];
                    i++;
                }
            }
        }
        else
        {
            result = XWriteableBitmap.Clone(bmp);
        }
        return result;
    }

    /// <summary>
    /// Rotates the bitmap in any degree returns a new rotated WriteableBitmap.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="angle">Arbitrary angle in 360 Degrees (positive = clockwise).</param>
    /// <param name="crop">if true: keep the size, false: adjust canvas to new size</param>
    /// <returns>A new WriteableBitmap that is a rotated version of the input.</returns>
    public static WriteableBitmap RotateFree(this WriteableBitmap bmp, double angle, bool crop = true)
    {
        // rotating clockwise, so it's negative relative to Cartesian quadrants
        double cnAngle = -1.0 * (Math.PI / 180) * angle;

        // general iterators
        int i, j;
        // calculated indices in Cartesian coordinates
        int x, y;
        double fDistance, fPolarAngle;
        // for use in neighboring indices in Cartesian coordinates
        int iFloorX, iCeilingX, iFloorY, iCeilingY;
        // calculated indices in Cartesian coordinates with trailing decimals
        double fTrueX, fTrueY;
        // for interpolation
        double fDeltaX, fDeltaY;

        // interpolated "top" pixels
        double fTopRed, fTopGreen, fTopBlue, fTopAlpha;

        // interpolated "bottom" pixels
        double fBottomRed, fBottomGreen, fBottomBlue, fBottomAlpha;

        // final interpolated color components
        int iRed, iGreen, iBlue, iAlpha;

        int iCentreX, iCentreY;
        int iDestCentreX, iDestCentreY;
        int iWidth, iHeight, newWidth, newHeight;
        using var bmpContext = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);

        iWidth = bmpContext.Width;
        iHeight = bmpContext.Height;

        if (crop)
        {
            newWidth = iWidth;
            newHeight = iHeight;
        }
        else
        {
            var rad = angle / (180 / Math.PI);
            newWidth = (int)Math.Ceiling(Math.Abs(Math.Sin(rad) * iHeight) + Math.Abs(Math.Cos(rad) * iWidth));
            newHeight = (int)Math.Ceiling(Math.Abs(Math.Sin(rad) * iWidth) + Math.Abs(Math.Cos(rad) * iHeight));
        }


        iCentreX = iWidth / 2;
        iCentreY = iHeight / 2;

        iDestCentreX = newWidth / 2;
        iDestCentreY = newHeight / 2;

        var bmBilinearInterpolation = XWriteableBitmap.New(newWidth, newHeight);

        using var bilinearContext = bmBilinearInterpolation.GetContext();
        var newp = bilinearContext.Pixels;
        var oldp = bmpContext.Pixels;
        var oldw = bmpContext.Width;

        // assigning pixels of destination image from source image
        // with bilinear interpolation
        for (i = 0; i < newHeight; ++i)
        {
            for (j = 0; j < newWidth; ++j)
            {
                // convert raster to Cartesian
                x = j - iDestCentreX;
                y = iDestCentreY - i;

                // convert Cartesian to polar
                fDistance = Math.Sqrt(x * x + y * y);
                if (x == 0)
                {
                    if (y == 0)
                    {
                        // center of image, no rotation needed
                        newp[i * newWidth + j] = oldp[iCentreY * oldw + iCentreX];
                        continue;
                    }
                    if (y < 0)
                    {
                        fPolarAngle = 1.5 * Math.PI;
                    }
                    else
                    {
                        fPolarAngle = 0.5 * Math.PI;
                    }
                }
                else
                {
                    fPolarAngle = Math.Atan2(y, x);
                }

                // the crucial rotation part
                // "reverse" rotate, so minus instead of plus
                fPolarAngle -= cnAngle;

                // convert polar to Cartesian
                fTrueX = fDistance * Math.Cos(fPolarAngle);
                fTrueY = fDistance * Math.Sin(fPolarAngle);

                // convert Cartesian to raster
                fTrueX += iCentreX;
                fTrueY = iCentreY - fTrueY;

                iFloorX = (int)(Math.Floor(fTrueX));
                iFloorY = (int)(Math.Floor(fTrueY));
                iCeilingX = (int)(Math.Ceiling(fTrueX));
                iCeilingY = (int)(Math.Ceiling(fTrueY));

                // check bounds
                if (iFloorX < 0 || iCeilingX < 0 || iFloorX >= iWidth || iCeilingX >= iWidth || iFloorY < 0 ||
                    iCeilingY < 0 || iFloorY >= iHeight || iCeilingY >= iHeight) continue;

                fDeltaX = fTrueX - iFloorX;
                fDeltaY = fTrueY - iFloorY;

                var clrTopLeft = oldp[iFloorY * oldw + iFloorX];
                var clrTopRight = oldp[iFloorY * oldw + iCeilingX];
                var clrBottomLeft = oldp[iCeilingY * oldw + iFloorX];
                var clrBottomRight = oldp[iCeilingY * oldw + iCeilingX];

                fTopAlpha = (1 - fDeltaX) * ((clrTopLeft >> 24) & 0xFF) + fDeltaX * ((clrTopRight >> 24) & 0xFF);
                fTopRed = (1 - fDeltaX) * ((clrTopLeft >> 16) & 0xFF) + fDeltaX * ((clrTopRight >> 16) & 0xFF);
                fTopGreen = (1 - fDeltaX) * ((clrTopLeft >> 8) & 0xFF) + fDeltaX * ((clrTopRight >> 8) & 0xFF);
                fTopBlue = (1 - fDeltaX) * (clrTopLeft & 0xFF) + fDeltaX * (clrTopRight & 0xFF);

                // linearly interpolate horizontally between bottom neighbors
                fBottomAlpha = (1 - fDeltaX) * ((clrBottomLeft >> 24) & 0xFF) + fDeltaX * ((clrBottomRight >> 24) & 0xFF);
                fBottomRed = (1 - fDeltaX) * ((clrBottomLeft >> 16) & 0xFF) + fDeltaX * ((clrBottomRight >> 16) & 0xFF);
                fBottomGreen = (1 - fDeltaX) * ((clrBottomLeft >> 8) & 0xFF) + fDeltaX * ((clrBottomRight >> 8) & 0xFF);
                fBottomBlue = (1 - fDeltaX) * (clrBottomLeft & 0xFF) + fDeltaX * (clrBottomRight & 0xFF);

                // linearly interpolate vertically between top and bottom interpolated results
                iRed = (int)(Math.Round((1 - fDeltaY) * fTopRed + fDeltaY * fBottomRed));
                iGreen = (int)(Math.Round((1 - fDeltaY) * fTopGreen + fDeltaY * fBottomGreen));
                iBlue = (int)(Math.Round((1 - fDeltaY) * fTopBlue + fDeltaY * fBottomBlue));
                iAlpha = (int)(Math.Round((1 - fDeltaY) * fTopAlpha + fDeltaY * fBottomAlpha));

                // make sure color values are valid
                if (iRed < 0) iRed = 0;
                if (iRed > 255) iRed = 255;
                if (iGreen < 0) iGreen = 0;
                if (iGreen > 255) iGreen = 255;
                if (iBlue < 0) iBlue = 0;
                if (iBlue > 255) iBlue = 255;
                if (iAlpha < 0) iAlpha = 0;
                if (iAlpha > 255) iAlpha = 255;

                var a = iAlpha + 1;
                newp[i * newWidth + j] = (iAlpha << 24)
                                        | ((byte)((iRed * a) >> 8) << 16)
                                        | ((byte)((iGreen * a) >> 8) << 8)
                                        | ((byte)((iBlue * a) >> 8));
            }
        }
        return bmBilinearInterpolation;
    }

    #endregion

    #region Flip

    /// <summary>
    /// Flips (reflects the image) either vertical or horizontal.
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="flipMode">The flip mode.</param>
    /// <returns>A new WriteableBitmap that is a flipped version of the input.</returns>
    public static WriteableBitmap Flip(this WriteableBitmap bmp, FlipMode flipMode)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        // Use refs for faster access (really important!) speeds up a lot!
        var w = context.Width;
        var h = context.Height;
        var p = context.Pixels;
        var i = 0;
        WriteableBitmap result = null;

        if (flipMode == FlipMode.Vertical)
        {
            result = XWriteableBitmap.New(w, h);
            using var destContext = result.GetContext();
            var rp = destContext.Pixels;
            for (var y = h - 1; y >= 0; y--)
            {
                for (var x = 0; x < w; x++)
                {
                    var srcInd = y * w + x;
                    rp[i] = p[srcInd];
                    i++;
                }
            }
        }
        else if (flipMode == FlipMode.Horizontal)
        {
            result = XWriteableBitmap.New(w, h);
            using var destContext = result.GetContext();
            var rp = destContext.Pixels;
            for (var y = 0; y < h; y++)
            {
                for (var x = w - 1; x >= 0; x--)
                {
                    var srcInd = y * w + x;
                    rp[i] = p[srcInd];
                    i++;
                }
            }
        }

        return result;
    }

    #endregion

    #endregion

    #region Write

    /// <summary>
    /// Writes the WriteableBitmap as a TGA image to a stream. 
    /// Used with permission from Nokola: http://nokola.com/blog/post/2010/01/21/Quick-and-Dirty-Output-of-WriteableBitmap-as-TGA-Image.aspx
    /// </summary>
    /// <param name="bmp">The WriteableBitmap.</param>
    /// <param name="destination">The destination stream.</param>
    public static void WriteTga(this WriteableBitmap bmp, Stream destination)
    {
        using var context = bmp.GetContext(Imaging.ReadWriteMode.ReadOnly);
        int width = context.Width;
        int height = context.Height;
        var pixels = context.Pixels;
        byte[] data = new byte[context.Length * XWriteableBitmap.SizeOfArgb];

        // Copy bitmap data as BGRA
        int offsetSource = 0;
        int width4 = width << 2;
        int width8 = width << 3;
        int offsetDest = (height - 1) * width4;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Account for pre-multiplied alpha
                int c = pixels[offsetSource];
                var a = (byte)(c >> 24);

                // Prevent division by zero
                int ai = a;
                if (ai == 0)
                {
                    ai = 1;
                }

                // Scale inverse alpha to use cheap integer mul bit shift
                ai = ((255 << 8) / ai);
                data[offsetDest + 3] = (byte)a;                                // A
                data[offsetDest + 2] = (byte)((((c >> 16) & 0xFF) * ai) >> 8); // R
                data[offsetDest + 1] = (byte)((((c >> 8) & 0xFF) * ai) >> 8);  // G
                data[offsetDest] = (byte)((((c & 0xFF) * ai) >> 8));           // B

                offsetSource++;
                offsetDest += XWriteableBitmap.SizeOfArgb;
            }
            offsetDest -= width8;
        }

        // Create header
        var header = new byte[]
 {
        0, // ID length
        0, // no color map
        2, // uncompressed, true color
        0, 0, 0, 0,
        0,
        0, 0, 0, 0, // x and y origin
        (byte)(width & 0x00FF),
        (byte)((width & 0xFF00) >> 8),
        (byte)(height & 0x00FF),
        (byte)((height & 0xFF00) >> 8),
        32, // 32 bit bitmap
        0
 };

        // Write header and data
        using var writer = new BinaryWriter(destination);
        writer.Write(header);
        writer.Write(data);
    }

    #endregion
}