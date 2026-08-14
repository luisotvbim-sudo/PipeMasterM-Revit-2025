using System.Collections.Generic;
using System.IO;

namespace PipeMasterMEP;

public static class MemoriaPipeMaster
{
    private static string arquivoPath = Path.Combine(Path.GetTempPath(), "PipeMasterSettings.txt");

    public static string UltimoTipoTubo { get; set; } = "";

    public static string UltimoSistema { get; set; } = "";

    public static string UltimaElevacao { get; set; } = "0";

    public static Dictionary<double, string> InclinacoesPorDiametro { get; set; } = new Dictionary<double, string>();

    public static bool ApagarLinhas { get; set; } = false;

    public static void Salvar(string elev, bool apagar, string tipo, string sis, Dictionary<double, string> incs)
    {
        try
        {
            List<string> linhas = new List<string>
            {
                "ELEV=" + elev,
                $"APAGAR={apagar}",
                "TIPO=" + tipo,
                "SIS=" + sis
            };
            foreach (KeyValuePair<double, string> kv in incs)
            {
                linhas.Add($"INC_{kv.Key}={kv.Value}");
            }
            File.WriteAllLines(arquivoPath, linhas);
        }
        catch
        {
        }
    }

    public static void Carregar()
    {
        InclinacoesPorDiametro.Clear();
        try
        {
            if (!File.Exists(arquivoPath))
            {
                return;
            }
            string[] array = File.ReadAllLines(arquivoPath);
            foreach (string linha in array)
            {
                if (linha.StartsWith("ELEV="))
                {
                    UltimaElevacao = linha.Substring(5);
                }
                else if (linha.StartsWith("APAGAR="))
                {
                    ApagarLinhas = bool.Parse(linha.Substring(7));
                }
                else if (linha.StartsWith("TIPO="))
                {
                    UltimoTipoTubo = linha.Substring(5);
                }
                else if (linha.StartsWith("SIS="))
                {
                    UltimoSistema = linha.Substring(4);
                }
                else if (linha.StartsWith("INC_"))
                {
                    string[] partes = linha.Substring(4).Split('=');
                    if (partes.Length == 2 && double.TryParse(partes[0], out var diam))
                    {
                        InclinacoesPorDiametro[diam] = partes[1];
                    }
                }
            }
        }
        catch
        {
        }
    }
}
