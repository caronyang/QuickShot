using System.Windows.Media.Imaging;
using QuickShot.Services;
using Drawing = System.Drawing;

namespace QuickShot.Models;

/// <summary>
/// One captured region.
/// Memory strategy: the full resolution pixels are only ever held as encoded PNG bytes;
/// the floating window renders a down-scaled <see cref="Preview"/>. The full resolution
/// decode is materialised on demand (100% zoom / clipboard) and released again.
/// </summary>
public sealed class Snapshot
{
    private static long _counter;
    private BitmapSource? _fullImage;

    public Snapshot(byte[] png, int pixelWidth, int pixelHeight, Drawing.Rectangle physicalBounds, BitmapSource preview)
    {
        Png = png;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        PhysicalBounds = physicalBounds;
        Preview = preview;
        Sequence = Interlocked.Increment(ref _counter);
        CreatedLocal = DateTime.Now;
    }

    /// <summary>Lossless PNG of the captured region at native resolution.</summary>
    public byte[] Png { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    /// <summary>Where the region was on the virtual desktop, in physical pixels.</summary>
    public Drawing.Rectangle PhysicalBounds { get; }

    /// <summary>Down-scaled, frozen preview used by the floating window.</summary>
    public BitmapSource Preview { get; }

    /// <summary>Monotonic creation order, used to pick the "oldest" snapshot.</summary>
    public long Sequence { get; }

    public DateTime CreatedLocal { get; }

    public bool IsPinned { get; set; } = true;

    /// <summary>True while the floating window is rolled up into a small thumbnail.</summary>
    public bool IsCollapsed { get; set; }

    public string SizeText => $"{PixelWidth}×{PixelHeight}";

    public string TimeText => CreatedLocal.ToString("HH:mm:ss");

    /// <summary>Full resolution image; decoded lazily and cached until <see cref="ReleaseFullImage"/>.</summary>
    public BitmapSource GetFullImage() => _fullImage ??= ScreenCaptureService.DecodePng(Png);

    public void ReleaseFullImage() => _fullImage = null;
}
