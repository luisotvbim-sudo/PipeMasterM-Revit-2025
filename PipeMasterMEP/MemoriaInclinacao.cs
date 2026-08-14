using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PipeMasterMEP;

public static class MemoriaInclinacao
{
    public static Dictionary<string, Dictionary<int, double>> RegrasPorSistema = new Dictionary<string, Dictionary<int, double>>();

    public static bool NivelarTampaCaixas = true;

    private static string CaminhoArquivo => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeMasterMEP", "configuracoes.json");

    public static double? ObterInclinacao(string sistema, int diametroMm)
    {
        if (RegrasPorSistema.ContainsKey(sistema) && RegrasPorSistema[sistema].ContainsKey(diametroMm))
        {
            return RegrasPorSistema[sistema][diametroMm];
        }
        return null;
    }

    public static void Salvar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoArquivo));
            ConfiguracaoArquivo modelo = new ConfiguracaoArquivo
            {
                NivelarTampaCaixas = NivelarTampaCaixas,
                RegrasPorSistema = new Dictionary<string, Dictionary<string, double>>()
            };
            foreach (KeyValuePair<string, Dictionary<int, double>> kvpSistema in RegrasPorSistema)
            {
                Dictionary<string, double> dicString = new Dictionary<string, double>();
                foreach (KeyValuePair<int, double> kvpDiam in kvpSistema.Value)
                {
                    dicString[kvpDiam.Key.ToString()] = kvpDiam.Value;
                }
                modelo.RegrasPorSistema[kvpSistema.Key] = dicString;
            }
            string json = JsonSerializer.Serialize(modelo, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(CaminhoArquivo, json);
        }
        catch
        {
        }
    }

    public static void Carregar()
    {
        try
        {
            if (!File.Exists(CaminhoArquivo))
            {
                return;
            }
            string json = File.ReadAllText(CaminhoArquivo);
            ConfiguracaoArquivo modelo = JsonSerializer.Deserialize<ConfiguracaoArquivo>(json);
            if (modelo == null)
            {
                return;
            }
            NivelarTampaCaixas = modelo.NivelarTampaCaixas;
            RegrasPorSistema = new Dictionary<string, Dictionary<int, double>>();
            foreach (KeyValuePair<string, Dictionary<string, double>> kvpSistema in modelo.RegrasPorSistema)
            {
                Dictionary<int, double> dicInt = new Dictionary<int, double>();
                foreach (KeyValuePair<string, double> kvpDiam in kvpSistema.Value)
                {
                    if (int.TryParse(kvpDiam.Key, out var diam))
                    {
                        dicInt[diam] = kvpDiam.Value;
                    }
                }
                RegrasPorSistema[kvpSistema.Key] = dicInt;
            }
        }
        catch
        {
        }
    }
}
