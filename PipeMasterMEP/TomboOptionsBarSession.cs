using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Windows;

namespace PipeMasterMEP;

public sealed class TomboOptionsBarSession : IDisposable
{
    private readonly FrameworkElement _bar;

    private Panel _targetPanel;

    private FrameworkElement _injectedControl;

    private Dictionary<UIElement, Visibility> _savedVisibilities = new Dictionary<UIElement, Visibility>();

    private bool _active;

    private static FrameworkElement _cachedBar;

    private TomboOptionsBarSession(FrameworkElement bar)
    {
        _bar = bar;
    }

    public static TomboOptionsBarSession Begin(FrameworkElement customControl)
    {
        FrameworkElement bar = FindBar();
        if (bar == null)
        {
            return null;
        }
        TomboOptionsBarSession session = new TomboOptionsBarSession(bar);
        session.Inject(customControl);
        return session;
    }

    private static FrameworkElement FindBar()
    {
        //IL_01dc: Unknown result type (might be due to invalid IL or missing references)
        //IL_01e3: Unknown result type (might be due to invalid IL or missing references)
        //IL_01e8: Unknown result type (might be due to invalid IL or missing references)
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
                if (fullType.IndexOf("OptionsBar", StringComparison.OrdinalIgnoreCase) >= 0 || fullType.IndexOf("OptionBar", StringComparison.OrdinalIgnoreCase) >= 0 || (fe.Name != null && fe.Name.IndexOf("option", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _cachedBar = fe;
                    return fe;
                }
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
                            _cachedBar = fe;
                            return fe;
                        }
                    }
                }
                catch
                {
                }
            }
            cursor = parent;
        }
        return null;
    }

    private void Inject(FrameworkElement newContent)
    {
        _injectedControl = newContent;
        _targetPanel = FindVisualChild<Panel>((DependencyObject)(object)_bar) ?? (_bar as Panel);
        if (_targetPanel != null)
        {
            foreach (UIElement child in _targetPanel.Children)
            {
                _savedVisibilities[child] = child.Visibility;
                child.Visibility = Visibility.Collapsed;
            }
            _targetPanel.Children.Insert(0, _injectedControl);
        }
        else if (_bar is ContentControl cc)
        {
            Grid grid = new Grid();
            if (cc.Content is UIElement oldUI)
            {
                _savedVisibilities[oldUI] = oldUI.Visibility;
                oldUI.Visibility = Visibility.Collapsed;
                cc.Content = null;
                grid.Children.Add(oldUI);
            }
            grid.Children.Add(_injectedControl);
            cc.Content = grid;
        }
        _active = true;
    }

    public void Dispose()
    {
        if (!_active)
        {
            return;
        }
        if (_targetPanel != null)
        {
            _targetPanel.Children.Remove(_injectedControl);
            foreach (KeyValuePair<UIElement, Visibility> kvp in _savedVisibilities)
            {
                kvp.Key.Visibility = kvp.Value;
            }
        }
        else if (_bar is ContentControl { Content: Grid grid } cc)
        {
            grid.Children.Remove(_injectedControl);
            if (grid.Children.Count > 0)
            {
                UIElement oldUI = grid.Children[0];
                grid.Children.Remove(oldUI);
                oldUI.Visibility = (_savedVisibilities.ContainsKey(oldUI) ? _savedVisibilities[oldUI] : Visibility.Visible);
                cc.Content = oldUI;
            }
            else
            {
                cc.Content = null;
            }
        }
        _active = false;
    }

    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            return default(T);
        }
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            T t = (T)(object)((child is T) ? child : null);
            if (t != null)
            {
                return t;
            }
            T childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return default(T);
    }
}
