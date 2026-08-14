using System;
using System.Windows;
using System.Windows.Media;
using Autodesk.Windows;

namespace PipeMasterMEP;

public static class OptionsBarLocator
{
    private static FrameworkElement _cachedBar;

    public static FrameworkElement Find()
    {
        //IL_01d8: Unknown result type (might be due to invalid IL or missing references)
        //IL_01df: Unknown result type (might be due to invalid IL or missing references)
        //IL_01e4: Unknown result type (might be due to invalid IL or missing references)
        if (_cachedBar != null && PresentationSource.FromVisual(_cachedBar) != null)
        {
            return _cachedBar;
        }
        DependencyObject ribbon = (DependencyObject)(object)ComponentManager.Ribbon;
        if (ribbon == null)
        {
            return null;
        }
        DependencyObject cursor = ribbon;
        for (int depth = 0; depth < 6; depth++)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(cursor);
            if (parent == null)
            {
                break;
            }
            int n = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < n; i++)
            {
                if (!(VisualTreeHelper.GetChild(parent, i) is FrameworkElement fe) || (object)fe == cursor)
                {
                    continue;
                }
                string fullType = ((object)fe).GetType().FullName ?? string.Empty;
                if (fullType.IndexOf("StatusBar", StringComparison.OrdinalIgnoreCase) >= 0 || fullType.IndexOf("StatusStrip", StringComparison.OrdinalIgnoreCase) >= 0 || fullType.IndexOf("Footer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                if (fullType.IndexOf("OptionsBar", StringComparison.OrdinalIgnoreCase) < 0 && fullType.IndexOf("OptionBar", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    string name = fe.Name;
                    if (name == null || name.IndexOf("option", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        if (!(fe.ActualHeight > 4.0) || !(fe.ActualHeight < 48.0) || !(fe.ActualWidth > 400.0) || fe is RibbonControl || fullType.IndexOf("Ribbon", StringComparison.OrdinalIgnoreCase) >= 0 || VisualTreeHelper.GetChildrenCount((DependencyObject)(object)fe) <= 0)
                        {
                            continue;
                        }
                        try
                        {
                            Window win = Window.GetWindow((DependencyObject)(object)fe);
                            if (win != null)
                            {
                                Point pos = fe.TranslatePoint(new Point(0.0, 0.0), win);
                                if (pos.Y < 250.0)
                                {
                                    return _cachedBar = fe;
                                }
                            }
                        }
                        catch
                        {
                        }
                        continue;
                    }
                }
                return _cachedBar = fe;
            }
            cursor = parent;
        }
        return null;
    }
}
