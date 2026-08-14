using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace PipeMasterMEP;

public partial class JanelaLancamentoAgua : Window, IComponentConnector
{
    private readonly LancamentoAguaViewModel _viewModel;

    private readonly Action<PecaAguaDetectada> _destacarPeca;

    private readonly Action _limparDestaque;

    private int _etapa = 1;

    public bool Result { get; private set; } = false;

    public bool SolicitarSelecaoJanela { get; private set; } = false;

    public JanelaLancamentoAgua(LancamentoAguaViewModel viewModel, Action<PecaAguaDetectada> destacarPeca = null, Action limparDestaque = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _destacarPeca = destacarPeca;
        _limparDestaque = limparDestaque;
        base.DataContext = viewModel;
        AtualizarEtapa();
    }

    private void AtualizarEtapa()
    {
        bool primeira = _etapa == 1;
        Pagina1.Visibility = ((!primeira) ? Visibility.Collapsed : Visibility.Visible);
        Pagina2.Visibility = (primeira ? Visibility.Collapsed : Visibility.Visible);
        BtnVoltar.Visibility = (primeira ? Visibility.Collapsed : Visibility.Visible);
        BtnAvancar.Content = (primeira ? "AVANÇAR  ▶" : "GERAR TUBULAÇÃO");
        TxtEtapa.Text = (primeira ? "ETAPA 1/2 — SISTEMA E APARELHOS" : "ETAPA 2/2 — CONFIGURAÇÕES");
        TxtRodape.Text = (primeira ? "Clique numa linha para destacar o aparelho na planta." : "Após gerar, clique na face interna da parede onde ficará o registro.");
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ListaPecas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!(ListaPecas.SelectedItem is PecaAguaItemViewModel { Origem: not null } item))
        {
            return;
        }
        try
        {
            _destacarPeca?.Invoke(item.Origem);
        }
        catch
        {
        }
    }

    private void BtnMapeamento_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MapeamentoAparelhosViewModel vmMap = _viewModel.ObterMapeamentoViewModel();
            JanelaMapeamentoAparelhos dlg = new JanelaMapeamentoAparelhos(vmMap)
            {
                Owner = this
            };
            dlg.ShowDialog();
            if (dlg.Confirmado)
            {
                vmMap.SalvarMapeamento();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível abrir o mapeamento de famílias:\n" + ex.Message, "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private void BtnConfigGeral_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            JanelaConfigAgua dlg = new JanelaConfigAgua(_viewModel)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível abrir as configurações:\n" + ex.Message, "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private void BtnSelecionarAparelhos_Click(object sender, RoutedEventArgs e)
    {
        SolicitarSelecaoJanela = true;
        Close();
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void BtnVoltar_Click(object sender, RoutedEventArgs e)
    {
        _etapa = 1;
        AtualizarEtapa();
    }

    private void BtnAvancar_Click(object sender, RoutedEventArgs e)
    {
        if (_etapa == 1)
        {
            if (!_viewModel.Pecas.Any((PecaAguaItemViewModel p) => p.Selecionada))
            {
                MessageBox.Show(this, "Selecione ao menos um aparelho para o lançamento.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            _viewModel.AtualizarResumo();
            _etapa = 2;
            AtualizarEtapa();
        }
        else if (_viewModel.SistemaSelecionado == null)
        {
            MessageBox.Show(this, "Nenhum sistema de tubulação de água encontrado no projeto.\nCrie um sistema de Água Fria/Quente (Domestic Cold/Hot Water) e tente novamente.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        else if (_viewModel.TipoTuboSelecionado == null)
        {
            MessageBox.Show(this, "Nenhum tipo de tubo (PipeType) encontrado no projeto.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        else if (_viewModel.AlturaPrumada <= _viewModel.AlturaRamal + 0.1)
        {
            MessageBox.Show(this, "A altura de início da prumada deve ser maior que a altura do ramal na parede.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        else
        {
            Result = true;
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _limparDestaque?.Invoke();
        }
        catch
        {
        }
        base.OnClosed(e);
    }
}
