using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class PontoConsumoAgua
{
    public XYZ Posicao { get; set; }

    public double ZPonto { get; set; }

    public string Nome { get; set; }

    public double OffsetLateralPes { get; set; }

    public bool EhChuveiro { get; set; }
}
