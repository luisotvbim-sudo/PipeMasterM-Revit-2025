using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PipeMasterMEP;

public class JanelaConfigAgua : Window
{
    private readonly LancamentoAguaViewModel _vm;

    private readonly ComboBox _cmbPerfis;

    private readonly TextBox _txtNovoNome;

    private readonly Dictionary<string, TextBox> _txtAlturas = new Dictionary<string, TextBox>();

    private readonly Dictionary<string, TextBox> _txtOffsets = new Dictionary<string, TextBox>();

    public JanelaConfigAgua(LancamentoAguaViewModel vm)
    {
        _vm = vm;
        base.Title = "PipeMaster - Configurações do Lançamento";
        base.Width = 520.0;
        base.Height = 680.0;
        base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        base.ResizeMode = ResizeMode.NoResize;
        base.Topmost = true;
        base.Background = PipeMasterTheme.Brush(PipeMasterTheme.Background);
        base.FontFamily = new FontFamily("Segoe UI");
        Grid root = new Grid
        {
            Margin = new Thickness(20.0)
        };
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1.0, GridUnitType.Star)
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        TextBlock tituloJanela = new TextBlock
        {
            Text = "CONFIGURAÇÕES E PADRÕES",
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.Accent),
            FontWeight = FontWeights.Bold,
            FontSize = 14.0,
            Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
        };
        Grid.SetRow(tituloJanela, 0);
        root.Children.Add(tituloJanela);
        StackPanel corpo = new StackPanel();
        Border cardPresets = NovoCard();
        StackPanel stPresets = new StackPanel
        {
            Children = { (UIElement)Titulo("PADRÕES (PRESETS)") }
        };
        _cmbPerfis = new ComboBox
        {
            Height = 24.0,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
        };
        AtualizarListaPerfis();
        stPresets.Children.Add(_cmbPerfis);
        Grid linhaSalvarExcluir = new Grid
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
        };
        linhaSalvarExcluir.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.0, GridUnitType.Star)
        });
        linhaSalvarExcluir.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });
        Button btnSalvar = BotaoPrimario("SALVAR", 90.0);
        btnSalvar.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
        btnSalvar.Click += delegate
        {
            SalvarSelecionado();
        };
        Grid.SetColumn(btnSalvar, 0);
        linhaSalvarExcluir.Children.Add(btnSalvar);
        Button btnExcluir = BotaoSecundario("EXCLUIR", 80.0);
        btnExcluir.Margin = new Thickness(0.0);
        btnExcluir.Click += delegate
        {
            ExcluirPerfil();
        };
        Grid.SetColumn(btnExcluir, 1);
        linhaSalvarExcluir.Children.Add(btnExcluir);
        stPresets.Children.Add(linhaSalvarExcluir);
        Grid linhaSalvar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition
                {
                    Width = new GridLength(1.0, GridUnitType.Star)
                },
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                }
            }
        };
        _txtNovoNome = new TextBox
        {
            Height = 24.0,
            Background = PipeMasterTheme.Brush(PipeMasterTheme.Control),
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.Text),
            BorderBrush = PipeMasterTheme.Brush(PipeMasterTheme.Border),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
        };
        Grid.SetColumn(_txtNovoNome, 0);
        linhaSalvar.Children.Add(_txtNovoNome);
        Button btnSalvarComo = BotaoPrimario("SALVAR COMO...", 120.0);
        btnSalvarComo.Click += delegate
        {
            SalvarComo();
        };
        Grid.SetColumn(btnSalvarComo, 1);
        linhaSalvar.Children.Add(btnSalvarComo);
        stPresets.Children.Add(linhaSalvar);
        stPresets.Children.Add(new TextBlock
        {
            Text = "Selecionar um padrão já carrega automaticamente as alturas/offsets abaixo e as configurações gerais (prumada, registro, ramal, recuo, diâmetros). Ex.: Padrão Cliente A, Padrão Escritório.",
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.TextMuted),
            FontSize = 10.0,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
        });
        cardPresets.Child = stPresets;
        corpo.Children.Add(cardPresets);
        Border cardPontos = NovoCard();
        StackPanel stPontos = new StackPanel
        {
            Children = { (UIElement)Titulo("ALTURAS E OFFSETS DOS PONTOS") }
        };
        Grid cab = new Grid
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        cab.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.0, GridUnitType.Star)
        });
        cab.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(80.0)
        });
        cab.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(80.0)
        });
        TextBlock h1 = Rotulo("Tipo do Ponto");
        Grid.SetColumn(h1, 0);
        cab.Children.Add(h1);
        TextBlock h2 = Rotulo("Altura (m)");
        Grid.SetColumn(h2, 1);
        cab.Children.Add(h2);
        TextBlock h3 = Rotulo("Offset (cm)");
        Grid.SetColumn(h3, 2);
        cab.Children.Add(h3);
        stPontos.Children.Add(cab);
        foreach (string tipo in LancamentoAguaViewModel.TiposPonto)
        {
            if (!(tipo == "Outro"))
            {
                Grid linha = new Grid
                {
                    Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
                };
                linha.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1.0, GridUnitType.Star)
                });
                linha.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(80.0)
                });
                linha.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(80.0)
                });
                TextBlock lbl = new TextBlock
                {
                    Text = tipo,
                    Foreground = PipeMasterTheme.Brush(PipeMasterTheme.Text),
                    FontSize = 12.0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lbl, 0);
                linha.Children.Add(lbl);
                TextBox txtA = CampoNumero(LancamentoAguaViewModel.AlturaPadrao(tipo).ToString("N2", CultureInfo.CurrentCulture));
                Grid.SetColumn(txtA, 1);
                linha.Children.Add(txtA);
                _txtAlturas[tipo] = txtA;
                TextBox txtO = CampoNumero((LancamentoAguaViewModel.OffsetLateralPadrao(tipo) * 100.0).ToString("N0", CultureInfo.CurrentCulture));
                Grid.SetColumn(txtO, 2);
                linha.Children.Add(txtO);
                _txtOffsets[tipo] = txtO;
                stPontos.Children.Add(linha);
            }
        }
        cardPontos.Child = stPontos;
        corpo.Children.Add(cardPontos);
        _cmbPerfis.SelectionChanged += delegate
        {
            AplicarPresetSelecionado();
        };
        ScrollViewer scroll = new ScrollViewer
        {
            Content = corpo,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        StackPanel rodape = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
        };
        Button btnFechar = BotaoSecundario("CANCELAR", 90.0);
        btnFechar.Click += delegate
        {
            Close();
        };
        Button btnAplicar = BotaoPrimario("APLICAR E FECHAR", 150.0);
        btnAplicar.Click += delegate
        {
            AplicarNoLancamento();
            Close();
        };
        rodape.Children.Add(btnFechar);
        rodape.Children.Add(btnAplicar);
        Grid.SetRow(rodape, 2);
        root.Children.Add(rodape);
        base.Content = root;
    }

    private void AtualizarListaPerfis()
    {
        List<string> nomes = GerenciadorPerfisAgua.Listar();
        _cmbPerfis.ItemsSource = nomes;
        PerfilAgua atual = GerenciadorPerfisAgua.PerfilAtual;
        if (atual != null && nomes.Contains(atual.Nome))
        {
            _cmbPerfis.SelectedItem = atual.Nome;
        }
        else if (nomes.Count > 0)
        {
            _cmbPerfis.SelectedIndex = 0;
        }
    }

    private void AplicarPresetSelecionado()
    {
        string nome = _cmbPerfis.SelectedItem as string;
        if (string.IsNullOrEmpty(nome))
        {
            return;
        }
        PerfilAgua p = GerenciadorPerfisAgua.Carregar(nome);
        if (p != null)
        {
            GerenciadorPerfisAgua.PerfilAtual = p;
            ConfigAguaCache.UltimoPerfil = nome;
            PersistirGeralNoCache(p);
            if (_vm != null)
            {
                _vm.AplicarPerfil(p);
            }
            PreencherCamposDoPerfil(p);
        }
    }

    private void SalvarSelecionado()
    {
        string nome = _cmbPerfis.SelectedItem as string;
        if (string.IsNullOrEmpty(nome))
        {
            MessageBox.Show(this, "Nenhum padrão selecionado. Use 'Salvar como...' para criar um novo.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;
        }
        PerfilAgua p = MontarPerfilDosCampos(nome);
        GerenciadorPerfisAgua.Salvar(p);
        GerenciadorPerfisAgua.PerfilAtual = p;
        ConfigAguaCache.UltimoPerfil = nome;
        PersistirGeralNoCache(p);
        MessageBox.Show(this, "Padrão '" + nome + "' atualizado.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Asterisk);
    }

    private void PreencherCamposDoPerfil(PerfilAgua p)
    {
        foreach (KeyValuePair<string, TextBox> kv in _txtAlturas)
        {
            kv.Value.Text = p.ObterAltura(kv.Key, LancamentoAguaViewModel.AlturaPadraoBase(kv.Key)).ToString("N2", CultureInfo.CurrentCulture);
        }
        foreach (KeyValuePair<string, TextBox> kv2 in _txtOffsets)
        {
            kv2.Value.Text = p.ObterOffsetCm(kv2.Key, LancamentoAguaViewModel.OffsetLateralPadraoBase(kv2.Key) * 100.0).ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    private void ExcluirPerfil()
    {
        string nome = _cmbPerfis.SelectedItem as string;
        if (!string.IsNullOrEmpty(nome) && MessageBox.Show(this, "Excluir o padrão '" + nome + "'?", "PipeMaster", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            GerenciadorPerfisAgua.Excluir(nome);
            if (GerenciadorPerfisAgua.PerfilAtual != null && GerenciadorPerfisAgua.PerfilAtual.Nome == nome)
            {
                GerenciadorPerfisAgua.PerfilAtual = null;
            }
            AtualizarListaPerfis();
        }
    }

    private PerfilAgua MontarPerfilDosCampos(string nome)
    {
        PerfilAgua p = new PerfilAgua
        {
            Nome = nome,
            AlturaPrumada = ((_vm != null) ? _vm.AlturaPrumada : ConfigAguaCache.AlturaPrumada),
            AlturaRegistro = ((_vm != null) ? _vm.AlturaRegistro : ConfigAguaCache.AlturaRegistro),
            AlturaRamal = ((_vm != null) ? _vm.AlturaRamal : ConfigAguaCache.AlturaRamal),
            RecuoParedeCm = ((_vm != null) ? _vm.RecuoParede : ConfigAguaCache.RecuoParedeCm),
            DiametroRamal = ((_vm != null) ? _vm.DiametroRamal : ConfigAguaCache.DiametroRamal),
            DiametroDescida = ((_vm != null) ? _vm.DiametroDescida : ConfigAguaCache.DiametroDescida),
            AlturaRegistroPressao = ((_vm != null) ? _vm.AlturaRegistroPressao : ConfigAguaCache.AlturaRegistroPressao),
            InverterSentidoBucha = ((_vm != null) ? _vm.InverterSentidoBucha : ConfigAguaCache.InverterSentidoBucha),
            DesviarPeloPiso = ((_vm != null) ? _vm.DesviarPeloPiso : ConfigAguaCache.DesviarPeloPiso),
            AlturaPiso = ((_vm != null) ? _vm.AlturaPiso : ConfigAguaCache.AlturaPiso)
        };
        foreach (KeyValuePair<string, TextBox> kv in _txtAlturas)
        {
            if (double.TryParse(kv.Value.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
            {
                p.Alturas[kv.Key] = v;
            }
        }
        foreach (KeyValuePair<string, TextBox> kv2 in _txtOffsets)
        {
            if (double.TryParse(kv2.Value.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v2))
            {
                p.Offsets[kv2.Key] = v2;
            }
        }
        return p;
    }

    private void SalvarComo()
    {
        string nome = GerenciadorPerfisAgua.LimparNome(_txtNovoNome.Text);
        if (string.IsNullOrEmpty(nome))
        {
            MessageBox.Show(this, "Digite um nome para o padrão (ex.: Padrão Cliente A).", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;
        }
        PerfilAgua p = MontarPerfilDosCampos(nome);
        GerenciadorPerfisAgua.Salvar(p);
        GerenciadorPerfisAgua.PerfilAtual = p;
        ConfigAguaCache.UltimoPerfil = nome;
        ConfigAguaCache.Salvar();
        AtualizarListaPerfis();
        _cmbPerfis.SelectedItem = nome;
        MessageBox.Show(this, "Padrão '" + nome + "' salvo.", "PipeMaster", MessageBoxButton.OK, MessageBoxImage.Asterisk);
    }

    private void AplicarNoLancamento()
    {
        PerfilAgua p = (GerenciadorPerfisAgua.PerfilAtual = MontarPerfilDosCampos((GerenciadorPerfisAgua.PerfilAtual != null) ? GerenciadorPerfisAgua.PerfilAtual.Nome : ""));
        if (_vm != null)
        {
            _vm.AplicarValoresPontos(p.Alturas, p.Offsets);
        }
        else
        {
            PersistirGeralNoCache(p);
        }
    }

    private static void PersistirGeralNoCache(PerfilAgua p)
    {
        ConfigAguaCache.AlturaPrumada = p.AlturaPrumada;
        ConfigAguaCache.AlturaRegistro = p.AlturaRegistro;
        ConfigAguaCache.AlturaRamal = p.AlturaRamal;
        ConfigAguaCache.RecuoParedeCm = p.RecuoParedeCm;
        ConfigAguaCache.DiametroRamal = p.DiametroRamal;
        ConfigAguaCache.DiametroDescida = p.DiametroDescida;
        ConfigAguaCache.AlturaRegistroPressao = p.AlturaRegistroPressao;
        ConfigAguaCache.InverterSentidoBucha = p.InverterSentidoBucha;
        ConfigAguaCache.DesviarPeloPiso = p.DesviarPeloPiso;
        ConfigAguaCache.AlturaPiso = p.AlturaPiso;
        ConfigAguaCache.Salvar();
    }

    private static Border NovoCard()
    {
        return new Border
        {
            Background = PipeMasterTheme.Brush(PipeMasterTheme.Surface),
            CornerRadius = new CornerRadius(6.0),
            Padding = new Thickness(15.0),
            Margin = new Thickness(0.0, 0.0, 0.0, 15.0)
        };
    }

    private static TextBlock Titulo(string texto)
    {
        return new TextBlock
        {
            Text = texto,
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.Text),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.0,
            Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
        };
    }

    private static TextBlock Rotulo(string texto)
    {
        return new TextBlock
        {
            Text = texto,
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.TextMuted),
            FontSize = 10.0
        };
    }

    private static TextBox CampoNumero(string valor)
    {
        return new TextBox
        {
            Text = valor,
            Width = 60.0,
            Height = 22.0,
            Background = PipeMasterTheme.Brush(PipeMasterTheme.Control),
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.Text),
            BorderBrush = PipeMasterTheme.Brush(PipeMasterTheme.Border),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static Button BotaoSecundario(string texto, double largura)
    {
        return new Button
        {
            Content = texto,
            Width = largura,
            Height = 30.0,
            Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
            Background = PipeMasterTheme.Brush(PipeMasterTheme.Control),
            Foreground = PipeMasterTheme.Brush(PipeMasterTheme.Text),
            BorderBrush = PipeMasterTheme.Brush(PipeMasterTheme.Border),
            BorderThickness = new Thickness(1.0)
        };
    }

    private static Button BotaoPrimario(string texto, double largura)
    {
        return new Button
        {
            Content = texto,
            Width = largura,
            Height = 30.0,
            Background = PipeMasterTheme.Brush(PipeMasterTheme.Accent),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0.0)
        };
    }
}
