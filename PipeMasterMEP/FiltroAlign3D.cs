using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroAlign3D : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem == null || elem.Category == null)
        {
            return false;
        }
        ElementId catId = elem.Category.Id;
        return catId == new ElementId(BuiltInCategory.OST_PipeCurves) || catId == new ElementId(BuiltInCategory.OST_PipeFitting) || catId == new ElementId(BuiltInCategory.OST_PipeAccessory) || catId == new ElementId(BuiltInCategory.OST_PlumbingFixtures);
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return true;
    }
}
