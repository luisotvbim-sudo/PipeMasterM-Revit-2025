using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FiltroCaixaSifonadaNova : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem.Category != null && (elem.Category.Id == new ElementId(BuiltInCategory.OST_PlumbingFixtures) || elem.Category.Id == new ElementId(BuiltInCategory.OST_PipeAccessory));
    }

    public bool AllowReference(Reference refC, XYZ pos)
    {
        return true;
    }
}
