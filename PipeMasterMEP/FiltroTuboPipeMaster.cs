using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroTuboPipeMaster : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem.Category == null)
        {
            return false;
        }
        long catId = elem.Category.Id.Value;
        return catId == -2008044;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return true;
    }
}
