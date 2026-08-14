using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroElementosMEP : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem.Category == null)
        {
            return false;
        }
        long catId = elem.Category.Id.Value;
        return catId == -2008044 || catId == -2008049 || catId == -2008055 || catId == -2001160;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
