using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class PecaAguaBruta
{
    public FamilyInstance Instancia { get; set; }

    public Transform Transformacao { get; set; }

    public bool IsDoVinculo { get; set; }
}
