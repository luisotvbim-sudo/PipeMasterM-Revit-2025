using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class LinhaComDNA
{
    public CurveElement ElementoRevit;

    public double DiametroMm;

    public double Inclinacao;

    public bool IsVaso;

    public bool IsVentilacao;

    public ElementId SistemaId;

    public ElementId TipoTuboId;
}
