using System;
using System.Collections.Generic;
using System.IO;

namespace PipeMasterMEP;

public static class ConfigCache
{
    private static readonly string _caminhoArquivo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeMasterMEP", "config.txt");

    public static string TipoTuboNome = "";

    public static string SistemaNome = "";

    public static string Elevacao = "-0.15";

    public static int DiamLavatorioIndex = 0;

    public static string AltLavatorio = "0.60";

    public static bool DesvioViga = false;

    public static bool Vaso = true;

    public static bool Caixa = true;

    public static bool Lavatorio = true;

    public static bool Chuveiro = true;

    public static bool Pia = false;

    public static bool Maquina = false;

    public static string DistanciaVaso = "30";

    public static int OpcaoVentilacao = 0;

    public static bool RotacaoTe90 = true;

    public static bool Joelho45NoChicote = false;

    public static string AltVentilacaoCavalete = "0.56";

    public static bool BloquearHorizontais = false;

    public static int TabAtiva = 0;

    public static int DestinoVaso = 0;

    public static int DestinoPia = 0;

    public static int DestinoMaquina = 0;

    public static int DestinoCaixa = 0;

    public static void Carregar()
    {
        try
        {
            if (!File.Exists(_caminhoArquivo))
            {
                return;
            }
            string[] linhas = File.ReadAllLines(_caminhoArquivo);
            Dictionary<string, string> d = new Dictionary<string, string>();
            string[] array = linhas;
            foreach (string l in array)
            {
                int idx = l.IndexOf('=');
                if (idx > 0)
                {
                    d[l.Substring(0, idx).Trim()] = l.Substring(idx + 1).Trim();
                }
            }
            if (d.TryGetValue("TipoTuboNome", out var v) && !string.IsNullOrEmpty(v))
            {
                TipoTuboNome = v;
            }
            if (d.TryGetValue("SistemaNome", out v) && !string.IsNullOrEmpty(v))
            {
                SistemaNome = v;
            }
            if (d.TryGetValue("Elevacao", out v))
            {
                Elevacao = v;
            }
            if (d.TryGetValue("DiamLavatorioIndex", out v) && int.TryParse(v, out var di))
            {
                DiamLavatorioIndex = di;
            }
            if (d.TryGetValue("AltLavatorio", out v))
            {
                AltLavatorio = v;
            }
            if (d.TryGetValue("DesvioViga", out v) && bool.TryParse(v, out var bv))
            {
                DesvioViga = bv;
            }
            if (d.TryGetValue("Vaso", out v) && bool.TryParse(v, out bv))
            {
                Vaso = bv;
            }
            if (d.TryGetValue("Caixa", out v) && bool.TryParse(v, out bv))
            {
                Caixa = bv;
            }
            if (d.TryGetValue("Lavatorio", out v) && bool.TryParse(v, out bv))
            {
                Lavatorio = bv;
            }
            if (d.TryGetValue("Chuveiro", out v) && bool.TryParse(v, out bv))
            {
                Chuveiro = bv;
            }
            if (d.TryGetValue("Pia", out v) && bool.TryParse(v, out bv))
            {
                Pia = bv;
            }
            if (d.TryGetValue("Maquina", out v) && bool.TryParse(v, out bv))
            {
                Maquina = bv;
            }
            if (d.TryGetValue("OpcaoVentilacao", out v) && int.TryParse(v, out var iv))
            {
                OpcaoVentilacao = iv;
            }
            if (d.TryGetValue("AltVentilacaoCavalete", out v))
            {
                AltVentilacaoCavalete = v;
            }
            if (d.TryGetValue("RotacaoTe90", out v) && bool.TryParse(v, out bv))
            {
                RotacaoTe90 = bv;
            }
            if (d.TryGetValue("Joelho45NoChicote", out v) && bool.TryParse(v, out bv))
            {
                Joelho45NoChicote = bv;
            }
            if (d.TryGetValue("BloquearHorizontais", out v) && bool.TryParse(v, out bv))
            {
                BloquearHorizontais = bv;
            }
            if (d.TryGetValue("TabAtiva", out v) && int.TryParse(v, out var ta))
            {
                TabAtiva = ta;
            }
            if (d.TryGetValue("DestinoVaso", out v) && int.TryParse(v, out var dv))
            {
                DestinoVaso = dv;
            }
            if (d.TryGetValue("DestinoPia", out v) && int.TryParse(v, out var dp))
            {
                DestinoPia = dp;
            }
            if (d.TryGetValue("DestinoMaquina", out v) && int.TryParse(v, out var dm))
            {
                DestinoMaquina = dm;
            }
            if (d.TryGetValue("DestinoCaixa", out v) && int.TryParse(v, out var dc))
            {
                DestinoCaixa = dc;
            }
            if (d.TryGetValue("DistanciaVaso", out v))
            {
                DistanciaVaso = v;
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
            string[] linhas = new string[23]
            {
                "TipoTuboNome=" + TipoTuboNome,
                "SistemaNome=" + SistemaNome,
                "Elevacao=" + Elevacao,
                $"DiamLavatorioIndex={DiamLavatorioIndex}",
                "AltLavatorio=" + AltLavatorio,
                $"DesvioViga={DesvioViga}",
                $"Vaso={Vaso}",
                $"Caixa={Caixa}",
                $"Lavatorio={Lavatorio}",
                $"Chuveiro={Chuveiro}",
                $"Pia={Pia}",
                $"Maquina={Maquina}",
                $"OpcaoVentilacao={OpcaoVentilacao}",
                "AltVentilacaoCavalete=" + AltVentilacaoCavalete,
                $"RotacaoTe90={RotacaoTe90}",
                $"Joelho45NoChicote={Joelho45NoChicote}",
                $"BloquearHorizontais={BloquearHorizontais}",
                $"TabAtiva={TabAtiva}",
                $"DestinoVaso={DestinoVaso}",
                $"DestinoPia={DestinoPia}",
                $"DestinoMaquina={DestinoMaquina}",
                $"DestinoCaixa={DestinoCaixa}",
                "DistanciaVaso=" + DistanciaVaso
            };
            File.WriteAllLines(_caminhoArquivo, linhas);
        }
        catch
        {
        }
    }
}
