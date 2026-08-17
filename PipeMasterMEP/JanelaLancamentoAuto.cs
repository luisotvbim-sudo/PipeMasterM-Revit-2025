using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeMasterMEP;

public class JanelaLancamentoAuto : Window
{
    private ComboBox _cmbSistema;

    private ComboBox _cmbTipoTubo;

    private TextBox _txtElevacao;

    private ComboBox _cmbDiamLavatorio;

    private TextBox _txtAltLavatorio;

    private CheckBox _chkDesvioViga;

    private TextBox _txtAltVentCavalete;

    private TextBox _txtDistVaso;

    private CheckBox _chkVaso;

    private CheckBox _chkCaixa;

    private CheckBox _chkLavatorio;

    private CheckBox _chkChuveiro;

    private CheckBox _chkPia;

    private CheckBox _chkMaquina;

    private CheckBox _chkBloquearHorizontais;

    private RadioButton _rbVentBaixo;

    private RadioButton _rbVentCavalete;

    private RadioButton _rbRotReto;

    private RadioButton _rbRotIncl;

    private RadioButton _rbRotInclJ45;

    private RadioButton _rbVasoLivre;

    private RadioButton _rbVasoPrumada;

    private RadioButton _rbVasoMultiplo;

    private RadioButton _rbCaixaLivre;

    private RadioButton _rbCaixaPrumada;

    private RadioButton _rbCaixaMultiplo;

    private RadioButton _rbCaixaColetor;

    private RadioButton _rbPiaLivre;

    private RadioButton _rbPiaPrumada;

    private RadioButton _rbPiaMultiplo;

    private RadioButton _rbMaquinaLivre;

    private RadioButton _rbMaquinaPrumada;

    private RadioButton _rbMaquinaMultiplo;

    private StackPanel _pnlCaixaDestino;

    private StackPanel _pnlVasoDestino;

    private StackPanel _pnlPiaDestino;

    private StackPanel _pnlMaquinaDestino;

    private TextBlock _tituloConfigLavPia;

    private TextBlock _tituloDestino;

    private bool _isVentilacaoAtiva = false;

    private readonly System.Windows.Media.Color bgMain = PipeMasterTheme.Background;

    private readonly System.Windows.Media.Color bgCard = PipeMasterTheme.Surface;

    private readonly System.Windows.Media.Color bgControl = PipeMasterTheme.Control;

    private readonly System.Windows.Media.Color strokeColor = PipeMasterTheme.Border;

    private readonly System.Windows.Media.Color accentColor = PipeMasterTheme.Accent;

    private readonly System.Windows.Media.Color textMain = PipeMasterTheme.Text;

    private readonly System.Windows.Media.Color textMuted = PipeMasterTheme.TextMuted;

    public ConfigLancamentoAuto Configuracao { get; private set; } = new ConfigLancamentoAuto();

    public JanelaLancamentoAuto(Document doc)
    {
        ConfigCache.Carregar();
        base.Title = "F.A Projetos | PipeMaster [M] - Lançamento Automático";
        base.Width = 750.0;
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
                    Text = "Lançamento Automático",
                    Foreground = new SolidColorBrush(textMain),
                    FontSize = 18.0,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
                },
                (UIElement)new TextBlock
                {
                    Text = "Traçado guiado por cliques e projeções de rota.",
                    Foreground = new SolidColorBrush(textMuted),
                    FontSize = 12.0
                }
            }
        };
        root.Children.Add(header);
        StackPanel tabContainer = new StackPanel
        {
            Margin = new Thickness(20.0, 10.0, 20.0, 20.0)
        };
        StackPanel tabHeaderPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        Border activeTabBorder = new Border
        {
            Background = new SolidColorBrush(bgCard),
            BorderBrush = new SolidColorBrush(accentColor),
            BorderThickness = new Thickness(1.0, 1.0, 1.0, 2.0),
            Padding = new Thickness(16.0, 8.0, 16.0, 8.0),
            CornerRadius = new CornerRadius(4.0, 4.0, 0.0, 0.0)
        };
        activeTabBorder.Child = new TextBlock
        {
            Text = "Rede de Esgoto",
            Foreground = new SolidColorBrush(accentColor),
            FontSize = 13.0,
            FontWeight = FontWeights.SemiBold
        };
        Border inactiveTabBorder = new Border
        {
            Background = new SolidColorBrush(bgMain),
            BorderBrush = new SolidColorBrush(bgMain),
            BorderThickness = new Thickness(0.0, 0.0, 0.0, 2.0),
            Padding = new Thickness(16.0, 8.0, 16.0, 8.0),
            Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
            CornerRadius = new CornerRadius(4.0, 4.0, 0.0, 0.0)
        };
        inactiveTabBorder.Child = new TextBlock
        {
            Text = "Rede de Ventilação",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 13.0,
            FontWeight = FontWeights.Normal
        };
        tabHeaderPanel.Children.Add(activeTabBorder);
        tabHeaderPanel.Children.Add(inactiveTabBorder);
        Border tabLine = new Border
        {
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
            Margin = new Thickness(0.0, -1.0, 0.0, 15.0)
        };
        StackPanel contentEsgoto = new StackPanel
        {
            Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
        };
        StackPanel contentVentilacao = new StackPanel
        {
            Margin = new Thickness(0.0, 5.0, 0.0, 0.0),
            Visibility = System.Windows.Visibility.Collapsed
        };
        Action ativarAbaEsgoto = delegate
        {
            ConfigCache.TabAtiva = 0;
            _isVentilacaoAtiva = false;
            activeTabBorder.Background = new SolidColorBrush(bgCard);
            activeTabBorder.BorderBrush = new SolidColorBrush(accentColor);
            activeTabBorder.BorderThickness = new Thickness(1.0, 1.0, 1.0, 2.0);
            ((TextBlock)activeTabBorder.Child).Foreground = new SolidColorBrush(accentColor);
            ((TextBlock)activeTabBorder.Child).FontWeight = FontWeights.SemiBold;
            inactiveTabBorder.Background = new SolidColorBrush(bgMain);
            inactiveTabBorder.BorderBrush = new SolidColorBrush(bgMain);
            inactiveTabBorder.BorderThickness = new Thickness(0.0, 0.0, 0.0, 2.0);
            ((TextBlock)inactiveTabBorder.Child).Foreground = new SolidColorBrush(textMuted);
            ((TextBlock)inactiveTabBorder.Child).FontWeight = FontWeights.Normal;
            contentEsgoto.Visibility = System.Windows.Visibility.Visible;
            contentVentilacao.Visibility = System.Windows.Visibility.Collapsed;
        };
        activeTabBorder.MouseDown += delegate
        {
            ativarAbaEsgoto();
        };
        Action ativarAbaVentilacao = delegate
        {
            ConfigCache.TabAtiva = 1;
            _isVentilacaoAtiva = true;
            inactiveTabBorder.Background = new SolidColorBrush(bgCard);
            inactiveTabBorder.BorderBrush = new SolidColorBrush(accentColor);
            inactiveTabBorder.BorderThickness = new Thickness(1.0, 1.0, 1.0, 2.0);
            ((TextBlock)inactiveTabBorder.Child).Foreground = new SolidColorBrush(accentColor);
            ((TextBlock)inactiveTabBorder.Child).FontWeight = FontWeights.SemiBold;
            activeTabBorder.Background = new SolidColorBrush(bgMain);
            activeTabBorder.BorderBrush = new SolidColorBrush(bgMain);
            activeTabBorder.BorderThickness = new Thickness(0.0, 0.0, 0.0, 2.0);
            ((TextBlock)activeTabBorder.Child).Foreground = new SolidColorBrush(textMuted);
            ((TextBlock)activeTabBorder.Child).FontWeight = FontWeights.Normal;
            contentVentilacao.Visibility = System.Windows.Visibility.Visible;
            contentEsgoto.Visibility = System.Windows.Visibility.Collapsed;
        };
        inactiveTabBorder.MouseDown += delegate
        {
            ativarAbaVentilacao();
        };
        Border card1 = MakeCard();
        StackPanel cc1 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("1. PARÂMETROS GERAIS") }
        };
        _cmbTipoTubo = new ComboBox
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
            Padding = new Thickness(4.0),
            Background = new SolidColorBrush(bgControl),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        foreach (PipeType t in from PipeType pipeType in new FilteredElementCollector(doc).OfClass(typeof(PipeType))
                               orderby pipeType.Name
                               select pipeType)
        {
            _cmbTipoTubo.Items.Add(new ComboItemRevit
            {
                Nome = t.Name,
                Id = t.Id
            });
        }
        int idxTubo = _cmbTipoTubo.Items.Cast<ComboItemRevit>().ToList().FindIndex((ComboItemRevit i) => i.Nome == ConfigCache.TipoTuboNome);
        _cmbTipoTubo.SelectedIndex = ((idxTubo >= 0) ? idxTubo : ((_cmbTipoTubo.Items.Count <= 0) ? (-1) : 0));
        _cmbSistema = new ComboBox
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
            Padding = new Thickness(4.0),
            Background = new SolidColorBrush(bgControl),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        foreach (PipingSystemType s in from PipingSystemType pipingSystemType in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
                                       orderby pipingSystemType.Name
                                       select pipingSystemType)
        {
            _cmbSistema.Items.Add(new ComboItemRevit
            {
                Nome = s.Name,
                Id = s.Id
            });
        }
        int idxSis = _cmbSistema.Items.Cast<ComboItemRevit>().ToList().FindIndex((ComboItemRevit i) => i.Nome == ConfigCache.SistemaNome);
        _cmbSistema.SelectedIndex = ((idxSis >= 0) ? idxSis : ((_cmbSistema.Items.Count <= 0) ? (-1) : 0));
        System.Windows.Controls.Grid gridElev = new System.Windows.Controls.Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition
                {
                    Width = new GridLength(1.0, GridUnitType.Star)
                },
                new ColumnDefinition
                {
                    Width = new GridLength(80.0)
                }
            }
        };
        TextBlock lblElev = new TextBlock
        {
            Text = "Elevação do Coletor na Laje (m):",
            Foreground = new SolidColorBrush(textMain),
            VerticalAlignment = VerticalAlignment.Center
        };
        _txtElevacao = new TextBox
        {
            Text = ConfigCache.Elevacao,
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        System.Windows.Controls.Grid.SetColumn(lblElev, 0);
        System.Windows.Controls.Grid.SetColumn(_txtElevacao, 1);
        gridElev.Children.Add(lblElev);
        gridElev.Children.Add(_txtElevacao);
        cc1.Children.Add(SubLabel("Tipo de Tubo (Esgoto)"));
        cc1.Children.Add(_cmbTipoTubo);
        cc1.Children.Add(SubLabel("Sistema Hidráulico"));
        cc1.Children.Add(_cmbSistema);
        cc1.Children.Add(gridElev);
        card1.Child = cc1;
        Border cardLav = MakeCard();
        StackPanel ccLav = new StackPanel();
        _tituloConfigLavPia = SectionTitle("2. CONFIGURAÇÕES DO LAVATÓRIO");
        ccLav.Children.Add(_tituloConfigLavPia);
        System.Windows.Controls.Grid gridLav = new System.Windows.Controls.Grid
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
        };
        gridLav.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.0, GridUnitType.Star)
        });
        gridLav.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.0, GridUnitType.Star)
        });
        StackPanel pnlDiam = new StackPanel
        {
            Margin = new Thickness(0.0, 0.0, 5.0, 0.0)
        };
        pnlDiam.Children.Add(SubLabel("Diâmetro do tubo (mm)"));
        _cmbDiamLavatorio = new ComboBox
        {
            Padding = new Thickness(4.0),
            Background = new SolidColorBrush(bgControl),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        _cmbDiamLavatorio.Items.Add("40");
        _cmbDiamLavatorio.Items.Add("50");
        _cmbDiamLavatorio.SelectedIndex = ConfigCache.DiamLavatorioIndex;
        pnlDiam.Children.Add(_cmbDiamLavatorio);
        StackPanel pnlAlt = new StackPanel
        {
            Margin = new Thickness(5.0, 0.0, 0.0, 0.0)
        };
        pnlAlt.Children.Add(SubLabel("Altura do ponto na parede (m)"));
        _txtAltLavatorio = new TextBox
        {
            Text = ConfigCache.AltLavatorio,
            Padding = new Thickness(4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        pnlAlt.Children.Add(_txtAltLavatorio);
        System.Windows.Controls.Grid.SetColumn(pnlDiam, 0);
        System.Windows.Controls.Grid.SetColumn(pnlAlt, 1);
        gridLav.Children.Add(pnlDiam);
        gridLav.Children.Add(pnlAlt);
        _chkDesvioViga = new CheckBox
        {
            Content = "Compatibilizar saída (desviar de vigas)",
            IsChecked = ConfigCache.DesvioViga,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
        };
        ccLav.Children.Add(gridLav);
        ccLav.Children.Add(_chkDesvioViga);
        cardLav.Child = ccLav;
        Border card2 = MakeCard();
        card2.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
        StackPanel cc2 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("3. PEÇAS DO AMBIENTE") }
        };
        _chkVaso = new CheckBox
        {
            Content = "Vaso Sanitário",
            IsChecked = ConfigCache.Vaso,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
        };
        _chkCaixa = new CheckBox
        {
            Content = "Caixa Sifonada",
            IsChecked = ConfigCache.Caixa,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
        };
        _chkLavatorio = new CheckBox
        {
            Content = "Lavatório",
            IsChecked = ConfigCache.Lavatorio,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
        };
        _chkChuveiro = new CheckBox
        {
            Content = "Ralo do Chuveiro",
            IsChecked = ConfigCache.Chuveiro,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        Border subBloq = new Border
        {
            BorderBrush = new SolidColorBrush(accentColor),
            BorderThickness = new Thickness(2.0, 0.0, 0.0, 0.0),
            Margin = new Thickness(18.0, 0.0, 0.0, 0.0),
            Padding = new Thickness(6.0, 3.0, 0.0, 3.0)
        };
        TextBlock txtBloq = new TextBlock
        {
            Text = "Bloquear conectores horizontais",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0
        };
        _chkBloquearHorizontais = new CheckBox
        {
            Content = txtBloq,
            IsChecked = ConfigCache.BloquearHorizontais,
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0
        };
        subBloq.Child = _chkBloquearHorizontais;
        cc2.Children.Add(_chkVaso);
        cc2.Children.Add(_chkCaixa);
        cc2.Children.Add(_chkLavatorio);
        cc2.Children.Add(_chkChuveiro);
        cc2.Children.Add(subBloq);
        Border boxPia = new Border
        {
            BorderBrush = new SolidColorBrush(accentColor),
            BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
            Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
            Padding = new Thickness(0.0, 10.0, 0.0, 0.0)
        };
        StackPanel pnlPia = new StackPanel
        {
            Children = { (UIElement)new TextBlock
            {
                Text = "Lançamento independente",
                Foreground = new SolidColorBrush(textMuted),
                FontSize = 11.0,
                Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
            } }
        };
        _chkPia = new CheckBox
        {
            Content = "Pia",
            IsChecked = ConfigCache.Pia,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _chkMaquina = new CheckBox
        {
            Content = "Máquina de Lavar",
            IsChecked = ConfigCache.Maquina,
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
        };
        pnlPia.Children.Add(_chkPia);
        pnlPia.Children.Add(_chkMaquina);
        boxPia.Child = pnlPia;
        cc2.Children.Add(boxPia);
        card2.Child = cc2;
        Border card3 = MakeCard();
        card3.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
        StackPanel cc3 = new StackPanel();
        _tituloDestino = SectionTitle("4. DESTINO Ø100 VASO");
        cc3.Children.Add(_tituloDestino);
        _pnlVasoDestino = new StackPanel();
        _rbVasoLivre = new RadioButton
        {
            Content = "Ponto livre (45º)",
            GroupName = "DestinoVaso",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoVaso == 0),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlVasoDestino.Children.Add(_rbVasoLivre);
        _pnlVasoDestino.Children.Add(new TextBlock
        {
            Text = "Clique num ponto vazio para lançar curva de 45º.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbVasoPrumada = new RadioButton
        {
            Content = "Queda individual (90º)",
            GroupName = "DestinoVaso",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoVaso == 1),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlVasoDestino.Children.Add(_rbVasoPrumada);
        _pnlVasoDestino.Children.Add(new TextBlock
        {
            Text = "Clique num tubo Ø100 vertical para conectar na cota de chegada.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbVasoMultiplo = new RadioButton
        {
            Content = "Tubo de queda múltiplo",
            GroupName = "DestinoVaso",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoVaso == 2),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlVasoDestino.Children.Add(_rbVasoMultiplo);
        _pnlVasoDestino.Children.Add(new TextBlock
        {
            Text = "Junção simples 45º numa prumada passante. Mova o mouse para alternar a rota.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 0.0),
            TextWrapping = TextWrapping.Wrap
        });
        cc3.Children.Add(_pnlVasoDestino);
        card3.Child = cc3;
        StackPanel pnlDistVaso = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
        };
        pnlDistVaso.Children.Add(new TextBlock
        {
            Text = "Distância do Vaso na parede (cm): ",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            VerticalAlignment = VerticalAlignment.Center
        });
        _txtDistVaso = new TextBox
        {
            Text = ConfigCache.DistanciaVaso,
            Width = 50.0,
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(2.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor),
            FontSize = 11.0
        };
        pnlDistVaso.Children.Add(_txtDistVaso);
        cc3.Children.Add(pnlDistVaso);
        _pnlCaixaDestino = new StackPanel
        {
            Visibility = System.Windows.Visibility.Collapsed
        };
        Border separadorCaixa = new Border
        {
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
            Margin = new Thickness(0.0, 6.0, 0.0, 10.0)
        };
        _pnlCaixaDestino.Children.Add(separadorCaixa);
        _rbCaixaLivre = new RadioButton
        {
            Content = "Ponto livre (45º)",
            GroupName = "DestinoCaixa",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoCaixa == 0),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlCaixaDestino.Children.Add(_rbCaixaLivre);
        _pnlCaixaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num ponto vazio para lançar curva de 45º.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbCaixaPrumada = new RadioButton
        {
            Content = "Queda individual (90º)",
            GroupName = "DestinoCaixa",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoCaixa == 1),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlCaixaDestino.Children.Add(_rbCaixaPrumada);
        _pnlCaixaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num tubo Ø100 vertical para conectar na cota de chegada.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbCaixaMultiplo = new RadioButton
        {
            Content = "Tubo de queda múltiplo",
            GroupName = "DestinoCaixa",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoCaixa == 2),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlCaixaDestino.Children.Add(_rbCaixaMultiplo);
        _pnlCaixaDestino.Children.Add(new TextBlock
        {
            Text = "Junção simples 45º numa prumada passante. Mova o mouse para alternar a rota.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbCaixaColetor = new RadioButton
        {
            Content = "Tubo coletor (45º)",
            GroupName = "DestinoCaixa",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoCaixa == 3),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlCaixaDestino.Children.Add(_rbCaixaColetor);
        _pnlCaixaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num tubo coletor horizontal para conectar com junção de 45º.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 0.0),
            TextWrapping = TextWrapping.Wrap
        });
        cc3.Children.Add(_pnlCaixaDestino);
        _pnlPiaDestino = new StackPanel
        {
            Visibility = System.Windows.Visibility.Collapsed
        };
        _rbPiaLivre = new RadioButton
        {
            Content = "Ponto livre (45º)",
            GroupName = "DestinoPia",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoPia == 0),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlPiaDestino.Children.Add(_rbPiaLivre);
        _pnlPiaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num ponto vazio para lançar curva de 45º.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbPiaPrumada = new RadioButton
        {
            Content = "Queda individual (90º)",
            GroupName = "DestinoPia",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoPia == 1),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlPiaDestino.Children.Add(_rbPiaPrumada);
        _pnlPiaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num tubo Ø50 vertical para conectar na cota de chegada.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbPiaMultiplo = new RadioButton
        {
            Content = "Tubo de queda múltiplo",
            GroupName = "DestinoPia",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoPia == 2),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlPiaDestino.Children.Add(_rbPiaMultiplo);
        _pnlPiaDestino.Children.Add(new TextBlock
        {
            Text = "Junção simples 45º numa prumada passante. Mova o mouse para alternar a rota.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 0.0),
            TextWrapping = TextWrapping.Wrap
        });
        cc3.Children.Add(_pnlPiaDestino);
        _pnlMaquinaDestino = new StackPanel
        {
            Visibility = System.Windows.Visibility.Collapsed
        };
        _rbMaquinaLivre = new RadioButton
        {
            Content = "Ponto livre (45º)",
            GroupName = "DestinoMaquina",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoMaquina == 0),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlMaquinaDestino.Children.Add(_rbMaquinaLivre);
        _pnlMaquinaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num ponto vazio para lançar curva de 45º.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbMaquinaPrumada = new RadioButton
        {
            Content = "Queda individual (90º)",
            GroupName = "DestinoMaquina",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoMaquina == 1),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlMaquinaDestino.Children.Add(_rbMaquinaPrumada);
        _pnlMaquinaDestino.Children.Add(new TextBlock
        {
            Text = "Clique num tubo Ø50 vertical para conectar na cota de chegada.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbMaquinaMultiplo = new RadioButton
        {
            Content = "Tubo de queda múltiplo",
            GroupName = "DestinoMaquina",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.DestinoMaquina == 2),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        _pnlMaquinaDestino.Children.Add(_rbMaquinaMultiplo);
        _pnlMaquinaDestino.Children.Add(new TextBlock
        {
            Text = "Junção simples 45º numa prumada passante. Mova o mouse para alternar a rota.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 0.0),
            TextWrapping = TextWrapping.Wrap
        });
        cc3.Children.Add(_pnlMaquinaDestino);
        System.Windows.Controls.Grid gridEsgoto = new System.Windows.Controls.Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition
                {
                    Width = new GridLength(1.0, GridUnitType.Star)
                },
                new ColumnDefinition
                {
                    Width = new GridLength(10.0, GridUnitType.Pixel)
                },
                new ColumnDefinition
                {
                    Width = new GridLength(1.0, GridUnitType.Star)
                }
            }
        };
        StackPanel colEsgoto1 = new StackPanel
        {
            Children =
            {
                (UIElement)card1,
                (UIElement)card2
            }
        };
        StackPanel colEsgoto2 = new StackPanel
        {
            Children =
            {
                (UIElement)cardLav,
                (UIElement)card3
            }
        };
        System.Windows.Controls.Grid.SetColumn(colEsgoto1, 0);
        gridEsgoto.Children.Add(colEsgoto1);
        System.Windows.Controls.Grid.SetColumn(colEsgoto2, 2);
        gridEsgoto.Children.Add(colEsgoto2);
        contentEsgoto.Children.Add(gridEsgoto);
        bool atualizandoSelecao = false;
        Action atualizarModoEsgoto = delegate
        {
            bool valueOrDefault = _chkPia.IsChecked == true;
            bool valueOrDefault2 = _chkMaquina.IsChecked == true;
            bool flag = !valueOrDefault && !valueOrDefault2 && _chkCaixa.IsChecked == true && _chkVaso.IsChecked == false;
            bool flag2 = valueOrDefault || valueOrDefault2 || _chkVaso.IsChecked == true || flag;
            bool flag3 = valueOrDefault || valueOrDefault2 || _chkLavatorio.IsChecked == true;
            _tituloConfigLavPia.Text = (valueOrDefault ? "2. CONFIGURAÇÕES DA PIA" : (valueOrDefault2 ? "2. CONFIGURAÇÕES DA MÁQUINA" : "2. CONFIGURAÇÕES DO LAVATÓRIO"));
            _tituloDestino.Text = (valueOrDefault ? "4. DESTINO DA PIA" : (valueOrDefault2 ? "4. DESTINO DA MÁQUINA" : (flag ? "4. DESTINO DA CAIXA SIFONADA" : "4. DESTINO Ø100 VASO")));
            card3.IsEnabled = flag2;
            card3.Opacity = (flag2 ? 1.0 : 0.4);
            cardLav.IsEnabled = flag3;
            cardLav.Opacity = (flag3 ? 1.0 : 0.4);
            _pnlVasoDestino.Visibility = ((valueOrDefault || valueOrDefault2 || flag) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
            _pnlCaixaDestino.Visibility = ((!flag) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
            _pnlPiaDestino.Visibility = ((!valueOrDefault) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
            _pnlMaquinaDestino.Visibility = ((!valueOrDefault2) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
            bool flag4 = valueOrDefault || valueOrDefault2;
            _chkVaso.IsEnabled = !flag4;
            _chkCaixa.IsEnabled = !flag4;
            _chkLavatorio.IsEnabled = !flag4;
            _chkChuveiro.IsEnabled = !flag4;
            _chkBloquearHorizontais.IsEnabled = !flag4;
            pnlDistVaso.Visibility = ((flag4 || flag) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
        };
        Action desmarcarIndepSeBanheiro = delegate
        {
            if (!atualizandoSelecao)
            {
                if (_chkVaso.IsChecked == true || _chkCaixa.IsChecked == true || _chkLavatorio.IsChecked == true || _chkChuveiro.IsChecked == true)
                {
                    atualizandoSelecao = true;
                    _chkPia.IsChecked = false;
                    _chkMaquina.IsChecked = false;
                    atualizandoSelecao = false;
                }
                atualizarModoEsgoto();
            }
        };
        _chkPia.Checked += delegate
        {
            if (!atualizandoSelecao)
            {
                atualizandoSelecao = true;
                _chkVaso.IsChecked = false;
                _chkCaixa.IsChecked = false;
                _chkLavatorio.IsChecked = false;
                _chkChuveiro.IsChecked = false;
                _chkMaquina.IsChecked = false;
                atualizandoSelecao = false;
                atualizarModoEsgoto();
            }
        };
        _chkPia.Unchecked += delegate
        {
            atualizarModoEsgoto();
        };
        _chkMaquina.Checked += delegate
        {
            if (!atualizandoSelecao)
            {
                atualizandoSelecao = true;
                _chkVaso.IsChecked = false;
                _chkCaixa.IsChecked = false;
                _chkLavatorio.IsChecked = false;
                _chkChuveiro.IsChecked = false;
                _chkPia.IsChecked = false;
                atualizandoSelecao = false;
                atualizarModoEsgoto();
            }
        };
        _chkMaquina.Unchecked += delegate
        {
            atualizarModoEsgoto();
        };
        _chkCaixa.Checked += delegate
        {
            desmarcarIndepSeBanheiro();
            atualizarModoEsgoto();
        };
        _chkCaixa.Unchecked += delegate
        {
            atualizarModoEsgoto();
        };
        _chkLavatorio.Checked += delegate
        {
            desmarcarIndepSeBanheiro();
        };
        _chkLavatorio.Unchecked += delegate
        {
            atualizarModoEsgoto();
        };
        _chkChuveiro.Checked += delegate
        {
            desmarcarIndepSeBanheiro();
        };
        _chkChuveiro.Unchecked += delegate
        {
            atualizarModoEsgoto();
        };
        _chkVaso.Checked += delegate
        {
            if (!atualizandoSelecao)
            {
                if (ConfigCache.DestinoVaso == 1)
                {
                    _rbVasoPrumada.IsChecked = true;
                }
                else if (ConfigCache.DestinoVaso == 2)
                {
                    _rbVasoMultiplo.IsChecked = true;
                }
                else
                {
                    _rbVasoLivre.IsChecked = true;
                }
            }
            desmarcarIndepSeBanheiro();
            atualizarModoEsgoto();
        };
        _chkVaso.Unchecked += delegate
        {
            atualizarModoEsgoto();
        };
        if (_chkPia.IsChecked == true || _chkMaquina.IsChecked == true)
        {
            atualizandoSelecao = true;
            _chkVaso.IsChecked = false;
            _chkCaixa.IsChecked = false;
            _chkLavatorio.IsChecked = false;
            _chkChuveiro.IsChecked = false;
            if (_chkPia.IsChecked == true)
            {
                if (ConfigCache.DestinoPia == 1)
                {
                    _rbPiaPrumada.IsChecked = true;
                }
                else if (ConfigCache.DestinoPia == 2)
                {
                    _rbPiaMultiplo.IsChecked = true;
                }
                else
                {
                    _rbPiaLivre.IsChecked = true;
                }
            }
            else if (_chkMaquina.IsChecked == true)
            {
                if (ConfigCache.DestinoMaquina == 1)
                {
                    _rbMaquinaPrumada.IsChecked = true;
                }
                else if (ConfigCache.DestinoMaquina == 2)
                {
                    _rbMaquinaMultiplo.IsChecked = true;
                }
                else
                {
                    _rbMaquinaLivre.IsChecked = true;
                }
            }
            atualizandoSelecao = false;
        }
        atualizarModoEsgoto();
        Border cardVent1 = MakeCard();
        StackPanel ccVent1 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("TIPO DE LIGAÇÃO NA COLUNA EXISTENTE") }
        };
        _rbVentBaixo = new RadioButton
        {
            Content = "Joelho 90º por Baixo",
            GroupName = "OpcaoVentilacao",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.OpcaoVentilacao == 0),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        ccVent1.Children.Add(_rbVentBaixo);
        ccVent1.Children.Add(new TextBlock
        {
            Text = "O ramal reduz para encaixar num joelho de 90º acoplado à base da prumada.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0),
            TextWrapping = TextWrapping.Wrap
        });
        _rbVentCavalete = new RadioButton
        {
            Content = "Cavalete Lateral (Junção 45º)",
            GroupName = "OpcaoVentilacao",
            Foreground = new SolidColorBrush(textMain),
            FontWeight = FontWeights.SemiBold,
            IsChecked = (ConfigCache.OpcaoVentilacao == 1),
            Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
        };
        ccVent1.Children.Add(_rbVentCavalete);
        ccVent1.Children.Add(new TextBlock
        {
            Text = "Sobe um tubo de ventilação a 10cm da prumada e intercepta usando uma junção.",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            Margin = new Thickness(18.0, 0.0, 0.0, 4.0),
            TextWrapping = TextWrapping.Wrap
        });
        StackPanel pnlAltVent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(18.0, 0.0, 0.0, 10.0)
        };
        pnlAltVent.Children.Add(new TextBlock
        {
            Text = "Altura da junção na prumada (m): ",
            Foreground = new SolidColorBrush(textMuted),
            FontSize = 11.0,
            VerticalAlignment = VerticalAlignment.Center
        });
        _txtAltVentCavalete = new TextBox
        {
            Text = ConfigCache.AltVentilacaoCavalete,
            Width = 50.0,
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(2.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor),
            FontSize = 11.0
        };
        pnlAltVent.Children.Add(_txtAltVentCavalete);
        ccVent1.Children.Add(pnlAltVent);
        cardVent1.Child = ccVent1;
        contentVentilacao.Children.Add(cardVent1);
        Border cardVent2 = MakeCard();
        StackPanel ccVent2 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("ROTAÇÃO DO TÊ") }
        };
        StackPanel pnlRotRadios = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        _rbRotReto = new RadioButton
        {
            Content = "Tê Reto (90º)",
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 15.0, 0.0),
            IsChecked = ConfigCache.RotacaoTe90
        };
        _rbRotIncl = new RadioButton
        {
            Content = "Tê Inclinado (45º) + Joelho 90º",
            Foreground = new SolidColorBrush(textMain),
            Margin = new Thickness(0.0, 0.0, 15.0, 0.0),
            IsChecked = (!ConfigCache.RotacaoTe90 && !ConfigCache.Joelho45NoChicote)
        };
        _rbRotInclJ45 = new RadioButton
        {
            Content = "Tê Inclinado (45º) + Joelho 45º",
            Foreground = new SolidColorBrush(textMain),
            IsChecked = (!ConfigCache.RotacaoTe90 && ConfigCache.Joelho45NoChicote)
        };
        pnlRotRadios.Children.Add(_rbRotReto);
        pnlRotRadios.Children.Add(_rbRotIncl);
        pnlRotRadios.Children.Add(_rbRotInclJ45);
        ccVent2.Children.Add(pnlRotRadios);
        cardVent2.Child = ccVent2;
        contentVentilacao.Children.Add(cardVent2);
        tabContainer.Children.Add(tabHeaderPanel);
        tabContainer.Children.Add(tabLine);
        tabContainer.Children.Add(contentEsgoto);
        tabContainer.Children.Add(contentVentilacao);
        root.Children.Add(tabContainer);
        if (ConfigCache.TabAtiva == 1)
        {
            ativarAbaVentilacao();
        }
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
        Button btnCancelar = new Button
        {
            Content = "Cancelar",
            Width = 90.0,
            Height = 32.0,
            Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
            Background = new SolidColorBrush(bgMain),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(1.0)
        };
        btnCancelar.Click += delegate
        {
            Close();
        };
        Button btnLancar = new Button
        {
            Content = "Iniciar Lançamento ▶",
            Height = 32.0,
            Padding = new Thickness(20.0, 0.0, 20.0, 0.0),
            Background = new SolidColorBrush(accentColor),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0.0)
        };
        btnLancar.Click += BtnLancar_Click;
        painelBotoes.Children.Add(btnCancelar);
        painelBotoes.Children.Add(btnLancar);
        footer.Child = painelBotoes;
        root.Children.Add(footer);
        base.Content = root;
        Border MakeCard()
        {
            return new Border
            {
                Background = new SolidColorBrush(bgCard),
                BorderBrush = new SolidColorBrush(strokeColor),
                BorderThickness = new Thickness(1.0),
                CornerRadius = new CornerRadius(4.0),
                Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
                Padding = new Thickness(16.0, 14.0, 16.0, 14.0)
            };
        }
        TextBlock SectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(accentColor),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.0,
                Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
            };
        }
        TextBlock SubLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(textMuted),
                FontSize = 11.0,
                Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
            };
        }
    }

    private void BtnLancar_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(_txtElevacao.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var elev) && double.TryParse(_txtAltLavatorio.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var altPia) && double.TryParse(_txtAltVentCavalete.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var altVent) && double.TryParse(_txtDistVaso.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var distVaso))
        {
            Configuracao.Confirmado = true;
            Configuracao.ElevacaoColetorMetros = elev;
            Configuracao.AlturaLavatorio = altPia;
            Configuracao.DiametroLavatorio = ((_cmbDiamLavatorio.SelectedIndex == 0) ? 40 : 50);
            Configuracao.DesviarVigaLavatorio = _chkDesvioViga.IsChecked == true;
            Configuracao.DiametroMaquina = Configuracao.DiametroLavatorio;
            Configuracao.AlturaMaquina = Configuracao.AlturaLavatorio;
            Configuracao.TemVaso = !_isVentilacaoAtiva && _chkVaso.IsChecked == true;
            Configuracao.TemCaixaSifonada = !_isVentilacaoAtiva && _chkCaixa.IsChecked == true;
            Configuracao.TemLavatorio = !_isVentilacaoAtiva && _chkLavatorio.IsChecked == true;
            Configuracao.TemChuveiro = !_isVentilacaoAtiva && _chkChuveiro.IsChecked == true;
            Configuracao.TemPia = !_isVentilacaoAtiva && _chkPia.IsChecked == true;
            Configuracao.TemMaquina = !_isVentilacaoAtiva && _chkMaquina.IsChecked == true;
            Configuracao.IniciarVentilacao = _isVentilacaoAtiva;
            Configuracao.OpcaoVentilacao = ((_rbVentBaixo.IsChecked != true) ? 1 : 0);
            Configuracao.AltVentilacaoCavalete = altVent;
            Configuracao.RotacaoTe90 = _rbRotReto.IsChecked == true;
            Configuracao.Joelho45NoChicote = _rbRotInclJ45.IsChecked == true;
            Configuracao.BloquearConectoresHorizontais = _chkBloquearHorizontais.IsChecked == true;
            Configuracao.DestinoVaso = ((_rbVasoPrumada.IsChecked == true) ? 1 : ((_rbVasoMultiplo.IsChecked == true) ? 2 : 0));
            Configuracao.DestinoPia = ((_rbPiaPrumada.IsChecked == true) ? 1 : ((_rbPiaMultiplo.IsChecked == true) ? 2 : 0));
            Configuracao.DestinoMaquina = ((_rbMaquinaPrumada.IsChecked == true) ? 1 : ((_rbMaquinaMultiplo.IsChecked == true) ? 2 : 0));
            bool caixaIndepConf = _chkCaixa.IsChecked == true && _chkVaso.IsChecked == false && _chkPia.IsChecked == false;
            Configuracao.CaixaIndependente = caixaIndepConf;
            Configuracao.DestinoCaixa = (caixaIndepConf ? ((_rbCaixaPrumada.IsChecked == true) ? 1 : ((_rbCaixaMultiplo.IsChecked == true) ? 2 : ((_rbCaixaColetor.IsChecked == true) ? 3 : 0))) : 0);
            Configuracao.DistanciaVaso = distVaso;
            if (_cmbSistema.SelectedItem is ComboItemRevit sis)
            {
                Configuracao.SistemaId = sis.Id;
            }
            if (_cmbTipoTubo.SelectedItem is ComboItemRevit tb)
            {
                Configuracao.TipoTuboEsgotoId = tb.Id;
            }
            ConfigCache.TipoTuboNome = (_cmbTipoTubo.SelectedItem as ComboItemRevit)?.Nome;
            ConfigCache.SistemaNome = (_cmbSistema.SelectedItem as ComboItemRevit)?.Nome;
            ConfigCache.Elevacao = _txtElevacao.Text;
            ConfigCache.DiamLavatorioIndex = _cmbDiamLavatorio.SelectedIndex;
            ConfigCache.AltLavatorio = _txtAltLavatorio.Text;
            ConfigCache.DesvioViga = _chkDesvioViga.IsChecked == true;
            ConfigCache.Vaso = _chkVaso.IsChecked == true;
            ConfigCache.Caixa = _chkCaixa.IsChecked == true;
            ConfigCache.Lavatorio = _chkLavatorio.IsChecked == true;
            ConfigCache.Chuveiro = _chkChuveiro.IsChecked == true;
            ConfigCache.Pia = _chkPia.IsChecked == true;
            ConfigCache.Maquina = _chkMaquina.IsChecked == true;
            ConfigCache.OpcaoVentilacao = Configuracao.OpcaoVentilacao;
            ConfigCache.RotacaoTe90 = Configuracao.RotacaoTe90;
            ConfigCache.Joelho45NoChicote = Configuracao.Joelho45NoChicote;
            ConfigCache.AltVentilacaoCavalete = _txtAltVentCavalete.Text;
            ConfigCache.BloquearHorizontais = Configuracao.BloquearConectoresHorizontais;
            ConfigCache.DestinoVaso = Configuracao.DestinoVaso;
            ConfigCache.DestinoPia = Configuracao.DestinoPia;
            ConfigCache.DestinoMaquina = Configuracao.DestinoMaquina;
            ConfigCache.DestinoCaixa = Configuracao.DestinoCaixa;
            ConfigCache.DistanciaVaso = _txtDistVaso.Text;
            ConfigCache.Salvar();
            Close();
        }
    }
}
