using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoDesconectar : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        try
        {
            Reference ref1 = uidoc.Selection.PickObject(ObjectType.Element, new FiltroElementosMEP(), "PipeMaster [M]: Selecione o PRIMEIRO elemento...");
            Element elem1 = doc.GetElement(ref1);
            Reference ref2 = uidoc.Selection.PickObject(ObjectType.Element, new FiltroElementosMEP(), "PipeMaster [M]: Selecione o SEGUNDO elemento para desconectar...");
            Element elem2 = doc.GetElement(ref2);
            if (elem1.Id == elem2.Id)
            {
                TaskDialog.Show("PipeMaster [M]", "Você selecionou o mesmo elemento duas vezes. Selecione dois elementos diferentes.");
                return Result.Cancelled;
            }
            ConnectorSet conectores1 = ObterConectores(elem1);
            ConnectorSet conectores2 = ObterConectores(elem2);
            if (conectores1 == null || conectores2 == null)
            {
                return Result.Failed;
            }
            bool desconectou = false;
            using (Transaction t = new Transaction(doc, "PipeMaster: Desconectar"))
            {
                t.Start();
                foreach (Connector c1 in conectores1)
                {
                    if (!c1.IsConnected)
                    {
                        continue;
                    }
                    foreach (Connector cConectado in c1.AllRefs)
                    {
                        if (cConectado.Owner.Id == elem2.Id && cConectado.ConnectorType != ConnectorType.Logical)
                        {
                            c1.DisconnectFrom(cConectado);
                            desconectou = true;
                        }
                    }
                }
                if (!desconectou)
                {
                    t.RollBack();
                    TaskDialog.Show("PipeMaster [M]", "Estes dois elementos não estão conectados um ao outro.");
                    return Result.Cancelled;
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex2)
        {
            TaskDialog.Show("PipeMaster [M] - Erro", ex2.Message);
            return Result.Failed;
        }
    }

    private ConnectorSet ObterConectores(Element elem)
    {
        if (elem is MEPCurve curva)
        {
            return curva.ConnectorManager.Connectors;
        }
        if (elem is FamilyInstance { MEPModel: not null } fi)
        {
            return fi.MEPModel.ConnectorManager.Connectors;
        }
        return null;
    }
}
