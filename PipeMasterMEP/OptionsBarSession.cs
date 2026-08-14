using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PipeMasterMEP;

public sealed class OptionsBarSession : IDisposable
{
    private readonly FrameworkElement _bar;

    private Panel _targetPanel;

    private FrameworkElement _injectedControl;

    private Dictionary<UIElement, Visibility> _savedVisibilities = new Dictionary<UIElement, Visibility>();

    private bool _active;

    private OptionsBarSession(FrameworkElement bar)
    {
        _bar = bar;
    }

    public static OptionsBarSession Begin(FrameworkElement customControl)
    {
        FrameworkElement bar = OptionsBarLocator.Find();
        if (bar == null)
        {
            return null;
        }
        OptionsBarSession session = new OptionsBarSession(bar);
        session.Inject(customControl);
        return session;
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
