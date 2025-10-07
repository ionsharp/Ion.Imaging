﻿using Ion.Colors;
using Ion.Numeral;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Media;

namespace Ion.Imaging;

public class ClearTypeLetterGlyph
{
    public struct Item
    {
        public short X;
        public short Y;
        public int Color;
    }

    public char Ch;
    public int Width;
    public int Height;

    //// instruction:
    //// y, x0, xcount, [color 1...color xcount]
    //// end: y=-1
    //public int[] Instructions;

    public Item[] Items;

    public static ClearTypeLetterGlyph CreateSpaceGlyph(GlyphTypeface glyphTypeface, double size)
    {
        int spaceWidth = (int)Math.Ceiling(glyphTypeface.AdvanceWidths[glyphTypeface.CharacterToGlyphMap[' ']] * size);
        return new ClearTypeLetterGlyph
        {
            Ch = ' ',
            Height = (int)Math.Ceiling(glyphTypeface.Height * size),
            Width = spaceWidth,
        };
    }


    public static ClearTypeLetterGlyph CreateGlyph(GlyphTypeface glyphTypeface, Font font, double size, char ch, System.Windows.Media.Color fontColor, System.Windows.Media.Color bgColor)
    {
        if (ch == ' ') return CreateSpaceGlyph(glyphTypeface, size);

        int width;
        int height;

        using (var bmp1 = new System.Drawing.Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
        {
            using var g = Graphics.FromImage(bmp1);
            //var sizef = g.MeasureString("" + ch, font, new PointF(0, 0), StringFormat.GenericTypographic);
            var sizef = g.MeasureString("" + ch, font, new PointF(0, 0), System.Drawing.StringFormat.GenericTypographic);
            width = (int)Math.Ceiling(sizef.Width);
            height = (int)Math.Ceiling(sizef.Height);
        }

        if (width == 0 || height == 0) return null;

        var res = new List<Item>();

        using (var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
        {
            var fg2 = System.Drawing.Color.FromArgb(fontColor.A, fontColor.R, fontColor.G, fontColor.B);
            var bg2 = System.Drawing.Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B);

            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.FillRectangle(new System.Drawing.SolidBrush(bg2), new Rectangle(0, 0, width, height));
                g.DrawString("" + ch, font, new System.Drawing.SolidBrush(fg2), 0, 0, System.Drawing.StringFormat.GenericTypographic);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    if (color != bg2)
                    {
                        res.Add(new Item
                        {
                            X = (short)x,
                            Y = (short)y,
                            Color = new ByteVector4(color.A, color.R, color.G, color.B).Encode(),
                        });
                    }
                }
            }
        }

        //var res = new List<int>();

        //using (var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
        //{
        //    var fg2 = System.Drawing.Color.FromArgb(fontColor.A, fontColor.R, fontColor.G, fontColor.B);
        //    var bg2 = System.Drawing.Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B);

        //    using (var g = System.Drawing.Graphics.FromImage(bmp))
        //    {
        //        g.FillRectangle(new System.Drawing.SolidBrush(bg2), new Rectangle(0, 0, width, height));
        //        g.DrawString("" + ch, font, new System.Drawing.SolidBrush(fg2), 0, 0, StringFormat.GenericTypographic);
        //    }

        //    int bgint = BitmapFactory.ConvertColor(Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
        //    for (int y = 0; y < height; y++)
        //    {
        //        var line = new List<int>();
        //        for (int x = 0; x < width; x++)
        //        {
        //            var color = bmp.GetPixel(x, y);
        //            line.Add(BitmapFactory.ConvertColor(Color.FromArgb(color.A, color.R, color.G, color.B)));
        //        }
        //        if (line.All(x => x == bgint)) continue; // all pixels filled with BG color
        //        int minx = line.FindIndex(x => x != bgint);
        //        int maxx = line.FindLastIndex(x => x != bgint);
        //        res.Add(y);
        //        res.Add(minx);
        //        res.Add(maxx - minx + 1);
        //        for (int i = minx; i <= maxx; i++) res.Add(line[i]);
        //    }
        //}

        //res.Add(-1); // end mark

        return new ClearTypeLetterGlyph
        {
            Width = width,
            Height = height,
            Ch = ch,
            //Instructions = res.ToArray(),
            Items = [.. res],
        };
    }
}
