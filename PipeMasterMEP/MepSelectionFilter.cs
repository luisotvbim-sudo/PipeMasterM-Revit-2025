using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class MepSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem is MEPCurve)
        {
            return true;
        }
        if (elem is FamilyInstance { MEPModel: not null })
        {
            return true;
        }
        return false;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
