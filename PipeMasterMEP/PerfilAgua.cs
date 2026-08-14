using System.Collections.Generic;

namespace PipeMasterMEP;

public class PerfilAgua
{
    public string Nome = "";

    public Dictionary<string, double> Alturas = new Dictionary<string, double>();

    public Dictionary<string, double> Offsets = new Dictionary<string, double>();

    public double AlturaPrumada = 2.5;

    public double AlturaRegistro = 1.8;

    public double AlturaRamal = 0.6;

    public double RecuoParedeCm = 3.0;

    public double DiametroRamal = 25.0;

    public double DiametroDescida = 25.0;

    public double AlturaRegistroPressao = 1.1;

    public bool InverterSentidoBucha = false;

    public bool DesviarPeloPiso = false;

    public double AlturaPiso = 0.0;

    public double ObterAltura(string tipo, double padrao)
    {
        double v;
        return Alturas.TryGetValue(tipo ?? "", out v) ? v : padrao;
    }

    public double ObterOffsetCm(string tipo, double padrao)
    {
        double v;
        return Offsets.TryGetValue(tipo ?? "", out v) ? v : padrao;
    }
}
