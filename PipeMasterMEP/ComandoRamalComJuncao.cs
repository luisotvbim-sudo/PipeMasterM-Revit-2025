using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoRamalComJuncao : IExternalCommand
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
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("Pipe Master", "Este comando só pode ser executado em projetos (.rvt).");
                return Result.Failed;
            }
            Reference refCol = uidoc.Selection.PickObject(ObjectType.Element, new FiltroApenasTubos(), "Pipe Master — Selecione o COLETOR principal.");
            if (!(doc.GetElement(refCol) is Pipe tuboColetor))
            {
                return Result.Cancelled;
            }
            XYZ pontoClicado = uidoc.Selection.PickPoint("Pipe Master — Clique no LADO desejado para nascer o ramal.");
            Line lCol = (tuboColetor.Location as LocationCurve).Curve as Line;
            if (lCol == null)
            {
                TaskDialog.Show("Erro", "O coletor selecionado não é um tubo linear.");
                return Result.Failed;
            }
            XYZ vCol = VetorFluxo(lCol);
            XYZ pWye = ProjetarNaReta(pontoClicado, lCol);
            IntersectionResult snap = lCol.Project(pWye);
            if (snap == null)
            {
                return Result.Failed;
            }
            pWye = snap.XYZPoint;
            double distBorda0 = pWye.DistanceTo(lCol.GetEndPoint(0));
            double distBorda1 = pWye.DistanceTo(lCol.GetEndPoint(1));
            if (distBorda0 < 0.25 || distBorda1 < 0.25)
            {
                TaskDialog.Show("Pipe Master", "Clique mais afastado das pontas do tubo para ter espaço físico.");
                return Result.Failed;
            }
            XYZ Z = XYZ.BasisZ;
            XYZ vPerp = vCol.CrossProduct(Z).Normalize();
            if (vPerp.DotProduct(pontoClicado - pWye) < 0.0)
            {
                vPerp = -vPerp;
            }
            using (Transaction t = new Transaction(doc, "PipeMaster: Ramal Padrão"))
            {
                t.Start();
                ElementId sysTypeId = tuboColetor.MEPSystem?.GetTypeId();
                ElementId pipeTypeId = tuboColetor.GetTypeId();
                ElementId levelId = tuboColetor.LevelId;
                double diamCol = ((Element)tuboColetor).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                double diamRamal = UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters);
                XYZ pStubFim = pWye + vPerp * 0.9842519685039369;
                Pipe stub = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, pWye, pStubFim);
                ((Element)stub).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCol);
                doc.Regenerate();
                ElementId idJusante = PlumbingUtils.BreakCurve(doc, tuboColetor.Id, pWye);
                Pipe coletorJus = doc.GetElement(idJusante) as Pipe;
                doc.Regenerate();
                FamilyInstance wye = doc.Create.NewTeeFitting(ConectorMaisProximo(tuboColetor, pWye), ConectorMaisProximo(coletorJus, pWye), ConectorMaisProximo(stub, pWye));
                doc.Regenerate();
                doc.Delete(stub.Id);
                SetarParametroAngulo(wye, 45.0);
                doc.Regenerate();
                double anguloRad = Math.PI * 3.0 / 8.0;
                XYZ rot1 = Transform.CreateRotation(vCol, anguloRad).OfVector(vPerp);
                XYZ rot2 = Transform.CreateRotation(vCol, 0.0 - anguloRad).OfVector(vPerp);
                double anguloFinal = ((rot1.Z > rot2.Z) ? anguloRad : (0.0 - anguloRad));
                ElementTransformUtils.RotateElement(doc, wye.Id, Line.CreateUnbound(pWye, vCol), anguloFinal);
                doc.Regenerate();
                Connector connLivreWye = ObterConectorDerivacaoDoY(wye, vCol);
                if (connLivreWye == null)
                {
                    t.RollBack();
                    return Result.Failed;
                }
                XYZ pWyeOut = connLivreWye.Origin;
                XYZ dirWye = connLivreWye.CoordinateSystem.BasisZ;
                double compChicoteInterno = 0.49212598425196846;
                XYZ pChicoteFim = pWyeOut + dirWye * compChicoteInterno;
                Pipe chicote = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, pWyeOut, pChicoteFim);
                ((Element)chicote).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamal);
                doc.Regenerate();
                ConectorMaisProximo(chicote, pWyeOut).ConnectTo(connLivreWye);
                doc.Regenerate();
                XYZ vDiagonal2D = (-vCol + vPerp).Normalize();
                XYZ dirRamal = new XYZ(vDiagonal2D.X, vDiagonal2D.Y, 0.02).Normalize();
                double compRamalInterno = 1.6404199475065615;
                XYZ pRamalLonge = pChicoteFim + dirRamal * compRamalInterno;
                Pipe ramal = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, pRamalLonge, pChicoteFim);
                ((Element)ramal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamal);
                doc.Regenerate();
                Connector cChicoteFim = ConectorMaisProximo(chicote, pChicoteFim);
                Connector cRamInicio = ConectorMaisProximo(ramal, pChicoteFim);
                FamilyInstance joelho = doc.Create.NewElbowFitting(cChicoteFim, cRamInicio);
                doc.Regenerate();
                if (joelho != null)
                {
                    SetarParametroBool(joelho, "Inverter Sentido da Luva", valor: true);
                    doc.Regenerate();
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
            TaskDialog.Show("PipeMaster – Erro", "Erro: " + ex2.Message);
            return Result.Failed;
        }
    }

    private Connector ObterConectorDerivacaoDoY(FamilyInstance wye, XYZ vCol)
    {
        if (wye?.MEPModel?.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in wye.MEPModel.ConnectorManager.Connectors)
        {
            if (c.ConnectorType == ConnectorType.End)
            {
                XYZ cDir = c.CoordinateSystem.BasisZ.Normalize();
                if (Math.Abs(cDir.DotProduct(vCol)) < 0.9)
                {
                    return c;
                }
            }
        }
        return null;
    }

    private void SetarParametroBool(FamilyInstance fitting, string nomeParam, bool valor)
    {
        if (fitting != null)
        {
            Parameter p = fitting.LookupParameter(nomeParam);
            if (p != null && !p.IsReadOnly)
            {
                p.Set(valor ? 1 : 0);
            }
        }
    }

    private void SetarParametroAngulo(FamilyInstance fitting, double graus)
    {
        if (fitting == null)
        {
            return;
        }
        double rad = graus * Math.PI / 180.0;
        string[] nomes = new string[7] { "Ângulo 1", "Angulo 1", "Angle 1", "Angle", "Branch Angle", "Ângulo", "Angulo" };
        string[] array = nomes;
        foreach (string nome in array)
        {
            Parameter p = fitting.LookupParameter(nome);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
            {
                try
                {
                    p.Set(rad);
                    break;
                }
                catch
                {
                }
            }
        }
    }

    private Connector ConectorMaisProximo(Pipe p, XYZ pRef)
    {
        if (p?.ConnectorManager == null)
        {
            return null;
        }
        return (from Connector c in p.ConnectorManager.Connectors
                where c.ConnectorType == ConnectorType.End
                orderby c.Origin.DistanceTo(pRef)
                select c).FirstOrDefault();
    }

    private XYZ ProjetarNaReta(XYZ p, Line l)
    {
        XYZ o = l.GetEndPoint(0);
        XYZ d = l.Direction.Normalize();
        return o + d * (p - o).DotProduct(d);
    }

    private XYZ VetorFluxo(Line linha)
    {
        XYZ p0 = linha.GetEndPoint(0);
        XYZ p1 = linha.GetEndPoint(1);
        double dZ = p1.Z - p0.Z;
        if (Math.Abs(dZ) > 0.001)
        {
            return (dZ < 0.0) ? (p1 - p0).Normalize() : (p0 - p1).Normalize();
        }
        return linha.Direction.Normalize();
    }
}
