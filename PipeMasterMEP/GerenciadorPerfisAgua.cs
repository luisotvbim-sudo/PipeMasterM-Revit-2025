using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PipeMasterMEP;

public static class GerenciadorPerfisAgua
{
    public static PerfilAgua PerfilAtual;

    private static string Pasta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeMasterMEP", "PerfisAgua");

    public static List<string> Listar()
    {
        try
        {
            if (!Directory.Exists(Pasta))
            {
                return new List<string>();
            }
            return (from n in Directory.GetFiles(Pasta, "*.txt").Select(Path.GetFileNameWithoutExtension)
                    orderby n
                    select n).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static PerfilAgua Carregar(string nome)
    {
        try
        {
            if (string.IsNullOrEmpty(nome))
            {
                return null;
            }
            string caminho = Path.Combine(Pasta, nome + ".txt");
            if (!File.Exists(caminho))
            {
                return null;
            }
            PerfilAgua p = new PerfilAgua
            {
                Nome = nome
            };
            string[] array = File.ReadAllLines(caminho);
            foreach (string l in array)
            {
                int idx = l.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }
                string chave = l.Substring(0, idx).Trim();
                string valor = l.Substring(idx + 1).Trim();
                if (chave == "InverterSentidoBucha" || chave == "DesviarPeloPiso")
                {
                    if (bool.TryParse(valor, out var bv))
                    {
                        if (chave == "InverterSentidoBucha")
                        {
                            p.InverterSentidoBucha = bv;
                        }
                        else
                        {
                            p.DesviarPeloPiso = bv;
                        }
                    }
                }
                else
                {
                    if (!double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        continue;
                    }
                    if (chave.StartsWith("Altura."))
                    {
                        p.Alturas[chave.Substring(7)] = d;
                        continue;
                    }
                    if (chave.StartsWith("Offset."))
                    {
                        p.Offsets[chave.Substring(7)] = d;
                        continue;
                    }
                    switch (chave)
                    {
                        case "AlturaPrumada":
                            p.AlturaPrumada = d;
                            break;
                        case "AlturaRegistro":
                            p.AlturaRegistro = d;
                            break;
                        case "AlturaRamal":
                            p.AlturaRamal = d;
                            break;
                        case "RecuoParedeCm":
                            p.RecuoParedeCm = d;
                            break;
                        case "DiametroRamal":
                            p.DiametroRamal = d;
                            break;
                        case "DiametroDescida":
                            p.DiametroDescida = d;
                            break;
                        case "AlturaRegistroPressao":
                            p.AlturaRegistroPressao = d;
                            break;
                        case "AlturaPiso":
                            p.AlturaPiso = d;
                            break;
                    }
                }
            }
            return p;
        }
        catch
        {
            return null;
        }
    }

    public static void Salvar(PerfilAgua p)
    {
        try
        {
            if (p == null || string.IsNullOrEmpty(p.Nome))
            {
                return;
            }
            if (!Directory.Exists(Pasta))
            {
                Directory.CreateDirectory(Pasta);
            }
            List<string> linhas = new List<string>();
            foreach (KeyValuePair<string, double> kv in p.Alturas)
            {
                linhas.Add("Altura." + kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            foreach (KeyValuePair<string, double> kv2 in p.Offsets)
            {
                linhas.Add("Offset." + kv2.Key + "=" + kv2.Value.ToString(CultureInfo.InvariantCulture));
            }
            linhas.Add("AlturaPrumada=" + p.AlturaPrumada.ToString(CultureInfo.InvariantCulture));
            linhas.Add("AlturaRegistro=" + p.AlturaRegistro.ToString(CultureInfo.InvariantCulture));
            linhas.Add("AlturaRamal=" + p.AlturaRamal.ToString(CultureInfo.InvariantCulture));
            linhas.Add("RecuoParedeCm=" + p.RecuoParedeCm.ToString(CultureInfo.InvariantCulture));
            linhas.Add("DiametroRamal=" + p.DiametroRamal.ToString(CultureInfo.InvariantCulture));
            linhas.Add("DiametroDescida=" + p.DiametroDescida.ToString(CultureInfo.InvariantCulture));
            linhas.Add("AlturaRegistroPressao=" + p.AlturaRegistroPressao.ToString(CultureInfo.InvariantCulture));
            linhas.Add("InverterSentidoBucha=" + p.InverterSentidoBucha);
            linhas.Add("DesviarPeloPiso=" + p.DesviarPeloPiso);
            linhas.Add("AlturaPiso=" + p.AlturaPiso.ToString(CultureInfo.InvariantCulture));
            File.WriteAllLines(Path.Combine(Pasta, p.Nome + ".txt"), linhas);
        }
        catch
        {
        }
    }

    public static void Excluir(string nome)
    {
        try
        {
            string caminho = Path.Combine(Pasta, nome + ".txt");
            if (File.Exists(caminho))
            {
                File.Delete(caminho);
            }
        }
        catch
        {
        }
    }

    public static string LimparNome(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return "";
        }
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidFileNameChars)
        {
            nome = nome.Replace(c.ToString(), "");
        }
        return nome.Trim();
    }
}
