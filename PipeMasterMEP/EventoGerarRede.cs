using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

public class EventoGerarRede : IExternalEventHandler
{
    private static readonly XYZ BOLSA_AXIS_LOCAL = XYZ.BasisX;

    public List<LinhaComDNA> LinhasComDNA { get; set; }

    public XYZ PontoDescarga { get; set; }

    public double ElevacaoMetros { get; set; }

    public ElementId SistemaId { get; set; }

    public ElementId TipoTuboId { get; set; }

    public bool ApagarLinhas { get; set; }

    public List<XYZ> PontasVentilacao { get; set; } = new List<XYZ>();

    public void Execute(UIApplication app)
    {
        UIDocument uidoc = app.ActiveUIDocument;
        Document doc = uidoc.Document;
        try
        {
            ElementId levelId = ObterNivelDaVista(doc, uidoc.ActiveView);
            double zNivel = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;
            double zDesc = zNivel + UnitUtils.ConvertToInternalUnits(ElevacaoMetros, UnitTypeId.Meters);
            List<SegmentoEsgoto> segmentosEsgoto = ProcessarIntersecoes(LinhasComDNA, 0.098);
            List<XYZ> ptsBrutos = new List<XYZ>();
            foreach (SegmentoEsgoto seg in segmentosEsgoto)
            {
                ptsBrutos.Add(seg.A);
                ptsBrutos.Add(seg.B);
            }
            List<XYZ> clusters = new List<XYZ>();
            Dictionary<int, int> ptToClust = new Dictionary<int, int>();
            for (int i = 0; i < ptsBrutos.Count; i++)
            {
                XYZ pt = ptsBrutos[i];
                int cId = -1;
                for (int k = 0; k < clusters.Count; k++)
                {
                    if (Dist2D(clusters[k], pt) <= 0.098)
                    {
                        cId = k;
                        break;
                    }
                }
                if (cId == -1)
                {
                    cId = clusters.Count;
                    clusters.Add(pt);
                }
                ptToClust[i] = cId;
            }
            Dictionary<int, HashSet<int>> adj = new Dictionary<int, HashSet<int>>();
            List<(int, int, double, double, bool, bool, ElementId, ElementId)> segmentosFinais = new List<(int, int, double, double, bool, bool, ElementId, ElementId)>();
            for (int j = 0; j < segmentosEsgoto.Count; j++)
            {
                int cA = ptToClust[j * 2];
                int cB = ptToClust[j * 2 + 1];
                if (cA != cB)
                {
                    if (!adj.ContainsKey(cA))
                    {
                        adj[cA] = new HashSet<int>();
                    }
                    if (!adj.ContainsKey(cB))
                    {
                        adj[cB] = new HashSet<int>();
                    }
                    adj[cA].Add(cB);
                    adj[cB].Add(cA);
                    segmentosFinais.Add((cA, cB, segmentosEsgoto[j].Diametro, segmentosEsgoto[j].Inclinacao, segmentosEsgoto[j].IsVaso, segmentosEsgoto[j].IsVentilacao, segmentosEsgoto[j].SistemaId, segmentosEsgoto[j].TipoTuboId));
                }
            }
            int clustDesc = ClusterMaisProximo(clusters, PontoDescarga);
            Dictionary<int, double> elevZ = new Dictionary<int, double> { [clustDesc] = zDesc };
            Queue<int> fila = new Queue<int>();
            fila.Enqueue(clustDesc);
            while (fila.Count > 0)
            {
                int cur = fila.Dequeue();
                if (!adj.ContainsKey(cur))
                {
                    continue;
                }
                foreach (int viz in adj[cur])
                {
                    if (!elevZ.ContainsKey(viz))
                    {
                        (int, int, double, double, bool, bool, ElementId, ElementId) edge = segmentosFinais.FirstOrDefault<(int, int, double, double, bool, bool, ElementId, ElementId)>(((int cA, int cB, double diam, double inc, bool isVaso, bool isVentilacao, ElementId sisId, ElementId tipoId) s) => (s.cA == cur && s.cB == viz) || (s.cA == viz && s.cB == cur));
                        elevZ[viz] = elevZ[cur] + Dist2D(clusters[cur], clusters[viz]) * edge.Item4;
                        fila.Enqueue(viz);
                    }
                }
            }
            for (int i2 = 0; i2 < clusters.Count; i2++)
            {
                if (!elevZ.ContainsKey(i2))
                {
                    elevZ[i2] = zDesc;
                }
            }
            int nTubos = 0;
            int nFittings = 0;
            int nCaixas = 0;
            List<string> errosFitting = new List<string>();
            using (Transaction t = new Transaction(doc, "PipeMaster: Rede Completa Esgoto"))
            {
                t.Start();
                try
                {
                    FailureHandlingOptions fho = t.GetFailureHandlingOptions();
                    t.SetFailureHandlingOptions(fho);
                }
                catch
                {
                }
                List<(Pipe, int, int, bool, bool)> tubosCriados = new List<(Pipe, int, int, bool, bool)>();
                for (int i3 = 0; i3 < segmentosFinais.Count; i3++)
                {
                    (int, int, double, double, bool, bool, ElementId, ElementId) seg2 = segmentosFinais[i3];
                    double extraZ = 0.0;
                    if (seg2.Item6)
                    {
                        extraZ = UnitUtils.ConvertToInternalUnits(0.08, UnitTypeId.Meters);
                    }
                    XYZ ptA = new XYZ(clusters[seg2.Item1].X, clusters[seg2.Item1].Y, elevZ[seg2.Item1] + extraZ);
                    XYZ ptB = new XYZ(clusters[seg2.Item2].X, clusters[seg2.Item2].Y, elevZ[seg2.Item2] + extraZ);
                    if (!(ptA.DistanceTo(ptB) < 0.1))
                    {
                        XYZ ptStart = ((ptA.Z >= ptB.Z) ? ptA : ptB);
                        XYZ ptEnd = ((ptA.Z >= ptB.Z) ? ptB : ptA);
                        double diamInterno = UnitUtils.ConvertToInternalUnits(seg2.Item3, UnitTypeId.Millimeters);
                        try
                        {
                            Pipe p = Pipe.Create(doc, seg2.Item7, seg2.Rest.Item1, levelId, ptStart, ptEnd);
                            ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamInterno);
                            tubosCriados.Add((p, seg2.Item1, seg2.Item2, seg2.Item5, seg2.Item6));
                            nTubos++;
                        }
                        catch
                        {
                        }
                    }
                }
                if (tubosCriados.Count == 0)
                {
                    t.RollBack();
                    return;
                }
                doc.Regenerate();
                Dictionary<int, List<Connector>> connPorCluster = new Dictionary<int, List<Connector>>();
                foreach (var tupla in tubosCriados)
                {
                    if (!tupla.Item1.IsValidObject)
                    {
                        continue;
                    }
                    XYZ ptA3D = new XYZ(clusters[tupla.Item2].X, clusters[tupla.Item2].Y, elevZ[tupla.Item2]);
                    XYZ ptB3D = new XYZ(clusters[tupla.Item3].X, clusters[tupla.Item3].Y, elevZ[tupla.Item3]);
                    Connector cA_conn = ConnectorMaisProximo(tupla.Item1, ptA3D);
                    Connector cB_conn = ConnectorMaisProximo(tupla.Item1, ptB3D);
                    if (cA_conn != null)
                    {
                        if (!connPorCluster.ContainsKey(tupla.Item2))
                        {
                            connPorCluster[tupla.Item2] = new List<Connector>();
                        }
                        connPorCluster[tupla.Item2].Add(cA_conn);
                    }
                    if (cB_conn != null)
                    {
                        if (!connPorCluster.ContainsKey(tupla.Item3))
                        {
                            connPorCluster[tupla.Item3] = new List<Connector>();
                        }
                        connPorCluster[tupla.Item3].Add(cB_conn);
                    }
                }
                foreach (KeyValuePair<int, List<Connector>> item in connPorCluster.Where((KeyValuePair<int, List<Connector>> g) => g.Value.Count >= 3))
                {
                    List<Connector> livres = item.Value.Where((Connector connector) => connector.IsValidObject && !connector.IsConnected).ToList();
                    if (livres.Count < 3)
                    {
                        continue;
                    }
                    int idxMain1 = -1;
                    int idxMain2 = -1;
                    int idxBranch = -1;
                    double minDot = 1.0;
                    for (int a = 0; a < livres.Count; a++)
                    {
                        for (int b = a + 1; b < livres.Count; b++)
                        {
                            double dot = livres[a].CoordinateSystem.BasisZ.DotProduct(livres[b].CoordinateSystem.BasisZ);
                            if (dot < minDot)
                            {
                                minDot = dot;
                                idxMain1 = a;
                                idxMain2 = b;
                            }
                        }
                    }
                    for (int i4 = 0; i4 < livres.Count; i4++)
                    {
                        if (i4 != idxMain1 && i4 != idxMain2)
                        {
                            idxBranch = i4;
                            break;
                        }
                    }
                    if (idxMain1 == -1 || idxMain2 == -1 || idxBranch == -1)
                    {
                        continue;
                    }
                    Connector main1 = livres[idxMain1];
                    Connector main2 = livres[idxMain2];
                    Connector branch = livres[idxBranch];
                    Connector cDown = ((main1.Origin.Z < main2.Origin.Z) ? main1 : main2);
                    Connector cUp = ((main1.Origin.Z < main2.Origin.Z) ? main2 : main1);
                    double dDown = cDown.Owner.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    double dUp = cUp.Owner.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    if (!(Math.Abs(dDown - dUp) > 0.001))
                    {
                        try
                        {
                            FamilyInstance tee = doc.Create.NewTeeFitting(cDown, cUp, branch);
                            doc.Regenerate();
                            AjustarLuvaGeometrico(tee, AcharConnectorTuboMontante(tee, doc), doc);
                            nFittings++;
                            ForcarInclinacaoAposConexao(cUp.Owner as Pipe, tee.Id, doc);
                            ForcarInclinacaoAposConexao(branch.Owner as Pipe, tee.Id, doc);
                        }
                        catch
                        {
                            try
                            {
                                XYZ origin = cDown.Origin;
                                XYZ dirMain = cDown.CoordinateSystem.BasisZ;
                                XYZ dirBranch = -branch.CoordinateSystem.BasisZ;
                                XYZ proj = dirBranch - dirBranch.DotProduct(dirMain) * dirMain;
                                XYZ perpDir = ((proj.GetLength() < 0.001) ? new XYZ(0.0 - dirMain.Y, dirMain.X, 0.0).Normalize() : proj.Normalize());
                                XYZ ptFimTemp = origin + perpDir * 2.0;
                                Pipe tempPipe = Pipe.Create(doc, SistemaId, TipoTuboId, levelId, origin, ptFimTemp);
                                ((Element)tempPipe).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(branch.Radius * 2.0);
                                Connector tempConn = ConnectorMaisProximo(tempPipe, origin);
                                if (tempConn == null)
                                {
                                    continue;
                                }
                                FamilyInstance teeInst = doc.Create.NewTeeFitting(cDown, cUp, tempConn);
                                doc.Delete(tempPipe.Id);
                                doc.Regenerate();
                                Connector teeLivre = null;
                                foreach (Connector c in teeInst.MEPModel.ConnectorManager.Connectors)
                                {
                                    if (!c.IsConnected)
                                    {
                                        teeLivre = c;
                                        break;
                                    }
                                }
                                if (teeLivre == null)
                                {
                                    continue;
                                }
                                double anguloRad = dirMain.AngleTo(dirBranch);
                                if (anguloRad > Math.PI / 2.0)
                                {
                                    anguloRad = Math.PI - anguloRad;
                                }
                                Parameter paramAngulo = teeInst.LookupParameter("Ângulo") ?? teeInst.LookupParameter("Angle") ?? teeInst.LookupParameter("Ângulo 1");
                                if (paramAngulo != null && !paramAngulo.IsReadOnly)
                                {
                                    try
                                    {
                                        paramAngulo.Set(anguloRad);
                                    }
                                    catch
                                    {
                                    }
                                    doc.Regenerate();
                                }
                                if (branch.Owner.Location is LocationCurve { Curve: var oldCurve } locCurve)
                                {
                                    XYZ pt2 = oldCurve.GetEndPoint(0);
                                    XYZ pt3 = oldCurve.GetEndPoint(1);
                                    if (pt2.DistanceTo(origin) < pt3.DistanceTo(origin))
                                    {
                                        pt2 = teeLivre.Origin;
                                    }
                                    else
                                    {
                                        pt3 = teeLivre.Origin;
                                    }
                                    if (pt2.DistanceTo(pt3) > 0.05)
                                    {
                                        try
                                        {
                                            locCurve.Curve = Line.CreateBound(pt2, pt3);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                                branch.ConnectTo(teeLivre);
                                doc.Regenerate();
                                AjustarLuvaGeometrico(teeInst, AcharConnectorTuboMontante(teeInst, doc), doc);
                                nFittings++;
                                ForcarInclinacaoAposConexao(cUp.Owner as Pipe, teeInst.Id, doc);
                                ForcarInclinacaoAposConexao(branch.Owner as Pipe, teeInst.Id, doc);
                            }
                            catch (Exception ex)
                            {
                                if (!errosFitting.Contains(ex.Message))
                                {
                                    errosFitting.Add("Erro na Junção: " + ex.Message);
                                }
                            }
                        }
                        continue;
                    }
                    try
                    {
                        Pipe pipeUp = cUp.Owner as Pipe;
                        LocationCurve lcUp = pipeUp.Location as LocationCurve;
                        XYZ origin2 = cDown.Origin;
                        XYZ dirMain2 = cDown.CoordinateSystem.BasisZ;
                        XYZ pt4 = lcUp.Curve.GetEndPoint(0);
                        XYZ pt5 = lcUp.Curve.GetEndPoint(1);
                        bool isPt0Perto = pt4.DistanceTo(origin2) < pt5.DistanceTo(origin2);
                        XYZ ptUpPerto = (isPt0Perto ? pt4 : pt5);
                        XYZ ptUpLonge = (isPt0Perto ? pt5 : pt4);
                        XYZ dirUp = (ptUpLonge - ptUpPerto).Normalize();
                        double recuo = UnitUtils.ConvertToInternalUnits(0.5, UnitTypeId.Meters);
                        XYZ novoPtPerto = ptUpPerto + dirUp * recuo;
                        if (!(ptUpPerto.DistanceTo(ptUpLonge) > recuo + 0.05))
                        {
                            continue;
                        }
                        lcUp.Curve = (isPt0Perto ? Line.CreateBound(novoPtPerto, ptUpLonge) : Line.CreateBound(ptUpLonge, novoPtPerto));
                        doc.Regenerate();
                        XYZ ptFimHub = origin2 + dirMain2 * recuo;
                        Pipe tuboHub = Pipe.Create(doc, SistemaId, TipoTuboId, levelId, origin2, ptFimHub);
                        ((Element)tuboHub).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(dDown);
                        doc.Regenerate();
                        Connector cHubBase = ConnectorMaisProximo(tuboHub, origin2);
                        FamilyInstance teeInst2 = null;
                        try
                        {
                            teeInst2 = doc.Create.NewTeeFitting(cDown, cHubBase, branch);
                        }
                        catch
                        {
                            XYZ dirBranchReal = -branch.CoordinateSystem.BasisZ;
                            XYZ proj2 = dirBranchReal - dirBranchReal.DotProduct(dirMain2) * dirMain2;
                            XYZ perpDir2 = ((proj2.GetLength() < 0.001) ? new XYZ(0.0 - dirMain2.Y, dirMain2.X, 0.0).Normalize() : proj2.Normalize());
                            XYZ ptFimTemp2 = origin2 + perpDir2 * 1.0;
                            Pipe tempPipe2 = Pipe.Create(doc, SistemaId, TipoTuboId, levelId, origin2, ptFimTemp2);
                            ((Element)tempPipe2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(dDown);
                            Connector tempConn2 = ConnectorMaisProximo(tempPipe2, origin2);
                            if (tempConn2 != null)
                            {
                                teeInst2 = doc.Create.NewTeeFitting(cDown, cHubBase, tempConn2);
                                doc.Delete(tempPipe2.Id);
                                doc.Regenerate();
                                Connector teeLivreBr = null;
                                foreach (Connector c2 in teeInst2.MEPModel.ConnectorManager.Connectors)
                                {
                                    if (!c2.IsConnected && c2.ConnectorType != ConnectorType.Logical && c2.CoordinateSystem.BasisZ.DotProduct(perpDir2) > 0.8)
                                    {
                                        teeLivreBr = c2;
                                    }
                                }
                                if (teeLivreBr != null)
                                {
                                    double dBranchReal = branch.Owner.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                                    foreach (Parameter p2 in teeInst2.Parameters)
                                    {
                                        string pName = p2.Definition.Name.ToLower();
                                        if ((pName.Contains("raio") || pName.Contains("radius")) && (pName.Contains("2") || pName.Contains("3") || pName.Contains("ramal")))
                                        {
                                            try
                                            {
                                                p2.Set(dBranchReal / 2.0);
                                            }
                                            catch
                                            {
                                            }
                                        }
                                        else if ((pName.Contains("diâmetro") || pName.Contains("diametro") || pName.Contains("diameter")) && (pName.Contains("2") || pName.Contains("3") || pName.Contains("ramal")))
                                        {
                                            try
                                            {
                                                p2.Set(dBranchReal);
                                            }
                                            catch
                                            {
                                            }
                                        }
                                    }
                                    doc.Regenerate();
                                    double anguloRad2 = dirMain2.AngleTo(dirBranchReal);
                                    if (anguloRad2 > Math.PI / 2.0)
                                    {
                                        anguloRad2 = Math.PI - anguloRad2;
                                    }
                                    Parameter paramAngulo2 = teeInst2.LookupParameter("Ângulo") ?? teeInst2.LookupParameter("Angle") ?? teeInst2.LookupParameter("Ângulo 1");
                                    if (paramAngulo2 != null && !paramAngulo2.IsReadOnly)
                                    {
                                        try
                                        {
                                            paramAngulo2.Set(anguloRad2);
                                        }
                                        catch
                                        {
                                        }
                                        doc.Regenerate();
                                    }
                                    if (branch.Owner.Location is LocationCurve lcb)
                                    {
                                        XYZ b2 = lcb.Curve.GetEndPoint(0);
                                        XYZ b3 = lcb.Curve.GetEndPoint(1);
                                        if (b2.DistanceTo(origin2) < b3.DistanceTo(origin2))
                                        {
                                            b2 = teeLivreBr.Origin;
                                        }
                                        else
                                        {
                                            b3 = teeLivreBr.Origin;
                                        }
                                        if (b2.DistanceTo(b3) > 0.05)
                                        {
                                            try
                                            {
                                                lcb.Curve = Line.CreateBound(b2, b3);
                                            }
                                            catch
                                            {
                                            }
                                        }
                                    }
                                    try
                                    {
                                        branch.ConnectTo(teeLivreBr);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            doc.Create.NewTransitionFitting(teeLivreBr, branch);
                                            goto end_IL_138f;
                                        }
                                        catch
                                        {
                                            goto end_IL_138f;
                                        }
                                    end_IL_138f:;
                                    }
                                }
                            }
                        }
                        if (teeInst2 == null)
                        {
                            continue;
                        }
                        doc.Regenerate();
                        AjustarLuvaGeometrico(teeInst2, AcharConnectorTuboMontante(teeInst2, doc), doc);
                        nFittings++;
                        doc.Delete(tuboHub.Id);
                        doc.Regenerate();
                        Connector teeLivreUp = null;
                        foreach (Connector c3 in teeInst2.MEPModel.ConnectorManager.Connectors)
                        {
                            if (!c3.IsConnected && c3.ConnectorType != ConnectorType.Logical && c3.CoordinateSystem.BasisZ.DotProduct(dirMain2) > 0.8)
                            {
                                teeLivreUp = c3;
                                break;
                            }
                        }
                        if (teeLivreUp == null)
                        {
                            continue;
                        }
                        double tamApoio = UnitUtils.ConvertToInternalUnits(0.2, UnitTypeId.Meters);
                        XYZ ptApoioFim = teeLivreUp.Origin + dirMain2 * tamApoio;
                        Pipe tuboApoio = Pipe.Create(doc, SistemaId, TipoTuboId, levelId, teeLivreUp.Origin, ptApoioFim);
                        ((Element)tuboApoio).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(dDown);
                        doc.Regenerate();
                        Connector cApoioBase = ConnectorMaisProximo(tuboApoio, teeLivreUp.Origin);
                        Connector cApoioTopo = ConnectorMaisProximo(tuboApoio, ptApoioFim);
                        try
                        {
                            cApoioBase.ConnectTo(teeLivreUp);
                        }
                        catch
                        {
                        }
                        doc.Regenerate();
                        Connector cUpRecuado = ConnectorMaisProximo(pipeUp, novoPtPerto);
                        FamilyInstance reducao = null;
                        if (cUpRecuado != null)
                        {
                            try
                            {
                                reducao = doc.Create.NewTransitionFitting(cApoioTopo, cUpRecuado);
                                doc.Regenerate();
                                nFittings++;
                            }
                            catch (Exception ex2)
                            {
                                errosFitting.Add("Erro Redução: " + ex2.Message);
                            }
                        }
                        if (reducao != null)
                        {
                            Connector connRed100 = null;
                            foreach (Connector c4 in reducao.MEPModel.ConnectorManager.Connectors)
                            {
                                if (c4.ConnectorType == ConnectorType.Logical || !c4.IsConnected)
                                {
                                    continue;
                                }
                                foreach (Connector refC in c4.AllRefs)
                                {
                                    if (refC.Owner.Id == tuboApoio.Id)
                                    {
                                        connRed100 = c4;
                                        break;
                                    }
                                }
                            }
                            doc.Delete(tuboApoio.Id);
                            doc.Regenerate();
                            ForcarReducaoExcentrica(reducao, doc);
                            doc.Regenerate();
                            if (connRed100 != null)
                            {
                                XYZ translacao = teeLivreUp.Origin - connRed100.Origin;
                                ElementTransformUtils.MoveElement(doc, reducao.Id, translacao);
                                doc.Regenerate();
                                try
                                {
                                    connRed100.ConnectTo(teeLivreUp);
                                }
                                catch
                                {
                                }
                                doc.Regenerate();
                                ForcarInclinacaoAposConexao(pipeUp, reducao.Id, doc);
                                ForcarInclinacaoAposConexao(branch.Owner as Pipe, teeInst2.Id, doc);
                            }
                        }
                        else
                        {
                            doc.Delete(tuboApoio.Id);
                        }
                    }
                    catch (Exception ex3)
                    {
                        errosFitting.Add("Erro na Redução Específica: " + ex3.Message);
                    }
                }
                foreach (KeyValuePair<int, List<Connector>> item2 in connPorCluster.Where((KeyValuePair<int, List<Connector>> g) => g.Value.Count == 2))
                {
                    List<Connector> livres2 = item2.Value.Where((Connector connector) => connector.IsValidObject && !connector.IsConnected).ToList();
                    if (livres2.Count >= 2)
                    {
                        try
                        {
                            Connector cUp2 = ((livres2[0].Origin.Z > livres2[1].Origin.Z) ? livres2[0] : livres2[1]);
                            Connector cDown2 = ((livres2[0].Origin.Z > livres2[1].Origin.Z) ? livres2[1] : livres2[0]);
                            FamilyInstance joelho = doc.Create.NewElbowFitting(cUp2, cDown2);
                            doc.Regenerate();
                            Connector tuboMontante = AcharConnectorTuboMontante(joelho, doc);
                            AjustarLuvaGeometrico(joelho, tuboMontante, doc);
                            nFittings++;
                            ForcarInclinacaoAposConexao(cUp2.Owner as Pipe, joelho.Id, doc);
                            ForcarInclinacaoAposConexao(cDown2.Owner as Pipe, joelho.Id, doc);
                        }
                        catch
                        {
                        }
                    }
                }
                foreach (var tupla2 in tubosCriados)
                {
                    if (!tupla2.Item4 || !tupla2.Item1.IsValidObject)
                    {
                        continue;
                    }
                    List<Connector> livres3 = new List<Connector>();
                    foreach (Connector c5 in tupla2.Item1.ConnectorManager.Connectors)
                    {
                        if (c5.ConnectorType != ConnectorType.Logical && !c5.IsConnected)
                        {
                            livres3.Add(c5);
                        }
                    }
                    if (livres3.Count <= 0)
                    {
                        continue;
                    }
                    Connector connVaso = livres3.OrderByDescending((Connector connector) => connector.Origin.Z).First();
                    XYZ ptBase = connVaso.Origin;
                    XYZ ptTopo = new XYZ(ptBase.X, ptBase.Y, zNivel);
                    if (!(ptTopo.Z - ptBase.Z > 0.05))
                    {
                        continue;
                    }
                    try
                    {
                        double diam = ((Element)tupla2.Item1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                        Pipe vertPipe = Pipe.Create(doc, SistemaId, TipoTuboId, levelId, ptBase, ptTopo);
                        ((Element)vertPipe).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diam);
                        Connector bottomVertConn = ConnectorMaisProximo(vertPipe, ptBase);
                        if (bottomVertConn != null)
                        {
                            FamilyInstance cotoveloVaso = doc.Create.NewElbowFitting(connVaso, bottomVertConn);
                            doc.Regenerate();
                            AjustarLuvaGeometrico(cotoveloVaso, AcharConnectorTuboMontante(cotoveloVaso, doc), doc);
                            nTubos++;
                            nFittings++;
                        }
                    }
                    catch (Exception ex4)
                    {
                        errosFitting.Add("Subida Vaso: " + ex4.Message);
                    }
                }
                List<FamilyInstance> caixasSifonadas = new FilteredElementCollector(doc, uidoc.ActiveView.Id).WhereElementIsNotElementType().WherePasses(new LogicalOrFilter(new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures), new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory))).OfType<FamilyInstance>()
                    .ToList();
                List<Connector> pontasSoltas = new List<Connector>();
                foreach (var tupla3 in tubosCriados)
                {
                    if (!tupla3.Item1.IsValidObject)
                    {
                        continue;
                    }
                    foreach (Connector c6 in tupla3.Item1.ConnectorManager.Connectors)
                    {
                        if (c6.ConnectorType != ConnectorType.Logical && !c6.IsConnected)
                        {
                            pontasSoltas.Add(c6);
                        }
                    }
                }
                foreach (Connector pConn in pontasSoltas)
                {
                    if (pConn.IsConnected)
                    {
                        continue;
                    }
                    Connector bestBoxConn = null;
                    double minDist = 0.4;
                    FamilyInstance bestBox = null;
                    foreach (FamilyInstance caixa in caixasSifonadas)
                    {
                        if (caixa.MEPModel == null || caixa.MEPModel.ConnectorManager == null)
                        {
                            continue;
                        }
                        foreach (Connector bxC in caixa.MEPModel.ConnectorManager.Connectors)
                        {
                            if (bxC.ConnectorType != ConnectorType.Logical && !bxC.IsConnected)
                            {
                                double d2d = Dist2D(pConn.Origin, bxC.Origin);
                                if (d2d < minDist)
                                {
                                    minDist = d2d;
                                    bestBoxConn = bxC;
                                    bestBox = caixa;
                                }
                            }
                        }
                    }
                    if (bestBoxConn == null || bestBox == null)
                    {
                        continue;
                    }
                    try
                    {
                        Pipe pipe = pConn.Owner as Pipe;
                        Connector pOpposite = null;
                        foreach (Connector c7 in pipe.ConnectorManager.Connectors)
                        {
                            if (c7.Id != pConn.Id && c7.ConnectorType != ConnectorType.Logical)
                            {
                                pOpposite = c7;
                                break;
                            }
                        }
                        if (pOpposite == null)
                        {
                            continue;
                        }
                        double slopeVerdadeiro = ObterInclinacao(pipe);
                        double newLen2D = Dist2D(pOpposite.Origin, bestBoxConn.Origin);
                        double newZ = pOpposite.Origin.Z + newLen2D * slopeVerdadeiro;
                        double deltaZ = newZ - bestBoxConn.Origin.Z;
                        try
                        {
                            ElementTransformUtils.MoveElement(doc, bestBox.Id, new XYZ(0.0, 0.0, deltaZ));
                        }
                        catch
                        {
                            Parameter pElev = ((Element)bestBox).get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM) ?? ((Element)bestBox).get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                            if (pElev != null && !pElev.IsReadOnly)
                            {
                                pElev.Set(pElev.AsDouble() + deltaZ);
                            }
                        }
                        doc.Regenerate();
                        if (pipe.Location is LocationCurve { Curve: var oldCurve2 } locCurve2)
                        {
                            XYZ pt6 = oldCurve2.GetEndPoint(0);
                            XYZ pt7 = oldCurve2.GetEndPoint(1);
                            XYZ newBoxPos = new XYZ(bestBoxConn.Origin.X, bestBoxConn.Origin.Y, newZ);
                            if (pt6.DistanceTo(pConn.Origin) < pt7.DistanceTo(pConn.Origin))
                            {
                                pt6 = newBoxPos;
                            }
                            else
                            {
                                pt7 = newBoxPos;
                            }
                            if (pt6.DistanceTo(pt7) > 0.05)
                            {
                                locCurve2.Curve = Line.CreateBound(pt6, pt7);
                                doc.Regenerate();
                            }
                        }
                        pConn.ConnectTo(bestBoxConn);
                        doc.Regenerate();
                        nCaixas++;
                    }
                    catch (Exception ex5)
                    {
                        errosFitting.Add("Ímã de Caixa: " + ex5.Message);
                    }
                }
                if (PontasVentilacao != null && PontasVentilacao.Count > 0)
                {
                    double altStub = 0.262467;
                    foreach (XYZ ptVent in PontasVentilacao)
                    {
                        try
                        {
                            Pipe ventPipe = null;
                            Connector ventConn = null;
                            double minD = double.MaxValue;
                            foreach (var tCriado in tubosCriados)
                            {
                                if (tCriado.Item1 == null)
                                {
                                    continue;
                                }
                                foreach (Connector c8 in tCriado.Item1.ConnectorManager.Connectors)
                                {
                                    if (!c8.IsConnected)
                                    {
                                        double d = c8.Origin.DistanceTo(ptVent);
                                        if (d < 0.1 && d < minD)
                                        {
                                            minD = d;
                                            ventPipe = tCriado.Item1;
                                            ventConn = c8;
                                        }
                                    }
                                }
                            }
                            if (ventConn == null)
                            {
                                continue;
                            }
                            List<Pipe> tubosRevit = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).WhereElementIsNotElementType().Cast<Pipe>()
                                .Where(delegate (Pipe pipe2)
                                {
                                    MEPSystem mEPSystem = pipe2.MEPSystem;
                                    int result;
                                    if (mEPSystem == null || mEPSystem.Name?.ToLower().Contains("ventil") != true)
                                    {
                                        MEPSystem mEPSystem2 = pipe2.MEPSystem;
                                        if (mEPSystem2 == null || mEPSystem2.Name?.ToLower().Contains("vent") != true)
                                        {
                                            MEPSystem mEPSystem3 = pipe2.MEPSystem;
                                            result = ((mEPSystem3 == null || mEPSystem3.Name?.ToLower().Contains("aeraç") != true) ? 1 : 0);
                                            goto IL_00ca;
                                        }
                                    }
                                    result = 0;
                                    goto IL_00ca;
                                IL_00ca:
                                    return (byte)result != 0;
                                })
                                .ToList();
                            Pipe esgotoPipe = null;
                            Connector esgotoConn = null;
                            minD = double.MaxValue;
                            foreach (Pipe p3 in tubosRevit)
                            {
                                foreach (Connector c9 in p3.ConnectorManager.Connectors)
                                {
                                    if (!c9.IsConnected)
                                    {
                                        XYZ oXY = new XYZ(c9.Origin.X, c9.Origin.Y, 0.0);
                                        XYZ ptXY = new XYZ(ptVent.X, ptVent.Y, 0.0);
                                        double dXY = oXY.DistanceTo(ptXY);
                                        if (dXY < 3.28 && dXY < minD)
                                        {
                                            minD = dXY;
                                            esgotoPipe = p3;
                                            esgotoConn = c9;
                                        }
                                    }
                                }
                            }
                            if (esgotoConn == null)
                            {
                                continue;
                            }
                            XYZ ptBase2 = esgotoConn.Origin;
                            XYZ newVentEnd = new XYZ(ptBase2.X, ptBase2.Y, ventConn.Origin.Z);
                            if (ventPipe.Location is LocationCurve { Curve: var oldCurve3 } locCurve3)
                            {
                                XYZ p4 = oldCurve3.GetEndPoint(0);
                                XYZ p5 = oldCurve3.GetEndPoint(1);
                                if (p4.DistanceTo(ventConn.Origin) < p5.DistanceTo(ventConn.Origin))
                                {
                                    p4 = newVentEnd;
                                }
                                else
                                {
                                    p5 = newVentEnd;
                                }
                                if (p4.DistanceTo(p5) > 0.1)
                                {
                                    locCurve3.Curve = Line.CreateBound(p4, p5);
                                    doc.Regenerate();
                                }
                            }
                            ventConn = null;
                            foreach (Connector c10 in ventPipe.ConnectorManager.Connectors)
                            {
                                if (!c10.IsConnected && c10.Origin.DistanceTo(newVentEnd) < 0.1)
                                {
                                    ventConn = c10;
                                }
                            }
                            if (ventConn == null)
                            {
                                continue;
                            }
                            double zTopo = Math.Max(newVentEnd.Z, ptBase2.Z + altStub);
                            XYZ ptTopo2 = new XYZ(ptBase2.X, ptBase2.Y, zTopo);
                            Pipe stubPipe = Pipe.Create(doc, esgotoPipe.MEPSystem?.GetTypeId() ?? SistemaId, esgotoPipe.PipeType?.Id ?? TipoTuboId, levelId, ptBase2, ptTopo2);
                            ((Element)stubPipe).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(((Element)ventPipe).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble());
                            Connector stubBaseConn = null;
                            Connector stubTopoConn = null;
                            foreach (Connector c11 in stubPipe.ConnectorManager.Connectors)
                            {
                                if (c11.Origin.DistanceTo(ptBase2) < 0.01)
                                {
                                    stubBaseConn = c11;
                                }
                                else
                                {
                                    stubTopoConn = c11;
                                }
                            }
                            XYZ esgotoDir = esgotoConn.CoordinateSystem.BasisZ;
                            XYZ ptRabicho = ptBase2 + esgotoDir * 0.656;
                            Pipe rabicho = Pipe.Create(doc, esgotoPipe.MEPSystem?.GetTypeId() ?? SistemaId, esgotoPipe.PipeType?.Id ?? TipoTuboId, levelId, ptBase2, ptRabicho);
                            ((Element)rabicho).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(((Element)esgotoPipe).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble());
                            Connector rabBaseConn = null;
                            foreach (Connector c12 in rabicho.ConnectorManager.Connectors)
                            {
                                if (c12.Origin.DistanceTo(ptBase2) < 0.01)
                                {
                                    rabBaseConn = c12;
                                }
                            }
                            try
                            {
                                doc.Create.NewTeeFitting(esgotoConn, rabBaseConn, stubBaseConn);
                            }
                            catch
                            {
                                try
                                {
                                    doc.Delete(rabicho.Id);
                                    doc.Create.NewElbowFitting(esgotoConn, stubBaseConn);
                                }
                                catch
                                {
                                }
                            }
                            try
                            {
                                doc.Create.NewElbowFitting(stubTopoConn, ventConn);
                            }
                            catch
                            {
                            }
                        }
                        catch (Exception ex6)
                        {
                            errosFitting.Add("Ventilação Stub: " + ex6.Message);
                        }
                    }
                }
                if (ApagarLinhas && LinhasComDNA != null)
                {
                    foreach (LinhaComDNA linha in LinhasComDNA)
                    {
                        try
                        {
                            doc.Delete(linha.ElementoRevit.Id);
                        }
                        catch
                        {
                        }
                    }
                }
                else if (!ApagarLinhas && LinhasComDNA != null)
                {
                    OverrideGraphicSettings ogsReset = new OverrideGraphicSettings();
                    foreach (LinhaComDNA linha2 in LinhasComDNA)
                    {
                        try
                        {
                            doc.ActiveView.SetElementOverrides(linha2.ElementoRevit.Id, ogsReset);
                        }
                        catch
                        {
                        }
                    }
                }
                t.Commit();
            }
            if (errosFitting.Count > 0)
            {
                string msg = string.Join("\n", errosFitting.Distinct());
                TaskDialog.Show("PipeMaster [M] — Alertas de Conexões", "A rede foi modelada, mas algumas conexões não puderam ser geradas automaticamente pelo Revit:\n\n" + msg);
            }
        }
        catch (Exception ex7)
        {
            TaskDialog.Show("PipeMaster [M] — Erro Crítico", ex7.Message);
        }
    }

    public string GetName()
    {
        return "Gerar Rede PipeMaster";
    }

    private void AjustarLuvaGeometrico(FamilyInstance fitting, Connector connTuboMontante, Document doc)
    {
        if (fitting?.MEPModel?.ConnectorManager == null || connTuboMontante == null)
        {
            return;
        }
        Connector fcMontante = null;
        double minDist = double.MaxValue;
        foreach (Connector c in fitting.MEPModel.ConnectorManager.Connectors)
        {
            if (c.ConnectorType != ConnectorType.Logical)
            {
                double d = c.Origin.DistanceTo(connTuboMontante.Origin);
                if (d < minDist)
                {
                    minDist = d;
                    fcMontante = c;
                }
            }
        }
        if (fcMontante == null)
        {
            return;
        }
        Transform T = fitting.GetTotalTransform();
        XYZ dirMontanteLocal = T.Inverse.OfVector(fcMontante.CoordinateSystem.BasisZ);
        int valorAlvo = ((dirMontanteLocal.DotProduct(BOLSA_AXIS_LOCAL) > 0.0) ? 1 : 0);
        foreach (Parameter param in fitting.Parameters)
        {
            string nome = param.Definition.Name.ToLower();
            if (nome.Contains("inverter") && nome.Contains("luva") && !param.IsReadOnly && param.StorageType == StorageType.Integer)
            {
                param.Set(valorAlvo);
                break;
            }
        }
    }

    private Connector AcharConnectorTuboMontante(FamilyInstance fitting, Document doc)
    {
        Connector melhor = null;
        double maiorZExterno = double.MinValue;
        if (fitting?.MEPModel?.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector fc in fitting.MEPModel.ConnectorManager.Connectors)
        {
            if (fc.ConnectorType == ConnectorType.Logical)
            {
                continue;
            }
            foreach (Connector refConn in fc.AllRefs)
            {
                if (refConn.Owner is Pipe { Location: LocationCurve lc })
                {
                    XYZ p0 = lc.Curve.GetEndPoint(0);
                    XYZ p1 = lc.Curve.GetEndPoint(1);
                    double d0 = fc.Origin.DistanceTo(p0);
                    double d1 = fc.Origin.DistanceTo(p1);
                    double zExterno = ((d0 >= d1) ? p0.Z : p1.Z);
                    if (zExterno > maiorZExterno)
                    {
                        maiorZExterno = zExterno;
                        melhor = refConn;
                    }
                }
            }
        }
        return melhor;
    }

    private void ForcarReducaoExcentrica(FamilyInstance transicao, Document doc)
    {
        if (transicao == null)
        {
            return;
        }
        FamilySymbol simboloExcentrico = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeFitting).Cast<FamilySymbol>()
            .FirstOrDefault((FamilySymbol fs) => fs.FamilyName.IndexOf("excentric", StringComparison.OrdinalIgnoreCase) >= 0 || fs.FamilyName.IndexOf("excêntric", StringComparison.OrdinalIgnoreCase) >= 0 || fs.Name.IndexOf("excentric", StringComparison.OrdinalIgnoreCase) >= 0 || fs.Name.IndexOf("excêntric", StringComparison.OrdinalIgnoreCase) >= 0 || fs.FamilyName.IndexOf("Reducao Excentrica", StringComparison.OrdinalIgnoreCase) >= 0);
        if (simboloExcentrico == null)
        {
            return;
        }
        if (!simboloExcentrico.IsActive)
        {
            try
            {
                simboloExcentrico.Activate();
                doc.Regenerate();
            }
            catch
            {
            }
        }
        try
        {
            transicao.ChangeTypeId(simboloExcentrico.Id);
            doc.Regenerate();
            Parameter pLigacao = transicao.LookupParameter("Ligação em Tubo");
            if (pLigacao != null && !pLigacao.IsReadOnly)
            {
                pLigacao.Set(0);
            }
        }
        catch
        {
        }
    }

    private double ObterInclinacao(Pipe pipe)
    {
        double dInterno = ((Element)pipe).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
        double dMm = Math.Round(UnitUtils.ConvertFromInternalUnits(dInterno, UnitTypeId.Millimeters), 1);
        if (MemoriaPipeMaster.InclinacoesPorDiametro.ContainsKey(dMm) && double.TryParse(MemoriaPipeMaster.InclinacoesPorDiametro[dMm].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return Math.Abs(val) / 100.0;
        }
        return (dMm >= 100.0) ? 0.01 : 0.02;
    }

    private void ForcarInclinacaoAposConexao(Pipe pipe, ElementId idAncora, Document doc)
    {
        if (pipe == null || !(pipe.Location is LocationCurve lc) || idAncora == ElementId.InvalidElementId)
        {
            return;
        }
        XYZ p0 = lc.Curve.GetEndPoint(0);
        XYZ p1 = lc.Curve.GetEndPoint(1);
        if (Math.Abs(p0.X - p1.X) < 0.01 && Math.Abs(p0.Y - p1.Y) < 0.01)
        {
            return;
        }
        Connector conFix = null;
        if (pipe.ConnectorManager != null)
        {
            foreach (Connector c in pipe.ConnectorManager.Connectors)
            {
                if (c.IsConnected)
                {
                    foreach (Connector cRef in c.AllRefs)
                    {
                        if (cRef.Owner.Id == idAncora)
                        {
                            conFix = c;
                            break;
                        }
                    }
                }
                if (conFix != null)
                {
                    break;
                }
            }
        }
        if (conFix == null)
        {
            return;
        }
        XYZ ptFix = conFix.Origin;
        bool p0Perto = p0.DistanceTo(ptFix) < p1.DistanceTo(ptFix);
        XYZ ptMovelOriginal = (p0Perto ? p1 : p0);
        double inc = ObterInclinacao(pipe);
        double dist2d = Math.Sqrt(Math.Pow(ptMovelOriginal.X - ptFix.X, 2.0) + Math.Pow(ptMovelOriginal.Y - ptFix.Y, 2.0));
        double sinal = ((ptMovelOriginal.Z >= ptFix.Z) ? 1.0 : (-1.0));
        double novoZ = ptFix.Z + dist2d * inc * sinal;
        XYZ novoPtMovel = new XYZ(ptMovelOriginal.X, ptMovelOriginal.Y, novoZ);
        try
        {
            lc.Curve = (p0Perto ? Line.CreateBound(ptFix, novoPtMovel) : Line.CreateBound(novoPtMovel, ptFix));
            doc.Regenerate();
        }
        catch
        {
        }
    }

    private static List<SegmentoEsgoto> ProcessarIntersecoes(List<LinhaComDNA> linhas, double tol)
    {
        List<SegmentoEsgoto> segs = new List<SegmentoEsgoto>();
        foreach (LinhaComDNA l in linhas)
        {
            Line c = (Line)l.ElementoRevit.GeometryCurve;
            segs.Add(new SegmentoEsgoto
            {
                A = c.GetEndPoint(0),
                B = c.GetEndPoint(1),
                Diametro = l.DiametroMm,
                Inclinacao = l.Inclinacao,
                IsVaso = l.IsVaso,
                IsVentilacao = l.IsVentilacao,
                SistemaId = l.SistemaId,
                TipoTuboId = l.TipoTuboId
            });
        }
        bool quebrou = true;
        while (quebrou)
        {
            quebrou = false;
            for (int i = 0; i < segs.Count; i++)
            {
                SegmentoEsgoto segI = segs[i];
                XYZ AB = segI.B - segI.A;
                double lenAB = AB.GetLength();
                if (lenAB < 0.1)
                {
                    continue;
                }
                for (int j = 0; j < segs.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    SegmentoEsgoto segJ = segs[j];
                    XYZ[] pts = new XYZ[2] { segJ.A, segJ.B };
                    for (int pIdx = 0; pIdx < 2; pIdx++)
                    {
                        XYZ P = pts[pIdx];
                        XYZ AP = P - segI.A;
                        double t = AP.DotProduct(AB) / AB.DotProduct(AB);
                        double distInício = t * lenAB;
                        double distFim = (1.0 - t) * lenAB;
                        if (!(t > 0.01) || !(t < 0.99) || !(distInício > 0.1) || !(distFim > 0.1))
                        {
                            continue;
                        }
                        XYZ proj = segI.A + AB * t;
                        if (Dist2D(P, proj) <= tol)
                        {
                            segs[i] = new SegmentoEsgoto
                            {
                                A = segI.A,
                                B = proj,
                                Diametro = segI.Diametro,
                                Inclinacao = segI.Inclinacao,
                                IsVaso = segI.IsVaso,
                                IsVentilacao = segI.IsVentilacao,
                                SistemaId = segI.SistemaId,
                                TipoTuboId = segI.TipoTuboId
                            };
                            segs.Add(new SegmentoEsgoto
                            {
                                A = proj,
                                B = segI.B,
                                Diametro = segI.Diametro,
                                Inclinacao = segI.Inclinacao,
                                IsVaso = segI.IsVaso,
                                IsVentilacao = segI.IsVentilacao,
                                SistemaId = segI.SistemaId,
                                TipoTuboId = segI.TipoTuboId
                            });
                            if (pIdx == 0)
                            {
                                segs[j] = new SegmentoEsgoto
                                {
                                    A = proj,
                                    B = segJ.B,
                                    Diametro = segJ.Diametro,
                                    Inclinacao = segJ.Inclinacao,
                                    IsVaso = segJ.IsVaso,
                                    IsVentilacao = segJ.IsVentilacao,
                                    SistemaId = segJ.SistemaId,
                                    TipoTuboId = segJ.TipoTuboId
                                };
                            }
                            else
                            {
                                segs[j] = new SegmentoEsgoto
                                {
                                    A = segJ.A,
                                    B = proj,
                                    Diametro = segJ.Diametro,
                                    Inclinacao = segJ.Inclinacao,
                                    IsVaso = segJ.IsVaso,
                                    IsVentilacao = segJ.IsVentilacao,
                                    SistemaId = segJ.SistemaId,
                                    TipoTuboId = segJ.TipoTuboId
                                };
                            }
                            quebrou = true;
                            break;
                        }
                    }
                    if (quebrou)
                    {
                        break;
                    }
                }
                if (quebrou)
                {
                    break;
                }
            }
        }
        return segs;
    }

    private static double Dist2D(XYZ a, XYZ b)
    {
        return Math.Sqrt(Math.Pow(a.X - b.X, 2.0) + Math.Pow(a.Y - b.Y, 2.0));
    }

    private static int ClusterMaisProximo(List<XYZ> cls, XYZ alvo)
    {
        int best = 0;
        double d = double.MaxValue;
        for (int i = 0; i < cls.Count; i++)
        {
            double di = Dist2D(cls[i], alvo);
            if (di < d)
            {
                d = di;
                best = i;
            }
        }
        return best;
    }

    private static Connector ConnectorMaisProximo(Pipe pipe, XYZ alvo)
    {
        if (pipe.ConnectorManager == null)
        {
            return null;
        }
        Connector best = null;
        double d = double.MaxValue;
        foreach (Connector c in pipe.ConnectorManager.Connectors)
        {
            if (c.ConnectorType != ConnectorType.Logical)
            {
                double di = c.Origin.DistanceTo(alvo);
                if (di < d)
                {
                    d = di;
                    best = c;
                }
            }
        }
        return best;
    }

    private static ElementId ObterNivelDaVista(Document doc, View vista)
    {
        if (vista != null)
        {
            Parameter paramNivel = ((Element)vista).get_Parameter(BuiltInParameter.PLAN_VIEW_LEVEL);
            if (paramNivel != null && paramNivel.AsElementId() != ElementId.InvalidElementId)
            {
                return paramNivel.AsElementId();
            }
            if (vista.GenLevel != null)
            {
                return vista.GenLevel.Id;
            }
        }
        if (vista?.LevelId != null && vista.LevelId != ElementId.InvalidElementId)
        {
            return vista.LevelId;
        }
        return (from Level l in new FilteredElementCollector(doc).OfClass(typeof(Level))
                orderby l.Elevation
                select l).FirstOrDefault()?.Id ?? ElementId.InvalidElementId;
    }
}
