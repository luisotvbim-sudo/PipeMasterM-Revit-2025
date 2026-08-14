using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PipeMasterMEP;

public static class ConfigAguaCache
{
    private static readonly string _caminhoArquivo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeMasterMEP", "config_agua.txt");

    public static bool IsAguaFria = true;

    public static string SistemaAF = "";

    public static string SistemaAQ = "";

    public static string TipoTuboNome = "";

    public static double AlturaPrumada = 2.5;

    public static double AlturaRegistro = 1.8;

    public static double AlturaRamal = 0.6;

    public static double RecuoParedeCm = 3.0;

    public static double DiametroRamal = 25.0;

    public static double DiametroDescida = 25.0;

    public static bool InserirRegistro = true;

    public static string FamiliaRegistroNome = "";

    public static bool InserirRegistroPressao = true;

    public static bool InverterSentidoBucha = false;

    public static bool DesviarPeloPiso = false;

    public static double AlturaPiso = 0.0;

    public static string FamiliaRegistroPressaoNome = "";

    public static double AlturaRegistroPressao = 1.1;

    public static string UltimoPerfil = "";

    public static bool UsarVinculo = true;

    public static bool ImportarDoVinculo = false;

    public static void Carregar()
    {
        try
        {
            if (!File.Exists(_caminhoArquivo))
            {
                return;
            }
            Dictionary<string, string> d = new Dictionary<string, string>();
            string[] array = File.ReadAllLines(_caminhoArquivo);
            foreach (string l in array)
            {
                int idx = l.IndexOf('=');
                if (idx > 0)
                {
                    d[l.Substring(0, idx).Trim()] = l.Substring(idx + 1).Trim();
                }
            }
            if (d.TryGetValue("IsAguaFria", out var v) && bool.TryParse(v, out var bv))
            {
                IsAguaFria = bv;
            }
            if (d.TryGetValue("SistemaAF", out v))
            {
                SistemaAF = v;
            }
            if (d.TryGetValue("SistemaAQ", out v))
            {
                SistemaAQ = v;
            }
            if (d.TryGetValue("TipoTuboNome", out v))
            {
                TipoTuboNome = v;
            }
            if (d.TryGetValue("AlturaPrumada", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
            {
                AlturaPrumada = dv;
            }
            if (d.TryGetValue("AlturaRegistro", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                AlturaRegistro = dv;
            }
            if (d.TryGetValue("AlturaRamal", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                AlturaRamal = dv;
            }
            if (d.TryGetValue("RecuoParedeCm", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                RecuoParedeCm = dv;
            }
            if (d.TryGetValue("DiametroRamal", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                DiametroRamal = dv;
            }
            if (d.TryGetValue("DiametroDescida", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                DiametroDescida = dv;
            }
            if (d.TryGetValue("InserirRegistro", out v) && bool.TryParse(v, out bv))
            {
                InserirRegistro = bv;
            }
            if (d.TryGetValue("FamiliaRegistroNome", out v))
            {
                FamiliaRegistroNome = v;
            }
            if (d.TryGetValue("InserirRegistroPressao", out v) && bool.TryParse(v, out bv))
            {
                InserirRegistroPressao = bv;
            }
            if (d.TryGetValue("InverterSentidoBucha", out v) && bool.TryParse(v, out bv))
            {
                InverterSentidoBucha = bv;
            }
            if (d.TryGetValue("DesviarPeloPiso", out v) && bool.TryParse(v, out bv))
            {
                DesviarPeloPiso = bv;
            }
            if (d.TryGetValue("AlturaPiso", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                AlturaPiso = dv;
            }
            if (d.TryGetValue("FamiliaRegistroPressaoNome", out v))
            {
                FamiliaRegistroPressaoNome = v;
            }
            if (d.TryGetValue("AlturaRegistroPressao", out v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv))
            {
                AlturaRegistroPressao = dv;
            }
            if (d.TryGetValue("UltimoPerfil", out v))
            {
                UltimoPerfil = v;
            }
            if (d.TryGetValue("UsarVinculo", out v) && bool.TryParse(v, out bv))
            {
                UsarVinculo = bv;
            }
            if (d.TryGetValue("ImportarDoVinculo", out v) && bool.TryParse(v, out bv))
            {
                ImportarDoVinculo = bv;
            }
        }
        catch
        {
        }
    }

    public static void Salvar()
    {
        try
        {
            string dir = Path.GetDirectoryName(_caminhoArquivo);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string[] linhas = new string[21]
            {
                "IsAguaFria=" + IsAguaFria,
                "SistemaAF=" + SistemaAF,
                "SistemaAQ=" + SistemaAQ,
                "TipoTuboNome=" + TipoTuboNome,
                "AlturaPrumada=" + AlturaPrumada.ToString(CultureInfo.InvariantCulture),
                "AlturaRegistro=" + AlturaRegistro.ToString(CultureInfo.InvariantCulture),
                "AlturaRamal=" + AlturaRamal.ToString(CultureInfo.InvariantCulture),
                "RecuoParedeCm=" + RecuoParedeCm.ToString(CultureInfo.InvariantCulture),
                "DiametroRamal=" + DiametroRamal.ToString(CultureInfo.InvariantCulture),
                "DiametroDescida=" + DiametroDescida.ToString(CultureInfo.InvariantCulture),
                "InserirRegistro=" + InserirRegistro,
                "FamiliaRegistroNome=" + FamiliaRegistroNome,
                "InserirRegistroPressao=" + InserirRegistroPressao,
                "InverterSentidoBucha=" + InverterSentidoBucha,
                "DesviarPeloPiso=" + DesviarPeloPiso,
                "AlturaPiso=" + AlturaPiso.ToString(CultureInfo.InvariantCulture),
                "FamiliaRegistroPressaoNome=" + FamiliaRegistroPressaoNome,
                "AlturaRegistroPressao=" + AlturaRegistroPressao.ToString(CultureInfo.InvariantCulture),
                "UltimoPerfil=" + UltimoPerfil,
                "UsarVinculo=" + UsarVinculo,
                "ImportarDoVinculo=" + ImportarDoVinculo
            };
            File.WriteAllLines(_caminhoArquivo, linhas);
        }
        catch
        {
        }
    }
}
