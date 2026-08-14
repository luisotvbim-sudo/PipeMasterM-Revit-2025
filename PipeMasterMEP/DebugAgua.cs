using System;
using System.Collections.Generic;
using System.IO;

namespace PipeMasterMEP;

public static class DebugAgua
{
    public static bool Ativo;

    public static string CaminhoAtivo;

    private static IEnumerable<string> Caminhos()
    {
        List<string> lista = new List<string>();
        try
        {
            lista.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PipeMasterM_AguaDebug.txt"));
        }
        catch
        {
        }
        try
        {
            lista.Add(Path.Combine(Path.GetTempPath(), "PipeMasterM_AguaDebug.txt"));
        }
        catch
        {
        }
        return lista;
    }

    public static void Iniciar()
    {
        if (!Ativo)
        {
            return;
        }
        CaminhoAtivo = null;
        string cab = "=== PipeMaster Água — diagnóstico " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===" + Environment.NewLine;
        foreach (string c in Caminhos())
        {
            try
            {
                File.WriteAllText(c, cab);
                if (CaminhoAtivo == null)
                {
                    CaminhoAtivo = c;
                }
            }
            catch
            {
            }
        }
    }

    public static void Log(string linha)
    {
        if (!Ativo)
        {
            return;
        }
        foreach (string c in Caminhos())
        {
            try
            {
                File.AppendAllText(c, linha + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
