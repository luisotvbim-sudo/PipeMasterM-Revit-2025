using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoLinhasParaEsgoto : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (App.AppCarregado && !VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        EventoGerarRede handler = new EventoGerarRede();
        ExternalEvent exEvent = ExternalEvent.Create(handler);
        EventoPintarLinhas handlerPintar = new EventoPintarLinhas();
        ExternalEvent exEventPintar = ExternalEvent.Create(handlerPintar);
        JanelaLinhasEsgoto janela = new JanelaLinhasEsgoto(commandData.Application.ActiveUIDocument, commandData.Application.MainWindowHandle, handler, exEvent, handlerPintar, exEventPintar);
        janela.Show();
        return Result.Succeeded;
    }
}
