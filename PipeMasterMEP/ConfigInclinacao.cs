using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ConfigInclinacao : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        Document doc = commandData.Application.ActiveUIDocument.Document;
        MemoriaInclinacao.Carregar();
        JanelaConfigInclinacao janela = new JanelaConfigInclinacao(doc);
        janela.ShowDialog();
        return Result.Succeeded;
    }
}
