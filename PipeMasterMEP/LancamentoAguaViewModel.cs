using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeMasterMEP;

public class LancamentoAguaViewModel : INotifyPropertyChanged
{
    private string _nomeAmbiente = "Ambiente";

    private readonly List<ComboItemRevit> _sistemasFrios = new List<ComboItemRevit>();

    private readonly List<ComboItemRevit> _sistemasQuentes = new List<ComboItemRevit>();

    private ComboItemRevit _sistemaSelecionado;

    private ComboItemRevit _tipoTuboSelecionado;

    private bool _isAguaFria = true;

    private bool _usarVinculo = true;

    private bool _importarDoVinculo = false;

    private List<PecaAguaItemViewModel> _resumo = new List<PecaAguaItemViewModel>();

    private bool _temChuveiroSelecionado;

    private ComboItemRevit _familiaRegistroSelecionada;

    private bool _inserirRegistroPressao = true;

    private ComboItemRevit _familiaRegistroPressaoSelecionada;

    private bool _inverterSentidoBucha = false;

    private bool _desviarPeloPiso = false;

    private double _alturaPiso = 0.0;

    private double _alturaRegistroPressao = 1.1;

    private double _alturaPrumada = 2.5;

    private double _alturaRegistro = 1.8;

    private double _alturaRamal = 0.6;

    private double _recuoParede = 3.0;

    private bool _inserirRegistro = true;

    private double _diametroRamal = 25.0;

    private double _diametroDescida = 25.0;

    private readonly Document _doc;

    private MapeamentoAparelhosViewModel _mapeamentoViewModel;

    private List<PecaAguaDetectada> _todasPecasDetectadas = new List<PecaAguaDetectada>();

    public static List<string> TiposPonto { get; } = new List<string>
    {
        "Bacia Sanitária", "Bacia c/ Válvula", "Lavatório", "Chuveiro", "Ducha Higiênica", "Pia", "Filtro", "Mictório", "Tanque", "Máquina de Lavar",
        "Máquina de Lavar Louça", "Outro"
    };

    public string NomeAmbiente
    {
        get
        {
            return _nomeAmbiente;
        }
        set
        {
            _nomeAmbiente = value;
            Notify("NomeAmbiente");
        }
    }

    public ObservableCollection<PecaAguaItemViewModel> Pecas { get; } = new ObservableCollection<PecaAguaItemViewModel>();

    public List<ComboItemRevit> SistemasDisponiveis { get; private set; } = new List<ComboItemRevit>();

    public List<ComboItemRevit> TiposTuboDisponiveis { get; private set; } = new List<ComboItemRevit>();

    public ComboItemRevit SistemaSelecionado
    {
        get
        {
            return _sistemaSelecionado;
        }
        set
        {
            _sistemaSelecionado = value;
            Notify("SistemaSelecionado");
        }
    }

    public ComboItemRevit TipoTuboSelecionado
    {
        get
        {
            return _tipoTuboSelecionado;
        }
        set
        {
            _tipoTuboSelecionado = value;
            Notify("TipoTuboSelecionado");
        }
    }

    public bool IsAguaFria
    {
        get
        {
            return _isAguaFria;
        }
        set
        {
            _isAguaFria = value;
            Notify("IsAguaFria");
            if (value)
            {
                AtualizarSistemas();
            }
        }
    }

    public bool UsarVinculo
    {
        get
        {
            return _usarVinculo;
        }
        set
        {
            _usarVinculo = value;
            Notify("UsarVinculo");
            Notify("VisibilidadeOpcaoImportar");
            AtualizarPecasVisiveis();
        }
    }

    public bool ImportarDoVinculo
    {
        get
        {
            return _importarDoVinculo;
        }
        set
        {
            _importarDoVinculo = value;
            Notify("ImportarDoVinculo");
        }
    }

    public System.Windows.Visibility VisibilidadeOpcaoImportar => (!_usarVinculo) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public List<PecaAguaItemViewModel> Resumo
    {
        get
        {
            return _resumo;
        }
        private set
        {
            _resumo = value;
            Notify("Resumo");
        }
    }

    public bool TemChuveiroSelecionado
    {
        get
        {
            return _temChuveiroSelecionado;
        }
        private set
        {
            _temChuveiroSelecionado = value;
            Notify("TemChuveiroSelecionado");
        }
    }

    public List<ComboItemRevit> FamiliasRegistroDisponiveis { get; private set; } = new List<ComboItemRevit>();

    public ComboItemRevit FamiliaRegistroSelecionada
    {
        get
        {
            return _familiaRegistroSelecionada;
        }
        set
        {
            _familiaRegistroSelecionada = value;
            Notify("FamiliaRegistroSelecionada");
        }
    }

    public bool InserirRegistroPressao
    {
        get
        {
            return _inserirRegistroPressao;
        }
        set
        {
            _inserirRegistroPressao = value;
            Notify("InserirRegistroPressao");
        }
    }

    public ComboItemRevit FamiliaRegistroPressaoSelecionada
    {
        get
        {
            return _familiaRegistroPressaoSelecionada;
        }
        set
        {
            _familiaRegistroPressaoSelecionada = value;
            Notify("FamiliaRegistroPressaoSelecionada");
        }
    }

    public bool InverterSentidoBucha
    {
        get
        {
            return _inverterSentidoBucha;
        }
        set
        {
            _inverterSentidoBucha = value;
            Notify("InverterSentidoBucha");
        }
    }

    public bool DesviarPeloPiso
    {
        get
        {
            return _desviarPeloPiso;
        }
        set
        {
            _desviarPeloPiso = value;
            Notify("DesviarPeloPiso");
        }
    }

    public double AlturaPiso
    {
        get
        {
            return _alturaPiso;
        }
        set
        {
            _alturaPiso = value;
            Notify("AlturaPiso");
        }
    }

    public double AlturaRegistroPressao
    {
        get
        {
            return _alturaRegistroPressao;
        }
        set
        {
            _alturaRegistroPressao = value;
            Notify("AlturaRegistroPressao");
        }
    }

    public double AlturaPrumada
    {
        get
        {
            return _alturaPrumada;
        }
        set
        {
            _alturaPrumada = value;
            Notify("AlturaPrumada");
        }
    }

    public double AlturaRegistro
    {
        get
        {
            return _alturaRegistro;
        }
        set
        {
            _alturaRegistro = value;
            Notify("AlturaRegistro");
        }
    }

    public double AlturaRamal
    {
        get
        {
            return _alturaRamal;
        }
        set
        {
            _alturaRamal = value;
            Notify("AlturaRamal");
        }
    }

    public double RecuoParede
    {
        get
        {
            return _recuoParede;
        }
        set
        {
            _recuoParede = value;
            Notify("RecuoParede");
        }
    }

    public bool InserirRegistro
    {
        get
        {
            return _inserirRegistro;
        }
        set
        {
            _inserirRegistro = value;
            Notify("InserirRegistro");
        }
    }

    public double DiametroRamal
    {
        get
        {
            return _diametroRamal;
        }
        set
        {
            _diametroRamal = value;
            Notify("DiametroRamal");
        }
    }

    public double DiametroDescida
    {
        get
        {
            return _diametroDescida;
        }
        set
        {
            _diametroDescida = value;
            Notify("DiametroDescida");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void Notify([CallerMemberName] string p = "")
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public static double AlturaPadrao(string tipo)
    {
        double basePadrao = AlturaPadraoBase(tipo);
        return GerenciadorPerfisAgua.PerfilAtual?.ObterAltura(tipo, basePadrao) ?? basePadrao;
    }

    public static double AlturaPadraoBase(string tipo)
    {
        return tipo switch
        {
            "Bacia Sanitária" => 0.2,
            "Bacia c/ Válvula" => 1.1,
            "Lavatório" => 0.6,
            "Chuveiro" => 2.2,
            "Ducha Higiênica" => 0.3,
            "Pia" => 0.6,
            "Filtro" => 1.0,
            "Tanque" => 0.9,
            "Máquina de Lavar" => 0.9,
            "Máquina de Lavar Louça" => 0.6,
            "Mictório" => 1.1,
            _ => 0.6,
        };
    }

    public static double OffsetLateralPadrao(string tipo)
    {
        double basePadrao = OffsetLateralPadraoBase(tipo);
        PerfilAgua p = GerenciadorPerfisAgua.PerfilAtual;
        return (p != null) ? (p.ObterOffsetCm(tipo, basePadrao * 100.0) / 100.0) : basePadrao;
    }

    public static double OffsetLateralPadraoBase(string tipo)
    {
        if (!(tipo == "Bacia Sanitária"))
        {
            if (tipo == "Lavatório")
            {
                return 0.1;
            }
            return 0.0;
        }
        return -0.2;
    }

    public static string DetectarTipo(string nome)
    {
        string n = (nome ?? "").ToLower();
        if (n.Contains("higi"))
        {
            return "Ducha Higiênica";
        }
        if (n.Contains("chuveiro") || n.Contains("ducha"))
        {
            return "Chuveiro";
        }
        if (n.Contains("mict"))
        {
            return "Mictório";
        }
        if (n.Contains("vaso") || n.Contains("bacia") || n.Contains("sanit"))
        {
            return (n.Contains("válvula") || n.Contains("valvula") || n.Contains("descarga")) ? "Bacia c/ Válvula" : "Bacia Sanitária";
        }
        if (n.Contains("lavat") || n.Contains("cuba"))
        {
            return "Lavatório";
        }
        if (n.Contains("tanque"))
        {
            return "Tanque";
        }
        if (n.Contains("louç") || n.Contains("louc") || n.Contains("lava-louç") || n.Contains("lava louç"))
        {
            return "Máquina de Lavar Louça";
        }
        if (n.Contains("máquina") || n.Contains("maquina") || n.Contains("lava roupa") || n.Contains("lava-roupa"))
        {
            return "Máquina de Lavar";
        }
        if (n.Contains("filtro") || n.Contains("purificador"))
        {
            return "Filtro";
        }
        if (n.Contains("pia"))
        {
            return "Pia";
        }
        return "Outro";
    }

    public void AtualizarResumo()
    {
        Resumo = Pecas.Where((PecaAguaItemViewModel p) => p.Selecionada).ToList();
        TemChuveiroSelecionado = Resumo.Any((PecaAguaItemViewModel p) => p.TipoSelecionado == "Chuveiro");
    }

    public LancamentoAguaViewModel(Document doc, string nomeAmbiente, List<PecaAguaDetectada> pecas)
    {
        _doc = doc;
        ConfigAguaCache.Carregar();
        NomeAmbiente = nomeAmbiente;
        if (GerenciadorPerfisAgua.PerfilAtual == null && !string.IsNullOrEmpty(ConfigAguaCache.UltimoPerfil))
        {
            GerenciadorPerfisAgua.PerfilAtual = GerenciadorPerfisAgua.Carregar(ConfigAguaCache.UltimoPerfil);
        }
        _alturaPrumada = ConfigAguaCache.AlturaPrumada;
        _alturaRegistro = ConfigAguaCache.AlturaRegistro;
        _alturaRamal = ConfigAguaCache.AlturaRamal;
        _recuoParede = ConfigAguaCache.RecuoParedeCm;
        _diametroRamal = ConfigAguaCache.DiametroRamal;
        _diametroDescida = ConfigAguaCache.DiametroDescida;
        _inserirRegistro = ConfigAguaCache.InserirRegistro;
        _inserirRegistroPressao = ConfigAguaCache.InserirRegistroPressao;
        _inverterSentidoBucha = ConfigAguaCache.InverterSentidoBucha;
        _desviarPeloPiso = ConfigAguaCache.DesviarPeloPiso;
        _alturaPiso = ConfigAguaCache.AlturaPiso;
        _alturaRegistroPressao = ConfigAguaCache.AlturaRegistroPressao;
        _isAguaFria = true;
        _usarVinculo = ConfigAguaCache.UsarVinculo;
        _importarDoVinculo = ConfigAguaCache.ImportarDoVinculo;
        CarregarSistemasETubos(doc);
        CarregarFamiliasRegistro(doc);
        AtualizarSistemas();
        MontarListaPecas(pecas);
        if (GerenciadorPerfisAgua.PerfilAtual != null)
        {
            AplicarPerfil(GerenciadorPerfisAgua.PerfilAtual);
        }
    }

    public MapeamentoAparelhosViewModel ObterMapeamentoViewModel()
    {
        if (_mapeamentoViewModel == null)
        {
            List<FamiliaVinculoInfo> familias = (from g in Pecas.Where((PecaAguaItemViewModel p) => p.Origem != null && p.Origem.Instancia != null && p.Origem.IsDoVinculo).GroupBy<PecaAguaItemViewModel, string>((PecaAguaItemViewModel p) => p.Origem.Instancia.Symbol.FamilyName, StringComparer.OrdinalIgnoreCase)
                                                 select new FamiliaVinculoInfo
                                                 {
                                                     NomeFamilia = g.Key,
                                                     TipoIdentificado = g.First().TipoSelecionado,
                                                     Quantidade = g.Count()
                                                 }).ToList();
            _mapeamentoViewModel = new MapeamentoAparelhosViewModel(_doc, familias);
        }
        return _mapeamentoViewModel;
    }

    private void CarregarFamiliasRegistro(Document doc)
    {
        FamiliasRegistroDisponiveis = (from FamilySymbol s in new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeAccessory)
                                       orderby s.FamilyName, s.Name
                                       select new ComboItemRevit
                                       {
                                           Nome = s.FamilyName + " - " + s.Name,
                                           Id = s.Id
                                       }).ToList();
        ComboItemRevit salvo = FamiliasRegistroDisponiveis.FirstOrDefault((ComboItemRevit f) => f.Nome == ConfigAguaCache.FamiliaRegistroNome);
        if (salvo == null)
        {
            FamilySymbol sugestao = MotorRoteamentoAgua.BuscarSimboloRegistro(doc, DiametroRamal / 304.8);
            if (sugestao != null)
            {
                salvo = FamiliasRegistroDisponiveis.FirstOrDefault((ComboItemRevit f) => f.Id == sugestao.Id);
            }
        }
        FamiliaRegistroSelecionada = salvo ?? FamiliasRegistroDisponiveis.FirstOrDefault();
        ComboItemRevit salvoP = FamiliasRegistroDisponiveis.FirstOrDefault((ComboItemRevit f) => f.Nome == ConfigAguaCache.FamiliaRegistroPressaoNome);
        if (salvoP == null)
        {
            FamilySymbol sugestaoP = MotorRoteamentoAgua.BuscarSimboloRegistroPressao(doc, DiametroDescida / 304.8);
            if (sugestaoP != null)
            {
                salvoP = FamiliasRegistroDisponiveis.FirstOrDefault((ComboItemRevit f) => f.Id == sugestaoP.Id);
            }
        }
        FamiliaRegistroPressaoSelecionada = salvoP;
    }

    private void CarregarSistemasETubos(Document doc)
    {
        foreach (PipingSystemType st in from PipingSystemType s in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
                                        orderby s.Name
                                        select s)
        {
            if (st.SystemClassification == MEPSystemClassification.DomesticColdWater)
            {
                _sistemasFrios.Add(new ComboItemRevit
                {
                    Nome = st.Name,
                    Id = st.Id
                });
            }
            else if (st.SystemClassification == MEPSystemClassification.DomesticHotWater)
            {
                _sistemasQuentes.Add(new ComboItemRevit
                {
                    Nome = st.Name,
                    Id = st.Id
                });
            }
        }
        TiposTuboDisponiveis = (from PipeType t in new FilteredElementCollector(doc).OfClass(typeof(PipeType))
                                orderby t.Name
                                select new ComboItemRevit
                                {
                                    Nome = t.Name,
                                    Id = t.Id
                                }).ToList();
        TipoTuboSelecionado = TiposTuboDisponiveis.FirstOrDefault((ComboItemRevit t) => t.Nome == ConfigAguaCache.TipoTuboNome) ?? TiposTuboDisponiveis.FirstOrDefault();
    }

    private void AtualizarSistemas()
    {
        SistemasDisponiveis = (_isAguaFria ? _sistemasFrios : _sistemasQuentes);
        Notify("SistemasDisponiveis");
        string salvo = (_isAguaFria ? ConfigAguaCache.SistemaAF : ConfigAguaCache.SistemaAQ);
        SistemaSelecionado = SistemasDisponiveis.FirstOrDefault((ComboItemRevit s) => s.Nome == salvo) ?? SistemasDisponiveis.FirstOrDefault();
    }

    private static bool EhGenerico(PecaAguaDetectada p)
    {
        FamilyInstance inst = p?.Instancia;
        return inst != null && inst.Category != null && inst.Category.Id == new ElementId(BuiltInCategory.OST_GenericModel);
    }

    private void MontarListaPecas(List<PecaAguaDetectada> pecas)
    {
        _todasPecasDetectadas = pecas ?? new List<PecaAguaDetectada>();
        AtualizarPecasVisiveis();
    }

    private void AtualizarPecasVisiveis()
    {
        Pecas.Clear();
        List<PecaAguaDetectada> filtradas = (_usarVinculo ? _todasPecasDetectadas : _todasPecasDetectadas.Where((PecaAguaDetectada p) => !p.IsDoVinculo).ToList());
        var itens = filtradas.Select(delegate (PecaAguaDetectada p)
        {
            string text = ((p.Instancia != null) ? p.Instancia.Symbol.FamilyName : "");
            string tipo = MapeamentoFamiliasAgua.ObterTipo(text) ?? DetectarTipo(p.Nome + " " + text);
            return new
            {
                Peca = p,
                Tipo = tipo
            };
        }).ToList();
        List<PecaAguaDetectada> mantidas = new List<PecaAguaDetectada>();
        foreach (var x in itens.OrderBy(a => ((a.Tipo == "Outro") ? 2 : 0) + (EhGenerico(a.Peca) ? 1 : 0)))
        {
            XYZ pos = x.Peca.Posicao;
            if (pos == null || !mantidas.Any((PecaAguaDetectada m) => m.Posicao != null && Math.Abs(m.Posicao.X - pos.X) < 0.5 && Math.Abs(m.Posicao.Y - pos.Y) < 0.5))
            {
                mantidas.Add(x.Peca);
            }
        }
        itens = itens.Where(anon => mantidas.Contains(anon.Peca)).ToList();
        Dictionary<string, int> contagem = (from anon in itens
                                            group anon by anon.Tipo).ToDictionary(g => g.Key, g => g.Count());
        Dictionary<string, int> indice = new Dictionary<string, int>();
        foreach (var x2 in itens)
        {
            string nomeExib = x2.Tipo;
            if (contagem[x2.Tipo] > 1)
            {
                if (!indice.ContainsKey(x2.Tipo))
                {
                    indice[x2.Tipo] = 0;
                }
                indice[x2.Tipo]++;
                nomeExib = x2.Tipo + " " + indice[x2.Tipo].ToString("00");
            }
            PecaAguaItemViewModel item = new PecaAguaItemViewModel
            {
                Origem = x2.Peca,
                NomeExibicao = nomeExib,
                NomeOriginal = x2.Peca.Nome,
                AlturaPonto = AlturaPadrao(x2.Tipo),
                OffsetCm = OffsetLateralPadrao(x2.Tipo) * 100.0
            };
            item.DefinirTipoInicial(x2.Tipo);
            Pecas.Add(item);
        }
        AtualizarResumo();
    }

    public void MarcarPecasNaCaixa(double minX, double minY, double maxX, double maxY)
    {
        foreach (PecaAguaItemViewModel item in Pecas)
        {
            XYZ pos = ((item.Origem != null) ? item.Origem.Posicao : null);
            item.Selecionada = pos != null && pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY;
        }
        AtualizarResumo();
    }

    public void SalvarCache()
    {
        ConfigAguaCache.IsAguaFria = _isAguaFria;
        if (_isAguaFria)
        {
            ConfigAguaCache.SistemaAF = ((SistemaSelecionado != null) ? SistemaSelecionado.Nome : "");
        }
        else
        {
            ConfigAguaCache.SistemaAQ = ((SistemaSelecionado != null) ? SistemaSelecionado.Nome : "");
        }
        ConfigAguaCache.TipoTuboNome = ((TipoTuboSelecionado != null) ? TipoTuboSelecionado.Nome : "");
        ConfigAguaCache.AlturaPrumada = _alturaPrumada;
        ConfigAguaCache.AlturaRegistro = _alturaRegistro;
        ConfigAguaCache.AlturaRamal = _alturaRamal;
        ConfigAguaCache.RecuoParedeCm = _recuoParede;
        ConfigAguaCache.DiametroRamal = _diametroRamal;
        ConfigAguaCache.DiametroDescida = _diametroDescida;
        ConfigAguaCache.InserirRegistro = _inserirRegistro;
        ConfigAguaCache.FamiliaRegistroNome = ((FamiliaRegistroSelecionada != null) ? FamiliaRegistroSelecionada.Nome : "");
        ConfigAguaCache.InserirRegistroPressao = _inserirRegistroPressao;
        ConfigAguaCache.InverterSentidoBucha = _inverterSentidoBucha;
        ConfigAguaCache.DesviarPeloPiso = _desviarPeloPiso;
        ConfigAguaCache.AlturaPiso = _alturaPiso;
        ConfigAguaCache.FamiliaRegistroPressaoNome = ((FamiliaRegistroPressaoSelecionada != null) ? FamiliaRegistroPressaoSelecionada.Nome : "");
        ConfigAguaCache.AlturaRegistroPressao = _alturaRegistroPressao;
        ConfigAguaCache.UsarVinculo = _usarVinculo;
        ConfigAguaCache.ImportarDoVinculo = _importarDoVinculo;
        ConfigAguaCache.Salvar();
    }

    public void AplicarPerfil(PerfilAgua p)
    {
        if (p == null)
        {
            return;
        }
        AlturaPrumada = p.AlturaPrumada;
        AlturaRegistro = p.AlturaRegistro;
        AlturaRamal = p.AlturaRamal;
        RecuoParede = p.RecuoParedeCm;
        DiametroRamal = p.DiametroRamal;
        DiametroDescida = p.DiametroDescida;
        AlturaRegistroPressao = p.AlturaRegistroPressao;
        InverterSentidoBucha = p.InverterSentidoBucha;
        DesviarPeloPiso = p.DesviarPeloPiso;
        AlturaPiso = p.AlturaPiso;
        foreach (PecaAguaItemViewModel item in Pecas)
        {
            item.AlturaPonto = p.ObterAltura(item.TipoSelecionado, AlturaPadraoBase(item.TipoSelecionado));
            item.OffsetCm = p.ObterOffsetCm(item.TipoSelecionado, OffsetLateralPadraoBase(item.TipoSelecionado) * 100.0);
        }
        AtualizarResumo();
    }

    public void AplicarValoresPontos(Dictionary<string, double> alturas, Dictionary<string, double> offsetsCm)
    {
        foreach (PecaAguaItemViewModel item in Pecas)
        {
            if (alturas != null && alturas.TryGetValue(item.TipoSelecionado ?? "", out var v))
            {
                item.AlturaPonto = v;
            }
            if (offsetsCm != null && offsetsCm.TryGetValue(item.TipoSelecionado ?? "", out v))
            {
                item.OffsetCm = v;
            }
        }
        AtualizarResumo();
    }
}
