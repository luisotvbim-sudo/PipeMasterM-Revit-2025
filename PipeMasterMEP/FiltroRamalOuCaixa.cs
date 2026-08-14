using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroRamalOuCaixa : ISelectionFilter
{
    public bool AllowElement(Element e)
    {
        if (e is Pipe)
        {
            return true;
        }
        if (e is FamilyInstance)
        {
            return true;
        }
        return false;
    }

    public bool AllowReference(Reference r, XYZ p)
    {
        return false;
    }
}
