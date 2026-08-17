using System.Windows.Media;

namespace PipeMasterMEP;

internal static class PipeMasterTheme
{
    public static readonly Color Background = Color.FromRgb(230, 230, 230);

    public static readonly Color Surface = Color.FromRgb(242, 242, 242);

    public static readonly Color Control = Colors.White;

    public static readonly Color Border = Color.FromRgb(200, 200, 200);

    public static readonly Color Text = Color.FromRgb(51, 51, 51);

    public static readonly Color TextMuted = Color.FromRgb(107, 107, 107);

    public static readonly Color Accent = Color.FromRgb(245, 124, 0);

    public static readonly Color Success = Color.FromRgb(46, 125, 50);

    public static SolidColorBrush Brush(Color color) => new SolidColorBrush(color);
}
