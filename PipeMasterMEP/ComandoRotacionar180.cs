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
public class ComandoRotacionar180 : IExternalCommand
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
            Reference refPeca = uidoc.Selection.PickObject(ObjectType.Element, new FiltroConexoes(), "PipeMaster [M]: Selecione a conexão para rotacionar 180º...");
            if (!(doc.GetElement(refPeca) is FamilyInstance { MEPModel: not null } conexao))
            {
                return Result.Cancelled;
            }
            double anguloRadianos = Math.PI;
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
            Line eixoDeRotacao;
            if (pivotConnector != null)
            {
                eixoDeRotacao = Line.CreateUnbound(pivotConnector.Origin, pivotConnector.CoordinateSystem.BasisZ);
            }
            else
            {
                if (!(conexao.Location is LocationPoint locPoint))
                {
                    return Result.Failed;
                }
                eixoDeRotacao = Line.CreateUnbound(locPoint.Point, XYZ.BasisZ);
            }
            using (Transaction t = new Transaction(doc, "PipeMaster: Rotacionar 180º"))
            {
                t.Start();
                ElementTransformUtils.RotateElement(doc, conexao.Id, eixoDeRotacao, anguloRadianos);
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
}
