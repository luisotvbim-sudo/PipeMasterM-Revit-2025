using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PipeMasterMEP;

public class ItemMapeamento : INotifyPropertyChanged
{
    private bool _incluir = true;

    private string _familiaSelecionada;

    public bool Incluir
    {
        get
        {
            return _incluir;
        }
        set
        {
            _incluir = value;
            Notify("Incluir");
        }
    }

    public string NomeFamiliaVinculo { get; set; }

    public string TipoIdentificado { get; set; }

    public int Quantidade { get; set; }

    public List<string> FamiliasProjetoDisponiveis { get; set; } = new List<string>();

    public string FamiliaSelecionada
    {
        get
        {
            return _familiaSelecionada;
        }
        set
        {
            _familiaSelecionada = value;
            Notify("FamiliaSelecionada");
        }
    }

    public int Confianca { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    private void Notify([CallerMemberName] string p = "")
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
