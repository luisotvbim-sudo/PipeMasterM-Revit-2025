using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoTomboColetor : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        UIRamalOptionsViewModel viewModel = new UIRamalOptionsViewModel();
        viewModel.AjustarTema(commandData.Application.Application.BackgroundColor);
        UIRamalOptionsBar optionsControl = new UIRamalOptionsBar
        {
            DataContext = viewModel
        };
        using TomboOptionsBarSession session = TomboOptionsBarSession.Begin(optionsControl);
        if (session == null)
        {
            TaskDialog.Show("PipeMaster [M]", "Aviso: A Options Bar não pôde ser acessada nesta versão do Revit.");
        }
        try
        {
            FiltroTuboTombo filtro = new FiltroTuboTombo();
            Reference refPrincipal = uidoc.Selection.PickObject(ObjectType.Element, filtro, "PipeMaster [M]: 1. Selecione o Tubo PRINCIPAL (Coletor abaixo)...");
            MEPCurve tuboPrincipal = doc.GetElement(refPrincipal) as MEPCurve;
            Reference refRamal = uidoc.Selection.PickObject(ObjectType.Element, filtro, "PipeMaster [M]: 2. Selecione o Tubo RAMAL (Tubo de cima)...");
            MEPCurve tuboRamal = doc.GetElement(refRamal) as MEPCurve;
            if (tuboPrincipal == null || tuboRamal == null || tuboPrincipal.Id == tuboRamal.Id)
            {
                return Result.Cancelled;
            }
            bool usarDuplo45 = viewModel.IsSuave;
            Line linhaPrin = (tuboPrincipal.Location as LocationCurve)?.Curve as Line;
            Line linhaRamal = (tuboRamal.Location as LocationCurve)?.Curve as Line;
            if (linhaPrin == null || linhaRamal == null)
            {
                TaskDialog.Show("PipeMaster [M]", "⚠\ufe0f Os elementos precisam ser segmentos retos.");
                return Result.Failed;
            }
            XYZ dirPrin2D = new XYZ(linhaPrin.Direction.X, linhaPrin.Direction.Y, 0.0).Normalize();
            XYZ dirRamal2D = new XYZ(linhaRamal.Direction.X, linhaRamal.Direction.Y, 0.0).Normalize();
            if (Math.Abs(dirPrin2D.DotProduct(dirRamal2D)) > 0.05)
            {
                TaskDialog.Show("PipeMaster [M]", "O Tubo superior não tem altura suficiente para conectar ao tubo inferior e/ou as tubulações não se encontram na perpendicular.");
                return Result.Failed;
            }
            using (Transaction trans = new Transaction(doc, "PipeMaster: Tombo Coletor"))
            {
                FailureHandlingOptions options = trans.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new SupressorAvisoTombo());
                trans.SetFailureHandlingOptions(options);
                trans.Start();
                XYZ ptInt2D = EncontrarIntersecao2D(linhaPrin.GetEndPoint(0), linhaPrin.GetEndPoint(1), linhaRamal.GetEndPoint(0), linhaRamal.GetEndPoint(1));
                XYZ ptTop3D;
                XYZ fixedEnd;
                if (ptInt2D != null)
                {
                    double zRamalVirtual = GetZAt2DPoint(linhaRamal, ptInt2D);
                    ptTop3D = new XYZ(ptInt2D.X, ptInt2D.Y, zRamalVirtual);
                    XYZ pR1 = linhaRamal.GetEndPoint(0);
                    XYZ pR2 = linhaRamal.GetEndPoint(1);
                    fixedEnd = ((pR1.DistanceTo(ptTop3D) > pR2.DistanceTo(ptTop3D)) ? pR1 : pR2);
                }
                else
                {
                    XYZ clickRamal = refRamal.GlobalPoint;
                    XYZ pR3 = linhaRamal.GetEndPoint(0);
                    XYZ pR4 = linhaRamal.GetEndPoint(1);
                    ptTop3D = ((clickRamal.DistanceTo(pR3) < clickRamal.DistanceTo(pR4)) ? pR3 : pR4);
                    fixedEnd = (ptTop3D.IsAlmostEqualTo(pR3) ? pR4 : pR3);
                    if (usarDuplo45)
                    {
                        TaskDialog.Show("PipeMaster [M]", "Aviso: Tubos paralelos já descem com 1 único joelho de 45º. O Duplo 45º é aplicado em cruzamentos perpendiculares.");
                        usarDuplo45 = false;
                    }
                }
                XYZ pM0 = linhaPrin.GetEndPoint(0);
                XYZ pM1 = linhaPrin.GetEndPoint(1);
                XYZ vMain = (pM1 - pM0).Normalize();
                if (pM0.Z < pM1.Z)
                {
                    vMain = -vMain;
                }
                else if (Math.Abs(pM0.Z - pM1.Z) < 0.001)
                {
                    vMain = linhaPrin.Direction.Normalize();
                }
                XYZ U = pM0 - ptTop3D;
                double a = U.DotProduct(vMain);
                double discriminant = U.GetLength() * U.GetLength() - a * a;
                if (discriminant < 0.0)
                {
                    discriminant = 0.0;
                }
                double t = 0.0 - a + Math.Sqrt(discriminant);
                XYZ ptBot3D = pM0 + vMain * t;
                try
                {
                    IntersectionResult projecao = linhaPrin.Project(ptBot3D);
                    if (projecao != null)
                    {
                        ptBot3D = projecao.XYZPoint;
                    }
                    XYZ vToTop = (ptTop3D - ptBot3D).Normalize();
                    XYZ v90 = (vToTop - vMain * vToTop.DotProduct(vMain)).Normalize();
                    if (v90.GetLength() < 0.1)
                    {
                        v90 = XYZ.BasisZ;
                    }
                    ElementId sysId = tuboRamal.MEPSystem?.GetTypeId() ?? new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).FirstElementId();
                    double diametro = ((Element)tuboRamal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    ElementId idJusante = PlumbingUtils.BreakCurve(doc, tuboPrincipal.Id, ptBot3D);
                    MEPCurve segJusante = doc.GetElement(idJusante) as MEPCurve;
                    doc.Regenerate();
                    XYZ ptFalsoEnd = ptBot3D + v90 * 2.0;
                    MEPCurve tuboFalso = Pipe.Create(doc, sysId, tuboRamal.GetTypeId(), tuboRamal.LevelId, ptBot3D, ptFalsoEnd);
                    ((Element)tuboFalso).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                    doc.Regenerate();
                    Connector cM1 = ObterConectorMaisProximo(tuboPrincipal, ptBot3D);
                    Connector cM2 = ObterConectorMaisProximo(segJusante, ptBot3D);
                    Connector cFalso = ObterConectorMaisProximo(tuboFalso, ptBot3D);
                    FamilyInstance tee = doc.Create.NewTeeFitting(cM1, cM2, cFalso);
                    doc.Regenerate();
                    doc.Delete(tuboFalso.Id);
                    doc.Regenerate();
                    List<Tuple<Connector, Connector>> conexoesParaDesfazer = new List<Tuple<Connector, Connector>>();
                    foreach (Connector c in tee.MEPModel.ConnectorManager.Connectors)
                    {
                        if (!c.IsConnected || c.ConnectorType != ConnectorType.End)
                        {
                            continue;
                        }
                        foreach (Connector refC in c.AllRefs)
                        {
                            if (refC.Owner.Id != tee.Id && refC.ConnectorType == ConnectorType.End)
                            {
                                conexoesParaDesfazer.Add(new Tuple<Connector, Connector>(c, refC));
                            }
                        }
                    }
                    Connector cM1_pipe = null;
                    Connector cM2_pipe = null;
                    foreach (Tuple<Connector, Connector> pair in conexoesParaDesfazer)
                    {
                        if (pair.Item2.Owner.Id == tuboPrincipal.Id)
                        {
                            cM1_pipe = pair.Item2;
                        }
                        if (pair.Item2.Owner.Id == segJusante.Id)
                        {
                            cM2_pipe = pair.Item2;
                        }
                        pair.Item1.DisconnectFrom(pair.Item2);
                    }
                    doc.Regenerate();
                    string[] nomesAngulo = new string[4] { "Ângulo", "Ângulo 1", "Angle", "Angulo" };
                    string[] array = nomesAngulo;
                    foreach (string nomeParam in array)
                    {
                        Parameter p = tee.LookupParameter(nomeParam);
                        if (p != null && !p.IsReadOnly)
                        {
                            if (p.StorageType == StorageType.Double)
                            {
                                p.Set(Math.PI / 4.0);
                            }
                            else
                            {
                                p.Set(45);
                            }
                        }
                    }
                    doc.Regenerate();
                    Connector cBranch = ObterConectorRamal(tee, vMain);
                    if (cBranch != null)
                    {
                        XYZ vBranch = cBranch.CoordinateSystem.BasisZ.Normalize();
                        XYZ projBranch = (vBranch - vMain * vBranch.DotProduct(vMain)).Normalize();
                        XYZ projTop = (vToTop - vMain * vToTop.DotProduct(vMain)).Normalize();
                        if (projBranch.DotProduct(projTop) < 0.5)
                        {
                            ElementTransformUtils.RotateElement(doc, tee.Id, Line.CreateUnbound(ptBot3D, vMain), Math.PI);
                            doc.Regenerate();
                        }
                    }
                    if (cM1_pipe != null)
                    {
                        Connector cT1 = ObterConectorMaisProximo(tee, cM1_pipe.Origin);
                        if (cT1 != null)
                        {
                            try
                            {
                                cT1.ConnectTo(cM1_pipe);
                            }
                            catch
                            {
                            }
                        }
                    }
                    if (cM2_pipe != null)
                    {
                        Connector cT2 = ObterConectorMaisProximo(tee, cM2_pipe.Origin);
                        if (cT2 != null)
                        {
                            try
                            {
                                cT2.ConnectTo(cM2_pipe);
                            }
                            catch
                            {
                            }
                        }
                    }
                    doc.Regenerate();
                    cBranch = ObterConectorRamal(tee, vMain);
                    XYZ ptAlvoBase = ((cBranch != null) ? cBranch.Origin : ptBot3D);
                    if (usarDuplo45 && ptInt2D != null)
                    {
                        double distDescida = ptTop3D.DistanceTo(ptAlvoBase);
                        double distHorizontal = fixedEnd.DistanceTo(ptTop3D);
                        double d = diametro * 3.0;
                        d = Math.Min(d, distDescida * 0.45);
                        d = Math.Min(d, distHorizontal * 0.8);
                        XYZ vIn = (ptTop3D - fixedEnd).Normalize();
                        XYZ vOut = (ptAlvoBase - ptTop3D).Normalize();
                        XYZ p2 = ptTop3D - vIn * d;
                        XYZ p3 = ptTop3D + vOut * d;
                        (tuboRamal.Location as LocationCurve).Curve = Line.CreateBound(fixedEnd, p2);
                        doc.Regenerate();
                        MEPCurve tuboTrans = Pipe.Create(doc, sysId, tuboRamal.GetTypeId(), tuboRamal.LevelId, p2, p3);
                        ((Element)tuboTrans).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                        MEPCurve tuboQueda = Pipe.Create(doc, sysId, tuboRamal.GetTypeId(), tuboRamal.LevelId, p3, ptAlvoBase);
                        ((Element)tuboQueda).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                        doc.Regenerate();
                        if (cBranch != null)
                        {
                            ObterConectorMaisProximo(tuboQueda, ptAlvoBase)?.ConnectTo(cBranch);
                        }
                        doc.Regenerate();
                        Connector cR = ObterConectorMaisProximo(tuboRamal, p2);
                        Connector cT3 = ObterConectorMaisProximo(tuboTrans, p2);
                        if (cR != null && cT3 != null)
                        {
                            doc.Create.NewElbowFitting(cR, cT3);
                        }
                        Connector cT4 = ObterConectorMaisProximo(tuboTrans, p3);
                        Connector cQ = ObterConectorMaisProximo(tuboQueda, p3);
                        if (cT4 != null && cQ != null)
                        {
                            doc.Create.NewElbowFitting(cT4, cQ);
                        }
                    }
                    else
                    {
                        (tuboRamal.Location as LocationCurve).Curve = Line.CreateBound(fixedEnd, ptTop3D);
                        doc.Regenerate();
                        MEPCurve tuboQueda2 = Pipe.Create(doc, sysId, tuboRamal.GetTypeId(), tuboRamal.LevelId, ptTop3D, ptAlvoBase);
                        ((Element)tuboQueda2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                        doc.Regenerate();
                        if (cBranch != null)
                        {
                            ObterConectorMaisProximo(tuboQueda2, ptAlvoBase)?.ConnectTo(cBranch);
                        }
                        doc.Regenerate();
                        Connector cRamalEnd = ObterConectorMaisProximo(tuboRamal, ptTop3D);
                        Connector cQuedaTop = ObterConectorMaisProximo(tuboQueda2, ptTop3D);
                        if (cRamalEnd != null && cQuedaTop != null)
                        {
                            doc.Create.NewElbowFitting(cRamalEnd, cQuedaTop);
                        }
                    }
                    trans.Commit();
                }
                catch
                {
                    if (trans.HasStarted())
                    {
                        trans.RollBack();
                    }
                    TaskDialog.Show("PipeMaster [M]", "O Tubo superior não tem altura suficiente para conectar ao tubo inferior e/ou as tubulações não se encontram na perpendicular.");
                    return Result.Failed;
                }
            }
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex2)
        {
            TaskDialog.Show("PipeMaster [M]", "Erro inesperado: " + ex2.Message);
            return Result.Failed;
        }
    }

    private Connector ObterConectorRamal(FamilyInstance fi, XYZ vMain)
    {
        Connector cBranch = null;
        double minDot = double.MaxValue;
        if (fi.MEPModel?.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
        {
            if (c.ConnectorType == ConnectorType.End)
            {
                double dotAbs = Math.Abs(c.CoordinateSystem.BasisZ.Normalize().DotProduct(vMain));
                if (dotAbs < minDot)
                {
                    minDot = dotAbs;
                    cBranch = c;
                }
            }
        }
        return cBranch;
    }

    private Connector ObterConectorMaisProximo(MEPCurve tubo, XYZ alvo)
    {
        Connector melhor = null;
        double menorDist = double.MaxValue;
        if (tubo.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in tubo.ConnectorManager.Connectors)
        {
            double d = c.Origin.DistanceTo(alvo);
            if (d < menorDist)
            {
                menorDist = d;
                melhor = c;
            }
        }
        return melhor;
    }

    private Connector ObterConectorMaisProximo(FamilyInstance fi, XYZ alvo)
    {
        Connector melhor = null;
        double menorDist = double.MaxValue;
        if (fi.MEPModel?.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
        {
            double d = c.Origin.DistanceTo(alvo);
            if (d < menorDist)
            {
                menorDist = d;
                melhor = c;
            }
        }
        return melhor;
    }

    private double GetZAt2DPoint(Line linha, XYZ pt2D)
    {
        XYZ p0 = linha.GetEndPoint(0);
        XYZ p1 = linha.GetEndPoint(1);
        double distTotal = Math.Sqrt(Math.Pow(p1.X - p0.X, 2.0) + Math.Pow(p1.Y - p0.Y, 2.0));
        if (distTotal < 1E-09)
        {
            return p0.Z;
        }
        double distPt = Math.Sqrt(Math.Pow(pt2D.X - p0.X, 2.0) + Math.Pow(pt2D.Y - p0.Y, 2.0));
        return p0.Z + (p1.Z - p0.Z) * (distPt / distTotal);
    }

    private XYZ EncontrarIntersecao2D(XYZ p1, XYZ p2, XYZ p3, XYZ p4)
    {
        double A1 = p2.Y - p1.Y;
        double B1 = p1.X - p2.X;
        double C1 = A1 * p1.X + B1 * p1.Y;
        double A2 = p4.Y - p3.Y;
        double B2 = p3.X - p4.X;
        double C2 = A2 * p3.X + B2 * p3.Y;
        double determinante = A1 * B2 - A2 * B1;
        if (Math.Abs(determinante) < 1E-09)
        {
            return null;
        }
        double x = (B2 * C1 - B1 * C2) / determinante;
        double y = (A1 * C2 - A2 * C1) / determinante;
        double minX = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X)) - 0.5;
        double maxX = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X)) + 0.5;
        double minY = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y)) - 0.5;
        double maxY = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y)) + 0.5;
        if (x >= minX && x <= maxX && y >= minY && y <= maxY)
        {
            return new XYZ(x, y, 0.0);
        }
        return null;
    }
}
