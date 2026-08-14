using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeMasterMEP;

public class PipeMasterOptionsViewModel : INotifyPropertyChanged
{
    private static bool _memIsPadrao = true;

    private static bool _memIsPersonalizado = false;

    private static string _memTipoNome = null;

    private static string _memSistemaNome = null;

    private static string _memDiametro = null;

    private static string _memComprimento = "0.50";

    private Document _doc;

    private bool _isPadrao;

    private bool _isPersonalizado;

    private PipeType _tipoSelecionado;

    private PipingSystemType _sistemaSelecionado;

    private string _diametroSelecionado;

    private string _comprimento;

    private Brush _textColor = Brushes.Black;

    public Brush TextColor
    {
        get
        {
            return _textColor;
        }
        set
        {
            _textColor = value;
            OnPropertyChanged("TextColor");
        }
    }

    public ObservableCollection<PipeType> TiposDeTubo { get; set; } = new ObservableCollection<PipeType>();

    public ObservableCollection<PipingSystemType> Sistemas { get; set; } = new ObservableCollection<PipingSystemType>();

    public ObservableCollection<string> Diametros { get; set; } = new ObservableCollection<string>();

    public bool IsPadrao
    {
        get
        {
            return _isPadrao;
        }
        set
        {
            _isPadrao = value;
            _memIsPadrao = value;
            OnPropertyChanged("IsPadrao");
        }
    }

    public bool IsPersonalizado
    {
        get
        {
            return _isPersonalizado;
        }
        set
        {
            _isPersonalizado = value;
            _memIsPersonalizado = value;
            OnPropertyChanged("IsPersonalizado");
        }
    }

    public PipeType TipoSelecionado
    {
        get
        {
            return _tipoSelecionado;
        }
        set
        {
            _tipoSelecionado = value;
            _memTipoNome = value?.Name;
            OnPropertyChanged("TipoSelecionado");
            AtualizarDiametros();
        }
    }

    public PipingSystemType SistemaSelecionado
    {
        get
        {
            return _sistemaSelecionado;
        }
        set
        {
            _sistemaSelecionado = value;
            _memSistemaNome = value?.Name;
            OnPropertyChanged("SistemaSelecionado");
        }
    }

    public string DiametroSelecionado
    {
        get
        {
            return _diametroSelecionado;
        }
        set
        {
            _diametroSelecionado = value;
            _memDiametro = value;
            OnPropertyChanged("DiametroSelecionado");
        }
    }

    public string Comprimento
    {
        get
        {
            return _comprimento;
        }
        set
        {
            _comprimento = value;
            _memComprimento = value;
            OnPropertyChanged("Comprimento");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void AjustarTema(Autodesk.Revit.DB.Color revitBgColor)
    {
        double brilho = 0.299 * (double)(int)revitBgColor.Red + 0.587 * (double)(int)revitBgColor.Green + 0.114 * (double)(int)revitBgColor.Blue;
        TextColor = ((brilho < 128.0) ? Brushes.White : Brushes.Black);
    }

    public void Initialize(Document doc)
    {
        _doc = doc;
        List<PipeType> tipos = (from PipeType p in new FilteredElementCollector(doc).OfClass(typeof(PipeType))
                                orderby p.Name
                                select p).ToList();
        foreach (PipeType t in tipos)
        {
            TiposDeTubo.Add(t);
        }
        List<PipingSystemType> sistemas = (from PipingSystemType pipingSystemType in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
                                           orderby pipingSystemType.Name
                                           select pipingSystemType).ToList();
        foreach (PipingSystemType s in sistemas)
        {
            Sistemas.Add(s);
        }
        _isPadrao = _memIsPadrao;
        _isPersonalizado = _memIsPersonalizado;
        _comprimento = _memComprimento;
        if (!string.IsNullOrEmpty(_memSistemaNome))
        {
            _sistemaSelecionado = Sistemas.FirstOrDefault((PipingSystemType pipingSystemType) => pipingSystemType.Name == _memSistemaNome) ?? Sistemas.FirstOrDefault();
        }
        else
        {
            _sistemaSelecionado = Sistemas.FirstOrDefault();
        }
        if (!string.IsNullOrEmpty(_memTipoNome))
        {
            _tipoSelecionado = TiposDeTubo.FirstOrDefault((PipeType pipeType) => pipeType.Name == _memTipoNome) ?? TiposDeTubo.FirstOrDefault();
        }
        else
        {
            _tipoSelecionado = TiposDeTubo.FirstOrDefault();
        }
        OnPropertyChanged("IsPadrao");
        OnPropertyChanged("IsPersonalizado");
        OnPropertyChanged("SistemaSelecionado");
        OnPropertyChanged("TipoSelecionado");
        OnPropertyChanged("Comprimento");
        AtualizarDiametros();
    }

    private void AtualizarDiametros()
    {
        Diametros.Clear();
        if (_tipoSelecionado == null || _doc == null)
        {
            return;
        }
        try
        {
            RoutingPreferenceManager rpm = _tipoSelecionado.RoutingPreferenceManager;
            RoutingPreferenceRule segmentRule = rpm.GetRule(RoutingPreferenceRuleGroupType.Segments, 0);
            if (segmentRule != null && _doc.GetElement(segmentRule.MEPPartId) is PipeSegment segment)
            {
                foreach (MEPSize size in segment.GetSizes())
                {
                    double diamMm = UnitUtils.ConvertFromInternalUnits(size.NominalDiameter, UnitTypeId.Millimeters);
                    Diametros.Add(Math.Round(diamMm, 1).ToString());
                }
            }
        }
        catch
        {
        }
        if (!Diametros.Any())
        {
            string[] fallback = new string[8] { "20", "25", "32", "40", "50", "75", "100", "150" };
            string[] array = fallback;
            foreach (string d in array)
            {
                Diametros.Add(d);
            }
        }
        if (!string.IsNullOrEmpty(_memDiametro) && Diametros.Contains(_memDiametro))
        {
            DiametroSelecionado = _memDiametro;
        }
        else if (Diametros.Any())
        {
            DiametroSelecionado = Diametros.First();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
