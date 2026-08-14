using System.Windows;
using System.Windows.Markup;

namespace PipeMasterMEP;

public partial class JanelaMapeamentoAparelhos : Window, IComponentConnector
{
    public bool Confirmado { get; private set; } = false;

    public JanelaMapeamentoAparelhos(MapeamentoAparelhosViewModel vm)
    {
        InitializeComponent();
        base.DataContext = vm;
    }

    private void BtnImportar_Click(object sender, RoutedEventArgs e)
    {
        Confirmado = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        Confirmado = false;
        Close();
    }
}
