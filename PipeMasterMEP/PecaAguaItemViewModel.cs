using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PipeMasterMEP;

public class PecaAguaItemViewModel : INotifyPropertyChanged
{
    private bool _selecionada = true;

    private string _tipoSelecionado;

    private double _alturaPonto;

    private double _offsetCm;

    public PecaAguaDetectada Origem { get; set; }

    public string NomeExibicao { get; set; }

    public string NomeOriginal { get; set; }

    public bool Selecionada
    {
        get
        {
            return _selecionada;
        }
        set
        {
            _selecionada = value;
            Notify("Selecionada");
        }
    }

    public string TipoSelecionado
    {
        get
        {
            return _tipoSelecionado;
        }
        set
        {
            if (!(_tipoSelecionado == value))
            {
                _tipoSelecionado = value;
                Notify("TipoSelecionado");
                AlturaPonto = LancamentoAguaViewModel.AlturaPadrao(value);
                OffsetCm = LancamentoAguaViewModel.OffsetLateralPadrao(value) * 100.0;
            }
        }
    }

    public double AlturaPonto
    {
        get
        {
            return _alturaPonto;
        }
        set
        {
            _alturaPonto = value;
            Notify("AlturaPonto");
        }
    }

    public double OffsetCm
    {
        get
        {
            return _offsetCm;
        }
        set
        {
            _offsetCm = value;
            Notify("OffsetCm");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void Notify([CallerMemberName] string p = "")
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public void DefinirTipoInicial(string tipo)
    {
        _tipoSelecionado = tipo;
    }
}
