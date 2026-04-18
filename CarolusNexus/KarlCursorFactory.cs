using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace CarolusNexus;

/// <summary>
/// Rasterisiert die klassische Büroklammer-Silhouette in einen plattform-Cursor (Hotspot an der unteren „Spitze“).
/// </summary>
public static class KarlCursorFactory
{
    private const string ClipPathData =
        "M 58 6 C 78 6 94 22 94 44 L 94 158 C 94 180 76 196 54 196 C 32 196 14 180 14 158 L 14 48 C 14 26 32 8 54 8 C 72 8 86 22 86 42 L 86 150 C 86 164 74 176 58 176 C 42 176 30 164 30 150 L 30 52 C 30 40 40 30 54 30 C 68 30 78 40 78 52 L 78 138 C 78 146 70 152 60 152 C 50 152 44 146 44 138 L 44 62 C 44 56 48 52 54 52 C 60 52 64 56 64 62 L 64 128 C 64 130 62 132 60 132 C 58 132 56 130 56 128 L 56 68 C 56 66 57 65 59 65 C 61 65 62 66 62 68 L 62 118";

    public static Cursor Create()
    {
        const double scale = 0.168;
        const double tx = 6;
        const double ty = 3;
        var clip = StreamGeometry.Parse(ClipPathData);
        var fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C)), 2.2);

        var size = new PixelSize(40, 40);
        var dpi = new Vector(96, 96);
        var bitmap = new RenderTargetBitmap(size, dpi);

        var transform = Matrix.CreateTranslation(tx, ty) * Matrix.CreateScale(scale, scale) *
                        Matrix.CreateTranslation(-52, -96);

        using (var ctx = bitmap.CreateDrawingContext())
        {
            using (ctx.PushTransform(transform))
            {
                ctx.DrawGeometry(fill, outline, clip);
                ctx.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1), new Point(73, 29), 9, 11);
                ctx.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1), new Point(91, 31), 9, 11);
                ctx.DrawEllipse(Brushes.Black, null, new Point(72.5, 28.5), 3.2, 3.2);
                ctx.DrawEllipse(Brushes.Black, null, new Point(90.5, 30.5), 3.2, 3.2);
            }
        }

        var tipPath = new Point(56, 188);
        var hotspot = transform.Transform(tipPath);
        var hx = (int)double.Clamp(hotspot.X, 0, size.Width - 1);
        var hy = (int)double.Clamp(hotspot.Y, 0, size.Height - 1);

        return new Cursor(bitmap, new PixelPoint(hx, hy));
    }
}
