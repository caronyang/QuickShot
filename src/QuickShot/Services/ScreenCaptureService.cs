using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace QuickShot.Services;

/// <summary>
/// GDI screen capture plus PNG/crop helpers.
/// <see cref="CaptureBgra"/> is deliberately WPF-free so it can run on a worker thread;
/// the WPF objects are created on the UI thread afterwards.
/// </summary>
internal static class ScreenCaptureService
{
    /// <summary>Grabs <paramref name="rect"/> (physical pixels) as a top-down BGRA32 buffer.</summary>
    public static byte[] CaptureBgra(Drawing.Rectangle rect, out int width, out int height)
    {
        width = Math.Max(1, rect.Width);
        height = Math.Max(1, rect.Height);

        using var bitmap = new Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, new Drawing.Size(width, height), Drawing.CopyPixelOperation.SourceCopy);
        }

        var stride = width * 4;
        var buffer = new byte[stride * height];

        var data = bitmap.LockBits(new Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            if (data.Stride > 0)
            {
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            }
            else
            {
                // Bottom-up bitmap: copy row by row into a top-down buffer.
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), buffer, (height - 1 - y) * stride, stride);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return buffer;
    }

    /// <summary>UI thread only.</summary>
    public static BitmapSource ToBitmapSource(byte[] bgra, int width, int height)
    {
        var writeable = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        writeable.WritePixels(new Int32Rect(0, 0, width, height), bgra, width * 4, 0);
        writeable.Freeze();
        return writeable;
    }

    public static BitmapSource Crop(BitmapSource source, Int32Rect rect)
    {
        rect = Clamp(rect, source.PixelWidth, source.PixelHeight);
        var cropped = new CroppedBitmap(source, rect);
        cropped.Freeze();
        return cropped;
    }

    public static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes PNG bytes. When <paramref name="maxWidth"/> is given (and smaller than the
    /// natural width) the image is decoded down-scaled - that is what keeps 20 floating
    /// snapshots cheap.
    /// </summary>
    public static BitmapSource DecodePng(byte[] png, int maxWidth = 0)
    {
        using var stream = new MemoryStream(png, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.StreamSource = stream;
        if (maxWidth > 0) image.DecodePixelWidth = maxWidth;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>Natural width of the PNG without materialising the full bitmap.</summary>
    public static (int Width, int Height) ReadPngSize(byte[] png)
    {
        using var stream = new MemoryStream(png, writable: false);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        return (frame.PixelWidth, frame.PixelHeight);
    }

    private static Int32Rect Clamp(Int32Rect rect, int maxWidth, int maxHeight)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, maxWidth - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, maxHeight - 1));
        var w = Math.Clamp(rect.Width, 1, maxWidth - x);
        var h = Math.Clamp(rect.Height, 1, maxHeight - y);
        return new Int32Rect(x, y, w, h);
    }
}
