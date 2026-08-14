using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class MapeamentoAparelhosViewModel : INotifyPropertyChanged
{
    private static readonly string _caminhoJson = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PipeMasterMEP", "mapeamento_aparelhos.json");

    private bool _salvarParaProximosProjetos = true;

    public ObservableCollection<ItemMapeamento> Itens { get; } = new ObservableCollection<ItemMapeamento>();

    public bool SalvarParaProximosProjetos
    {
        get
        {
            return _salvarParaProximosProjetos;
        }
        set
        {
            _salvarParaProximosProjetos = value;
            Notify("SalvarParaProximosProjetos");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void Notify([CallerMemberName] string p = "")
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public MapeamentoAparelhosViewModel(Document doc, List<FamiliaVinculoInfo> familiasVinculo)
    {
        ElementId idGenerico = new ElementId(BuiltInCategory.OST_GenericModel);
        List<string> familiasProjetoPH = (from n in (from FamilySymbol s in new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_PlumbingFixtures,
                    BuiltInCategory.OST_SpecialityEquipment,
                    BuiltInCategory.OST_GenericModel
                })).OfClass(typeof(FamilySymbol))
                                                     where s.Category == null || s.Category.Id != idGenerico || PareceAparelho(s.FamilyName + " " + s.Name)
                                                     select s.FamilyName).Distinct<string>(StringComparer.OrdinalIgnoreCase)
                                          orderby n
                                          select n).ToList();
        Dictionary<string, string> mapeamentoSalvo = CarregarMapeamentoJson();
        foreach (FamiliaVinculoInfo f in from familiaVinculoInfo in familiasVinculo ?? new List<FamiliaVinculoInfo>()
                                         orderby familiaVinculoInfo.TipoIdentificado, familiaVinculoInfo.NomeFamilia
                                         select familiaVinculoInfo)
        {
            string famSalva = null;
            mapeamentoSalvo.TryGetValue(f.NomeFamilia, out famSalva);
            string melhor = famSalva;
            int confianca = ((famSalva != null) ? 100 : 0);
            if (melhor == null)
            {
                melhor = AutoMatch(f.NomeFamilia + " " + f.TipoIdentificado, familiasProjetoPH, out confianca);
            }
            Itens.Add(new ItemMapeamento
            {
                NomeFamiliaVinculo = f.NomeFamilia,
                TipoIdentificado = (string.IsNullOrEmpty(f.TipoIdentificado) ? "Outro" : f.TipoIdentificado),
                Quantidade = f.Quantidade,
                FamiliasProjetoDisponiveis = new List<string> { "-- não importar --" }.Concat(familiasProjetoPH).ToList(),
                FamiliaSelecionada = ((confianca >= 60) ? melhor : "-- não importar --"),
                Confianca = confianca,
                Incluir = (confianca >= 60)
            });
        }
    }

    private static string AutoMatch(string nomeVinculo, List<string> candidatos, out int melhorPontuacao)
    {
        Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        dictionary["bacia"] = new string[6] { "bacia", "vaso", "sanitário", "sanitaria", "wc", "toilet" };
        dictionary["lavatório"] = new string[6] { "lavatório", "lavatorio", "pia", "cuba", "sink", "wash" };
        dictionary["chuveiro"] = new string[3] { "chuveiro", "ducha", "shower" };
        dictionary["tanque"] = new string[4] { "tanque", "lavanderia", "laundry", "tub" };
        dictionary["mictório"] = new string[3] { "mictório", "mictorio", "urinal" };
        dictionary["torneira"] = new string[5] { "torneira", "faucet", "registro", "válvula", "valvula" };
        Dictionary<string, string[]> categorias = dictionary;
        string nomeL = Normalizar(nomeVinculo);
        char[] seps = new char[7] { ' ', '-', '_', '(', ')', '.', ',' };
        HashSet<string> tokensVinculo = nomeL.Split(seps, StringSplitOptions.RemoveEmptyEntries).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
        string categoriaVinculo = null;
        foreach (KeyValuePair<string, string[]> kv in categorias)
        {
            if (kv.Value.Any((string s) => nomeL.Contains(s)))
            {
                categoriaVinculo = kv.Key;
                break;
            }
        }
        string melhor = null;
        melhorPontuacao = 0;
        foreach (string cand in candidatos)
        {
            string candL = Normalizar(cand);
            HashSet<string> tokensCand = candL.Split(seps, StringSplitOptions.RemoveEmptyEntries).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
            int pts = 0;
            pts += tokensVinculo.Intersect(tokensCand).Count() * 10;
            if (categoriaVinculo != null && categorias.TryGetValue(categoriaVinculo, out var sinonimos) && sinonimos.Any((string s) => candL.Contains(s)))
            {
                pts += 25;
            }
            MatchCollection numVinculo = Regex.Matches(nomeL, "\\d+");
            MatchCollection numCand = Regex.Matches(candL, "\\d+");
            HashSet<string> setNum = new HashSet<string>(from Match m in numVinculo
                                                         select m.Value);
            pts += numCand.Cast<Match>().Count((Match m) => setNum.Contains(m.Value)) * 5;
            int normalizado = Math.Min(pts * 100 / Math.Max(pts + 20, 40), 100);
            if (normalizado > melhorPontuacao)
            {
                melhorPontuacao = normalizado;
                melhor = cand;
            }
        }
        return melhor;
    }

    private static bool PareceAparelho(string nome)
    {
        string n = Normalizar(nome);
        string[] termos = new string[20]
        {
            "vaso", "bacia", "sanit", "mict", "lavat", "cuba", "pia", "chuveiro", "ducha", "torneira",
            "misturador", "tanque", "maquina", "máquina", "bebedouro", "filtro", "valvula", "válvula", "descarga", "higien"
        };
        string[] array = termos;
        foreach (string t in array)
        {
            if (n.Contains(Normalizar(t)))
            {
                return true;
            }
        }
        return false;
    }

    private static string Normalizar(string s)
    {
        return s?.ToLower().Replace("ã", "a").Replace("â", "a")
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("ú", "u")
            .Replace("ç", "c")
            .Replace("ñ", "n") ?? "";
    }

    public void SalvarMapeamento()
    {
        if (!SalvarParaProximosProjetos)
        {
            return;
        }
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"mapeamentos\": [");
            IEnumerable<string> linhas = from i in Itens
                                         where i.FamiliaSelecionada != null && i.FamiliaSelecionada != "-- não importar --"
                                         select $"    {{ \"vinculo\": {Esc(i.NomeFamiliaVinculo)}, \"projeto\": {Esc(i.FamiliaSelecionada)} }}";
            sb.AppendLine(string.Join(",\n", linhas));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            string dir = Path.GetDirectoryName(_caminhoJson);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_caminhoJson, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static string Esc(string s)
    {
        return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static Dictionary<string, string> CarregarMapeamentoJson()
    {
        Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(_caminhoJson))
            {
                return d;
            }
            string txt = File.ReadAllText(_caminhoJson, Encoding.UTF8);
            MatchCollection matches = Regex.Matches(txt, "\\\"vinculo\\\"\\s*:\\s*\\\"([^\"]*)\\\",\\s*\\\"projeto\\\"\\s*:\\s*\\\"([^\"]*)\"");
            foreach (Match m in matches)
            {
                d[m.Groups[1].Value] = m.Groups[2].Value;
            }
        }
        catch
        {
        }
        return d;
    }
}
