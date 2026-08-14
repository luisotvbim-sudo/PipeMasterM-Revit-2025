using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class ConfigRoteamentoAgua
{
    public ElementId SistemaId { get; set; }

    public ElementId TipoTuboId { get; set; }

    public ElementId LevelId { get; set; }

    public double ZRamal { get; set; }

    public double ZTopoPrumada { get; set; }

    public double ZRegistro { get; set; }

    public bool InserirRegistro { get; set; }

    public ElementId RegistroSimboloId { get; set; }

    public bool InserirRegistroPressao { get; set; }

    public ElementId RegistroPressaoSimboloId { get; set; }

    public double ZRegistroPressao { get; set; }

    public double DiametroRamalPes { get; set; }

    public double DiametroDescidaPes { get; set; }

    public double RecuoParedePes { get; set; }

    public string NomeNivel { get; set; }

    public bool DesviarPeloPiso { get; set; }

    public double ZPiso { get; set; }

    public XYZ PontoSubidaPiso { get; set; }

    public bool InverterSentidoBucha { get; set; }
}
