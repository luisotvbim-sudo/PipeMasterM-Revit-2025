using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoMoveAndConnect : IExternalCommand
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
            MepSelectionFilter filtroMep = new MepSelectionFilter();
            Reference refAlvo = uidoc.Selection.PickObject(ObjectType.Element, filtroMep, "Fator FA [1/2]: Selecione o elemento ALVO (Que ficará PARADO). Pressione ESC para sair.");
            Element elAlvo = doc.GetElement(refAlvo);
            Reference refMovel = uidoc.Selection.PickObject(ObjectType.Element, filtroMep, "Fator FA [2/2]: Selecione o elemento que vai se MOVER e conectar ao alvo.");
            Element elMovel = doc.GetElement(refMovel);
            ConnectorSet conectoresMovel = ObterConectores(elMovel);
            ConnectorSet conectoresAlvo = ObterConectores(elAlvo);
            if (conectoresMovel == null || conectoresMovel.Size == 0 || conectoresAlvo == null || conectoresAlvo.Size == 0)
            {
                TaskDialog.Show("Move and Connect", "⚠\ufe0f Um dos elementos selecionados não possui conectores MEP válidos.");
                return Result.Failed;
            }
            Connector conectorMovelIdeal = null;
            Connector conectorAlvoIdeal = null;
            double menorDistancia = double.MaxValue;
            foreach (Connector cMovel in conectoresMovel)
            {
                if (cMovel.IsConnected)
                {
                    continue;
                }
                foreach (Connector cAlvo in conectoresAlvo)
                {
                    if (!cAlvo.IsConnected)
                    {
                        double distancia = cMovel.Origin.DistanceTo(cAlvo.Origin);
                        if (distancia < menorDistancia)
                        {
                            menorDistancia = distancia;
                            conectorMovelIdeal = cMovel;
                            conectorAlvoIdeal = cAlvo;
                        }
                    }
                }
            }
            if (conectorMovelIdeal == null || conectorAlvoIdeal == null)
            {
                TaskDialog.Show("Move and Connect", "⚠\ufe0f Não foram encontrados conectores livres para realizar a união.");
                return Result.Failed;
            }
            using (Transaction t = new Transaction(doc, "Move and Connect - Fator FA"))
            {
                t.Start();
                XYZ vetorDeslocamento = conectorAlvoIdeal.Origin - conectorMovelIdeal.Origin;
                ElementTransformUtils.MoveElement(doc, elMovel.Id, vetorDeslocamento);
                try
                {
                    conectorMovelIdeal.ConnectTo(conectorAlvoIdeal);
                }
                catch
                {
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
            TaskDialog.Show("Erro", "Ops! Algo deu errado: " + ex2.Message);
            return Result.Failed;
        }
    }

    private ConnectorSet ObterConectores(Element el)
    {
        if (el is MEPCurve tuboDuto)
        {
            return tuboDuto.ConnectorManager.Connectors;
        }
        if (el is FamilyInstance { MEPModel: not null } familiaBase && familiaBase.MEPModel.ConnectorManager != null)
        {
            return familiaBase.MEPModel.ConnectorManager.Connectors;
        }
        return null;
    }
}
