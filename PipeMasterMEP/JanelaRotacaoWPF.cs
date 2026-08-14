using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PipeMasterMEP;

public class JanelaRotacaoWPF : Window
{
    public double AnguloEscolhido { get; private set; } = 0.0;

    public bool Confirmado { get; private set; } = false;

    public JanelaRotacaoWPF()
    {
        base.Title = "Rotacionar Conexão";
        base.Width = 250.0;
        base.Height = 620.0;
        base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        base.ResizeMode = ResizeMode.NoResize;
        base.Background = new SolidColorBrush(Color.FromRgb(40, 44, 52));
        StackPanel painel = new StackPanel
        {
            Margin = new Thickness(15.0)
        };
        TextBlock lblTitulo = new TextBlock
        {
            Text = "Escolha o Ângulo",
            Foreground = Brushes.White,
            FontSize = 14.0,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0.0, 0.0, 0.0, 15.0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        painel.Children.Add(lblTitulo);
        double[] angulos = new double[13]
        {
            11.25, 22.5, 30.0, 45.0, 60.0, 90.0, 180.0, -90.0, -60.0, -45.0,
            -30.0, -22.5, -11.25
        };
        double[] array = angulos;
        for (int i = 0; i < array.Length; i++)
        {
            double angulo = array[i];
            Button btn = new Button
            {
                Content = angulo + "°",
                Height = 32.0,
                Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
                Background = new SolidColorBrush(Color.FromRgb(50, 54, 62)),
                Foreground = ((angulo < 0.0) ? Brushes.LightCoral : Brushes.LightSkyBlue),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0.0)
            };
            btn.Click += delegate
            {
                AnguloEscolhido = angulo;
                Confirmado = true;
                Close();
            };
            painel.Children.Add(btn);
        }
        Button btnCancel = new Button
        {
            Content = "Cancelar",
            Height = 35.0,
            Margin = new Thickness(0.0, 15.0, 0.0, 0.0),
            Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0.0)
        };
        btnCancel.Click += delegate
        {
            Close();
        };
        painel.Children.Add(btnCancel);
        base.Content = painel;
    }
}
