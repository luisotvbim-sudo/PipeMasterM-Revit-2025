using System.Collections.Generic;

namespace PipeMasterMEP;

public class ConfiguracaoArquivo
{
    public bool NivelarTampaCaixas { get; set; } = true;

    public Dictionary<string, Dictionary<string, double>> RegrasPorSistema { get; set; } = new Dictionary<string, Dictionary<string, double>>();
}
