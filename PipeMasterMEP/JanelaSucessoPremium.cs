using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PipeMasterMEP;

public class JanelaSucessoPremium : Window
{
    public JanelaSucessoPremium(string titulo, string mensagem)
    {
        Color bgMain = PipeMasterTheme.Background;
        Color bgCard = PipeMasterTheme.Surface;
        Color strokeColor = PipeMasterTheme.Border;
        Color accentColor = PipeMasterTheme.Accent;
        Color textMain = PipeMasterTheme.Text;
        Color textMuted = PipeMasterTheme.TextMuted;
        base.Title = "PipeMaster [M] - " + titulo;
        base.Width = 460.0;
        base.SizeToContent = SizeToContent.Height;
        base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        base.ResizeMode = ResizeMode.NoResize;
        base.Topmost = true;
        base.Background = new SolidColorBrush(bgMain);
        base.FontFamily = new FontFamily("Segoe UI");
        StackPanel root = new StackPanel();
        Border header = new Border
        {
            Background = new SolidColorBrush(bgCard),
            BorderBrush = new SolidColorBrush(accentColor),
            BorderThickness = new Thickness(0.0, 0.0, 0.0, 2.0),
            Padding = new Thickness(24.0, 18.0, 24.0, 18.0)
        };
        header.Child = new StackPanel
        {
            Children =
            {
                (UIElement)new TextBlock
                {
                    Text = titulo,
                    Foreground = new SolidColorBrush(textMain),
                    FontSize = 18.0,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
                },
                (UIElement)new TextBlock
                {
                    Text = "Operação concluída com sucesso.",
                    Foreground = new SolidColorBrush(textMuted),
                    FontSize = 12.0
                }
            }
        };
        root.Children.Add(header);
        Border body = new Border
        {
            Background = new SolidColorBrush(bgMain),
            Padding = new Thickness(24.0, 30.0, 24.0, 30.0)
        };
        StackPanel bStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        TextBlock checkText = new TextBlock
        {
            Text = "✔",
            Foreground = new SolidColorBrush(accentColor),
            FontSize = 32.0,
            Margin = new Thickness(0.0, 0.0, 16.0, 0.0),
            VerticalAlignment = VerticalAlignment.Center
        };
        TextBlock msgBlock = new TextBlock
        {
            Text = mensagem,
            Foreground = new SolidColorBrush(textMain),
            FontSize = 14.0,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 320.0
        };
        bStack.Children.Add(checkText);
        bStack.Children.Add(msgBlock);
        body.Child = bStack;
        root.Children.Add(body);
        Border footer = new Border
        {
            Background = new SolidColorBrush(bgCard),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
            Padding = new Thickness(20.0, 12.0, 20.0, 12.0)
        };
        StackPanel painelBotoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button btnOK = new Button
        {
            Content = "Continuar ▶",
            Height = 32.0,
            Padding = new Thickness(20.0, 0.0, 20.0, 0.0),
            Background = new SolidColorBrush(accentColor),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0.0),
            Cursor = Cursors.Hand
        };
        btnOK.Click += delegate
        {
            Close();
        };
        painelBotoes.Children.Add(btnOK);
        footer.Child = painelBotoes;
        root.Children.Add(footer);
        base.Content = root;
        DoubleAnimation fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200.0)),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        base.Loaded += delegate
        {
            BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
    }
}
