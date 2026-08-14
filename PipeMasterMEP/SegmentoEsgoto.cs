using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class SegmentoEsgoto
{
    public XYZ A;

    public XYZ B;

    public double Diametro;

    public double Inclinacao;

    public bool IsVaso;

    public bool IsVentilacao;

    public ElementId SistemaId;

    public ElementId TipoTuboId;
}
