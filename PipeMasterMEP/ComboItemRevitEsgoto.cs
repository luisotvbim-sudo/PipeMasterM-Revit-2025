using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class ComboItemRevitEsgoto
{
    public string Nome { get; set; }

    public ElementId Id { get; set; }

    public override string ToString()
    {
        return Nome;
    }
}
