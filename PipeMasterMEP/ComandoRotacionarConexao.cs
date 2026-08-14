using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoRotacionarConexao : IExternalCommand
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
            Reference refPeca = uidoc.Selection.PickObject(ObjectType.Element, new FiltroConexoes(), "Selecione a conexão...");
            FamilyInstance conexao = doc.GetElement(refPeca) as FamilyInstance;
            JanelaRotacaoWPF janela = new JanelaRotacaoWPF();
            janela.ShowDialog();
            if (!janela.Confirmado)
            {
                return Result.Cancelled;
            }
            double anguloRadianos = janela.AnguloEscolhido * -1.0 * (Math.PI / 180.0);
            Connector pivotConnector = null;
            if (conexao.MEPModel != null && conexao.MEPModel.ConnectorManager != null)
            {
                List<Connector> connectors = (from Connector c in conexao.MEPModel.ConnectorManager.Connectors
                                              where c.ConnectorType == ConnectorType.End
                                              select c).ToList();
                List<Connector> connectedConnectors = connectors.Where((Connector c) => c.IsConnected).ToList();
                if (connectedConnectors.Count == 1)
                {
                    pivotConnector = connectedConnectors.First();
                }
                else if (connectors.Count > 0)
                {
                    XYZ clickPt = refPeca.GlobalPoint;
                    pivotConnector = connectors.OrderByDescending((Connector c) => c.Origin.DistanceTo(clickPt)).First();
                }
            }
            Line eixo;
            if (pivotConnector != null)
            {
                eixo = Line.CreateUnbound(pivotConnector.Origin, pivotConnector.CoordinateSystem.BasisZ);
            }
            else
            {
                LocationPoint locPoint = conexao.Location as LocationPoint;
                eixo = Line.CreateUnbound(locPoint.Point, XYZ.BasisZ);
            }
            using (Transaction t = new Transaction(doc, "Rotacionar Conexão"))
            {
                t.Start();
                ElementTransformUtils.RotateElement(doc, conexao.Id, eixo, anguloRadianos);
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
            TaskDialog.Show("Erro PipeMaster", ex2.Message);
            return Result.Failed;
        }
    }
}
