using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroConexoes : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem.Category == null)
        {
            return false;
        }
        return elem.Category.Id.Value == -2008049;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
