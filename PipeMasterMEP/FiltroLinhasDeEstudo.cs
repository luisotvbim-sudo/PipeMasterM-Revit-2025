using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroLinhasDeEstudo : ISelectionFilter
{
    private HashSet<ElementId> _ignorados;

    public FiltroLinhasDeEstudo(HashSet<ElementId> ignorados = null)
    {
        _ignorados = ignorados ?? new HashSet<ElementId>();
    }

    public bool AllowElement(Element elem)
    {
        if (!(elem is DetailLine) && !(elem is ModelLine))
        {
            return false;
        }
        return !_ignorados.Contains(elem.Id);
    }

    public bool AllowReference(Reference r, XYZ p)
    {
        return false;
    }
}
