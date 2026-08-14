using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PipeMasterMEP;

public static class MapeamentoFamiliasAgua
{
    private static readonly string _caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeMasterMEP", "mapeamento_familias_agua.txt");

    private static Dictionary<string, string> _mapa;

    private static void Carregar()
    {
        if (_mapa != null)
        {
            return;
        }
        _mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(_caminho))
            {
                return;
            }
            string[] array = File.ReadAllLines(_caminho);
            foreach (string l in array)
            {
                int idx = l.IndexOf('=');
                if (idx > 0)
                {
                    _mapa[l.Substring(0, idx).Trim()] = l.Substring(idx + 1).Trim();
                }
            }
        }
        catch
        {
        }
    }

    public static string ObterTipo(string familia)
    {
        if (string.IsNullOrEmpty(familia))
        {
            return null;
        }
        Carregar();
        string t;
        return (_mapa.TryGetValue(familia, out t) && !string.IsNullOrEmpty(t)) ? t : null;
    }

}
