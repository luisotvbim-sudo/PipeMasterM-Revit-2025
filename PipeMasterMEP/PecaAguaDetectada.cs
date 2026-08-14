using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class PecaAguaDetectada
{
    public FamilyInstance Instancia { get; set; }

    public string Nome { get; set; }

    public bool RequerAguaFria { get; set; }

    public bool RequerAguaQuente { get; set; }

    public XYZ Posicao { get; set; }

    public bool IsDoVinculo { get; set; }
}
