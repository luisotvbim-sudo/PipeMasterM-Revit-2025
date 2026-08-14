using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class MepLinearFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem is MEPCurve;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
