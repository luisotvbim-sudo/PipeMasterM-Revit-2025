using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoMoverAlinharConectar : IExternalCommand
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
            Reference refDestino = uidoc.Selection.PickObject(ObjectType.Element, new FiltroElementosMEP(), "PipeMaster [M]: Clique no elemento de DESTINO (ficará parado e ditará a inclinação)...");
            Element elemDestino = doc.GetElement(refDestino);
            Reference refMovel = uidoc.Selection.PickObject(ObjectType.Element, new FiltroElementosMEP(), "PipeMaster [M]: Clique no elemento que vai MOVER e ALINHAR...");
            Element elemMovel = doc.GetElement(refMovel);
            if (elemDestino.Id == elemMovel.Id)
            {
                TaskDialog.Show("PipeMaster [M]", "Selecione elementos diferentes.");
                return Result.Cancelled;
            }
            ConnectorSet conectoresDestino = ObterConectores(elemDestino);
            ConnectorSet conectoresMovel = ObterConectores(elemMovel);
            if (conectoresDestino == null || conectoresMovel == null)
            {
                return Result.Failed;
            }
            Connector conectorDestinoAlvo = null;
            Connector conectorMovelAlvo = null;
            double menorDistancia = double.MaxValue;
            foreach (Connector cDest in conectoresDestino)
            {
                if (cDest.IsConnected)
                {
                    continue;
                }
                foreach (Connector cMov in conectoresMovel)
                {
                    if (!cMov.IsConnected)
                    {
                        double dist = cDest.Origin.DistanceTo(cMov.Origin);
                        if (dist < menorDistancia)
                        {
                            menorDistancia = dist;
                            conectorDestinoAlvo = cDest;
                            conectorMovelAlvo = cMov;
                        }
                    }
                }
            }
            if (conectorDestinoAlvo == null || conectorMovelAlvo == null)
            {
                TaskDialog.Show("PipeMaster [M]", "Não foram encontradas pontas soltas para conectar.");
                return Result.Cancelled;
            }
            XYZ vetorMovimento = conectorDestinoAlvo.Origin - conectorMovelAlvo.Origin;
            using (Transaction t = new Transaction(doc, "PipeMaster: Mover, Alinhar e Conectar"))
            {
                t.Start();
                ElementTransformUtils.MoveElement(doc, elemMovel.Id, vetorMovimento);
                Connector conectorMovelAtualizado = null;
                foreach (Connector cMovAtualizado in ObterConectores(elemMovel))
                {
                    if (cMovAtualizado.Origin.DistanceTo(conectorDestinoAlvo.Origin) < 0.01)
                    {
                        conectorMovelAtualizado = cMovAtualizado;
                        break;
                    }
                }
                if (conectorMovelAtualizado != null)
                {
                    XYZ vDestino = conectorDestinoAlvo.CoordinateSystem.BasisZ;
                    XYZ vMovel = conectorMovelAtualizado.CoordinateSystem.BasisZ;
                    XYZ vAlvo = -vDestino;
                    double angulo = vMovel.AngleTo(vAlvo);
                    if (angulo > 0.0001)
                    {
                        XYZ eixoRotacaoDir = vMovel.CrossProduct(vAlvo);
                        if (eixoRotacaoDir.GetLength() > 0.0001)
                        {
                            Line eixoRotacao = Line.CreateUnbound(conectorMovelAtualizado.Origin, eixoRotacaoDir);
                            ElementTransformUtils.RotateElement(doc, elemMovel.Id, eixoRotacao, angulo);
                            foreach (Connector cMovRotacionado in ObterConectores(elemMovel))
                            {
                                if (cMovRotacionado.Origin.DistanceTo(conectorDestinoAlvo.Origin) < 0.01)
                                {
                                    conectorMovelAtualizado = cMovRotacionado;
                                    break;
                                }
                            }
                        }
                    }
                    conectorDestinoAlvo.ConnectTo(conectorMovelAtualizado);
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
