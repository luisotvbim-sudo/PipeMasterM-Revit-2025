using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class JanelaLinhasEsgoto : Window
{
    private System.Windows.Controls.TextBox _txtElevacao;

    private System.Windows.Controls.ComboBox _cmbSistema;

    private System.Windows.Controls.ComboBox _cmbTipoTubo;

    private CheckBox _chkApagarLinhas;

    private StackPanel _pnlDiametros;

    private Button _btnVaso;

    private TextBlock _txtStatusVaso;

    private TextBlock _lblCard3;

    private CheckBox _chkHabilitarVent;

    private System.Windows.Controls.ComboBox _cmbSistemaVent;

    private System.Windows.Controls.ComboBox _cmbTipoTuboVent;

    private StackPanel _pnlDiametrosVent;

    private Button _btnVent;

    private TextBlock _txtStatusVent;

    private Dictionary<double, List<CurveElement>> _cacheSelecaoLinhasVent = new Dictionary<double, List<CurveElement>>();

    private Dictionary<double, List<CurveElement>> _cacheSelecaoLinhas = new Dictionary<double, List<CurveElement>>();

    private UIDocument _uidoc;

    private EventoGerarRede _handler;

    private ExternalEvent _exEvent;

    private EventoPintarLinhas _handlerPintar;

    private ExternalEvent _exEventPintar;

    private readonly System.Windows.Media.Color bgMain = PipeMasterTheme.Background;

    private readonly System.Windows.Media.Color bgCard = PipeMasterTheme.Surface;

    private readonly System.Windows.Media.Color bgControl = PipeMasterTheme.Control;

    private readonly System.Windows.Media.Color strokeColor = PipeMasterTheme.Border;

    private readonly System.Windows.Media.Color accentColor = PipeMasterTheme.Accent;

    private readonly System.Windows.Media.Color textMain = PipeMasterTheme.Text;

    private readonly System.Windows.Media.Color textMuted = PipeMasterTheme.TextMuted;

    private readonly System.Windows.Media.Color okGreen = PipeMasterTheme.Success;

    public List<LinhaConfigUI> LinhasConfiguradas { get; private set; } = new List<LinhaConfigUI>();

    public List<CurveElement> LinhasVasoSelecionadas { get; private set; } = new List<CurveElement>();

    public List<LinhaConfigUI> LinhasConfiguradasVent { get; private set; } = new List<LinhaConfigUI>();

    public List<CurveElement> PontasVentSelecionadas { get; private set; } = new List<CurveElement>();

    public JanelaLinhasEsgoto(UIDocument uidoc, nint mainWindowHandle, EventoGerarRede handler, ExternalEvent exEvent, EventoPintarLinhas handlerPintar, ExternalEvent exEventPintar)
    {
        MemoriaPipeMaster.Carregar();
        _uidoc = uidoc;
        _handler = handler;
        _exEvent = exEvent;
        _handlerPintar = handlerPintar;
        _exEventPintar = exEventPintar;
        new WindowInteropHelper(this).Owner = mainWindowHandle;
        Document doc = uidoc.Document;
        base.Title = "F.A Projetos | PipeMaster [M] — Ramal em Linhas";
        base.Width = 520.0;
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
                    Text = "PipeMaster [M] — Ramal em Linhas",
                    Foreground = new SolidColorBrush(textMain),
                    FontSize = 18.0,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
                },
                (UIElement)new TextBlock
                {
                    Text = "Mapeamento estrutural de linhas de detalhe para elementos 3D",
                    Foreground = new SolidColorBrush(textMuted),
                    FontSize = 12.0
                }
            }
        };
        root.Children.Add(header);
        StackPanel content = new StackPanel
        {
            Margin = new Thickness(20.0)
        };
        TabControl tabControl = new TabControl
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0.0)
        };
        string tabStyleXml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\r\n                <Style TargetType=\"TabItem\">\r\n                    <Setter Property=\"Template\">\r\n                        <Setter.Value>\r\n                            <ControlTemplate TargetType=\"TabItem\">\r\n                                <Border Name=\"Border\" BorderThickness=\"0,0,0,2\" BorderBrush=\"Transparent\" Margin=\"0,0,15,0\" Padding=\"5,5,5,8\">\r\n                                    <ContentPresenter x:Name=\"ContentSite\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" ContentSource=\"Header\"/>\r\n                                </Border>\r\n                                <ControlTemplate.Triggers>\r\n                                    <Trigger Property=\"IsSelected\" Value=\"True\">\r\n                                        <Setter TargetName=\"Border\" Property=\"BorderBrush\" Value=\"#F57C00\"/>\r\n                                        <Setter Property=\"TextElement.Foreground\" Value=\"#333333\"/>\r\n                                    </Trigger>\r\n                                    <Trigger Property=\"IsSelected\" Value=\"False\">\r\n                                        <Setter Property=\"TextElement.Foreground\" Value=\"#6B6B6B\"/>\r\n                                    </Trigger>\r\n                                    <MultiTrigger>\r\n                                        <MultiTrigger.Conditions>\r\n                                            <Condition Property=\"IsSelected\" Value=\"False\"/>\r\n                                            <Condition Property=\"IsMouseOver\" Value=\"True\"/>\r\n                                        </MultiTrigger.Conditions>\r\n                                        <Setter Property=\"TextElement.Foreground\" Value=\"#333333\"/>\r\n                                        <Setter TargetName=\"Border\" Property=\"BorderBrush\" Value=\"#FF9800\"/>\r\n                                    </MultiTrigger>\r\n                                </ControlTemplate.Triggers>\r\n                            </ControlTemplate>\r\n                        </Setter.Value>\r\n                    </Setter>\r\n                    <Setter Property=\"Background\" Value=\"Transparent\"/>\r\n                    <Setter Property=\"FontSize\" Value=\"14\"/>\r\n                    <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>\r\n                    <Setter Property=\"Cursor\" Value=\"Hand\"/>\r\n                </Style>\r\n            </ResourceDictionary>";
        tabControl.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(tabStyleXml));
        TabItem abaEsgoto = new TabItem
        {
            Header = "Esgoto Sanitário"
        };
        ScrollViewer scrollEsgoto = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 600.0
        };
        StackPanel panelEsgoto = new StackPanel
        {
            Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
        };
        Border card1 = MakeCard();
        StackPanel cc1 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("1. CONFIGURAÇÃO HIDRÁULICA") }
        };
        _cmbTipoTubo = new System.Windows.Controls.ComboBox
        {
            Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = Brushes.Black,
            Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        foreach (PipeType t in from PipeType pipeType in new FilteredElementCollector(doc).OfClass(typeof(PipeType))
                               orderby pipeType.Name
                               select pipeType)
        {
            _cmbTipoTubo.Items.Add(new ComboItemRevitEsgoto
            {
                Nome = t.Name,
                Id = t.Id
            });
        }
        _cmbSistema = new System.Windows.Controls.ComboBox
        {
            Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        foreach (PipingSystemType s in from PipingSystemType pipingSystemType in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
                                       orderby pipingSystemType.Name
                                       select pipingSystemType)
        {
            _cmbSistema.Items.Add(new ComboItemRevitEsgoto
            {
                Nome = s.Name,
                Id = s.Id
            });
        }
        if (_cmbTipoTubo.Items.Count > 0)
        {
            ComboItemRevitEsgoto sv = _cmbTipoTubo.Items.Cast<ComboItemRevitEsgoto>().FirstOrDefault((ComboItemRevitEsgoto x) => x.Nome == MemoriaPipeMaster.UltimoTipoTubo);
            _cmbTipoTubo.SelectedItem = sv ?? _cmbTipoTubo.Items[0];
        }
        if (_cmbSistema.Items.Count > 0)
        {
            ComboItemRevitEsgoto sv2 = _cmbSistema.Items.Cast<ComboItemRevitEsgoto>().FirstOrDefault((ComboItemRevitEsgoto x) => x.Nome == MemoriaPipeMaster.UltimoSistema);
            _cmbSistema.SelectedItem = sv2 ?? _cmbSistema.Items[0];
        }
        cc1.Children.Add(SubLabel("Tipo de Tubo (Família)"));
        cc1.Children.Add(_cmbTipoTubo);
        cc1.Children.Add(SubLabel("Classificação do Sistema"));
        cc1.Children.Add(_cmbSistema);
        card1.Child = cc1;
        panelEsgoto.Children.Add(card1);
        Border card2 = MakeCard();
        StackPanel cc2 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("2. ATRIBUIÇÃO DE DIÂMETROS E CAIMENTO") }
        };
        ScrollViewer scrollDiametros = new ScrollViewer
        {
            MaxHeight = 250.0,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _pnlDiametros = new StackPanel();
        scrollDiametros.Content = _pnlDiametros;
        cc2.Children.Add(scrollDiametros);
        card2.Child = cc2;
        panelEsgoto.Children.Add(card2);
        Border card3 = MakeCard();
        StackPanel cc3 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("3. MODELAGEM VERTICAL") }
        };
        System.Windows.Controls.Grid pnlVaso = new System.Windows.Controls.Grid
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
        };
        pnlVaso.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(240.0)
        });
        pnlVaso.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.0, GridUnitType.Star)
        });
        _btnVaso = new Button
        {
            Content = "Selecionar Linhas de Vaso",
            Height = 28.0,
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(1.0),
            Cursor = Cursors.Hand
        };
        _txtStatusVaso = new TextBlock
        {
            Text = "Nenhuma selecionada",
            Foreground = new SolidColorBrush(textMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
            FontSize = 11.0
        };
        System.Windows.Controls.Grid.SetColumn(_btnVaso, 0);
        System.Windows.Controls.Grid.SetColumn(_txtStatusVaso, 1);
        pnlVaso.Children.Add(_btnVaso);
        pnlVaso.Children.Add(_txtStatusVaso);
        _btnVaso.Click += delegate
        {
            base.Visibility = System.Windows.Visibility.Hidden;
            try
            {
                IList<Autodesk.Revit.DB.Reference> list = _uidoc.Selection.PickObjects(ObjectType.Element, new FiltroLinhasDeEstudo(), "PipeMaster: Selecione AS LINHAS que pertencem a Vasos Sanitários (Concluir no Revit)");
                _handlerPintar.IdsParaRestaurar = LinhasVasoSelecionadas.Select((CurveElement x) => x.Id).ToList();
                LinhasVasoSelecionadas.Clear();
                foreach (Autodesk.Revit.DB.Reference current in list)
                {
                    LinhasVasoSelecionadas.Add((CurveElement)doc.GetElement(current));
                }
                _handlerPintar.IdsParaPintar = LinhasVasoSelecionadas.Select((CurveElement x) => x.Id).ToList();
                _handlerPintar.CorOverride = new Autodesk.Revit.DB.Color(byte.MaxValue, 140, 0);
                _exEventPintar.Raise();
                _txtStatusVaso.Text = $"{LinhasVasoSelecionadas.Count} selecionada(s)";
                _txtStatusVaso.Foreground = new SolidColorBrush(okGreen);
                _btnVaso.BorderBrush = new SolidColorBrush(accentColor);
            }
            catch
            {
            }
            base.Visibility = System.Windows.Visibility.Visible;
        };
        _lblCard3 = SubLabel("Identificar ramais que exigem subida em 90° (Vaso Sanitário)");
        cc3.Children.Add(_lblCard3);
        cc3.Children.Add(pnlVaso);
        card3.Child = cc3;
        panelEsgoto.Children.Add(card3);
        scrollEsgoto.Content = panelEsgoto;
        abaEsgoto.Content = scrollEsgoto;
        tabControl.Items.Add(abaEsgoto);
        TabItem abaVent = new TabItem
        {
            Header = "Ventilação (Opcional)"
        };
        ScrollViewer scrollVent = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 600.0
        };
        StackPanel panelVent = new StackPanel
        {
            Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
        };
        StackPanel panelHabilitar = new StackPanel
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 15.0)
        };
        _chkHabilitarVent = new CheckBox
        {
            Content = "Habilitar geração simultânea de Ventilação",
            Foreground = new SolidColorBrush(textMain),
            FontSize = 13.0,
            FontWeight = FontWeights.Bold,
            IsChecked = false
        };
        panelHabilitar.Children.Add(_chkHabilitarVent);
        panelVent.Children.Add(panelHabilitar);
        StackPanel panelVentConfigs = new StackPanel
        {
            IsEnabled = false
        };
        _chkHabilitarVent.Checked += delegate
        {
            panelVentConfigs.IsEnabled = true;
        };
        _chkHabilitarVent.Unchecked += delegate
        {
            panelVentConfigs.IsEnabled = false;
        };
        Border card1v = MakeCard();
        StackPanel cc1v = new StackPanel
        {
            Children = { (UIElement)SectionTitle("1. CONFIGURAÇÃO DA VENTILAÇÃO") }
        };
        _cmbTipoTuboVent = new System.Windows.Controls.ComboBox
        {
            Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = Brushes.Black,
            Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        foreach (PipeType t2 in from PipeType pipeType in new FilteredElementCollector(doc).OfClass(typeof(PipeType))
                                orderby pipeType.Name
                                select pipeType)
        {
            _cmbTipoTuboVent.Items.Add(new ComboItemRevitEsgoto
            {
                Nome = t2.Name,
                Id = t2.Id
            });
        }
        _cmbSistemaVent = new System.Windows.Controls.ComboBox
        {
            Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(strokeColor)
        };
        foreach (PipingSystemType s2 in from PipingSystemType pipingSystemType in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
                                        orderby pipingSystemType.Name
                                        select pipingSystemType)
        {
            _cmbSistemaVent.Items.Add(new ComboItemRevitEsgoto
            {
                Nome = s2.Name,
                Id = s2.Id
            });
        }
        if (_cmbTipoTuboVent.Items.Count > 0)
        {
            ComboItemRevitEsgoto sv3 = _cmbTipoTuboVent.Items.Cast<ComboItemRevitEsgoto>().FirstOrDefault((ComboItemRevitEsgoto x) => x.Nome == MemoriaPipeMaster.UltimoTipoTubo);
            _cmbTipoTuboVent.SelectedItem = sv3 ?? _cmbTipoTuboVent.Items[0];
        }
        if (_cmbSistemaVent.Items.Count > 0)
        {
            ComboItemRevitEsgoto sv4 = _cmbSistemaVent.Items.Cast<ComboItemRevitEsgoto>().FirstOrDefault((ComboItemRevitEsgoto x) => x.Nome.ToLower().Contains("ventil"));
            _cmbSistemaVent.SelectedItem = sv4 ?? _cmbSistemaVent.Items[0];
        }
        cc1v.Children.Add(SubLabel("Tipo de Tubo para Ventilação (Família)"));
        cc1v.Children.Add(_cmbTipoTuboVent);
        cc1v.Children.Add(SubLabel("Sistema de Ventilação"));
        cc1v.Children.Add(_cmbSistemaVent);
        card1v.Child = cc1v;
        panelVentConfigs.Children.Add(card1v);
        Border card2v = MakeCard();
        StackPanel cc2v = new StackPanel
        {
            Children = { (UIElement)SectionTitle("2. ATRIBUIÇÃO DE DIÂMETROS E CAIMENTO") }
        };
        ScrollViewer scrollDiametrosVent = new ScrollViewer
        {
            MaxHeight = 250.0,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _pnlDiametrosVent = new StackPanel();
        scrollDiametrosVent.Content = _pnlDiametrosVent;
        cc2v.Children.Add(scrollDiametrosVent);
        card2v.Child = cc2v;
        panelVentConfigs.Children.Add(card2v);
        Border card3v = MakeCard();
        StackPanel cc3v = new StackPanel
        {
            Children = { (UIElement)SectionTitle("3. PONTAS DE CONEXÃO AO ESGOTO") }
        };
        System.Windows.Controls.Grid pnlVentBtn = new System.Windows.Controls.Grid
        {
            Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
        };
        pnlVentBtn.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(240.0)
        });
        pnlVentBtn.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.0, GridUnitType.Star)
        });
        _btnVent = new Button
        {
            Content = "Selecionar Pontas de Conexão",
            Height = 28.0,
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(1.0),
            Cursor = Cursors.Hand
        };
        _txtStatusVent = new TextBlock
        {
            Text = "Nenhuma selecionada",
            Foreground = new SolidColorBrush(textMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
            FontSize = 11.0
        };
        System.Windows.Controls.Grid.SetColumn(_btnVent, 0);
        System.Windows.Controls.Grid.SetColumn(_txtStatusVent, 1);
        pnlVentBtn.Children.Add(_btnVent);
        pnlVentBtn.Children.Add(_txtStatusVent);
        _btnVent.Click += delegate
        {
            base.Visibility = System.Windows.Visibility.Hidden;
            try
            {
                IList<Autodesk.Revit.DB.Reference> list = _uidoc.Selection.PickObjects(ObjectType.Element, new FiltroLinhasDeEstudo(), "PipeMaster: Selecione AS PONTAS das linhas de ventilação que conectam ao esgoto (Concluir no Revit)");
                _handlerPintar.IdsParaRestaurar = PontasVentSelecionadas.Select((CurveElement x) => x.Id).ToList();
                PontasVentSelecionadas.Clear();
                foreach (Autodesk.Revit.DB.Reference current in list)
                {
                    PontasVentSelecionadas.Add((CurveElement)doc.GetElement(current));
                }
                _handlerPintar.IdsParaPintar = PontasVentSelecionadas.Select((CurveElement x) => x.Id).ToList();
                _handlerPintar.CorOverride = new Autodesk.Revit.DB.Color(byte.MaxValue, 140, 0);
                _exEventPintar.Raise();
                _txtStatusVent.Text = $"{PontasVentSelecionadas.Count} selecionada(s)";
                _txtStatusVent.Foreground = new SolidColorBrush(okGreen);
                _btnVent.BorderBrush = new SolidColorBrush(accentColor);
            }
            catch
            {
            }
            base.Visibility = System.Windows.Visibility.Visible;
        };
        cc3v.Children.Add(SubLabel("Identificar pontas das linhas de ventilação que conectam ao esgoto"));
        cc3v.Children.Add(pnlVentBtn);
        card3v.Child = cc3v;
        panelVentConfigs.Children.Add(card3v);
        panelVent.Children.Add(panelVentConfigs);
        scrollVent.Content = panelVent;
        abaVent.Content = scrollVent;
        tabControl.Items.Add(abaVent);
        content.Children.Add(tabControl);
        _chkApagarLinhas = new CheckBox
        {
            Content = "Apagar linhas 2D de referência após o processamento",
            Foreground = new SolidColorBrush(textMain),
            IsChecked = MemoriaPipeMaster.ApagarLinhas,
            FontSize = 12.0,
            Margin = new Thickness(0.0, 15.0, 0.0, 5.0)
        };
        content.Children.Add(_chkApagarLinhas);
        Border card4 = MakeCard();
        StackPanel cc4 = new StackPanel
        {
            Children = { (UIElement)SectionTitle("4. REFERÊNCIA INICIAL DO ESGOTO") }
        };
        StackPanel pnlDesc = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        pnlDesc.Children.Add(new TextBlock
        {
            Text = "Elevação do Ponto de Descarga (m):",
            Foreground = new SolidColorBrush(textMain),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
            FontSize = 12.0
        });
        _txtElevacao = new System.Windows.Controls.TextBox
        {
            Text = MemoriaPipeMaster.UltimaElevacao,
            Width = 65.0,
            Padding = new Thickness(4.0),
            Background = new SolidColorBrush(bgControl),
            Foreground = new SolidColorBrush(textMain),
            BorderBrush = new SolidColorBrush(strokeColor),
            TextAlignment = TextAlignment.Center
        };
        pnlDesc.Children.Add(_txtElevacao);
        cc4.Children.Add(pnlDesc);
        card4.Child = cc4;
        content.Children.Add(card4);
        root.Children.Add(content);
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
            BorderThickness = new Thickness(1.0),
            Cursor = Cursors.Hand
        };
        btnCancelar.Click += delegate
        {
            RestaurarTodasAsCores();
            Close();
        };
        Button btnOk = new Button
        {
            Content = "Iniciar Modelagem 3D",
            Height = 32.0,
            Padding = new Thickness(20.0, 0.0, 20.0, 0.0),
            Background = new SolidColorBrush(accentColor),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0.0),
            Cursor = Cursors.Hand
        };
        btnOk.Click += BtnOk_Click;
        painelBotoes.Children.Add(btnCancelar);
        painelBotoes.Children.Add(btnOk);
        footer.Child = painelBotoes;
        root.Children.Add(footer);
        base.Content = root;
        _cmbTipoTubo.SelectionChanged += delegate
        {
            AtualizarListaDiametros();
        };
        _cmbTipoTuboVent.SelectionChanged += delegate
        {
            AtualizarListaDiametros(isVent: true);
        };
        _cmbSistema.SelectionChanged += delegate
        {
            AtualizarUIModo();
        };
        AtualizarListaDiametros();
        AtualizarListaDiametros(isVent: true);
        AtualizarUIModo();
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

    private bool ModoVentilacao()
    {
        if (_cmbSistema.SelectedItem is ComboItemRevitEsgoto sis)
        {
            string n = sis.Nome.ToLower();
            return n.Contains("ventil") || n.Contains("aeraç") || n.Contains("aerac") || n.Contains("vent");
        }
        return false;
    }

    private void AtualizarUIModo()
    {
        if (ModoVentilacao())
        {
            _lblCard3.Text = "Identificar pontas das linhas de ventilação que conectam ao esgoto";
            _btnVaso.Content = "Selecionar Pontas de Conexão";
        }
        else
        {
            _lblCard3.Text = "Identificar ramais que exigem subida em 90° (Vaso Sanitário)";
            _btnVaso.Content = "Selecionar Linhas de Vaso";
        }
    }

    private HashSet<ElementId> ObterIdsJaSelecionados(double diametroAtual, bool isVent = false)
    {
        HashSet<ElementId> ids = new HashSet<ElementId>();
        List<LinhaConfigUI> configs = (isVent ? LinhasConfiguradasVent : LinhasConfiguradas);
        foreach (LinhaConfigUI config in configs)
        {
            if (Math.Abs(config.Diametro - diametroAtual) < 0.01)
            {
                continue;
            }
            foreach (CurveElement l in config.LinhasSelecionadas)
            {
                ids.Add(l.Id);
            }
        }
        return ids;
    }

    private void RestaurarTodasAsCores()
    {
        List<ElementId> todosIds = new List<ElementId>();
        foreach (LinhaConfigUI c in LinhasConfiguradas)
        {
            todosIds.AddRange(c.LinhasSelecionadas.Select((CurveElement x) => x.Id));
        }
        foreach (LinhaConfigUI c2 in LinhasConfiguradasVent)
        {
            todosIds.AddRange(c2.LinhasSelecionadas.Select((CurveElement x) => x.Id));
        }
        todosIds.AddRange(LinhasVasoSelecionadas.Select((CurveElement x) => x.Id));
        todosIds.AddRange(PontasVentSelecionadas.Select((CurveElement x) => x.Id));
        if (todosIds.Count > 0)
        {
            _handlerPintar.IdsParaRestaurar = todosIds;
            _handlerPintar.IdsParaPintar.Clear();
            _exEventPintar.Raise();
        }
    }

    private void AtualizarListaDiametros(bool isVent = false)
    {
        List<LinhaConfigUI> linhasConfig = (isVent ? LinhasConfiguradasVent : LinhasConfiguradas);
        Dictionary<double, List<CurveElement>> cacheSelecao = (isVent ? _cacheSelecaoLinhasVent : _cacheSelecaoLinhas);
        StackPanel painel = (isVent ? _pnlDiametrosVent : _pnlDiametros);
        System.Windows.Controls.ComboBox cmbTubo = (isVent ? _cmbTipoTuboVent : _cmbTipoTubo);
        foreach (LinhaConfigUI cfg in linhasConfig)
        {
            if (cfg.LinhasSelecionadas.Count > 0)
            {
                cacheSelecao[cfg.Diametro] = cfg.LinhasSelecionadas.ToList();
            }
        }
        painel.Children.Clear();
        linhasConfig.Clear();
        List<double> sizes = new List<double>();
        if (cmbTubo.SelectedItem is ComboItemRevitEsgoto item && _uidoc.Document.GetElement(item.Id) is PipeType pType)
        {
            try
            {
                RoutingPreferenceManager rpm = pType.RoutingPreferenceManager;
                if (rpm.GetNumberOfRules(RoutingPreferenceRuleGroupType.Segments) > 0)
                {
                    RoutingPreferenceRule rule = rpm.GetRule(RoutingPreferenceRuleGroupType.Segments, 0);
                    if (_uidoc.Document.GetElement(rule.MEPPartId) is Segment segment)
                    {
                        foreach (MEPSize size in segment.GetSizes())
                        {
                            double dMm = Math.Round(UnitUtils.ConvertFromInternalUnits(size.NominalDiameter, UnitTypeId.Millimeters), 1);
                            sizes.Add(dMm);
                        }
                    }
                }
            }
            catch
            {
            }
        }
        if (sizes.Count == 0)
        {
            sizes = new List<double> { 40.0, 50.0, 75.0, 100.0, 150.0 };
        }
        sizes = (from x in sizes.Distinct()
                 orderby x
                 select x).ToList();
        Autodesk.Revit.DB.Color[] paleta = new Autodesk.Revit.DB.Color[5]
        {
            new Autodesk.Revit.DB.Color(byte.MaxValue, 0, 0),
            new Autodesk.Revit.DB.Color(byte.MaxValue, 0, byte.MaxValue),
            new Autodesk.Revit.DB.Color(byte.MaxValue, 215, 0),
            new Autodesk.Revit.DB.Color(138, 43, 226),
            new Autodesk.Revit.DB.Color(byte.MaxValue, 105, 180)
        };
        int indexCor = 0;
        foreach (double d in sizes)
        {
            bool temCache = cacheSelecao.ContainsKey(d);
            bool estavaAtivo = MemoriaPipeMaster.InclinacoesPorDiametro.ContainsKey(d) || temCache;
            string incSalva = (MemoriaPipeMaster.InclinacoesPorDiametro.ContainsKey(d) ? MemoriaPipeMaster.InclinacoesPorDiametro[d] : ((d >= 100.0) ? "1" : "2"));
            if (isVent)
            {
                incSalva = "1";
            }
            Autodesk.Revit.DB.Color corAtual = paleta[indexCor % paleta.Length];
            indexCor++;
            LinhaConfigUI config = new LinhaConfigUI
            {
                Diametro = d
            };
            if (temCache)
            {
                config.LinhasSelecionadas = cacheSelecao[d];
            }
            System.Windows.Controls.Grid rowGrid = new System.Windows.Controls.Grid
            {
                Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(90.0)
            });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(45.0)
            });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(50.0)
            });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(10.0)
            });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(85.0)
            });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1.0, GridUnitType.Star)
            });
            config.ChkAtivo = new CheckBox
            {
                Content = $"Ø {d} mm",
                Foreground = new SolidColorBrush(textMain),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = estavaAtivo,
                FontSize = 12.0
            };
            TextBlock lblInc = new TextBlock
            {
                Text = "Inc%:",
                Foreground = new SolidColorBrush(textMuted),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11.0,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
            };
            config.TxtInclinacao = new System.Windows.Controls.TextBox
            {
                Text = incSalva,
                IsEnabled = estavaAtivo,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(2.0),
                Background = new SolidColorBrush(bgControl),
                Foreground = new SolidColorBrush(textMain),
                BorderBrush = new SolidColorBrush(strokeColor)
            };
            config.BtnSelecionar = new Button
            {
                Content = "Selecionar",
                Height = 24.0,
                IsEnabled = estavaAtivo,
                Background = new SolidColorBrush(estavaAtivo ? accentColor : bgMain),
                Foreground = (estavaAtivo ? Brushes.White : Brushes.DarkGray),
                BorderThickness = new Thickness(0.0),
                FontSize = 11.0,
                Cursor = Cursors.Hand
            };
            int nL = config.LinhasSelecionadas.Count;
            config.TxtStatus = new TextBlock
            {
                Text = ((nL > 0) ? $"{nL} linhas" : "—"),
                Foreground = new SolidColorBrush((nL > 0) ? okGreen : textMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
                FontSize = 11.0
            };
            System.Windows.Controls.Grid.SetColumn(config.ChkAtivo, 0);
            System.Windows.Controls.Grid.SetColumn(lblInc, 1);
            System.Windows.Controls.Grid.SetColumn(config.TxtInclinacao, 2);
            System.Windows.Controls.Grid.SetColumn(config.BtnSelecionar, 4);
            System.Windows.Controls.Grid.SetColumn(config.TxtStatus, 5);
            rowGrid.Children.Add(config.ChkAtivo);
            rowGrid.Children.Add(lblInc);
            rowGrid.Children.Add(config.TxtInclinacao);
            rowGrid.Children.Add(config.BtnSelecionar);
            rowGrid.Children.Add(config.TxtStatus);
            config.ChkAtivo.Checked += delegate
            {
                config.TxtInclinacao.IsEnabled = true;
                config.BtnSelecionar.IsEnabled = true;
                config.BtnSelecionar.Background = new SolidColorBrush(accentColor);
                config.BtnSelecionar.Foreground = Brushes.White;
            };
            config.ChkAtivo.Unchecked += delegate
            {
                if (config.LinhasSelecionadas.Count > 0)
                {
                    _handlerPintar.IdsParaRestaurar = config.LinhasSelecionadas.Select((CurveElement x) => x.Id).ToList();
                    _handlerPintar.IdsParaPintar.Clear();
                    _exEventPintar.Raise();
                }
                config.TxtInclinacao.IsEnabled = false;
                config.BtnSelecionar.IsEnabled = false;
                config.LinhasSelecionadas.Clear();
                config.TxtStatus.Text = "—";
                config.TxtStatus.Foreground = new SolidColorBrush(textMuted);
                config.BtnSelecionar.Background = new SolidColorBrush(bgMain);
                config.BtnSelecionar.Foreground = Brushes.DarkGray;
            };
            config.BtnSelecionar.Click += delegate
            {
                base.Visibility = System.Windows.Visibility.Hidden;
                try
                {
                    HashSet<ElementId> ignorados = ObterIdsJaSelecionados(d, isVent);
                    string statusPrompt = (isVent ? $"PipeMaster: Selecione as linhas de Ventilação para Ø {d}mm (CONCLUIR no Revit)" : $"PipeMaster: Selecione as linhas para Ø {d}mm (CONCLUIR no Revit)");
                    IList<Autodesk.Revit.DB.Reference> list = _uidoc.Selection.PickObjects(ObjectType.Element, new FiltroLinhasDeEstudo(ignorados), statusPrompt);
                    _handlerPintar.IdsParaRestaurar = config.LinhasSelecionadas.Select((CurveElement x) => x.Id).ToList();
                    foreach (Autodesk.Revit.DB.Reference current in list)
                    {
                        CurveElement novaLinha = (CurveElement)_uidoc.Document.GetElement(current);
                        if (!config.LinhasSelecionadas.Any((CurveElement x) => x.Id == novaLinha.Id))
                        {
                            config.LinhasSelecionadas.Add(novaLinha);
                        }
                    }
                    _handlerPintar.IdsParaPintar = config.LinhasSelecionadas.Select((CurveElement x) => x.Id).ToList();
                    _handlerPintar.CorOverride = corAtual;
                    _exEventPintar.Raise();
                    config.TxtStatus.Text = $"{config.LinhasSelecionadas.Count} linhas";
                    config.TxtStatus.Foreground = new SolidColorBrush(okGreen);
                    cacheSelecao[d] = config.LinhasSelecionadas.ToList();
                }
                catch
                {
                }
                base.Visibility = System.Windows.Visibility.Visible;
            };
            linhasConfig.Add(config);
            painel.Children.Add(rowGrid);
        }
    }

    private void BtnOk_Click(object s, RoutedEventArgs e)
    {
        if (!double.TryParse(_txtElevacao.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var elev))
        {
            MessageBox.Show("Elevação inválida.", "PipeMaster [M]");
            return;
        }
        bool habilitaVent = _chkHabilitarVent.IsChecked == true;
        int totalLinhasEsg = LinhasConfiguradas.Sum((LinhaConfigUI c) => c.LinhasSelecionadas.Count);
        int totalLinhasVent = (habilitaVent ? LinhasConfiguradasVent.Sum((LinhaConfigUI c) => c.LinhasSelecionadas.Count) : 0);
        if (totalLinhasEsg == 0 && totalLinhasVent == 0)
        {
            MessageBox.Show("Selecione linhas para pelo menos um diâmetro antes de iniciar a modelagem.", "Aviso");
            return;
        }
        string tipoSalvo = ((_cmbTipoTubo.SelectedItem is ComboItemRevitEsgoto tp) ? tp.Nome : "");
        string sisSalvo = ((_cmbSistema.SelectedItem is ComboItemRevitEsgoto ss) ? ss.Nome : "");
        Dictionary<double, string> incsSalvas = new Dictionary<double, string>();
        ElementId esgSisId = (_cmbSistema.SelectedItem as ComboItemRevitEsgoto)?.Id ?? ElementId.InvalidElementId;
        ElementId esgTipoId = (_cmbTipoTubo.SelectedItem as ComboItemRevitEsgoto)?.Id ?? ElementId.InvalidElementId;
        ElementId ventSisId = ((!habilitaVent) ? ElementId.InvalidElementId : ((_cmbSistemaVent.SelectedItem as ComboItemRevitEsgoto)?.Id ?? ElementId.InvalidElementId));
        ElementId ventTipoId = ((!habilitaVent) ? ElementId.InvalidElementId : ((_cmbTipoTuboVent.SelectedItem as ComboItemRevitEsgoto)?.Id ?? ElementId.InvalidElementId));
        _handler.PontasVentilacao.Clear();
        if (habilitaVent && PontasVentSelecionadas.Count > 0)
        {
            _handler.PontasVentilacao.AddRange(PontasVentSelecionadas.SelectMany((CurveElement l) => new XYZ[2]
            {
                ((Line)l.GeometryCurve).GetEndPoint(0),
                ((Line)l.GeometryCurve).GetEndPoint(1)
            }));
        }
        else if (ModoVentilacao() && LinhasVasoSelecionadas.Count > 0)
        {
            _handler.PontasVentilacao.AddRange(LinhasVasoSelecionadas.SelectMany((CurveElement l) => new XYZ[2]
            {
                ((Line)l.GeometryCurve).GetEndPoint(0),
                ((Line)l.GeometryCurve).GetEndPoint(1)
            }));
        }
        List<LinhaComDNA> linhasProntas = new List<LinhaComDNA>();
        foreach (LinhaConfigUI config in LinhasConfiguradas.Where((LinhaConfigUI c) => c.ChkAtivo.IsChecked == true))
        {
            incsSalvas[config.Diametro] = config.TxtInclinacao.Text;
            if (config.LinhasSelecionadas.Count <= 0)
            {
                continue;
            }
            double.TryParse(config.TxtInclinacao.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var inc);
            foreach (CurveElement linha in config.LinhasSelecionadas)
            {
                bool isVentMode = ModoVentilacao();
                bool selectedInCard3 = LinhasVasoSelecionadas.Any((CurveElement v) => v.Id == linha.Id);
                linhasProntas.Add(new LinhaComDNA
                {
                    ElementoRevit = linha,
                    DiametroMm = config.Diametro,
                    Inclinacao = inc / 100.0,
                    IsVaso = (!isVentMode && selectedInCard3),
                    IsVentilacao = false,
                    SistemaId = esgSisId,
                    TipoTuboId = esgTipoId
                });
            }
        }
        if (habilitaVent)
        {
            foreach (LinhaConfigUI config2 in LinhasConfiguradasVent.Where((LinhaConfigUI c) => c.ChkAtivo.IsChecked == true))
            {
                if (config2.LinhasSelecionadas.Count <= 0)
                {
                    continue;
                }
                double.TryParse(config2.TxtInclinacao.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var inc2);
                foreach (CurveElement linha2 in config2.LinhasSelecionadas)
                {
                    linhasProntas.Add(new LinhaComDNA
                    {
                        ElementoRevit = linha2,
                        DiametroMm = config2.Diametro,
                        Inclinacao = inc2 / 100.0,
                        IsVaso = false,
                        IsVentilacao = true,
                        SistemaId = ventSisId,
                        TipoTuboId = ventTipoId
                    });
                }
            }
        }
        MemoriaPipeMaster.Salvar(_txtElevacao.Text, _chkApagarLinhas.IsChecked == true, tipoSalvo, sisSalvo, incsSalvas);
        base.Visibility = System.Windows.Visibility.Hidden;
        XYZ ptDesc = null;
        try
        {
            ptDesc = _uidoc.Selection.PickPoint(ObjectSnapTypes.Endpoints | ObjectSnapTypes.Nearest, "PipeMaster: Clique no PONTO DE DESCARGA no Revit...");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            Close();
            return;
        }
        _handler.LinhasComDNA = linhasProntas;
        _handler.PontoDescarga = ptDesc;
        _handler.ElevacaoMetros = elev;
        _handler.ApagarLinhas = _chkApagarLinhas.IsChecked == true;
        if (_cmbSistema.SelectedItem is ComboItemRevitEsgoto sis)
        {
            _handler.SistemaId = sis.Id;
        }
        if (_cmbTipoTubo.SelectedItem is ComboItemRevitEsgoto tipo)
        {
            _handler.TipoTuboId = tipo.Id;
        }
        _exEvent.Raise();
        Close();
    }
}
