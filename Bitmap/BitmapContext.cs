using System;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Ion.Imaging;

/// <summary>
/// A disposable cross-platform wrapper around a WriteableBitmap, allowing a common API for Silverlight + WPF with locking + unlocking if necessary
/// </summary>
/// <remarks>Attempting to put as many preprocessor hacks in this file, to keep the rest of the codebase relatively clean</remarks>
public unsafe struct BitmapContext : IDisposable
{
    private readonly WriteableBitmap _writeableBitmap;

    private readonly ReadWriteMode _mode;

    private readonly int _pixelWidth;

    private readonly int _pixelHeight;

    private readonly static IDictionary<WriteableBitmap, int> UpdateCountByBmp = new System.Collections.Concurrent.ConcurrentDictionary<WriteableBitmap, int>();

    private readonly static IDictionary<WriteableBitmap, BitmapContextBitmapProperties> BitmapPropertiesByBmp = new System.Collections.Concurrent.ConcurrentDictionary<WriteableBitmap, BitmapContextBitmapProperties>();

    private readonly int _length;

    private readonly int* _backBuffer;

    private readonly System.Windows.Media.PixelFormat _format;

    private readonly int _backBufferStride;

    /// <summary>
    /// The Bitmap
    /// </summary>
    public readonly WriteableBitmap WriteableBitmap { get { return _writeableBitmap; } }

    /// <summary>
    /// Width of the bitmap
    /// </summary>
    public readonly int Width { get { return _pixelWidth; } }

    /// <summary>
    /// Height of the bitmap
    /// </summary>
    public readonly int Height { get { return _pixelHeight; } }

    /// <summary>
    /// Creates an instance of a BitmapContext, with default mode = ReadWrite
    /// </summary>
    /// <param name="writeableBitmap"></param>
    public BitmapContext(WriteableBitmap writeableBitmap)
        : this(writeableBitmap, ReadWriteMode.ReadWrite) { }

    /// <summary>
    /// Creates an instance of a BitmapContext, with specified ReadWriteMode
    /// </summary>
    /// <param name="writeableBitmap"></param>
    /// <param name="mode"></param>
    public BitmapContext(WriteableBitmap writeableBitmap, ReadWriteMode mode)
    {
        _writeableBitmap = writeableBitmap;
        _mode = mode;
        //// Check if it's the Pbgra32 pixel format
        //if (writeableBitmap.Format != PixelFormats.Pbgra32)
        //{
        //   throw new ArgumentException("The input WriteableBitmap needs to have the Pbgra32 pixel format. Use the BitmapFactory.ConvertToPbgra32Format method to automatically convert any input BitmapSource to the right format accepted by this class.", "writeableBitmap");
        //}

        BitmapContextBitmapProperties bitmapProperties;

        lock (UpdateCountByBmp)
        {
            // Ensure the bitmap is in the dictionary of mapped Instances
            if (!UpdateCountByBmp.ContainsKey(writeableBitmap))
            {
                // Set UpdateCount to 1 for this bitmap 
                UpdateCountByBmp.Add(writeableBitmap, 1);

                // Lock the bitmap
                writeableBitmap.Lock();

                bitmapProperties = new BitmapContextBitmapProperties()
                {
                    BackBufferStride = writeableBitmap.BackBufferStride,
                    Pixels = (int*)writeableBitmap.BackBuffer,
                    Width = writeableBitmap.PixelWidth,
                    Height = writeableBitmap.PixelHeight,
                    Format = writeableBitmap.Format
                };
                BitmapPropertiesByBmp.Add(
                    writeableBitmap,
                    bitmapProperties);
            }
            else
            {
                // For previously contextualized bitmaps increment the update count
                IncrementRefCount(writeableBitmap);
                bitmapProperties = BitmapPropertiesByBmp[writeableBitmap];
            }

            _backBufferStride = bitmapProperties.BackBufferStride;
            _pixelWidth = bitmapProperties.Width;
            _pixelHeight = bitmapProperties.Height;
            _format = bitmapProperties.Format;
            _backBuffer = bitmapProperties.Pixels;

            double width = _backBufferStride / XWriteableBitmap.SizeOfArgb;
            _length = (int)(width * _pixelHeight);
        }
    }

    /// <summary>
    /// The pixels as ARGB integer values, where each channel is 8 bit.
    /// </summary>
    public readonly unsafe int* Pixels
    {
        [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
        get { return _backBuffer; }
    }

    /// <summary>
    /// The pixel format
    /// </summary>
    public readonly System.Windows.Media.PixelFormat Format
    {
        [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
        get { return _format; }
    }

    /// <summary>
    /// The number of pixels.
    /// </summary>
    public readonly int Length
    {
        [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
        get
        {
            return _length;
        }
    }

    /// <summary>
    /// Performs a Copy operation from source to destination BitmapContext
    /// </summary>
    /// <remarks>Equivalent to calling Buffer.BlockCopy in Silverlight, or native memcpy in WPF</remarks>
    [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
    public static unsafe void BlockCopy(BitmapContext src, int srcOffset, BitmapContext dest, int destOffset, int count)
    {
        BitmapContext.CopyUnmanagedMemory((byte*)src.Pixels, srcOffset, (byte*)dest.Pixels, destOffset, count);
    }

    /// <summary>
    /// Performs a Copy operation from source Array to destination BitmapContext
    /// </summary>
    /// <remarks>Equivalent to calling Buffer.BlockCopy in Silverlight, or native memcpy in WPF</remarks>
    [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
    public static unsafe void BlockCopy(int[] src, int srcOffset, BitmapContext dest, int destOffset, int count)
    {
        fixed (int* srcPtr = src)
        {
            BitmapContext.CopyUnmanagedMemory((byte*)srcPtr, srcOffset, (byte*)dest.Pixels, destOffset, count);
        }
    }

    /// <summary>
    /// Performs a Copy operation from source Array to destination BitmapContext
    /// </summary>
    /// <remarks>Equivalent to calling Buffer.BlockCopy in Silverlight, or native memcpy in WPF</remarks>
    [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
    public static unsafe void BlockCopy(byte[] src, int srcOffset, BitmapContext dest, int destOffset, int count)
    {
        fixed (byte* srcPtr = src)
        {
            BitmapContext.CopyUnmanagedMemory(srcPtr, srcOffset, (byte*)dest.Pixels, destOffset, count);
        }
    }

    /// <summary>
    /// Performs a Copy operation from source BitmapContext to destination Array
    /// </summary>
    /// <remarks>Equivalent to calling Buffer.BlockCopy in Silverlight, or native memcpy in WPF</remarks>
    [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
    public static unsafe void BlockCopy(BitmapContext src, int srcOffset, byte[] dest, int destOffset, int count)
    {
        fixed (byte* destPtr = dest)
        {
            BitmapContext.CopyUnmanagedMemory((byte*)src.Pixels, srcOffset, destPtr, destOffset, count);
        }
    }

    /// <summary>
    /// Performs a Copy operation from source BitmapContext to destination Array
    /// </summary>
    /// <remarks>Equivalent to calling Buffer.BlockCopy in Silverlight, or native memcpy in WPF</remarks>
    [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
    public static unsafe void BlockCopy(BitmapContext src, int srcOffset, int[] dest, int destOffset, int count)
    {
        fixed (int* destPtr = dest)
        {
            BitmapContext.CopyUnmanagedMemory((byte*)src.Pixels, srcOffset, (byte*)destPtr, destOffset, count);
        }
    }

    /// <summary>
    /// Clears the BitmapContext, filling the underlying bitmap with zeros
    /// </summary>
    [System.Runtime.TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
    public readonly void Clear()
    {
        BitmapContext.SetUnmanagedMemory((IntPtr)_backBuffer, 0, _backBufferStride * _pixelHeight);
    }

    /// <summary>
    /// Disposes the BitmapContext, unlocking it and invalidating if WPF
    /// </summary>
    public readonly void Dispose()
    {
        // Decrement the update count. If it hits zero
        if (DecrementRefCount(_writeableBitmap) == 0)
        {
            // Remove this bitmap from the update map 
            UpdateCountByBmp.Remove(_writeableBitmap);
            BitmapPropertiesByBmp.Remove(_writeableBitmap);

            // Invalidate the bitmap if ReadWrite _mode
            if (_mode == ReadWriteMode.ReadWrite)
            {
                _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _pixelWidth, _pixelHeight));
            }

            // Unlock the bitmap
            _writeableBitmap.Unlock();
        }
    }

    private static void IncrementRefCount(WriteableBitmap target)
    {
        UpdateCountByBmp[target]++;
    }

    private static int DecrementRefCount(WriteableBitmap target)
    {
        if (!UpdateCountByBmp.TryGetValue(target, out int current))
        {
            return -1;
        }
        current--;
        UpdateCountByBmp[target] = current;
        return current;
    }

    private struct BitmapContextBitmapProperties
    {
        public int Width;
        public int Height;
        public int* Pixels;
        public System.Windows.Media.PixelFormat Format;
        public int BackBufferStride;
    }

    /// <see cref="IEquatable{BitmapContext}"/>

    public override bool Equals(object obj)
    {
        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    public static bool operator ==(BitmapContext left, BitmapContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BitmapContext left, BitmapContext right)
    {
        return !(left == right);
    }

    [TargetedPatchingOptOut("Internal method only, inlined across NGen boundaries for performance reasons")]
    internal static unsafe void CopyUnmanagedMemory(byte* srcPtr, int srcOffset, byte* dstPtr, int dstOffset, int count)
    {
        srcPtr += srcOffset;
        dstPtr += dstOffset;

        memcpy(dstPtr, srcPtr, count);
    }

    [TargetedPatchingOptOut("Internal method only, inlined across NGen boundaries for performance reasons")]
    internal static void SetUnmanagedMemory(IntPtr dst, int filler, int count)
    {
        memset(dst, filler, count);
    }

    // Win32 memory copy function
    //[DllImport("ntdll.dll")]
    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    public static extern unsafe void* memcpy(
        void* dst,
        void* src,
        int count);

    // Win32 memory copy function
    //[DllImport("ntdll.dll")]
    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern unsafe byte* memcpy(
        byte* dst,
        byte* src,
        int count);

    // Win32 memory set function
    //[DllImport("ntdll.dll")]
    //[DllImport("coredll.dll", EntryPoint = "memset", SetLastError = false)]
    [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    private static extern void memset(
        IntPtr dst,
        int filler,
        int count);
}
