using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

public class BloqueioDeLogin : IExternalCommandAvailability
{
    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return (TestMode.Enabled || SessaoUsuario.Autenticado) && applicationData.ActiveUIDocument != null && applicationData.ActiveUIDocument.Document != null;
    }
}
