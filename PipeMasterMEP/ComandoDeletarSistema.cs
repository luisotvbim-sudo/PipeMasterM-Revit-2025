using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoDeletarSistema : IExternalCommand
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
            Reference refElemento = uidoc.Selection.PickObject(ObjectType.Element, new FiltroElementosSistema(), "PipeMaster [M]: Selecione um tubo ou peça para DELETAR o sistema lógico atual...");
            Element elemSelecionado = doc.GetElement(refElemento);
            if (elemSelecionado == null)
            {
                return Result.Cancelled;
            }
            HashSet<ElementId> sistemasParaDeletar = new HashSet<ElementId>();
            if (elemSelecionado is MEPCurve tubo)
            {
                if (tubo.MEPSystem != null && tubo.MEPSystem.Id != ElementId.InvalidElementId)
                {
                    sistemasParaDeletar.Add(tubo.MEPSystem.Id);
                }
            }
            else if (elemSelecionado is FamilyInstance { MEPModel: not null } fi)
            {
                foreach (Connector conector in fi.MEPModel.ConnectorManager.Connectors)
                {
                    if (conector.MEPSystem != null && conector.MEPSystem.Id != ElementId.InvalidElementId)
                    {
                        sistemasParaDeletar.Add(conector.MEPSystem.Id);
                    }
                }
            }
            if (sistemasParaDeletar.Count == 0)
            {
                TaskDialog.Show("PipeMaster [M]", "O elemento selecionado já está livre (não pertence a nenhum sistema lógico).");
                return Result.Cancelled;
            }
            using (Transaction t = new Transaction(doc, "PipeMaster: Deletar Sistema"))
            {
                t.Start();
                int deletados = 0;
                foreach (ElementId sysId in sistemasParaDeletar)
                {
                    try
                    {
                        doc.Delete(sysId);
                        deletados++;
                    }
                    catch
                    {
                    }
                }
                if (deletados <= 0)
                {
                    t.RollBack();
                    TaskDialog.Show("PipeMaster [M]", "Não foi possível deletar o sistema deste elemento. Ele pode estar travado pelo Revit.");
                    return Result.Failed;
                }
                doc.Regenerate();
                ElementTransformUtils.MoveElement(doc, elemSelecionado.Id, XYZ.Zero);
                t.Commit();
                uidoc.RefreshActiveView();
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
}
