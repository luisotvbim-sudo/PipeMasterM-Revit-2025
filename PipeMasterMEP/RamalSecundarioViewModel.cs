using System.ComponentModel;

namespace PipeMasterMEP;

public class RamalSecundarioViewModel : INotifyPropertyChanged
{
    public bool AlinharComPrimario
    {
        get
        {
            return ConfiguracoesRamal.AlinharComPrimario;
        }
        set
        {
            if (ConfiguracoesRamal.AlinharComPrimario != value)
            {
                ConfiguracoesRamal.AlinharComPrimario = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AlinharComPrimario"));
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AlinharComCaixa"));
            }
        }
    }

    public bool AlinharComCaixa
    {
        get
        {
            return !ConfiguracoesRamal.AlinharComPrimario;
        }
        set
        {
            if (ConfiguracoesRamal.AlinharComPrimario == value)
            {
                AlinharComPrimario = !value;
            }
        }
    }

    public bool NivelarTampa
    {
        get
        {
            return ConfiguracoesRamal.NivelarTampa;
        }
        set
        {
            if (ConfiguracoesRamal.NivelarTampa != value)
            {
                ConfiguracoesRamal.NivelarTampa = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NivelarTampa"));
            }
        }
    }

    public string Inclinacao
    {
        get
        {
            return ConfiguracoesRamal.Inclinacao;
        }
        set
        {
            if (ConfiguracoesRamal.Inclinacao != value)
            {
                ConfiguracoesRamal.Inclinacao = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Inclinacao"));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
