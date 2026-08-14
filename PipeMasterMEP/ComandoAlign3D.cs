using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoAlign3D : IExternalCommand
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
            FiltroAlign3D filtro = new FiltroAlign3D();
            Reference refAlvo = uidoc.Selection.PickObject(ObjectType.Element, filtro, "PipeMaster [M]: Selecione o Tubo ou Conexão de REFERÊNCIA (Eixo mestre)...");
            Element elemAlvo = doc.GetElement(refAlvo);
            Reference refMovel = uidoc.Selection.PickObject(ObjectType.Element, filtro, "PipeMaster [M]: Selecione o Tubo ou Conexão que será ALINHADO in 3D...");
            Element elemMovel = doc.GetElement(refMovel);
            if (elemAlvo == null || elemMovel == null || elemAlvo.Id == elemMovel.Id)
            {
                return Result.Cancelled;
            }
            XYZ ptRefTras = null;
            XYZ ptRefFrente = null;
            XYZ direcaoAlvo = null;
            if (elemAlvo is MEPCurve { Location: LocationCurve { Curve: Line linhaAlvo } })
            {
                ptRefTras = linhaAlvo.GetEndPoint(0);
                ptRefFrente = linhaAlvo.GetEndPoint(1);
                direcaoAlvo = linhaAlvo.Direction;
            }
            else if (elemAlvo is FamilyInstance { MEPModel: not null } pecaAlvo)
            {
                XYZ clickUtilizador = refAlvo.GlobalPoint;
                Connector conectorMaisProximo = null;
                double menorDistancia = double.MaxValue;
                foreach (Connector c in pecaAlvo.MEPModel.ConnectorManager.Connectors)
                {
                    double dist = c.Origin.DistanceTo(clickUtilizador);
                    if (dist < menorDistancia)
                    {
                        menorDistancia = dist;
                        conectorMaisProximo = c;
                    }
                }
                if (conectorMaisProximo != null)
                {
                    ptRefTras = conectorMaisProximo.Origin;
                    ptRefFrente = conectorMaisProximo.Origin;
                    direcaoAlvo = conectorMaisProximo.CoordinateSystem.BasisZ;
                }
            }
            if (ptRefFrente == null || direcaoAlvo == null)
            {
                TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f Não foi possível extrair um eixo de referência válido.");
                return Result.Failed;
            }
            using (Transaction t = new Transaction(doc, "PipeMaster: Align 3D Real"))
            {
                t.Start();
                if (elemMovel is MEPCurve { Location: LocationCurve { Curve: Line linhaMovel } locMovel })
                {
                    XYZ p0 = linhaMovel.GetEndPoint(0);
                    XYZ p1 = linhaMovel.GetEndPoint(1);
                    double comprimentoOriginal = linhaMovel.Length;
                    XYZ centroMovel = (p0 + p1) / 2.0;
                    double distCentro = (centroMovel - ptRefFrente).DotProduct(direcaoAlvo);
                    XYZ centroProjetado = ptRefFrente + direcaoAlvo.Multiply(distCentro);
                    double sinal = (((p1 - p0).DotProduct(direcaoAlvo) >= 0.0) ? 1.0 : (-1.0));
                    XYZ p0Final = centroProjetado - direcaoAlvo.Multiply(comprimentoOriginal / 2.0 * sinal);
                    XYZ p1Final = centroProjetado + direcaoAlvo.Multiply(comprimentoOriginal / 2.0 * sinal);
                    if (!(p0Final.DistanceTo(p1Final) > 0.05))
                    {
                        TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f Erro crítico na reconstrução do vetor do tubo.");
                        t.RollBack();
                        return Result.Failed;
                    }
                    locMovel.Curve = Line.CreateBound(p0Final, p1Final);
                }
                else
                {
                    if (!(elemMovel is FamilyInstance { Location: LocationPoint { Point: var ptAtual } } pecaMovel))
                    {
                        TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f O elemento selecionado não possui geometria compatível.");
                        t.RollBack();
                        return Result.Failed;
                    }
                    double distancia = (ptAtual - ptRefFrente).DotProduct(direcaoAlvo);
                    XYZ projecaoNaLinha = ptRefFrente + direcaoAlvo.Multiply(distancia);
                    XYZ vetorDeMovimento = projecaoNaLinha - ptAtual;
                    if (vetorDeMovimento.GetLength() > 1E-05)
                    {
                        ElementTransformUtils.MoveElement(doc, pecaMovel.Id, vetorDeMovimento);
                    }
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
}
