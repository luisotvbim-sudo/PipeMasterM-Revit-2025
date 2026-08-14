using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

public class FinalizarLancamentoHandler : IExternalEventHandler
{
    public class FiltroTubo : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Pipe;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    public class FiltroDeCaixas : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null && (elem.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PlumbingFixtures)) || elem.Category.Id.Equals(new ElementId(BuiltInCategory.OST_MechanicalEquipment)) || elem.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PipeAccessory)));
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public void Execute(UIApplication uiapp)
    {
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;
        try
        {
            JigLancamentoManager.DesmontarJigSeguro();
            using Transaction t = new Transaction(doc, "PipeMaster: Lançamento " + JigLancamentoManager.EtapaAtual);
            t.Start();
            if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Vaso_Ponto3)
            {
                ModelarVaso(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.CaixaSifonada)
            {
                ModelarCaixa(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Caixa_EscolherRota)
            {
                ModelarCaixaIndependente(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Pia_EscolherRota)
            {
                ModelarPia(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Maquina_EscolherRota)
            {
                ModelarMaquina(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Lavatorio_Caixa)
            {
                ModelarLavatorio(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Chuveiro_Caixa)
            {
                ModelarChuveiro(doc);
            }
            else if (JigLancamentoManager.EtapaAtual == JigLancamentoManager.Etapas.Ventilacao_EscolherRota)
            {
                ModelarVentilacao(doc, uidoc);
            }
            t.Commit();
        }
        catch (Exception ex)
        {
            string msg = ex.Message;
            string titulo = "Erro na modelagem";
            if (msg.Contains("too close") || msg.Contains("minimum length") || msg.Contains("too short"))
            {
                msg = "A distância entre os pontos clicados ou as configurações de altura (Ex: diferença entre a cota do piso e do coletor) ? muito curta para o Revit conseguir desenhar o tubo ou encaixar a conexão.\n\nTente clicar um pouco mais afastado ou revise as alturas informadas na janela do plugin.";
                titulo = "Distância Curta ou Altura Insuficiente";
            }
            else if (msg.Contains("finite number"))
            {
                msg = "Ocorreu um erro matemático ao tentar calcular as coordenadas da tubulação (provavelmente o clique foi feito num local impossível de projetar em 3D, ou colinear demais).\n\nTente refazer o traçado clicando numa angulação ou distância ligeiramente diferente.";
                titulo = "Erro Matemático de Coordenadas";
            }
            else if (msg.Contains("direction is parallel"))
            {
                msg = "Não ? possível criar a conexão pois as direções dos tubos estão paralelas sem espaço suficiente para a peça.";
                titulo = "Direções Paralelas";
            }
            else
            {
                msg = "Erro interno do Revit: " + msg + "\n\n" + ex.StackTrace;
            }
            TaskDialog.Show("PipeMaster [M] - " + titulo, msg);
            return;
        }
        AvancarParaProximaEtapa(uiapp);
    }

    public void AvancarParaProximaEtapa(UIApplication uiapp)
    {
        ConfigLancamentoAuto config = JigLancamentoManager.Cfg;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        JigLancamentoManager.Etapas etapaAtual = JigLancamentoManager.EtapaAtual;
        if (config.IniciarVentilacao)
        {
            if (etapaAtual < JigLancamentoManager.Etapas.Ventilacao_SelecionarColuna)
            {
                try
                {
                    Reference refColuna = uidoc.Selection.PickObject(ObjectType.Element, new FiltroTubo(), "PipeMaster [Ventilação]: Selecione a Coluna de Ventilação (CV) existente.");
                    if (uidoc.Document.GetElement(refColuna) is Pipe coluna)
                    {
                        JigLancamentoManager.TuboDestino = coluna;
                        XYZ p0 = (coluna.Location as LocationCurve).Curve.GetEndPoint(0);
                        XYZ p1 = (coluna.Location as LocationCurve).Curve.GetEndPoint(1);
                        XYZ baseColuna = ((p0.Z < p1.Z) ? p0 : p1);
                        if (config.OpcaoVentilacao == 1)
                        {
                            JigLancamentoManager.IniciarJigVentilacaoCavalete(uiapp, new XYZ(baseColuna.X, baseColuna.Y, JigLancamentoManager.ZPreview));
                        }
                        else
                        {
                            JigLancamentoManager.IniciarJigVentilacao(uiapp, baseColuna);
                        }
                    }
                    return;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    JigLancamentoManager.DesmontarJigSeguro();
                    return;
                }
            }
            JigLancamentoManager.DesmontarJigSeguro();
            new JanelaSucessoPremium("Sucesso", "Rede de Ventilação concluída com sucesso!").ShowDialog();
            return;
        }
        if (etapaAtual < JigLancamentoManager.Etapas.CaixaSifonada && config.TemCaixaSifonada)
        {
            try
            {
                Reference refCaixa = uidoc.Selection.PickObject(ObjectType.Element, new FiltroDeCaixas(), "PipeMaster [Caixa Sifonada]: Selecione a Caixa Sifonada / Ralo");
                if (uidoc.Document.GetElement(refCaixa) is FamilyInstance caixa)
                {
                    Connector conn = ObterConectorCaixa(caixa);
                    if (conn != null)
                    {
                        JigLancamentoManager.ConectorCaixa = conn;
                        XYZ dirOrig = conn.CoordinateSystem.BasisZ;
                        double angRad = Math.Atan2(dirOrig.Y, dirOrig.X);
                        double snapRad = Math.Round(angRad / (Math.PI / 4.0)) * (Math.PI / 4.0);
                        if (Math.Abs(angRad - snapRad) < Math.PI / 18.0)
                        {
                            JigLancamentoManager.DirCaixa = new XYZ(Math.Cos(snapRad), Math.Sin(snapRad), dirOrig.Z).Normalize();
                        }
                        else
                        {
                            JigLancamentoManager.DirCaixa = dirOrig;
                        }
                        JigLancamentoManager.IniciarJigCaixa(uiapp, conn.Origin);
                    }
                    else if (caixa.Location is LocationPoint lp)
                    {
                        JigLancamentoManager.ConectorCaixa = null;
                        JigLancamentoManager.DirCaixa = XYZ.Zero;
                        JigLancamentoManager.IniciarJigCaixa(uiapp, lp.Point);
                    }
                }
                return;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                JigLancamentoManager.DesmontarJigSeguro();
                return;
            }
        }
        if (etapaAtual < JigLancamentoManager.Etapas.Pia_Destino && config.TemPia)
        {
            try
            {
                XYZ ptParede = uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Intersections, "PipeMaster [Pia] 1/3: Clique na FACE DA PAREDE onde o tubo vai descer.");
                XYZ ptDirecao = uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Intersections, "PipeMaster [Pia] 2/3: Clique indicando a DIREÇÃO apontando PARA FORA da parede.");
                XYZ vetorDirecao = ptDirecao - ptParede;
                XYZ dirFace = ((Math.Abs(vetorDirecao.X) > Math.Abs(vetorDirecao.Y)) ? new XYZ(Math.Sign(vetorDirecao.X), 0.0, 0.0) : new XYZ(0.0, Math.Sign(vetorDirecao.Y), 0.0));
                JigLancamentoManager.IniciarJigPia(uiapp, ptParede, dirFace);
                return;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                JigLancamentoManager.DesmontarJigSeguro();
                return;
            }
        }
        if (etapaAtual < JigLancamentoManager.Etapas.Maquina_Destino && config.TemMaquina)
        {
            try
            {
                XYZ ptParede2 = uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Intersections, "PipeMaster [Máquina de Lavar] 1/3: Clique na FACE DA PAREDE onde o tubo vai descer.");
                XYZ ptDirecao2 = uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Intersections, "PipeMaster [Máquina de Lavar] 2/3: Clique indicando a DIREÇÃO apontando PARA FORA da parede.");
                XYZ vetorDirecao2 = ptDirecao2 - ptParede2;
                XYZ dirFace2 = ((Math.Abs(vetorDirecao2.X) > Math.Abs(vetorDirecao2.Y)) ? new XYZ(Math.Sign(vetorDirecao2.X), 0.0, 0.0) : new XYZ(0.0, Math.Sign(vetorDirecao2.Y), 0.0));
                JigLancamentoManager.IniciarJigMaquina(uiapp, ptParede2, dirFace2);
                return;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                JigLancamentoManager.DesmontarJigSeguro();
                return;
            }
        }
        if (etapaAtual < JigLancamentoManager.Etapas.Lavatorio_Caixa && config.TemLavatorio)
        {
            try
            {
                XYZ ptParede3 = uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Intersections, "PipeMaster [Lavatório] 1/3: Clique na FACE DA PAREDE onde o tubo vai descer.");
                XYZ ptDirecao3 = uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Intersections, "PipeMaster [Lavatório] 2/3: Clique indicando a DIREÇÃO apontando PARA FORA da parede.");
                XYZ vetorDirecao3 = ptDirecao3 - ptParede3;
                XYZ dirFace3 = ((Math.Abs(vetorDirecao3.X) > Math.Abs(vetorDirecao3.Y)) ? new XYZ(Math.Sign(vetorDirecao3.X), 0.0, 0.0) : new XYZ(0.0, Math.Sign(vetorDirecao3.Y), 0.0));
                JigLancamentoManager.IniciarJigLavatorio(uiapp, ptParede3, dirFace3);
                return;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                JigLancamentoManager.DesmontarJigSeguro();
                return;
            }
        }
        if (etapaAtual < JigLancamentoManager.Etapas.Chuveiro_Caixa && config.TemChuveiro)
        {
            try
            {
                Reference refRalo = uidoc.Selection.PickObject(ObjectType.Element, new FiltroDeCaixas(), "PipeMaster [Ralo do Chuveiro]: Selecione o Ralo");
                if (uidoc.Document.GetElement(refRalo) is FamilyInstance ralo)
                {
                    Connector conn2 = ObterConectorCaixa(ralo, JigLancamentoManager.Cfg.BloquearConectoresHorizontais);
                    if (conn2 != null)
                    {
                        JigLancamentoManager.ConectorChuveiro = conn2;
                        JigLancamentoManager.IniciarJigChuveiro(uiapp, conn2.Origin, conn2.CoordinateSystem.BasisZ);
                    }
                    else if (ralo.Location is LocationPoint lp2)
                    {
                        JigLancamentoManager.ConectorChuveiro = null;
                        JigLancamentoManager.IniciarJigChuveiro(uiapp, lp2.Point, XYZ.Zero);
                    }
                }
                return;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                JigLancamentoManager.DesmontarJigSeguro();
                return;
            }
        }
        JigLancamentoManager.DesmontarJigSeguro();
        new JanelaSucessoPremium("Sucesso", "Lançamento do Banheiro concluído com sucesso!").ShowDialog();
    }

    public static XYZ DeterminarLadoChicote(XYZ dirPipe, XYZ arrDir, XYZ ptDestino, XYZ ptCV, bool rotacaoTe90)
    {
        XYZ perp = new XYZ(0.0 - dirPipe.Y, dirPipe.X, 0.0).Normalize();
        XYZ dirToCV2D = new XYZ(ptCV.X - ptDestino.X, ptCV.Y - ptDestino.Y, 0.0);
        XYZ dirToCV = ((dirToCV2D.GetLength() < 1E-06) ? perp : dirToCV2D.Normalize());
        if (!rotacaoTe90)
        {
            if (JigLancamentoManager.PtMouseRota != null)
            {
                XYZ vecMouse = new XYZ(JigLancamentoManager.PtMouseRota.X - ptDestino.X, JigLancamentoManager.PtMouseRota.Y - ptDestino.Y, 0.0);
                return (perp.DotProduct(vecMouse) < 0.0) ? (-perp) : perp;
            }
            return (perp.DotProduct(dirToCV) < 0.0) ? (-perp) : perp;
        }
        if (Math.Abs(arrDir.DotProduct(perp)) > 0.5)
        {
            return -arrDir;
        }
        return (perp.DotProduct(dirToCV) < 0.0) ? (-perp) : perp;
    }

    private void ModelarVentilacao(Document doc, UIDocument uidoc)
    {
        XYZ ptCV = JigLancamentoManager.Pt1;
        XYZ clickPoint = JigLancamentoManager.PtClickFinal;
        XYZ ptMouseRota = JigLancamentoManager.PtMouseRota;
        ConfigLancamentoAuto cfg = JigLancamentoManager.Cfg;
        Pipe tuboDestino = JigLancamentoManager.TuboDestino;
        if (tuboDestino == null)
        {
            return;
        }
        ElementId sistemaVentId = cfg.SistemaId;
        PipingSystemType ventSysType = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault((PipingSystemType s) => s.SystemClassification == MEPSystemClassification.Vent);
        if (ventSysType != null)
        {
            sistemaVentId = ventSysType.Id;
        }
        FilteredElementCollector col = new FilteredElementCollector(doc, uidoc.ActiveView.Id);
        List<Pipe> pipes = col.OfClass(typeof(Pipe)).Cast<Pipe>().ToList();
        Pipe closestPipe = null;
        double minDist = double.MaxValue;
        XYZ ptIntersect = XYZ.Zero;
        foreach (Pipe p in pipes)
        {
            if (!(p.Id == tuboDestino.Id) && p.Location is LocationCurve lc)
            {
                XYZ p2 = lc.Curve.GetEndPoint(0);
                XYZ p3 = lc.Curve.GetEndPoint(1);
                XYZ p0_2D = new XYZ(p2.X, p2.Y, 0.0);
                XYZ p1_2D = new XYZ(p3.X, p3.Y, 0.0);
                XYZ click_2D = new XYZ(clickPoint.X, clickPoint.Y, 0.0);
                double d = ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(click_2D, p0_2D, p1_2D);
                if (d < minDist && d < 1.0)
                {
                    minDist = d;
                    closestPipe = p;
                    XYZ dir = (p1_2D - p0_2D).Normalize();
                    double proj = (click_2D - p0_2D).DotProduct(dir);
                    XYZ proj2D = p0_2D + dir * proj;
                    double len = p0_2D.DistanceTo(p1_2D);
                    double z = ((len > 0.0001) ? (p2.Z + (p3.Z - p2.Z) * (proj / len)) : p2.Z);
                    ptIntersect = new XYZ(proj2D.X, proj2D.Y, z);
                }
            }
        }
        if (closestPipe == null)
        {
            TaskDialog.Show("PipeMaster [M]", "Nenhum tubo de esgoto encontrado proximo ao clique.");
            return;
        }
        XYZ dirPipe = ((closestPipe.Location as LocationCurve).Curve.GetEndPoint(1) - (closestPipe.Location as LocationCurve).Curve.GetEndPoint(0)).Normalize();
        XYZ dirToCV_2D = new XYZ(ptCV.X - ptIntersect.X, ptCV.Y - ptIntersect.Y, 0.0);
        dirToCV_2D = ((!dirToCV_2D.IsAlmostEqualTo(XYZ.Zero)) ? dirToCV_2D.Normalize() : new XYZ(1.0, 0.0, 0.0));
        XYZ perpHorizontal;
        if (Math.Abs(dirPipe.Z) > 0.9)
        {
            perpHorizontal = JigLancamentoManager.DirCaixa ?? dirToCV_2D;
        }
        else
        {
            perpHorizontal = new XYZ(0.0 - dirPipe.Y, dirPipe.X, 0.0).Normalize();
            if (!cfg.RotacaoTe90)
            {
                if (perpHorizontal.DotProduct(dirToCV_2D) < 0.0)
                {
                    perpHorizontal = -perpHorizontal;
                }
            }
            else if (cfg.OpcaoVentilacao == 1 && JigLancamentoManager.DirCaixa != null)
            {
                double dotDirCaixa = JigLancamentoManager.DirCaixa.DotProduct(perpHorizontal);
                if (Math.Abs(dotDirCaixa) > 0.5)
                {
                    if (dotDirCaixa < 0.0)
                    {
                        perpHorizontal = -perpHorizontal;
                    }
                }
                else if (perpHorizontal.DotProduct(dirToCV_2D) > 0.0)
                {
                    perpHorizontal = -perpHorizontal;
                }
            }
            else if (perpHorizontal.DotProduct(dirToCV_2D) < 0.0)
            {
                perpHorizontal = -perpHorizontal;
            }
        }
        Curve curvaDestinoCol = (tuboDestino.Location as LocationCurve).Curve;
        XYZ p0Col = curvaDestinoCol.GetEndPoint(0);
        XYZ p1Col = curvaDestinoCol.GetEndPoint(1);
        XYZ dirCol = (p1Col - p0Col).Normalize();
        double tCol = (ptIntersect.Z - p0Col.Z) / dirCol.Z;
        XYZ ptBaseCV_Exato = p0Col + dirCol * tCol;
        XYZ ptCorner = null;
        XYZ ptBaseCV = ptBaseCV_Exato;
        Pipe tuboHor1 = null;
        Pipe tuboHor2 = null;
        XYZ trueUpForCand = perpHorizontal.CrossProduct(dirPipe).Normalize();
        if (trueUpForCand.Z < 0.0)
        {
            trueUpForCand = -trueUpForCand;
        }
        XYZ vecBase = ptBaseCV - ptIntersect;
        XYZ vecBaseXY = new XYZ(vecBase.X, vecBase.Y, 0.0);
        XYZ chegadaXY = (vecBaseXY.IsAlmostEqualTo(XYZ.Zero) ? new XYZ(1.0, 0.0, 0.0) : vecBaseXY.Normalize());
        XYZ bestPerp = DeterminarLadoChicote(dirPipe, chegadaXY, ptIntersect, ptCV, cfg.RotacaoTe90);
        if (cfg.OpcaoVentilacao == 1)
        {
            XYZ vecToPrumada = ((JigLancamentoManager.DirCaixa == null || JigLancamentoManager.DirCaixa.IsAlmostEqualTo(XYZ.Zero)) ? new XYZ(ptBaseCV.X - ptIntersect.X, ptBaseCV.Y - ptIntersect.Y, 0.0).Normalize() : JigLancamentoManager.DirCaixa);
            double diamPrumada = ((Element)tuboDestino).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
            double offsetBase = 0.23786089238845143 + diamPrumada / 2.0;
            ptBaseCV = ptBaseCV_Exato + vecToPrumada * offsetBase;
        }
        XYZ vecToNovoCV = ptBaseCV - ptIntersect;
        double distPerp = vecToNovoCV.DotProduct(bestPerp);
        double lReq = distPerp * Math.Sqrt(2.0);
        double length = 0.328084;
        XYZ dirBranch3D;
        if (Math.Abs(dirPipe.Z) > 0.9)
        {
            dirBranch3D = bestPerp;
        }
        else if (cfg.RotacaoTe90)
        {
            dirBranch3D = trueUpForCand;
            length = 0.393701;
        }
        else
        {
            dirBranch3D = (trueUpForCand + bestPerp).Normalize();
            if (cfg.Joelho45NoChicote)
            {
                length = 0.393701;
            }
            else if (distPerp > 0.0 && lReq <= 3.28)
            {
                length = Math.Max(lReq, 0.164042);
                if (length > lReq + 0.001)
                {
                    double distExtra = (length - lReq) / Math.Sqrt(2.0);
                    ptBaseCV += bestPerp * distExtra;
                }
            }
            else if (distPerp <= 0.0)
            {
                length = 0.492126;
            }
        }
        XYZ ptStubFim = ptIntersect + dirBranch3D * length;
        double diamVent = 125.0 / 762.0;
        Pipe stub = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptIntersect, ptStubFim);
        ((Element)stub).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
        ElementId novoTuboId = PlumbingUtils.BreakCurve(doc, closestPipe.Id, ptIntersect);
        Pipe novoTubo = doc.GetElement(novoTuboId) as Pipe;
        FamilyInstance te = null;
        try
        {
            Connector c1 = ComandoLancamentoAutomatico.GetConnectorClosestTo(closestPipe, ptIntersect);
            Connector c2 = ComandoLancamentoAutomatico.GetConnectorClosestTo(novoTubo, ptIntersect);
            Connector c3 = ComandoLancamentoAutomatico.GetConnectorClosestTo(stub, ptIntersect);
            if (c1 != null && c2 != null && c3 != null)
            {
                te = doc.Create.NewTeeFitting(c1, c2, c3);
                if (te != null)
                {
                    doc.Regenerate();
                    foreach (Parameter param in te.Parameters)
                    {
                        if (param.Definition.Name.Contains("Inverter Sentido da Luva") && !param.IsReadOnly)
                        {
                            try
                            {
                                param.Set(1);
                            }
                            catch
                            {
                            }
                        }
                        if (param.Definition.Name.Contains("Ligacao em Conexao") && !param.IsReadOnly)
                        {
                            try
                            {
                                param.Set(0);
                            }
                            catch
                            {
                            }
                        }
                    }
                    doc.Regenerate();
                    Func<FamilyInstance, XYZ, Connector> getConn = delegate (FamilyInstance f, XYZ pt)
                    {
                        Connector result = null;
                        double num = double.MaxValue;
                        if (f?.MEPModel?.ConnectorManager != null)
                        {
                            foreach (Connector connector in f.MEPModel.ConnectorManager.Connectors)
                            {
                                double num2 = connector.Origin.DistanceTo(pt);
                                if (num2 < num)
                                {
                                    num = num2;
                                    result = connector;
                                }
                            }
                        }
                        return result;
                    };
                    try
                    {
                        Connector t1 = getConn(te, c1.Origin);
                        Connector t2 = getConn(te, c2.Origin);
                        Connector t3 = getConn(te, c3.Origin);
                        if (t1 != null && !c1.IsConnected)
                        {
                            c1.ConnectTo(t1);
                        }
                        if (t2 != null && !c2.IsConnected)
                        {
                            c2.ConnectTo(t2);
                        }
                        if (t3 != null && !c3.IsConnected)
                        {
                            c3.ConnectTo(t3);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
        XYZ ptOriginalStubFim = ptStubFim;
        double dPar = Math.Abs(ptBaseCV.X - ptStubFim.X);
        double dPerp = Math.Abs(ptBaseCV.Y - ptStubFim.Y);
        double minD = Math.Min(dPar, dPerp);
        bool isOpcaoV0Te90 = cfg.OpcaoVentilacao == 0 && cfg.RotacaoTe90;
        double limiteUnico = (isOpcaoV0Te90 ? 0.29527 : 0.05);
        bool forcarHipotenusa = JigLancamentoManager.RotaEscolhida == 0 || dPar < limiteUnico || dPerp < limiteUnico;
        bool usar45Graus = (!cfg.RotacaoTe90 && cfg.Joelho45NoChicote) || (isOpcaoV0Te90 && minD >= 0.29527 && minD < 0.45931);
        if (!forcarHipotenusa && (JigLancamentoManager.RotaEscolhida == 1 || JigLancamentoManager.RotaEscolhida == 2 || (!cfg.RotacaoTe90 && cfg.Joelho45NoChicote)))
        {
            XYZ pDir = new XYZ(0.0 - dirPipe.Y, dirPipe.X, 0.0).Normalize();
            if (new XYZ(ptBaseCV.X - ptIntersect.X, ptBaseCV.Y - ptIntersect.Y, 0.0).DotProduct(pDir) < 0.0)
            {
                pDir = -pDir;
            }
            if (!usar45Graus)
            {
                ptCorner = ((JigLancamentoManager.RotaEscolhida != 1) ? new XYZ(ptBaseCV.X, ptStubFim.Y, ptStubFim.Z) : new XYZ(ptStubFim.X, ptBaseCV.Y, ptStubFim.Z));
            }
            else
            {
                List<XYZ> validCorners = new List<XYZ>();
                XYZ D1 = new XYZ(pDir.X * 0.707106781 - pDir.Y * 0.707106781, pDir.X * 0.707106781 + pDir.Y * 0.707106781, 0.0);
                XYZ D2 = new XYZ(pDir.X * 0.707106781 + pDir.Y * 0.707106781, (0.0 - pDir.X) * 0.707106781 + pDir.Y * 0.707106781, 0.0);
                XYZ delta = new XYZ(ptBaseCV.X - ptStubFim.X, ptBaseCV.Y - ptStubFim.Y, 0.0);
                double cross1 = pDir.X * D1.Y - pDir.Y * D1.X;
                if (Math.Abs(cross1) > 0.001)
                {
                    double t4 = (delta.X * D1.Y - delta.Y * D1.X) / cross1;
                    double u1 = (pDir.X * delta.Y - pDir.Y * delta.X) / cross1;
                    if (t4 >= 0.05 && u1 >= 0.05)
                    {
                        validCorners.Add(ptStubFim + pDir * t4);
                    }
                }
                double cross2 = pDir.X * D2.Y - pDir.Y * D2.X;
                if (Math.Abs(cross2) > 0.001)
                {
                    double t5 = (delta.X * D2.Y - delta.Y * D2.X) / cross2;
                    double u2 = (pDir.X * delta.Y - pDir.Y * delta.X) / cross2;
                    if (t5 >= 0.05 && u2 >= 0.05)
                    {
                        validCorners.Add(ptStubFim + pDir * t5);
                    }
                }
                List<XYZ> fallbackCorners = new List<XYZ>();
                double crossD1_pDir = D1.X * pDir.Y - D1.Y * pDir.X;
                if (Math.Abs(crossD1_pDir) > 0.001)
                {
                    double offset_d1 = (delta.X * pDir.Y - delta.Y * pDir.X) / crossD1_pDir;
                    double offset_d2 = (D1.X * delta.Y - D1.Y * delta.X) / crossD1_pDir;
                    if (offset_d1 >= 0.05 && offset_d2 >= 0.05)
                    {
                        fallbackCorners.Add(ptStubFim + D1 * offset_d1);
                    }
                }
                double crossD2_pDir = D2.X * pDir.Y - D2.Y * pDir.X;
                if (Math.Abs(crossD2_pDir) > 0.001)
                {
                    double offset_d3 = (delta.X * pDir.Y - delta.Y * pDir.X) / crossD2_pDir;
                    double offset_d4 = (D2.X * delta.Y - D2.Y * delta.X) / crossD2_pDir;
                    if (offset_d3 >= 0.05 && offset_d4 >= 0.05)
                    {
                        fallbackCorners.Add(ptStubFim + D2 * offset_d3);
                    }
                }
                validCorners.AddRange(fallbackCorners);
                if (validCorners.Count > 0)
                {
                    ptCorner = ((JigLancamentoManager.RotaEscolhida != 1 && JigLancamentoManager.RotaEscolhida != 0) ? ((validCorners.Count > 1) ? validCorners[1] : validCorners[0]) : validCorners[0]);
                    ptCorner = new XYZ(ptCorner.X, ptCorner.Y, ptStubFim.Z);
                }
                else
                {
                    double proj2 = new XYZ(ptBaseCV.X - ptStubFim.X, ptBaseCV.Y - ptStubFim.Y, 0.0).DotProduct(pDir);
                    if (proj2 < 0.05)
                    {
                        proj2 = 0.05;
                    }
                    ptCorner = ptStubFim + pDir * proj2;
                }
            }
            double dist1 = new XYZ(ptCorner.X - ptStubFim.X, ptCorner.Y - ptStubFim.Y, 0.0).GetLength();
            double dist2 = new XYZ(ptBaseCV.X - ptCorner.X, ptBaseCV.Y - ptCorner.Y, 0.0).GetLength();
            double distTotal = dist1 + dist2;
            if (distTotal < 0.01)
            {
                distTotal = 0.01;
            }
            double dz1 = dist1 / distTotal * (distTotal * 0.01);
            double dz2 = dist2 / distTotal * (distTotal * 0.01);
            XYZ ptCorner3D = new XYZ(ptCorner.X, ptCorner.Y, ptStubFim.Z + dz1);
            if (dist1 > 0.05)
            {
                tuboHor1 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptStubFim, ptCorner3D);
                ((Element)tuboHor1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
            }
            if (dist2 > 0.05)
            {
                XYZ ptFimHor2 = new XYZ(ptBaseCV.X, ptBaseCV.Y, ptCorner3D.Z + dz2);
                tuboHor2 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptCorner3D, ptFimHor2);
                ((Element)tuboHor2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
                ptBaseCV = new XYZ(ptBaseCV.X, ptBaseCV.Y, ptFimHor2.Z);
                ptCorner = ptCorner3D;
            }
            else
            {
                tuboHor2 = tuboHor1;
                ptCorner = ptCorner3D;
                ptBaseCV = new XYZ(ptBaseCV.X, ptBaseCV.Y, ptCorner3D.Z);
            }
            if (tuboHor1 == null && tuboHor2 != null)
            {
                tuboHor1 = tuboHor2;
            }
            if (tuboHor1 == null)
            {
                tuboHor1 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptStubFim, ptCorner3D);
                ((Element)tuboHor1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
                tuboHor2 = tuboHor1;
            }
        }
        else if (cfg.RotacaoTe90 || forcarHipotenusa)
        {
            double distXY = new XYZ(ptBaseCV.X - ptStubFim.X, ptBaseCV.Y - ptStubFim.Y, 0.0).GetLength();
            if (distXY < 0.1)
            {
                distXY = 0.1;
            }
            XYZ dirHor = new XYZ(ptBaseCV.X - ptStubFim.X, ptBaseCV.Y - ptStubFim.Y, 0.0).Normalize();
            double dz3 = distXY * 0.01;
            XYZ ptBaseCV_XY = new XYZ(ptStubFim.X, ptStubFim.Y, 0.0) + dirHor * distXY;
            ptBaseCV = new XYZ(ptBaseCV_XY.X, ptBaseCV_XY.Y, ptStubFim.Z + dz3);
            tuboHor1 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptStubFim, ptBaseCV);
            ((Element)tuboHor1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
        }
        else
        {
            XYZ vecToBase = new XYZ(ptBaseCV.X - ptStubFim.X, ptBaseCV.Y - ptStubFim.Y, 0.0);
            XYZ dirParallel = ((dirPipe.DotProduct(vecToBase) > 0.0) ? dirPipe : (-dirPipe));
            double distParallel = vecToBase.DotProduct(dirParallel);
            double dz4 = distParallel * 0.01;
            XYZ ptCorner_XY = new XYZ(ptStubFim.X, ptStubFim.Y, 0.0) + dirParallel * distParallel;
            ptCorner = new XYZ(ptCorner_XY.X, ptCorner_XY.Y, ptStubFim.Z + dz4);
            if (ptStubFim.DistanceTo(ptCorner) > 0.05)
            {
                tuboHor1 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptStubFim, ptCorner);
                ((Element)tuboHor1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
            }
            else
            {
                ptCorner = ptStubFim;
                dz4 = 0.0;
            }
            XYZ dirPerp2 = bestPerp;
            XYZ vecCornerToBase = new XYZ(ptBaseCV.X - ptCorner.X, ptBaseCV.Y - ptCorner.Y, 0.0);
            if (vecCornerToBase.DotProduct(dirPerp2) < 0.0)
            {
                dirPerp2 = -dirPerp2;
            }
            double distPerp2 = vecCornerToBase.DotProduct(dirPerp2);
            double dz5 = distPerp2 * 0.01;
            if (distPerp2 > 0.05)
            {
                XYZ ptBaseCV_XY2 = new XYZ(ptCorner.X, ptCorner.Y, 0.0) + dirPerp2 * distPerp2;
                XYZ ptFimHor3 = new XYZ(ptBaseCV_XY2.X, ptBaseCV_XY2.Y, ptCorner.Z + dz5);
                tuboHor2 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptCorner, ptFimHor3);
                ((Element)tuboHor2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
                ptBaseCV = new XYZ(ptBaseCV.X, ptBaseCV.Y, ptFimHor3.Z);
            }
            else
            {
                tuboHor2 = tuboHor1;
                ptCorner = new XYZ(ptBaseCV.X, ptBaseCV.Y, ptStubFim.Z + dz4);
                if (tuboHor1 == null)
                {
                    tuboHor1 = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptStubFim, ptCorner);
                    ((Element)tuboHor1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
                    tuboHor2 = tuboHor1;
                }
                ptBaseCV = new XYZ(ptBaseCV.X, ptBaseCV.Y, ptCorner.Z);
            }
        }
        Func<FamilyInstance, XYZ, Connector> getConn2 = delegate (FamilyInstance f, XYZ pt)
        {
            Connector result = null;
            double num = double.MaxValue;
            if (f?.MEPModel?.ConnectorManager != null)
            {
                foreach (Connector connector in f.MEPModel.ConnectorManager.Connectors)
                {
                    double num2 = connector.Origin.DistanceTo(pt);
                    if (num2 < num)
                    {
                        num = num2;
                        result = connector;
                    }
                }
            }
            return result;
        };
        try
        {
            Connector c4 = ComandoLancamentoAutomatico.GetConnectorClosestTo(stub, ptOriginalStubFim);
            Connector c5 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboHor1, ptStubFim);
            FamilyInstance elbow1 = doc.Create.NewElbowFitting(c4, c5);
            if (elbow1 != null)
            {
                doc.Regenerate();
                foreach (Parameter p4 in elbow1.Parameters)
                {
                    if (cfg.RotacaoTe90 && p4.Definition.Name.Contains("Inverter Sentido da Luva") && !p4.IsReadOnly)
                    {
                        try
                        {
                            p4.Set(1);
                        }
                        catch
                        {
                        }
                    }
                    if ((p4.Definition.Name.Contains("Ligacao em Conexao") || p4.Definition.Name.Contains("Ligação em Conexão")) && !p4.IsReadOnly)
                    {
                        try
                        {
                            p4.Set((!usar45Graus) ? 1 : 0);
                        }
                        catch
                        {
                        }
                    }
                }
                doc.Regenerate();
                try
                {
                    Connector e1 = getConn2(elbow1, c4.Origin);
                    Connector e2 = getConn2(elbow1, c5.Origin);
                    if (e1 != null && !c4.IsConnected)
                    {
                        c4.ConnectTo(e1);
                    }
                    if (e2 != null && !c5.IsConnected)
                    {
                        c5.ConnectTo(e2);
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        if (tuboHor2 != null && tuboHor1 != null && tuboHor2.Id != tuboHor1.Id)
        {
            try
            {
                Connector c6 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboHor1, ptCorner);
                Connector c7 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboHor2, ptCorner);
                FamilyInstance elbowInter = doc.Create.NewElbowFitting(c6, c7);
                if (elbowInter != null)
                {
                    doc.Regenerate();
                    try
                    {
                        Connector e3 = getConn2(elbowInter, c6.Origin);
                        Connector e4 = getConn2(elbowInter, c7.Origin);
                        if (e3 != null && !c6.IsConnected)
                        {
                            c6.ConnectTo(e3);
                        }
                        if (e4 != null && !c7.IsConnected)
                        {
                            c7.ConnectTo(e4);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
        Pipe lastHorPipe = ((tuboHor2 != null) ? tuboHor2 : tuboHor1);
        XYZ ptBaseCV_Real = ptBaseCV;
        if (cfg.OpcaoVentilacao == 0)
        {
            try
            {
                Curve curva0 = (tuboDestino.Location as LocationCurve).Curve;
                XYZ p5 = curva0.GetEndPoint(0);
                XYZ p6 = curva0.GetEndPoint(1);
                bool p0IsBottom = p5.Z < p6.Z;
                XYZ ptTopo = (p0IsBottom ? p6 : p5);
                if (ptTopo.Z > ptBaseCV_Real.Z + 0.05)
                {
                    Line novaLinha = (p0IsBottom ? Line.CreateBound(ptBaseCV_Real, ptTopo) : Line.CreateBound(ptTopo, ptBaseCV_Real));
                    (tuboDestino.Location as LocationCurve).Curve = novaLinha;
                }
            }
            catch
            {
            }
            doc.Regenerate();
            try
            {
                Connector c8 = ComandoLancamentoAutomatico.GetConnectorClosestTo(lastHorPipe, ptBaseCV_Real);
                Connector c9 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboDestino, ptBaseCV_Real);
                FamilyInstance elbow2 = doc.Create.NewElbowFitting(c8, c9);
                if (elbow2 != null)
                {
                    doc.Regenerate();
                    foreach (Parameter p7 in elbow2.Parameters)
                    {
                        if (cfg.RotacaoTe90 && (p7.Definition.Name.Contains("Inverter Sentido da Luva") || p7.Definition.Name.Contains("Ligacao em Conexao")) && !p7.IsReadOnly)
                        {
                            try
                            {
                                p7.Set(1);
                            }
                            catch
                            {
                            }
                        }
                    }
                    doc.Regenerate();
                    try
                    {
                        Connector e5 = getConn2(elbow2, c8.Origin);
                        Connector e6 = getConn2(elbow2, c9.Origin);
                        if (e5 != null && !c8.IsConnected)
                        {
                            c8.ConnectTo(e5);
                        }
                        if (e6 != null && !c9.IsConnected)
                        {
                            c9.ConnectTo(e6);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
        else
        {
            double colX = ((tuboDestino.Location is LocationCurve lcColA) ? lcColA.Curve.GetEndPoint(0).X : ptBaseCV_Real.X);
            double colY = ((tuboDestino.Location is LocationCurve lcColB) ? lcColB.Curve.GetEndPoint(0).Y : ptBaseCV_Real.Y);
            double distXYToCol = new XYZ(ptBaseCV_Real.X, ptBaseCV_Real.Y, 0.0).DistanceTo(new XYZ(colX, colY, 0.0));
            double zJuncao = JigLancamentoManager.ZNivel + cfg.AltVentilacaoCavalete / 0.3048;
            double zFinalCavalete = zJuncao - distXYToCol;
            XYZ ptTopoCV = new XYZ(ptBaseCV_Real.X, ptBaseCV_Real.Y, zFinalCavalete);
            Pipe tuboCavalete = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptBaseCV_Real, ptTopoCV);
            ((Element)tuboCavalete).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
            try
            {
                Connector c10 = ComandoLancamentoAutomatico.GetConnectorClosestTo(lastHorPipe, ptBaseCV_Real);
                Connector c11 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboCavalete, ptBaseCV_Real);
                FamilyInstance elbow3 = doc.Create.NewElbowFitting(c10, c11);
                if (elbow3 != null)
                {
                    doc.Regenerate();
                    foreach (Parameter p8 in elbow3.Parameters)
                    {
                        if (cfg.RotacaoTe90 && (p8.Definition.Name.Contains("Inverter Sentido da Luva") || p8.Definition.Name.Contains("Ligacao em Conexao")) && !p8.IsReadOnly)
                        {
                            try
                            {
                                p8.Set(1);
                            }
                            catch
                            {
                            }
                        }
                    }
                    doc.Regenerate();
                    try
                    {
                        Connector e7 = getConn2(elbow3, c10.Origin);
                        Connector e8 = getConn2(elbow3, c11.Origin);
                        if (e7 != null && !c10.IsConnected)
                        {
                            c10.ConnectTo(e7);
                        }
                        if (e8 != null && !c11.IsConnected)
                        {
                            c11.ConnectTo(e8);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            XYZ dirToCol = new XYZ((tuboDestino.Location is LocationCurve lct) ? (lct.Curve.GetEndPoint(0).X - ptTopoCV.X) : 0.0, (tuboDestino.Location is LocationCurve lct2) ? (lct2.Curve.GetEndPoint(0).Y - ptTopoCV.Y) : 0.0, 0.0).Normalize();
            XYZ ptIntersecaoColuna = new XYZ(ptTopoCV.X + dirToCol.X * distXYToCol, ptTopoCV.Y + dirToCol.Y * distXYToCol, ptTopoCV.Z + distXYToCol);
            Pipe tuboInclinado = Pipe.Create(doc, sistemaVentId, cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptTopoCV, ptIntersecaoColuna);
            ((Element)tuboInclinado).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamVent);
            ConectarJuncaoPrumada(doc, tuboDestino, tuboInclinado, ptIntersecaoColuna, deleteShortPipe: false, 135.0);
            FamilyInstance elbow45 = null;
            try
            {
                Connector c12 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboCavalete, ptTopoCV);
                Connector c13 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboInclinado, ptTopoCV);
                elbow45 = doc.Create.NewElbowFitting(c12, c13);
                if (elbow45 != null)
                {
                    doc.Regenerate();
                    try
                    {
                        Connector e9 = getConn2(elbow45, c12.Origin);
                        Connector e10 = getConn2(elbow45, c13.Origin);
                        if (e9 != null && !c12.IsConnected)
                        {
                            c12.ConnectTo(e9);
                        }
                        if (e10 != null && !c13.IsConnected)
                        {
                            c13.ConnectTo(e10);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            if (elbow45 != null)
            {
                Connector cTuboWye = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboInclinado, ptIntersecaoColuna);
                Connector cWye = null;
                if (cTuboWye != null && cTuboWye.IsConnected)
                {
                    foreach (Connector r in cTuboWye.AllRefs)
                    {
                        if (r.Owner.Id != tuboInclinado.Id && r.ConnectorType != ConnectorType.Logical)
                        {
                            cWye = r;
                            break;
                        }
                    }
                }
                if (cWye != null)
                {
                    foreach (Parameter param2 in elbow45.Parameters)
                    {
                        string nome = param2.Definition.Name.ToLower();
                        if (!nome.Contains("lig") || !nome.Contains("conex") || param2.IsReadOnly)
                        {
                            continue;
                        }
                        if (param2.StorageType == StorageType.Integer)
                        {
                            try
                            {
                                param2.Set(1);
                            }
                            catch
                            {
                            }
                        }
                        else if (param2.StorageType == StorageType.Double)
                        {
                            try
                            {
                                param2.Set(1.0);
                            }
                            catch
                            {
                            }
                        }
                        else if (param2.StorageType == StorageType.String)
                        {
                            try
                            {
                                param2.Set("1");
                            }
                            catch
                            {
                            }
                        }
                    }
                    doc.Regenerate();
                }
            }
            if (tuboCavalete != null && tuboCavalete.IsValidObject)
            {
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboCavalete.Id);
            }
            if (tuboInclinado != null && tuboInclinado.IsValidObject)
            {
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboInclinado.Id);
            }
        }
        if (tuboHor1 != null && tuboHor1.IsValidObject)
        {
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboHor1.Id);
        }
        if (tuboHor2 != null && tuboHor2.IsValidObject && tuboHor2.Id != tuboHor1.Id)
        {
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboHor2.Id);
        }
        if (stub != null && stub.IsValidObject)
        {
            JigLancamentoManager.IdsCriadosNestaSessao.Add(stub.Id);
        }
    }

    private void ModelarVaso(Document doc)
    {
        XYZ pt1 = JigLancamentoManager.Pt1;
        XYZ pt2 = JigLancamentoManager.Pt2;
        XYZ intEscolhida2D = ((JigLancamentoManager.RotaEscolhida == 1) ? JigLancamentoManager.IntA : JigLancamentoManager.IntB);
        if ((JigLancamentoManager.Cfg.DestinoVaso == 1 || JigLancamentoManager.Cfg.DestinoVaso == 2) && JigLancamentoManager.TuboDestino != null)
        {
            Curve cTarget = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
            pt2 = new XYZ(cTarget.GetEndPoint(0).X, cTarget.GetEndPoint(0).Y, 0.0);
        }
        double dist1 = new XYZ(pt1.X, pt1.Y, 0.0).DistanceTo(new XYZ(intEscolhida2D.X, intEscolhida2D.Y, 0.0));
        double dist2 = new XYZ(intEscolhida2D.X, intEscolhida2D.Y, 0.0).DistanceTo(new XYZ(pt2.X, pt2.Y, 0.0));
        double caimento1 = dist1 * 0.01;
        double caimento2 = dist2 * 0.01;
        XYZ ptTopo = new XYZ(pt1.X, pt1.Y, JigLancamentoManager.ZNivel);
        XYZ ptFundoBase = new XYZ(pt1.X, pt1.Y, JigLancamentoManager.ZColetor);
        XYZ intEscolhida3D = new XYZ(intEscolhida2D.X, intEscolhida2D.Y, JigLancamentoManager.ZColetor - caimento1);
        XYZ ptFim3D = new XYZ(pt2.X, pt2.Y, JigLancamentoManager.ZColetor - caimento1 - caimento2);
        Pipe tuboVert = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptTopo, ptFundoBase);
        ((Element)tuboVert).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(JigLancamentoManager.DiamVaso);
        JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboVert.Id);
        Pipe tuboHoriz1 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptFundoBase, intEscolhida3D);
        ((Element)tuboHoriz1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(JigLancamentoManager.DiamVaso);
        JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboHoriz1.Id);
        doc.Regenerate();
        FamilyInstance joelho1 = ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboVert, tuboHoriz1, ptFundoBase);
        AjustarLuvaEConexaoJoelho(joelho1, tuboVert, ligacaoEmConexao: false);
        if (dist2 > 0.05)
        {
            Pipe tuboHoriz2 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, intEscolhida3D, ptFim3D);
            ((Element)tuboHoriz2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(JigLancamentoManager.DiamVaso);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboHoriz2.Id);
            doc.Regenerate();
            FamilyInstance joelho2 = ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboHoriz1, tuboHoriz2, intEscolhida3D);
            AjustarLuvaEConexaoJoelho(joelho2, tuboHoriz1, ligacaoEmConexao: false);
            if (JigLancamentoManager.Cfg.DestinoVaso == 1 && JigLancamentoManager.TuboDestino != null)
            {
                Pipe tuboQueda = JigLancamentoManager.TuboDestino;
                Curve cTubo = (tuboQueda.Location as LocationCurve).Curve;
                XYZ cP0 = cTubo.GetEndPoint(0);
                XYZ cP1 = cTubo.GetEndPoint(1);
                if (cP0.Z > cP1.Z)
                {
                    cP0 = new XYZ(cP0.X, cP0.Y, ptFim3D.Z);
                }
                else
                {
                    cP1 = new XYZ(cP1.X, cP1.Y, ptFim3D.Z);
                }
                if (cP0.DistanceTo(cP1) > 0.1)
                {
                    (tuboQueda.Location as LocationCurve).Curve = Line.CreateBound(cP0, cP1);
                    doc.Regenerate();
                    ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboHoriz2, tuboQueda, ptFim3D);
                }
            }
            else if (JigLancamentoManager.Cfg.DestinoVaso == 2 && JigLancamentoManager.TuboDestino != null)
            {
                double distRecuo = 200.0 / 381.0;
                double minTrechoReto = 0.39370078740157477;
                XYZ pStart = intEscolhida3D;
                XYZ pEnd = ptFim3D;
                double distSeg = pStart.DistanceTo(pEnd);
                if (distSeg < distRecuo + minTrechoReto)
                {
                    throw new Exception($"too short: o trecho final da rota escolhida tem {distSeg * 0.3048 * 100.0:F0}cm, mas são necessários {(distRecuo + minTrechoReto) * 0.3048 * 100.0:F0}cm para o " + "joelho de 45º + junção na prumada. Alterne para a outra rota ou afaste o ponto.");
                }
                XYZ dirFinal = (pEnd - pStart).Normalize();
                XYZ recuo3D = dirFinal * distRecuo;
                XYZ pFimRecuado = pEnd - recuo3D;
                if (pStart.DistanceTo(pEnd) > distRecuo)
                {
                    (tuboHoriz2.Location as LocationCurve).Curve = Line.CreateBound(pStart, pFimRecuado);
                    doc.Regenerate();
                    XYZ pFimTubo45 = new XYZ(pEnd.X, pEnd.Y, pFimRecuado.Z - distRecuo);
                    Curve cTarget2 = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
                    pFimTubo45 = cTarget2.Project(pFimTubo45).XYZPoint;
                    Pipe tubo45 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pFimRecuado, pFimTubo45);
                    ((Element)tubo45).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(JigLancamentoManager.DiamVaso);
                    JigLancamentoManager.IdsCriadosNestaSessao.Add(tubo45.Id);
                    doc.Regenerate();
                    ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboHoriz2, tubo45, pFimRecuado);
                    ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45, pFimTubo45);
                }
            }
        }
        else if (JigLancamentoManager.Cfg.DestinoVaso == 1 && JigLancamentoManager.TuboDestino != null)
        {
            Pipe tuboQueda2 = JigLancamentoManager.TuboDestino;
            Curve cTubo2 = (tuboQueda2.Location as LocationCurve).Curve;
            XYZ cP2 = cTubo2.GetEndPoint(0);
            XYZ cP3 = cTubo2.GetEndPoint(1);
            if (cP2.Z > cP3.Z)
            {
                cP2 = new XYZ(cP2.X, cP2.Y, ptFim3D.Z);
            }
            else
            {
                cP3 = new XYZ(cP3.X, cP3.Y, ptFim3D.Z);
            }
            if (cP2.DistanceTo(cP3) > 0.1)
            {
                (tuboQueda2.Location as LocationCurve).Curve = Line.CreateBound(cP2, cP3);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboHoriz1, tuboQueda2, ptFim3D);
            }
        }
        else if (JigLancamentoManager.Cfg.DestinoVaso == 2 && JigLancamentoManager.TuboDestino != null)
        {
            double distRecuo2 = 200.0 / 381.0;
            double minTrechoReto2 = 0.39370078740157477;
            XYZ pStart2 = ptFundoBase;
            XYZ pEnd2 = ptFim3D;
            double distSeg2 = pStart2.DistanceTo(pEnd2);
            if (distSeg2 < distRecuo2 + minTrechoReto2)
            {
                throw new Exception($"too short: o trecho final da rota escolhida tem {distSeg2 * 0.3048 * 100.0:F0}cm, mas são necessários {(distRecuo2 + minTrechoReto2) * 0.3048 * 100.0:F0}cm para o " + "joelho de 45º + junção na prumada. Alterne para a outra rota ou afaste o ponto.");
            }
            XYZ dirFinal2 = (pEnd2 - pStart2).Normalize();
            XYZ recuo3D2 = dirFinal2 * distRecuo2;
            XYZ pFimRecuado2 = pEnd2 - recuo3D2;
            if (pStart2.DistanceTo(pEnd2) > distRecuo2)
            {
                (tuboHoriz1.Location as LocationCurve).Curve = Line.CreateBound(pStart2, pFimRecuado2);
                doc.Regenerate();
                XYZ pFimTubo46 = new XYZ(pEnd2.X, pEnd2.Y, pFimRecuado2.Z - distRecuo2);
                Curve cTarget3 = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
                pFimTubo46 = cTarget3.Project(pFimTubo46).XYZPoint;
                Pipe tubo46 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pFimRecuado2, pFimTubo46);
                ((Element)tubo46).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(JigLancamentoManager.DiamVaso);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tubo46.Id);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboHoriz1, tubo46, pFimRecuado2);
                ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo46, pFimTubo46);
            }
        }
    }

    private void ModelarCaixaIndependente(Document doc)
    {
        Connector conn = JigLancamentoManager.ConectorCaixa;
        XYZ ptOrigem = conn?.Origin ?? JigLancamentoManager.Pt1;
        double zOrigem = ptOrigem.Z;
        int rotaEscolhida = JigLancamentoManager.RotaEscolhida;
        XYZ pt2 = JigLancamentoManager.Pt2;
        XYZ dirConector = JigLancamentoManager.DirCaixa;
        if (dirConector == null || dirConector.IsZeroLength())
        {
            dirConector = new XYZ(1.0, 0.0, 0.0);
        }
        dirConector = new XYZ(dirConector.X, dirConector.Y, 0.0).Normalize();
        double diamCaixa = ((conn != null) ? (conn.Radius * 2.0) : UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters));
        double inclinacao = 0.02;
        XYZ ptFim2D = new XYZ(pt2.X, pt2.Y, 0.0);
        if ((JigLancamentoManager.Cfg.DestinoCaixa == 1 || JigLancamentoManager.Cfg.DestinoCaixa == 2 || JigLancamentoManager.Cfg.DestinoCaixa == 3) && JigLancamentoManager.TuboDestino != null)
        {
            Curve cTarget = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
            XYZ proj = cTarget.Project(pt2).XYZPoint;
            ptFim2D = new XYZ(proj.X, proj.Y, 0.0);
        }
        XYZ vecDestino2D = new XYZ(ptFim2D.X - ptOrigem.X, ptFim2D.Y - ptOrigem.Y, 0.0);
        double distDestino2D = vecDestino2D.GetLength();
        if (distDestino2D < 0.01)
        {
            return;
        }
        XYZ dirDestino2D = vecDestino2D.Normalize();
        double anguloComConector = dirConector.AngleTo(dirDestino2D) * (180.0 / Math.PI);
        _ = anguloComConector > 2.0;
        if (anguloComConector >= 70.0 && anguloComConector <= 110.0)
        {
            throw new Exception("Não ? possível sair na perpendicular da caixa sifonada. Escolha um destino alinhado com a direção do conector.");
        }
        if (JigLancamentoManager.RotasPia == null || rotaEscolhida < 0 || rotaEscolhida >= JigLancamentoManager.RotasPia.Count)
        {
            return;
        }
        List<XYZ> rotaPlan = JigLancamentoManager.RotasPia[rotaEscolhida];
        List<XYZ> pontos = new List<XYZ>();
        XYZ ptAtual2D = new XYZ(ptOrigem.X, ptOrigem.Y, 0.0);
        double cotaAtual = JigLancamentoManager.ZPreview;
        if (conn != null && conn.Owner != null)
        {
            double diffZ = cotaAtual - zOrigem;
            if (Math.Abs(diffZ) > 0.001)
            {
                try
                {
                    ElementTransformUtils.MoveElement(doc, conn.Owner.Id, new XYZ(0.0, 0.0, diffZ));
                    doc.Regenerate();
                    Connector newConn = null;
                    XYZ novaPosicaoConn = new XYZ(ptOrigem.X, ptOrigem.Y, cotaAtual);
                    if (conn.Owner is FamilyInstance { MEPModel: not null } fi && fi.MEPModel.ConnectorManager != null)
                    {
                        foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
                        {
                            if (c.Origin.DistanceTo(novaPosicaoConn) < 0.1)
                            {
                                newConn = c;
                                break;
                            }
                        }
                    }
                    conn = newConn ?? conn;
                }
                catch
                {
                }
            }
        }
        pontos.Add(new XYZ(ptOrigem.X, ptOrigem.Y, cotaAtual));
        for (int i = 1; i < rotaPlan.Count; i++)
        {
            XYZ pt2D = new XYZ(rotaPlan[i].X, rotaPlan[i].Y, 0.0);
            double dist = ptAtual2D.DistanceTo(pt2D);
            if (dist > 0.01)
            {
                cotaAtual -= dist * inclinacao;
                pontos.Add(new XYZ(pt2D.X, pt2D.Y, cotaAtual));
                ptAtual2D = pt2D;
            }
        }
        if (pontos.Count < 2)
        {
            return;
        }
        List<Pipe> tubos = new List<Pipe>();
        for (int j = 0; j < pontos.Count - 1; j++)
        {
            Pipe p = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pontos[j], pontos[j + 1]);
            ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            tubos.Add(p);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(p.Id);
        }
        doc.Regenerate();
        if (conn != null && tubos.Count > 0)
        {
            Connector cPipe = ComandoLancamentoAutomatico.GetConnectorClosestTo(tubos[0], pontos[0]);
            if (cPipe != null && !cPipe.IsConnected)
            {
                try
                {
                    cPipe.ConnectTo(conn);
                    doc.Regenerate();
                }
                catch
                {
                }
            }
        }
        for (int k = 0; k < tubos.Count - 1; k++)
        {
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tubos[k], tubos[k + 1], pontos[k + 1]);
            doc.Regenerate();
        }
        Pipe tuboFinal = tubos.Last();
        XYZ ptFimFinal = pontos.Last();
        if (JigLancamentoManager.Cfg.DestinoCaixa == 1 && JigLancamentoManager.TuboDestino != null)
        {
            Pipe tuboQueda = JigLancamentoManager.TuboDestino;
            Curve cTubo = (tuboQueda.Location as LocationCurve).Curve;
            XYZ cP0 = cTubo.GetEndPoint(0);
            XYZ cP1 = cTubo.GetEndPoint(1);
            if (cP0.Z > cP1.Z)
            {
                cP0 = new XYZ(cP0.X, cP0.Y, ptFimFinal.Z);
            }
            else
            {
                cP1 = new XYZ(cP1.X, cP1.Y, ptFimFinal.Z);
            }
            if (cP0.DistanceTo(cP1) > 0.1)
            {
                (tuboQueda.Location as LocationCurve).Curve = Line.CreateBound(cP0, cP1);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboFinal, tuboQueda, ptFimFinal);
            }
        }
        else if (JigLancamentoManager.Cfg.DestinoCaixa == 2 && JigLancamentoManager.TuboDestino != null)
        {
            double distRecuo = 275.0 / 762.0;
            Curve curvaFinal = (tuboFinal.Location as LocationCurve).Curve;
            XYZ f0 = curvaFinal.GetEndPoint(0);
            XYZ f1 = curvaFinal.GetEndPoint(1);
            XYZ pStart = ((f0.DistanceTo(ptFimFinal) > f1.DistanceTo(ptFimFinal)) ? f0 : f1);
            XYZ pEnd = ((f0.DistanceTo(ptFimFinal) <= f1.DistanceTo(ptFimFinal)) ? f0 : f1);
            XYZ dirFinal = (pEnd - pStart).Normalize();
            XYZ pFimRecuado = pEnd - dirFinal * distRecuo;
            if (pStart.DistanceTo(pEnd) > distRecuo)
            {
                (tuboFinal.Location as LocationCurve).Curve = Line.CreateBound(pStart, pFimRecuado);
                doc.Regenerate();
                XYZ pFimTubo45 = new XYZ(pEnd.X, pEnd.Y, pFimRecuado.Z - distRecuo);
                Curve cTarget2 = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
                pFimTubo45 = cTarget2.Project(pFimTubo45).XYZPoint;
                Pipe tubo45 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pFimRecuado, pFimTubo45);
                ((Element)tubo45).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tubo45.Id);
                FamilyInstance joelho45 = ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboFinal, tubo45, pFimRecuado);
                doc.Regenerate();
                ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45, pFimTubo45, deleteShortPipe: false);
                doc.Regenerate();
                if (joelho45 != null)
                {
                    Parameter pLigacao = joelho45.LookupParameter("Ligação em Conexão");
                    if (pLigacao != null && !pLigacao.IsReadOnly)
                    {
                        pLigacao.Set(1);
                    }
                }
            }
            else
            {
                ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tuboFinal, ptFimFinal, deleteShortPipe: false);
            }
        }
        else if (JigLancamentoManager.Cfg.DestinoCaixa == 3 && JigLancamentoManager.TuboDestino != null)
        {
            ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tuboFinal, ptFimFinal);
        }
    }

    private void ModelarCaixa(Document doc)
    {
        XYZ ptCaixa2D = JigLancamentoManager.Pt1;
        XYZ ptExatoNoTubo = JigLancamentoManager.Pt2;
        Pipe tuboMain = JigLancamentoManager.TuboDestino;
        if (tuboMain == null || ptExatoNoTubo == null)
        {
            return;
        }
        Curve curvaMain = (tuboMain.Location as LocationCurve).Curve;
        XYZ e0 = curvaMain.GetEndPoint(0);
        XYZ e1 = curvaMain.GetEndPoint(1);
        XYZ e0_2D = new XYZ(e0.X, e0.Y, 0.0);
        XYZ e1_2D = new XYZ(e1.X, e1.Y, 0.0);
        double len2D = e0_2D.DistanceTo(e1_2D);
        double margem = 0.26;
        if (len2D < 2.0 * margem + 0.05)
        {
            throw new Exception("O tubo coletor selecionado ? muito curto para receber a junção com folga das conexões existentes.");
        }
        XYZ dirMainSnap = (e1_2D - e0_2D).Normalize();
        double tClique = (new XYZ(ptExatoNoTubo.X, ptExatoNoTubo.Y, 0.0) - e0_2D).DotProduct(dirMainSnap);
        double tSnap = Math.Max(margem, Math.Min(len2D - margem, tClique));
        double zReal = e0.Z + (e1.Z - e0.Z) * (tSnap / len2D);
        XYZ novoXY = e0_2D + dirMainSnap * tSnap;
        bool pontoCorrigido = Math.Abs(tSnap - tClique) > 0.01 || Math.Abs(zReal - ptExatoNoTubo.Z) > 0.01;
        ptExatoNoTubo = new XYZ(novoXY.X, novoXY.Y, zReal);
        XYZ dirCaixaGlobal = JigLancamentoManager.DirCaixa;
        bool connCaixaVertical = dirCaixaGlobal == null || dirCaixaGlobal.IsZeroLength() || Math.Abs(dirCaixaGlobal.Z) > 0.9;
        bool saidaPerpendicular = false;
        if (!connCaixaVertical)
        {
            double angSaidaCx = new XYZ(dirCaixaGlobal.X, dirCaixaGlobal.Y, 0.0).Normalize().AngleTo(dirMainSnap) * 180.0 / Math.PI;
            saidaPerpendicular = Math.Abs(angSaidaCx - 90.0) <= 15.0;
        }
        XYZ intEscolhida = ((JigLancamentoManager.RotaEscolhida == 1) ? JigLancamentoManager.IntA : JigLancamentoManager.IntB);
        if (pontoCorrigido)
        {
            XYZ ptC2D = new XYZ(ptCaixa2D.X, ptCaixa2D.Y, 0.0);
            XYZ p3n = new XYZ(ptExatoNoTubo.X, ptExatoNoTubo.Y, 0.0);
            XYZ dirCx = JigLancamentoManager.DirCaixa;
            if (dirCx == null || dirCx.IsZeroLength() || Math.Abs(dirCx.Z) > 0.9)
            {
                (XYZ, XYZ) tuple = ComandoLancamentoAutomatico.CalcularProjecao45Graus(ptC2D, p3n, 0.0);
                XYZ iA = tuple.Item1;
                XYZ iB = tuple.Item2;
                double dA = ptC2D.DistanceTo(iA) + iA.DistanceTo(p3n);
                double dB = ptC2D.DistanceTo(iB) + iB.DistanceTo(p3n);
                intEscolhida = ((dA <= dB) ? iA : iB);
            }
            else
            {
                XYZ dirCx2D = new XYZ(dirCx.X, dirCx.Y, 0.0).Normalize();
                bool resolvido = false;
                XYZ qDireto = JigLancamentoManager.IntersecaoReta(ptC2D, dirCx2D, e0_2D, dirMainSnap);
                if (qDireto != null && !saidaPerpendicular)
                {
                    double tQ = (qDireto - e0_2D).DotProduct(dirMainSnap);
                    double ang = dirCx2D.AngleTo(dirMainSnap) * 180.0 / Math.PI;
                    bool alinhado = Math.Abs(ang - 45.0) < 1.0 || Math.Abs(ang - 135.0) < 1.0;
                    if ((qDireto - ptC2D).DotProduct(dirCx2D) > 0.01 && tQ >= margem && tQ <= len2D - margem && (alinhado || qDireto.DistanceTo(p3n) < 0.8))
                    {
                        double zQ = e0.Z + (e1.Z - e0.Z) * (tQ / len2D);
                        ptExatoNoTubo = new XYZ(qDireto.X, qDireto.Y, zQ);
                        intEscolhida = qDireto;
                        resolvido = true;
                    }
                }
                if (!resolvido)
                {
                    XYZ invMain = -dirMainSnap;
                    double c45 = 0.70710678;
                    double s45 = 0.70710678;
                    XYZ v45A = new XYZ(invMain.X * c45 - invMain.Y * s45, invMain.X * s45 + invMain.Y * c45, 0.0);
                    XYZ v45B = new XYZ(invMain.X * c45 + invMain.Y * s45, (0.0 - invMain.X) * s45 + invMain.Y * c45, 0.0);
                    XYZ iA2 = JigLancamentoManager.IntersecaoReta(ptC2D, dirCx2D, p3n, v45A);
                    XYZ iB2 = JigLancamentoManager.IntersecaoReta(ptC2D, dirCx2D, p3n, v45B);
                    double tA = ((iA2 != null) ? (iA2 - ptC2D).DotProduct(dirCx2D) : (-1.0));
                    double tB = ((iB2 != null) ? (iB2 - ptC2D).DotProduct(dirCx2D) : (-1.0));
                    if (saidaPerpendicular && qDireto != null)
                    {
                        double tCruz = (qDireto - ptC2D).DotProduct(dirCx2D);
                        if (tCruz > 0.01)
                        {
                            if (tA >= tCruz - 0.01)
                            {
                                tA = -1.0;
                            }
                            if (tB >= tCruz - 0.01)
                            {
                                tB = -1.0;
                            }
                        }
                    }
                    if (tA > 0.0 && tB > 0.0)
                    {
                        intEscolhida = ((tA < tB) ? iA2 : iB2);
                    }
                    else if (tA > 0.0)
                    {
                        intEscolhida = iA2;
                    }
                    else if (tB > 0.0)
                    {
                        intEscolhida = iB2;
                    }
                    else
                    {
                        if (saidaPerpendicular)
                        {
                            throw new Exception("Não foi possível montar o desvio de 45º da caixa sifonada até o coletor (trecho reto + joelho 45º + junção 45º). O ponto da junção caiu fora da faixa útil do coletor. Afaste a caixa do coletor ou aumente o tubo coletor.");
                        }
                        intEscolhida = p3n;
                    }
                }
            }
        }
        XYZ intEscolhida2D = new XYZ(intEscolhida.X, intEscolhida.Y, 0.0);
        XYZ p1 = ((JigLancamentoManager.ConectorCaixa != null) ? JigLancamentoManager.ConectorCaixa.Origin : new XYZ(ptCaixa2D.X, ptCaixa2D.Y, ptExatoNoTubo.Z));
        XYZ p1_2D = new XYZ(p1.X, p1.Y, 0.0);
        XYZ p2_2D = new XYZ(intEscolhida2D.X, intEscolhida2D.Y, 0.0);
        XYZ p3_2D = new XYZ(ptExatoNoTubo.X, ptExatoNoTubo.Y, 0.0);
        double minDistDesejada = 0.075;
        if (p1_2D.DistanceTo(p2_2D) > 0.01 && p2_2D.DistanceTo(p3_2D) > 0.01 && p1_2D.DistanceTo(p2_2D) < minDistDesejada)
        {
            XYZ dirCaixa2D = (p2_2D - p1_2D).Normalize();
            XYZ dirRamal2D = (p3_2D - p2_2D).Normalize();
            XYZ dirMain2D = (new XYZ(curvaMain.GetEndPoint(1).X, curvaMain.GetEndPoint(1).Y, 0.0) - new XYZ(curvaMain.GetEndPoint(0).X, curvaMain.GetEndPoint(0).Y, 0.0)).Normalize();
            XYZ p2_novo2D = p1_2D + dirCaixa2D * minDistDesejada;
            XYZ p3_novo2D = JigLancamentoManager.IntersecaoReta(p2_novo2D, dirRamal2D, p3_2D, dirMain2D);
            if (p3_novo2D != null)
            {
                XYZ startMain2D = new XYZ(curvaMain.GetEndPoint(0).X, curvaMain.GetEndPoint(0).Y, 0.0);
                double t = (p3_novo2D - startMain2D).DotProduct(dirMain2D);
                double totalLen = startMain2D.DistanceTo(new XYZ(curvaMain.GetEndPoint(1).X, curvaMain.GetEndPoint(1).Y, 0.0));
                double novoZ = curvaMain.GetEndPoint(0).Z + t / totalLen * (curvaMain.GetEndPoint(1).Z - curvaMain.GetEndPoint(0).Z);
                p3_2D = p3_novo2D;
                p2_2D = p2_novo2D;
                ptExatoNoTubo = new XYZ(p3_novo2D.X, p3_novo2D.Y, novoZ);
            }
        }
        double dist1 = p1_2D.DistanceTo(p2_2D);
        double dist2 = p2_2D.DistanceTo(p3_2D);
        XYZ vIn2D = ((dist1 > 0.001) ? (p2_2D - p1_2D).Normalize() : XYZ.BasisX);
        XYZ vOut2D = ((dist2 > 0.001) ? (p3_2D - p2_2D).Normalize() : XYZ.BasisY);
        bool is90 = Math.Abs(vIn2D.AngleTo(vOut2D) - Math.PI / 2.0) < 0.05;
        double dChanfro = 0.0;
        if (is90 && dist1 > 0.1 && dist2 > 0.1)
        {
            dChanfro = (dist1 + dist2) / (2.0 + 2.0 * Math.Sqrt(2.0));
            dChanfro = Math.Min(dChanfro, Math.Min(dist1 * 0.45, dist2 * 0.45));
        }
        double distTotalHorizontal = dist1 + dist2;
        if (dChanfro > 0.0)
        {
            distTotalHorizontal = dist1 + dist2 - 2.0 * dChanfro + dChanfro * Math.Sqrt(2.0);
        }
        double cotaAtual = ptExatoNoTubo.Z + distTotalHorizontal * 0.02;
        p1 = new XYZ(p1.X, p1.Y, cotaAtual);
        if (JigLancamentoManager.ConectorCaixa != null && JigLancamentoManager.ConectorCaixa.Owner != null)
        {
            double diffZ = cotaAtual - JigLancamentoManager.ConectorCaixa.Origin.Z;
            if (Math.Abs(diffZ) > 0.001)
            {
                try
                {
                    ElementTransformUtils.MoveElement(doc, JigLancamentoManager.ConectorCaixa.Owner.Id, new XYZ(0.0, 0.0, diffZ));
                    doc.Regenerate();
                    Connector newConn = null;
                    XYZ novaPosicaoConn = new XYZ(p1.X, p1.Y, cotaAtual);
                    if (JigLancamentoManager.ConectorCaixa.Owner is FamilyInstance { MEPModel: not null } fi && fi.MEPModel.ConnectorManager != null)
                    {
                        foreach (Connector c46 in fi.MEPModel.ConnectorManager.Connectors)
                        {
                            if (c46.Origin.DistanceTo(novaPosicaoConn) < 0.1)
                            {
                                newConn = c46;
                                break;
                            }
                        }
                    }
                    JigLancamentoManager.ConectorCaixa = newConn ?? JigLancamentoManager.ConectorCaixa;
                    p1 = novaPosicaoConn;
                }
                catch
                {
                }
            }
            if (JigLancamentoManager.Cfg.TemVaso)
            {
                XYZ currentDir = new XYZ(JigLancamentoManager.ConectorCaixa.CoordinateSystem.BasisZ.X, JigLancamentoManager.ConectorCaixa.CoordinateSystem.BasisZ.Y, 0.0).Normalize();
                XYZ targetDir = new XYZ(JigLancamentoManager.DirCaixa.X, JigLancamentoManager.DirCaixa.Y, 0.0).Normalize();
                double angleDiff = currentDir.AngleTo(targetDir);
                if (angleDiff > 0.001)
                {
                    if (currentDir.CrossProduct(targetDir).Z < 0.0)
                    {
                        angleDiff = 0.0 - angleDiff;
                    }
                    try
                    {
                        ElementTransformUtils.RotateElement(doc, JigLancamentoManager.ConectorCaixa.Owner.Id, Line.CreateUnbound(p1, XYZ.BasisZ), angleDiff);
                    }
                    catch
                    {
                    }
                }
            }
        }
        XYZ p3 = ptExatoNoTubo;
        XYZ p4 = new XYZ(p2_2D.X, p2_2D.Y, p1.Z - dist1 * 0.02);
        double diamCaixa = UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters);
        if (JigLancamentoManager.ConectorCaixa != null)
        {
            diamCaixa = JigLancamentoManager.ConectorCaixa.Radius * 2.0;
        }
        Pipe tuboPrimeiro;
        if (p1.DistanceTo(p4) < 0.05 || p4.DistanceTo(p3) < 0.05)
        {
            if (saidaPerpendicular)
            {
                throw new Exception("A saída da caixa sifonada é perpendicular ao coletor e não há espaço para montar o desvio de 45º (trecho reto + joelho 45º + junção 45º). Afaste a caixa do coletor ou escolha outro ponto de conexão.");
            }
            XYZ projFallback = new XYZ(p3.X, p3.Y, p1.Z - p1_2D.DistanceTo(p3_2D) * 0.02);
            Pipe tuboDireto = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, p1, projFallback);
            ((Element)tuboDireto).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            doc.Regenerate();
            EfetuarQuebraEConexao(doc, tuboMain, tuboDireto, p3);
            tuboPrimeiro = tuboDireto;
        }
        else if (dChanfro > 0.0)
        {
            XYZ p2a_2D = p2_2D - vIn2D * dChanfro;
            XYZ p2b_2D = p2_2D + vOut2D * dChanfro;
            XYZ p2a = new XYZ(p2a_2D.X, p2a_2D.Y, p1.Z - p1_2D.DistanceTo(p2a_2D) * 0.02);
            XYZ p2b = new XYZ(p2b_2D.X, p2b_2D.Y, p2a.Z - p2a_2D.DistanceTo(p2b_2D) * 0.02);
            Pipe tuboCaixa1 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, p1, p2a);
            ((Element)tuboCaixa1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            Pipe tuboIntermediario = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, p2a, p2b);
            ((Element)tuboIntermediario).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            Pipe tuboCaixa2 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, p2b, p3);
            ((Element)tuboCaixa2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboCaixa1, tuboIntermediario, p2a);
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboIntermediario, tuboCaixa2, p2b);
            EfetuarQuebraEConexao(doc, tuboMain, tuboCaixa2, p3);
            tuboPrimeiro = tuboCaixa1;
        }
        else
        {
            Pipe tuboCaixa3 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, p1, p4);
            ((Element)tuboCaixa3).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            Pipe tuboCaixa4 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, p4, p3);
            ((Element)tuboCaixa4).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamCaixa);
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboCaixa3, tuboCaixa4, p4);
            EfetuarQuebraEConexao(doc, tuboMain, tuboCaixa4, p3);
            tuboPrimeiro = tuboCaixa3;
        }
        if (JigLancamentoManager.ConectorCaixa == null || tuboPrimeiro == null)
        {
            return;
        }
        Connector cPipe = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboPrimeiro, p1);
        if (cPipe == null || cPipe.IsConnected)
        {
            return;
        }
        try
        {
            cPipe.ConnectTo(JigLancamentoManager.ConectorCaixa);
        }
        catch
        {
        }
    }

    private void ModelarChuveiro(Document doc)
    {
        XYZ ptSaidaOriginal = JigLancamentoManager.PtSaidaChuveiro;
        XYZ dirFace = JigLancamentoManager.DirSaidaChuveiro;
        XYZ ptTarget2D = JigLancamentoManager.Pt2;
        double diamChuveiro = 0.04;
        diamChuveiro = ((JigLancamentoManager.ConectorChuveiro == null) ? UnitUtils.ConvertToInternalUnits(40.0, UnitTypeId.Millimeters) : (JigLancamentoManager.ConectorChuveiro.Radius * 2.0));
        double zVistaMin = JigLancamentoManager.ZVistaMin;
        double zVistaMax = JigLancamentoManager.ZVistaMax;
        Outline outline = new Outline(new XYZ(ptTarget2D.X - 2.0, ptTarget2D.Y - 2.0, zVistaMin), new XYZ(ptTarget2D.X + 2.0, ptTarget2D.Y + 2.0, zVistaMax));
        List<FamilyInstance> caixas = (from FamilyInstance fi in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WherePasses(new BoundingBoxIntersectsFilter(outline))
                                       where fi.Category != null && (fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PlumbingFixtures)) || fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_MechanicalEquipment)) || fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PipeAccessory)))
                                       select fi).ToList();
        FamilyInstance caixaAlvo = null;
        double minCaixa = double.MaxValue;
        foreach (FamilyInstance c in caixas)
        {
            Parameter lvlParam = ((Element)c).get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (lvlParam != null && lvlParam.AsElementId() != ElementId.InvalidElementId && lvlParam.AsElementId() != JigLancamentoManager.LevelId)
            {
                continue;
            }
            if (lvlParam == null || lvlParam.AsElementId() == ElementId.InvalidElementId)
            {
                XYZ locPt = (c.Location as LocationPoint)?.Point;
                if (locPt == null)
                {
                    continue;
                }
                double zMin = JigLancamentoManager.ZNivel - 0.9842519685039369;
                double zMax = JigLancamentoManager.ZNivel + 9.84251968503937;
                if (locPt.Z < zMin || locPt.Z > zMax)
                {
                    continue;
                }
            }
            XYZ pBox = (c.Location as LocationPoint)?.Point ?? XYZ.Zero;
            double d = new XYZ(ptTarget2D.X, ptTarget2D.Y, 0.0).DistanceTo(new XYZ(pBox.X, pBox.Y, 0.0));
            if (d < minCaixa)
            {
                minCaixa = d;
                caixaAlvo = c;
            }
        }
        if (caixaAlvo == null)
        {
            throw new Exception("Caixa Sifonada não encontrada no local do clique.");
        }
        Connector connCaixa = ObterConectorLivreMaisProximo(caixaAlvo, ptTarget2D);
        if (connCaixa == null)
        {
            throw new Exception("A Caixa Sifonada não possui conectores de entrada livres nessa face.");
        }
        XYZ destinoReal = connCaixa.Origin;
        XYZ dirConector = connCaixa.CoordinateSystem.BasisZ;
        List<XYZ> rota = ComandoLancamentoAutomatico.ResolverRotaChuveiro(ptSaidaOriginal, dirFace, destinoReal, dirConector);
        if (rota == null || rota.Count < 2)
        {
            return;
        }
        List<XYZ> ptPiso3D = new List<XYZ>();
        double cotaAtual = destinoReal.Z;
        XYZ ptAtual2D = new XYZ(destinoReal.X, destinoReal.Y, 0.0);
        ptPiso3D.Add(destinoReal);
        double inclinacao = 0.02;
        for (int i = rota.Count - 2; i >= 0; i--)
        {
            XYZ pt2D = new XYZ(rota[i].X, rota[i].Y, 0.0);
            double dist = ptAtual2D.DistanceTo(pt2D);
            cotaAtual += dist * inclinacao;
            ptPiso3D.Add(new XYZ(pt2D.X, pt2D.Y, cotaAtual));
            ptAtual2D = pt2D;
        }
        ptPiso3D.Reverse();
        List<Pipe> tubosPiso = new List<Pipe>();
        for (int i2 = 0; i2 < ptPiso3D.Count - 1; i2++)
        {
            Pipe p = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptPiso3D[i2], ptPiso3D[i2 + 1]);
            ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamChuveiro);
            tubosPiso.Add(p);
        }
        for (int i3 = 0; i3 < tubosPiso.Count - 1; i3++)
        {
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tubosPiso[i3], tubosPiso[i3 + 1], ptPiso3D[i3 + 1]);
        }
        doc.Regenerate();
        Connector cPipeCaixa = ComandoLancamentoAutomatico.GetConnectorClosestTo(tubosPiso.Last(), ptPiso3D.Last());
        if (cPipeCaixa != null && !cPipeCaixa.IsConnected)
        {
            try
            {
                cPipeCaixa.ConnectTo(connCaixa);
            }
            catch
            {
            }
        }
        bool saidaHorizontal = Math.Abs(dirFace.Z) < 0.1;
        XYZ ptBasePrumada = ptPiso3D.First();
        if (Math.Abs(ptBasePrumada.Z - ptSaidaOriginal.Z) > 0.01)
        {
            XYZ ptTopoPrumada = new XYZ(ptBasePrumada.X, ptBasePrumada.Y, ptSaidaOriginal.Z);
            Pipe tuboVertical = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptTopoPrumada, ptBasePrumada);
            ((Element)tuboVertical).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamChuveiro);
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboVertical, tubosPiso.First(), ptBasePrumada);
            if (saidaHorizontal)
            {
                Pipe tuboHoriz = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptSaidaOriginal, ptTopoPrumada);
                ((Element)tuboHoriz).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamChuveiro);
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboHoriz, tuboVertical, ptTopoPrumada);
                if (JigLancamentoManager.ConectorChuveiro == null)
                {
                    return;
                }
                Connector cPipeTopo = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboHoriz, ptSaidaOriginal);
                if (cPipeTopo != null && !cPipeTopo.IsConnected)
                {
                    try
                    {
                        cPipeTopo.ConnectTo(JigLancamentoManager.ConectorChuveiro);
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                if (JigLancamentoManager.ConectorChuveiro == null)
                {
                    return;
                }
                Connector cPipeTopo2 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboVertical, ptTopoPrumada);
                if (cPipeTopo2 != null && !cPipeTopo2.IsConnected)
                {
                    try
                    {
                        cPipeTopo2.ConnectTo(JigLancamentoManager.ConectorChuveiro);
                    }
                    catch
                    {
                    }
                }
            }
        }
        else
        {
            if (JigLancamentoManager.ConectorChuveiro == null)
            {
                return;
            }
            Connector cPipeTopo3 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tubosPiso.First(), ptBasePrumada);
            if (cPipeTopo3 != null && !cPipeTopo3.IsConnected)
            {
                try
                {
                    cPipeTopo3.ConnectTo(JigLancamentoManager.ConectorChuveiro);
                }
                catch
                {
                }
            }
        }
    }

    public static double ObterOffsetFaceJoelho(Document doc, ElementId sysId, ElementId pipeTypeId, ElementId levelId, double diametro, XYZ dirFaceJoelho)
    {
        double offset = diametro / 2.0;
        using (SubTransaction st = new SubTransaction(doc))
        {
            try
            {
                st.Start();
                XYZ pTopo = new XYZ(0.0, 0.0, 2.0);
                XYZ pTocoTopo = pTopo + dirFaceJoelho * 2.0;
                XYZ ptFundoBase = new XYZ(0.0, 0.0, 0.0);
                Pipe pHorizontal = Pipe.Create(doc, sysId, pipeTypeId, levelId, pTocoTopo, pTopo);
                Pipe pVertical = Pipe.Create(doc, sysId, pipeTypeId, levelId, pTopo, ptFundoBase);
                ((Element)pHorizontal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                ((Element)pVertical).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                doc.Regenerate();
                Connector cHoriz = ObterConectorMaisProximo(pHorizontal, pTopo);
                Connector cVert = ObterConectorMaisProximo(pVertical, pTopo);
                FamilyInstance dummyElbow = doc.Create.NewElbowFitting(cHoriz, cVert);
                doc.Regenerate();
                if (dummyElbow != null)
                {
                    BoundingBoxXYZ bb = ((Element)dummyElbow).get_BoundingBox((View)null);
                    if (bb != null)
                    {
                        XYZ center = (dummyElbow.Location as LocationPoint).Point;
                        offset = ((!(Math.Abs(dirFaceJoelho.X) > 0.5)) ? ((dirFaceJoelho.Y > 0.0) ? (bb.Max.Y - center.Y) : (center.Y - bb.Min.Y)) : ((dirFaceJoelho.X > 0.0) ? (bb.Max.X - center.X) : (center.X - bb.Min.X)));
                    }
                }
                st.RollBack();
            }
            catch
            {
                if (st.HasStarted())
                {
                    st.RollBack();
                }
            }
        }
        return offset;
    }

    private static Connector ObterConectorMaisProximo(Pipe pipe, XYZ point)
    {
        Connector closest = null;
        double minDist = double.MaxValue;
        foreach (Connector c in pipe.ConnectorManager.Connectors)
        {
            if (c.Origin.DistanceTo(point) < minDist)
            {
                minDist = c.Origin.DistanceTo(point);
                closest = c;
            }
        }
        return closest;
    }

    private void ModelarPia(Document doc)
    {
        XYZ ptParedeOriginal = JigLancamentoManager.PtParedePia;
        XYZ dirFace = JigLancamentoManager.DirParedePia;
        XYZ pt2 = JigLancamentoManager.Pt2;
        int rotaEscolhida = JigLancamentoManager.RotaEscolhida;
        _ = ((rotaEscolhida == 1) ? JigLancamentoManager.IntA : JigLancamentoManager.IntB);
        if ((JigLancamentoManager.Cfg.DestinoPia == 1 || JigLancamentoManager.Cfg.DestinoPia == 2) && JigLancamentoManager.TuboDestino != null)
        {
            Curve cTarget = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
            XYZ proj = cTarget.Project(pt2).XYZPoint;
            _ = new XYZ(proj.X, proj.Y, 0.0);
        }
        double diamPia = UnitUtils.ConvertToInternalUnits(JigLancamentoManager.Cfg.DiametroLavatorio, UnitTypeId.Millimeters);
        double offsetJoelho = ObterOffsetFaceJoelho(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, diamPia, dirFace);
        double elevacaoLevel = (doc.GetElement(JigLancamentoManager.LevelId) as Level)?.Elevation ?? 0.0;
        double zTopo = elevacaoLevel + JigLancamentoManager.Cfg.AlturaLavatorio / 0.3048;
        XYZ ptSubidaNaParede = ptParedeOriginal - dirFace * offsetJoelho;
        double avancoViga = 125.0 / 381.0;
        XYZ ptFundoBase = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, JigLancamentoManager.ZColetor);
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            ptFundoBase += dirFace * avancoViga;
        }
        if (zTopo < ptFundoBase.Z + 0.3)
        {
            zTopo = ptFundoBase.Z + 0.3;
        }
        XYZ topoVerticalFinal = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, zTopo);
        double inclinacao = 0.02;
        List<XYZ> pontosHoriz = new List<XYZ>();
        if (JigLancamentoManager.RotasPia == null || rotaEscolhida < 0 || rotaEscolhida >= JigLancamentoManager.RotasPia.Count)
        {
            return;
        }
        List<XYZ> rotaPlan = JigLancamentoManager.RotasPia[rotaEscolhida];
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            XYZ dirPerp = new XYZ(dirFace.X, dirFace.Y, 0.0).Normalize();
            List<XYZ> dirsOrigem = new List<XYZ>
            {
                dirPerp,
                new XYZ(dirPerp.X - dirPerp.Y, dirPerp.X + dirPerp.Y, 0.0).Normalize(),
                new XYZ(dirPerp.X + dirPerp.Y, 0.0 - dirPerp.X + dirPerp.Y, 0.0).Normalize()
            };
            List<List<XYZ>> novasRotas = ComandoLancamentoAutomatico.CalcularRotasRosaDosVentos(ptFundoBase, rotaPlan.Last(), JigLancamentoManager.ZPreview, dirsOrigem, isCaixaSifonada: false, JigLancamentoManager.Cfg.DiametroLavatorio);
            if (novasRotas != null && novasRotas.Count > rotaEscolhida)
            {
                rotaPlan = novasRotas[rotaEscolhida];
            }
        }
        XYZ ptAtual2D = new XYZ(ptFundoBase.X, ptFundoBase.Y, 0.0);
        double cotaAtual = JigLancamentoManager.ZColetor;
        pontosHoriz.Add(ptFundoBase);
        for (int i = 1; i < rotaPlan.Count; i++)
        {
            XYZ pt2D = new XYZ(rotaPlan[i].X, rotaPlan[i].Y, 0.0);
            double dist = ptAtual2D.DistanceTo(pt2D);
            cotaAtual -= dist * inclinacao;
            pontosHoriz.Add(new XYZ(pt2D.X, pt2D.Y, cotaAtual));
            ptAtual2D = pt2D;
        }
        if (pontosHoriz.Count < 2)
        {
            return;
        }
        XYZ ptFim3D = pontosHoriz.Last();
        List<Pipe> tubosHoriz = new List<Pipe>();
        for (int j = 0; j < pontosHoriz.Count - 1; j++)
        {
            Pipe p = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pontosHoriz[j], pontosHoriz[j + 1]);
            ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
            tubosHoriz.Add(p);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(p.Id);
        }
        for (int k = 0; k < tubosHoriz.Count - 1; k++)
        {
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tubosHoriz[k], tubosHoriz[k + 1], pontosHoriz[k + 1]);
        }
        Pipe tuboPrumadaPrincipal;
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            double zBaixoDesvio = elevacaoLevel + -0.07381889763779527;
            double zAltoDesvio = zBaixoDesvio + avancoViga;
            XYZ pBaixoDesvio = new XYZ(ptFundoBase.X, ptFundoBase.Y, zBaixoDesvio);
            XYZ pAltoDesvio = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, zAltoDesvio);
            Pipe tuboCima = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, pAltoDesvio);
            Pipe tuboDiag = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pAltoDesvio, pBaixoDesvio);
            Pipe tuboBaixo = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pBaixoDesvio, ptFundoBase);
            ((Element)tuboCima).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
            ((Element)tuboDiag).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
            ((Element)tuboBaixo).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboCima.Id);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboDiag.Id);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboBaixo.Id);
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboCima, tuboDiag, pAltoDesvio);
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboDiag, tuboBaixo, pBaixoDesvio);
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboBaixo, tubosHoriz[0], ptFundoBase);
            tuboPrumadaPrincipal = tuboCima;
        }
        else
        {
            tuboPrumadaPrincipal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, ptFundoBase);
            ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboPrumadaPrincipal.Id);
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tubosHoriz[0], ptFundoBase);
        }
        Pipe tuboFinal = tubosHoriz.Last();
        if (JigLancamentoManager.Cfg.DestinoPia == 1 && JigLancamentoManager.TuboDestino != null)
        {
            Pipe tuboQueda = JigLancamentoManager.TuboDestino;
            Curve cTubo = (tuboQueda.Location as LocationCurve).Curve;
            XYZ cP0 = cTubo.GetEndPoint(0);
            XYZ cP1 = cTubo.GetEndPoint(1);
            if (cP0.Z > cP1.Z)
            {
                cP0 = new XYZ(cP0.X, cP0.Y, ptFim3D.Z);
            }
            else
            {
                cP1 = new XYZ(cP1.X, cP1.Y, ptFim3D.Z);
            }
            if (cP0.DistanceTo(cP1) > 0.1)
            {
                (tuboQueda.Location as LocationCurve).Curve = Line.CreateBound(cP0, cP1);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboFinal, tuboQueda, ptFim3D);
            }
        }
        else if (JigLancamentoManager.Cfg.DestinoPia == 2 && JigLancamentoManager.TuboDestino != null)
        {
            double distRecuo = 275.0 / 762.0;
            Curve curvaFinal = (tuboFinal.Location as LocationCurve).Curve;
            XYZ f0 = curvaFinal.GetEndPoint(0);
            XYZ f1 = curvaFinal.GetEndPoint(1);
            XYZ pStart = ((f0.DistanceTo(ptFim3D) > f1.DistanceTo(ptFim3D)) ? f0 : f1);
            XYZ pEnd = ((f0.DistanceTo(ptFim3D) <= f1.DistanceTo(ptFim3D)) ? f0 : f1);
            XYZ dirFinal = (pEnd - pStart).Normalize();
            XYZ pFimRecuado = pEnd - dirFinal * distRecuo;
            if (pStart.DistanceTo(pEnd) > distRecuo)
            {
                (tuboFinal.Location as LocationCurve).Curve = Line.CreateBound(pStart, pFimRecuado);
                doc.Regenerate();
                XYZ pFimTubo45 = new XYZ(pEnd.X, pEnd.Y, pFimRecuado.Z - distRecuo);
                Curve cTarget2 = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
                pFimTubo45 = cTarget2.Project(pFimTubo45).XYZPoint;
                Pipe tubo45 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pFimRecuado, pFimTubo45);
                ((Element)tubo45).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tubo45.Id);
                FamilyInstance joelho45 = ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboFinal, tubo45, pFimRecuado);
                ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45, pFimTubo45);
                doc.Regenerate();
                if (joelho45 != null)
                {
                    Parameter pLigacao = joelho45.LookupParameter("Ligação em Conexão");
                    if (pLigacao != null && !pLigacao.IsReadOnly)
                    {
                        pLigacao.Set(1);
                    }
                }
            }
        }
        XYZ pTopo = topoVerticalFinal;
        XYZ pTocoTopo = pTopo + dirFace * 0.5;
        Pipe tuboTocoTopo = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pTocoTopo, pTopo);
        ((Element)tuboTocoTopo).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamPia);
        doc.Regenerate();
        ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboTocoTopo, tuboPrumadaPrincipal, pTopo);
        doc.Regenerate();
        doc.Delete(tuboTocoTopo.Id);
    }

    private Pipe CriarRamalComReducaoJuncao(Document doc, XYZ pLadoElbow, XYZ pLadoJuncao, double diamMaquina, double diam40, Pipe tuboDestino, out Pipe tuboLadoJuncao, out bool precisaReducaoOut)
    {
        double diam50 = UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters);
        double diam75 = UnitUtils.ConvertToInternalUnits(75.0, UnitTypeId.Millimeters);
        double diam100 = UnitUtils.ConvertToInternalUnits(100.0, UnitTypeId.Millimeters);
        double tol = 5.0 / 508.0;
        bool precisaReducao = false;
        if (tuboDestino != null && Math.Abs(diamMaquina - diam40) < tol)
        {
            double diamDestino = ((Element)tuboDestino).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
            if (Math.Abs(diamDestino - diam75) < tol || Math.Abs(diamDestino - diam100) < tol)
            {
                precisaReducao = true;
            }
        }
        precisaReducaoOut = precisaReducao;
        if (!precisaReducao)
        {
            Pipe tuboUnico = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pLadoElbow, pLadoJuncao);
            ((Element)tuboUnico).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboUnico.Id);
            tuboLadoJuncao = tuboUnico;
            return tuboUnico;
        }
        double distTotal = pLadoElbow.DistanceTo(pLadoJuncao);
        double compTrecho40 = 125.0 / 762.0;
        if (compTrecho40 > distTotal)
        {
            compTrecho40 = distTotal;
        }
        XYZ dir = (pLadoJuncao - pLadoElbow).Normalize();
        XYZ pReducao = pLadoElbow + dir * compTrecho40;
        Pipe tubo40 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pLadoElbow, pReducao);
        ((Element)tubo40).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diam40);
        JigLancamentoManager.IdsCriadosNestaSessao.Add(tubo40.Id);
        Pipe tubo50 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pReducao, pLadoJuncao);
        ((Element)tubo50).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diam50);
        JigLancamentoManager.IdsCriadosNestaSessao.Add(tubo50.Id);
        doc.Regenerate();
        Connector c40 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tubo40, pReducao);
        Connector c50 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tubo50, pReducao);
        if (c40 != null && c50 != null)
        {
            try
            {
                FamilyInstance bucha = doc.Create.NewTransitionFitting(c50, c40);
                if (bucha != null)
                {
                    JigLancamentoManager.IdsCriadosNestaSessao.Add(bucha.Id);
                    Parameter pLigacao = bucha.LookupParameter("Ligação em Tubo");
                    if (pLigacao != null && !pLigacao.IsReadOnly)
                    {
                        pLigacao.Set(0);
                    }
                }
                doc.Regenerate();
            }
            catch
            {
            }
        }
        tuboLadoJuncao = tubo50;
        return tubo40;
    }

    private void ModelarMaquina(Document doc)
    {
        XYZ ptParedeOriginal = JigLancamentoManager.PtParedeMaquina;
        XYZ dirFace = JigLancamentoManager.DirParedeMaquina;
        XYZ pt2 = JigLancamentoManager.Pt2;
        int rotaEscolhida = JigLancamentoManager.RotaEscolhida;
        _ = ((rotaEscolhida == 1) ? JigLancamentoManager.IntA : JigLancamentoManager.IntB);
        if ((JigLancamentoManager.Cfg.DestinoMaquina == 1 || JigLancamentoManager.Cfg.DestinoMaquina == 2) && JigLancamentoManager.TuboDestino != null)
        {
            Curve cTarget = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
            XYZ proj = cTarget.Project(pt2).XYZPoint;
            _ = new XYZ(proj.X, proj.Y, 0.0);
        }
        double diamMaquina = UnitUtils.ConvertToInternalUnits(JigLancamentoManager.Cfg.DiametroMaquina, UnitTypeId.Millimeters);
        double diam40 = UnitUtils.ConvertToInternalUnits(40.0, UnitTypeId.Millimeters);
        double offsetJoelho = ObterOffsetFaceJoelho(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, diamMaquina, dirFace);
        double elevacaoLevel = (doc.GetElement(JigLancamentoManager.LevelId) as Level)?.Elevation ?? 0.0;
        double zTopo = elevacaoLevel + JigLancamentoManager.Cfg.AlturaMaquina / 0.3048;
        if (JigLancamentoManager.Cfg.DiametroMaquina > 45.0 && JigLancamentoManager.Cfg.DestinoMaquina == 0)
        {
            zTopo -= 0.11909448818897637;
        }
        if (JigLancamentoManager.Cfg.DiametroMaquina < 45.0 && JigLancamentoManager.Cfg.DestinoMaquina == 0)
        {
            zTopo += 0.046259842519685034;
        }
        double avancoExtraMaquina = ((JigLancamentoManager.Cfg.DiametroMaquina < 45.0) ? 0.0 : (25.0 / 381.0));
        XYZ ptSubidaNaParede = ptParedeOriginal - dirFace * (offsetJoelho - avancoExtraMaquina);
        double avancoViga = 0.22965879265091865;
        bool conexaoDireta45 = false;
        XYZ pAlvoPrumada = null;
        if (JigLancamentoManager.Cfg.DestinoMaquina == 2 && JigLancamentoManager.TuboDestino != null)
        {
            Curve cTarget2 = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
            XYZ ptPlanoXY = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, ptSubidaNaParede.Z);
            pAlvoPrumada = cTarget2.Project(ptPlanoXY).XYZPoint;
            double distEixosPrumada = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, 0.0).DistanceTo(new XYZ(pAlvoPrumada.X, pAlvoPrumada.Y, 0.0));
            if (distEixosPrumada < 250.0 / 381.0)
            {
                conexaoDireta45 = true;
            }
        }
        XYZ ptEixoVert = ptSubidaNaParede;
        if (conexaoDireta45)
        {
            double diamDestinoLocal = ((JigLancamentoManager.TuboDestino != null) ? ((Element)JigLancamentoManager.TuboDestino).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble() : diamMaquina);
            double folgaSuperficies = 0.09842519685039369;
            double distFundo = diamDestinoLocal / 2.0 + diamMaquina / 2.0 + folgaSuperficies;
            XYZ dirPrumadaToParede = (new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, 0.0) - new XYZ(pAlvoPrumada.X, pAlvoPrumada.Y, 0.0)).Normalize();
            ptEixoVert = pAlvoPrumada + dirPrumadaToParede * distFundo;
        }
        XYZ ptFundoBase = new XYZ(ptEixoVert.X, ptEixoVert.Y, JigLancamentoManager.ZColetor);
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            ptFundoBase += dirFace * avancoViga;
        }
        double zTopoFinal = zTopo;
        if (zTopoFinal < ptFundoBase.Z + 0.3)
        {
            zTopoFinal = ptFundoBase.Z + 0.3;
        }
        double distToco = ((JigLancamentoManager.Cfg.DiametroMaquina < 45.0) ? 0.3 : 0.1);
        XYZ pBuchaBase_XY = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, 0.0) + dirFace * (distToco * 0.7071);
        double distTopoXY = new XYZ(ptEixoVert.X, ptEixoVert.Y, 0.0).DistanceTo(pBuchaBase_XY);
        XYZ topoVerticalFinal = new XYZ(ptEixoVert.X, ptEixoVert.Y, zTopoFinal - distTopoXY);
        double inclinacao = 0.02;
        List<XYZ> pontosHoriz = new List<XYZ>();
        if (JigLancamentoManager.RotasPia == null || rotaEscolhida < 0 || rotaEscolhida >= JigLancamentoManager.RotasPia.Count)
        {
            return;
        }
        List<XYZ> rotaPlan = JigLancamentoManager.RotasPia[rotaEscolhida];
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            XYZ dirPerp = new XYZ(dirFace.X, dirFace.Y, 0.0).Normalize();
            bool regraDistanciaPorDiametro = JigLancamentoManager.Cfg.DestinoMaquina == 2;
            List<XYZ> dirsOrigem = new List<XYZ>
            {
                dirPerp,
                new XYZ(dirPerp.X - dirPerp.Y, dirPerp.X + dirPerp.Y, 0.0).Normalize(),
                new XYZ(dirPerp.X + dirPerp.Y, 0.0 - dirPerp.X + dirPerp.Y, 0.0).Normalize()
            };
            if (regraDistanciaPorDiametro)
            {
                dirsOrigem.Add(new XYZ(0.0 - dirPerp.Y, dirPerp.X, 0.0));
                dirsOrigem.Add(new XYZ(dirPerp.Y, 0.0 - dirPerp.X, 0.0));
            }
            List<List<XYZ>> novasRotas = ComandoLancamentoAutomatico.CalcularRotasRosaDosVentos(ptFundoBase, rotaPlan.Last(), JigLancamentoManager.ZPreview, dirsOrigem, isCaixaSifonada: false, JigLancamentoManager.Cfg.DiametroMaquina, regraDistanciaPorDiametro);
            if (novasRotas != null && novasRotas.Count > 0)
            {
                XYZ dirOriginal = ((rotaPlan.Count >= 2) ? (rotaPlan[1] - rotaPlan[0]).Normalize() : null);
                List<XYZ> melhorMatch = null;
                double melhorScore = double.MinValue;
                foreach (List<XYZ> cand in novasRotas)
                {
                    if (cand.Count >= 2)
                    {
                        double scoreForma = ((cand.Count == rotaPlan.Count) ? 10.0 : 0.0);
                        double scoreDirecao = ((dirOriginal != null) ? (cand[1] - cand[0]).Normalize().DotProduct(dirOriginal) : 0.0);
                        double score = scoreForma + scoreDirecao;
                        if (score > melhorScore)
                        {
                            melhorScore = score;
                            melhorMatch = cand;
                        }
                    }
                }
                rotaPlan = ((melhorMatch != null) ? melhorMatch : ((novasRotas.Count <= rotaEscolhida) ? novasRotas[0] : novasRotas[rotaEscolhida]));
            }
        }
        XYZ ptAtual2D = new XYZ(ptFundoBase.X, ptFundoBase.Y, 0.0);
        double cotaAtual = JigLancamentoManager.ZColetor;
        Pipe tuboPrumadaPrincipal;
        if (conexaoDireta45)
        {
            double distFundo2 = 125.0 / 762.0;
            double zFundoDrop = JigLancamentoManager.ZColetor + distFundo2;
            XYZ ptDrop = new XYZ(ptFundoBase.X, ptFundoBase.Y, zFundoDrop);
            XYZ ptJuncao = new XYZ(pAlvoPrumada.X, pAlvoPrumada.Y, JigLancamentoManager.ZColetor);
            if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
            {
                double zBaixoDesvio = elevacaoLevel + -0.07381889763779527;
                double zAltoDesvio = zBaixoDesvio + avancoViga;
                XYZ pBaixoDesvio = new XYZ(ptFundoBase.X, ptFundoBase.Y, zBaixoDesvio);
                XYZ pAltoDesvio = new XYZ(ptEixoVert.X, ptEixoVert.Y, zAltoDesvio);
                Pipe tuboCima = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, pAltoDesvio);
                Pipe tuboDiag = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pAltoDesvio, pBaixoDesvio);
                Pipe tuboBaixo = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pBaixoDesvio, ptDrop);
                ((Element)tuboCima).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                ((Element)tuboDiag).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                ((Element)tuboBaixo).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboCima.Id);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboDiag.Id);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboBaixo.Id);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboCima, tuboDiag, pAltoDesvio);
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboDiag, tuboBaixo, pBaixoDesvio);
                tuboPrumadaPrincipal = tuboCima;
                Pipe tubo45Juncao;
                bool precisaReducao1;
                Pipe tubo45Elbow = CriarRamalComReducaoJuncao(doc, ptDrop, ptJuncao, diamMaquina, diam40, JigLancamentoManager.TuboDestino, out tubo45Juncao, out precisaReducao1);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboBaixo, tubo45Elbow, ptDrop);
                FamilyInstance wyeMaquina1 = ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45Juncao, ptJuncao, !precisaReducao1);
                CorrigirRotacaoJuncaoSeInvertida(doc, wyeMaquina1, JigLancamentoManager.TuboDestino, ptDrop);
                doc.Regenerate();
                try
                {
                    Curve cb = (tuboBaixo.Location as LocationCurve).Curve;
                    XYZ pb0 = cb.GetEndPoint(0);
                    XYZ pb1 = cb.GetEndPoint(1);
                    XYZ botBaixo = ((pb0.Z < pb1.Z) ? pb0 : pb1);
                    XYZ topBaixo = ((pb0.Z > pb1.Z) ? pb0 : pb1);
                    XYZ pAlvoBucha = new XYZ(ptParedeOriginal.X, ptParedeOriginal.Y, zTopo);
                    double distHorizontal = new XYZ(botBaixo.X, botBaixo.Y, 0.0).DistanceTo(new XYZ(pAlvoBucha.X, pAlvoBucha.Y, 0.0));
                    if (distHorizontal < 100.0 / 381.0)
                    {
                        distHorizontal = 100.0 / 381.0;
                    }
                    XYZ newTopoVerticalFinal = new XYZ(botBaixo.X, botBaixo.Y, zTopo - distHorizontal);
                    XYZ shift3D = newTopoVerticalFinal - topoVerticalFinal;
                    XYZ newTopBaixo = topBaixo + shift3D;
                    (tuboBaixo.Location as LocationCurve).Curve = Line.CreateBound(newTopBaixo, botBaixo);
                    ElementTransformUtils.MoveElement(doc, tuboDiag.Id, shift3D);
                    ElementTransformUtils.MoveElement(doc, tuboCima.Id, shift3D);
                    topoVerticalFinal = newTopoVerticalFinal;
                }
                catch
                {
                }
            }
            else if (JigLancamentoManager.Cfg.DiametroMaquina > 45.0)
            {
                double dHoriz45 = new XYZ(ptFundoBase.X - pAlvoPrumada.X, ptFundoBase.Y - pAlvoPrumada.Y, 0.0).GetLength();
                if (dHoriz45 < 0.09842519685039369)
                {
                    dHoriz45 = 0.09842519685039369;
                }
                XYZ ptDropDN50 = new XYZ(ptFundoBase.X, ptFundoBase.Y, JigLancamentoManager.ZColetor + dHoriz45);
                XYZ ptJuncaoDN50 = new XYZ(pAlvoPrumada.X, pAlvoPrumada.Y, JigLancamentoManager.ZColetor);
                tuboPrumadaPrincipal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, ptDropDN50);
                ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboPrumadaPrincipal.Id);
                Pipe tubo45Juncao2;
                Pipe tubo45Elbow2 = CriarRamalComReducaoJuncao(doc, ptDropDN50, ptJuncaoDN50, diamMaquina, diam40, JigLancamentoManager.TuboDestino, out tubo45Juncao2, out _);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tubo45Elbow2, ptDropDN50);
                FamilyInstance wyeMaquina2 = ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45Juncao2, ptJuncaoDN50);
                CorrigirRotacaoJuncaoSeInvertida(doc, wyeMaquina2, JigLancamentoManager.TuboDestino, ptDropDN50);
                doc.Regenerate();
                try
                {
                    Curve c = (tuboPrumadaPrincipal.Location as LocationCurve).Curve;
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);
                    XYZ pBot = ((p0.Z < p1.Z) ? p0 : p1);
                    XYZ pAlvoBucha2 = new XYZ(ptParedeOriginal.X, ptParedeOriginal.Y, zTopo);
                    double distHorizontal2 = new XYZ(pBot.X, pBot.Y, 0.0).DistanceTo(new XYZ(pAlvoBucha2.X, pAlvoBucha2.Y, 0.0));
                    if (distHorizontal2 < 100.0 / 381.0)
                    {
                        distHorizontal2 = 100.0 / 381.0;
                    }
                    XYZ newPTop = new XYZ(pBot.X, pBot.Y, zTopo - distHorizontal2);
                    (tuboPrumadaPrincipal.Location as LocationCurve).Curve = Line.CreateBound(newPTop, pBot);
                    topoVerticalFinal = newPTop;
                }
                catch
                {
                }
            }
            else
            {
                tuboPrumadaPrincipal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, ptDrop);
                ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboPrumadaPrincipal.Id);
                Pipe tubo45Juncao3;
                bool precisaReducao2;
                Pipe tubo45Elbow3 = CriarRamalComReducaoJuncao(doc, ptDrop, ptJuncao, diamMaquina, diam40, JigLancamentoManager.TuboDestino, out tubo45Juncao3, out precisaReducao2);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tubo45Elbow3, ptDrop);
                FamilyInstance wyeMaquina3 = ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45Juncao3, ptJuncao, !precisaReducao2);
                CorrigirRotacaoJuncaoSeInvertida(doc, wyeMaquina3, JigLancamentoManager.TuboDestino, ptDrop);
                doc.Regenerate();
                try
                {
                    Curve c2 = (tuboPrumadaPrincipal.Location as LocationCurve).Curve;
                    XYZ p2 = c2.GetEndPoint(0);
                    XYZ p3 = c2.GetEndPoint(1);
                    XYZ pBot2 = ((p2.Z < p3.Z) ? p2 : p3);
                    XYZ pAlvoBucha3 = new XYZ(ptParedeOriginal.X, ptParedeOriginal.Y, zTopo);
                    double distHorizontal3 = new XYZ(pBot2.X, pBot2.Y, 0.0).DistanceTo(new XYZ(pAlvoBucha3.X, pAlvoBucha3.Y, 0.0));
                    if (distHorizontal3 < 100.0 / 381.0)
                    {
                        distHorizontal3 = 100.0 / 381.0;
                    }
                    XYZ newPTop2 = new XYZ(pBot2.X, pBot2.Y, zTopo - distHorizontal3);
                    (tuboPrumadaPrincipal.Location as LocationCurve).Curve = Line.CreateBound(newPTop2, pBot2);
                    topoVerticalFinal = newPTop2;
                }
                catch
                {
                }
            }
        }
        else
        {
            pontosHoriz.Add(ptFundoBase);
            for (int i = 1; i < rotaPlan.Count; i++)
            {
                XYZ pt2D = new XYZ(rotaPlan[i].X, rotaPlan[i].Y, 0.0);
                double dist = ptAtual2D.DistanceTo(pt2D);
                cotaAtual -= dist * inclinacao;
                pontosHoriz.Add(new XYZ(pt2D.X, pt2D.Y, cotaAtual));
                ptAtual2D = pt2D;
            }
            if (pontosHoriz.Count < 2)
            {
                return;
            }
            XYZ ptFim3D = pontosHoriz.Last();
            List<Pipe> tubosHoriz = new List<Pipe>();
            for (int j = 0; j < pontosHoriz.Count - 1; j++)
            {
                Pipe p4 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pontosHoriz[j], pontosHoriz[j + 1]);
                ((Element)p4).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                tubosHoriz.Add(p4);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(p4.Id);
            }
            for (int k = 0; k < tubosHoriz.Count - 1; k++)
            {
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tubosHoriz[k], tubosHoriz[k + 1], pontosHoriz[k + 1]);
            }
            if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
            {
                double zBaixoDesvio2 = elevacaoLevel + -0.07381889763779527;
                double zAltoDesvio2 = zBaixoDesvio2 + avancoViga;
                XYZ pBaixoDesvio2 = new XYZ(ptFundoBase.X, ptFundoBase.Y, zBaixoDesvio2);
                XYZ pAltoDesvio2 = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, zAltoDesvio2);
                Pipe tuboCima2 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, pAltoDesvio2);
                Pipe tuboDiag2 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pAltoDesvio2, pBaixoDesvio2);
                Pipe tuboBaixo2 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pBaixoDesvio2, ptFundoBase);
                ((Element)tuboCima2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                ((Element)tuboDiag2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                ((Element)tuboBaixo2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboCima2.Id);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboDiag2.Id);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboBaixo2.Id);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboCima2, tuboDiag2, pAltoDesvio2);
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboDiag2, tuboBaixo2, pBaixoDesvio2);
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboBaixo2, tubosHoriz[0], ptFundoBase);
                tuboPrumadaPrincipal = tuboCima2;
            }
            else
            {
                tuboPrumadaPrincipal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, ptFundoBase);
                ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
                JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboPrumadaPrincipal.Id);
                doc.Regenerate();
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tubosHoriz[0], ptFundoBase);
            }
            Pipe tuboFinal = tubosHoriz.Last();
            if (JigLancamentoManager.Cfg.DestinoMaquina == 1 && JigLancamentoManager.TuboDestino != null)
            {
                Pipe tuboQueda = JigLancamentoManager.TuboDestino;
                Curve cTubo = (tuboQueda.Location as LocationCurve).Curve;
                XYZ cP0 = cTubo.GetEndPoint(0);
                XYZ cP1 = cTubo.GetEndPoint(1);
                if (cP0.Z > cP1.Z)
                {
                    cP0 = new XYZ(cP0.X, cP0.Y, ptFim3D.Z);
                }
                else
                {
                    cP1 = new XYZ(cP1.X, cP1.Y, ptFim3D.Z);
                }
                if (cP0.DistanceTo(cP1) > 0.1)
                {
                    (tuboQueda.Location as LocationCurve).Curve = Line.CreateBound(cP0, cP1);
                    doc.Regenerate();
                    ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboFinal, tuboQueda, ptFim3D);
                }
            }
            else if (JigLancamentoManager.Cfg.DestinoMaquina == 2 && JigLancamentoManager.TuboDestino != null)
            {
                double distRecuo = 275.0 / 762.0;
                Curve curvaFinal = (tuboFinal.Location as LocationCurve).Curve;
                XYZ f0 = curvaFinal.GetEndPoint(0);
                XYZ f1 = curvaFinal.GetEndPoint(1);
                XYZ pStart = ((f0.DistanceTo(ptFim3D) > f1.DistanceTo(ptFim3D)) ? f0 : f1);
                XYZ pEnd = ((f0.DistanceTo(ptFim3D) <= f1.DistanceTo(ptFim3D)) ? f0 : f1);
                XYZ dirFinal = (pEnd - pStart).Normalize();
                XYZ pFimRecuado = pEnd - dirFinal * distRecuo;
                if (pStart.DistanceTo(pEnd) > distRecuo)
                {
                    (tuboFinal.Location as LocationCurve).Curve = Line.CreateBound(pStart, pFimRecuado);
                    doc.Regenerate();
                    Curve cTarget3 = (JigLancamentoManager.TuboDestino.Location as LocationCurve).Curve;
                    XYZ ptPrumadaProj = cTarget3.Project(new XYZ(pFimRecuado.X, pFimRecuado.Y, cTarget3.GetEndPoint(0).Z)).XYZPoint;
                    XYZ deltaXY = new XYZ(pFimRecuado.X - ptPrumadaProj.X, pFimRecuado.Y - ptPrumadaProj.Y, 0.0);
                    double dHoriz46 = deltaXY.GetLength();
                    double dRequired = 125.0 / 762.0;
                    if (dHoriz46 < dRequired)
                    {
                        double deficit = dRequired - dHoriz46;
                        XYZ pFimRecuadoNovo = pFimRecuado - dirFinal * deficit;
                        if (pStart.DistanceTo(pFimRecuadoNovo) > 0.01)
                        {
                            pFimRecuado = pFimRecuadoNovo;
                            (tuboFinal.Location as LocationCurve).Curve = Line.CreateBound(pStart, pFimRecuado);
                            doc.Regenerate();
                            deltaXY = new XYZ(pFimRecuado.X - ptPrumadaProj.X, pFimRecuado.Y - ptPrumadaProj.Y, 0.0);
                            dHoriz46 = deltaXY.GetLength();
                        }
                        dHoriz46 = Math.Max(dHoriz46, dRequired);
                    }
                    double zJuncao = pFimRecuado.Z - dHoriz46;
                    XYZ pJuncao = new XYZ(ptPrumadaProj.X, ptPrumadaProj.Y, zJuncao);
                    Pipe tubo45Juncao4;
                    bool precisaReducao3;
                    Pipe tubo45Elbow4 = CriarRamalComReducaoJuncao(doc, pFimRecuado, pJuncao, diamMaquina, diam40, JigLancamentoManager.TuboDestino, out tubo45Juncao4, out precisaReducao3);
                    doc.Regenerate();
                    FamilyInstance joelhoRecuo = ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboFinal, tubo45Elbow4, pFimRecuado);
                    FamilyInstance wyeMaquina4 = ConectarJuncaoPrumada(doc, JigLancamentoManager.TuboDestino, tubo45Juncao4, pJuncao, !precisaReducao3);
                    CorrigirRotacaoJuncaoSeInvertida(doc, wyeMaquina4, JigLancamentoManager.TuboDestino, pFimRecuado);
                    doc.Regenerate();
                    if (joelhoRecuo != null)
                    {
                        Parameter pLigacao = joelhoRecuo.LookupParameter("Ligação em Conexão");
                        if (pLigacao != null && !pLigacao.IsReadOnly)
                        {
                            pLigacao.Set(1);
                        }
                    }
                }
            }
        }
        double zEixoBucha = zTopo;
        XYZ pTopo = topoVerticalFinal;
        XYZ pAlvoBuchaFinal = new XYZ(ptParedeOriginal.X, ptParedeOriginal.Y, zEixoBucha) + dirFace * 0.020669291338582675;
        XYZ horizDelta = new XYZ(pAlvoBuchaFinal.X - pTopo.X, pAlvoBuchaFinal.Y - pTopo.Y, 0.0);
        double horizMag = horizDelta.GetLength();
        XYZ dirReal = ((horizMag > 1E-06) ? horizDelta.Normalize() : dirFace);
        double horizMagMin = ((diamMaquina == 40.0) ? (25.0 / 762.0) : (100.0 / 381.0));
        if (horizMag < horizMagMin)
        {
            horizMag = horizMagMin;
        }
        XYZ pPontoMaquina = new XYZ(pTopo.X, pTopo.Y, zEixoBucha) + dirReal * horizMag;
        if (diamMaquina == 40.0)
        {
            Pipe tuboToco40 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pTopo, pPontoMaquina);
            ((Element)tuboToco40).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
            JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboToco40.Id);
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboToco40, tuboPrumadaPrincipal, pTopo);
            doc.Regenerate();
            try
            {
                Connector cLivre40 = null;
                foreach (Connector c3 in tuboToco40.ConnectorManager.Connectors)
                {
                    if (!c3.IsConnected && c3.ConnectorType != ConnectorType.Logical)
                    {
                        cLivre40 = c3;
                        break;
                    }
                }
                if (cLivre40 != null && cLivre40.Origin.DistanceTo(pPontoMaquina) > 0.001)
                {
                    XYZ shift40 = pPontoMaquina - cLivre40.Origin;
                    Curve cToco40 = (tuboToco40.Location as LocationCurve).Curve;
                    XYZ end0_40 = cToco40.GetEndPoint(0);
                    XYZ end1_40 = cToco40.GetEndPoint(1);
                    if (end0_40.DistanceTo(pPontoMaquina) < end1_40.DistanceTo(pPontoMaquina))
                    {
                        (tuboToco40.Location as LocationCurve).Curve = Line.CreateBound(end0_40 + shift40, end1_40);
                    }
                    else
                    {
                        (tuboToco40.Location as LocationCurve).Curve = Line.CreateBound(end0_40, end1_40 + shift40);
                    }
                    doc.Regenerate();
                }
                return;
            }
            catch
            {
                return;
            }
        }
        double deltaZ50 = zEixoBucha - pTopo.Z;
        XYZ pToco50 = new XYZ(pTopo.X + dirReal.X * deltaZ50, pTopo.Y + dirReal.Y * deltaZ50, zEixoBucha);
        Pipe tuboToco50 = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pTopo, pToco50);
        ((Element)tuboToco50).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamMaquina);
        JigLancamentoManager.IdsCriadosNestaSessao.Add(tuboToco50.Id);
        doc.Regenerate();
        _ = ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboToco50, tuboPrumadaPrincipal, pTopo);
        doc.Regenerate();
        XYZ dirToco50 = (pToco50 - pTopo).Normalize();
        XYZ pToco51 = pToco50 + dirToco50 * 0.15;
        Pipe tuboTocoTemp = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pToco50, pToco51);
        ((Element)tuboTocoTemp).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diam40);
        doc.Regenerate();
        Connector c50 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboToco50, pToco50);
        Connector c51 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboTocoTemp, pToco50);
        if (c50 == null || c51 == null)
        {
            return;
        }
        try
        {
            FamilyInstance bucha = doc.Create.NewTransitionFitting(c50, c51);
            doc.Delete(tuboTocoTemp.Id);
            if (bucha != null)
            {
                JigLancamentoManager.IdsCriadosNestaSessao.Add(bucha.Id);
            }
            doc.Regenerate();
            if (JigLancamentoManager.Cfg.DestinoMaquina == 0)
            {
                if (bucha != null)
                {
                    Parameter pLigacao2 = bucha.LookupParameter("Ligação em Tubo");
                    if (pLigacao2 != null && !pLigacao2.IsReadOnly)
                    {
                        pLigacao2.Set(0);
                    }
                    doc.Regenerate();
                }
                return;
            }
            XYZ pTip = null;
            if (bucha != null)
            {
                ConnectorManager cm = bucha.MEPModel.ConnectorManager;
                foreach (Connector c52 in cm.Connectors)
                {
                    if (!c52.IsConnected)
                    {
                        pTip = c52.Origin;
                        break;
                    }
                }
            }
            if (pTip != null && pTip.DistanceTo(pPontoMaquina) > 0.001 && bucha != null)
            {
                XYZ shift41 = pPontoMaquina - pTip;
                try
                {
                    ElementTransformUtils.MoveElement(doc, bucha.Id, shift41);
                }
                catch
                {
                }
            }
            doc.Regenerate();
            if (bucha != null)
            {
                Parameter pLigacao3 = bucha.LookupParameter("Ligação em Tubo");
                if (pLigacao3 != null && !pLigacao3.IsReadOnly)
                {
                    pLigacao3.Set(1);
                }
                doc.Regenerate();
            }
        }
        catch
        {
        }
    }

    private void ModelarLavatorio(Document doc)
    {
        XYZ ptParedeOriginal = JigLancamentoManager.PtParedeLavatorio;
        XYZ dirFace = JigLancamentoManager.DirParedeLavatorio;
        XYZ ptTarget2D = JigLancamentoManager.Pt2;
        double diamLav = UnitUtils.ConvertToInternalUnits(JigLancamentoManager.Cfg.DiametroLavatorio, UnitTypeId.Millimeters);
        double offsetJoelho = diamLav;
        double zVistaMin = JigLancamentoManager.ZVistaMin;
        double zVistaMax = JigLancamentoManager.ZVistaMax;
        Outline outline = new Outline(new XYZ(ptTarget2D.X - 2.0, ptTarget2D.Y - 2.0, zVistaMin), new XYZ(ptTarget2D.X + 2.0, ptTarget2D.Y + 2.0, zVistaMax));
        List<FamilyInstance> caixas = (from FamilyInstance fi in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WherePasses(new BoundingBoxIntersectsFilter(outline))
                                       where fi.Category != null && (fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PlumbingFixtures)) || fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_MechanicalEquipment)) || fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PipeAccessory)))
                                       select fi).ToList();
        FamilyInstance caixaAlvo = null;
        double minCaixa = double.MaxValue;
        foreach (FamilyInstance c in caixas)
        {
            Parameter lvlParam = ((Element)c).get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (lvlParam != null && lvlParam.AsElementId() != ElementId.InvalidElementId && lvlParam.AsElementId() != JigLancamentoManager.LevelId)
            {
                continue;
            }
            if (lvlParam == null || lvlParam.AsElementId() == ElementId.InvalidElementId)
            {
                XYZ locPt = (c.Location as LocationPoint)?.Point;
                if (locPt == null)
                {
                    continue;
                }
                double zMin = JigLancamentoManager.ZNivel - 0.9842519685039369;
                double zMax = JigLancamentoManager.ZNivel + 9.84251968503937;
                if (locPt.Z < zMin || locPt.Z > zMax)
                {
                    continue;
                }
            }
            XYZ pBox = (c.Location as LocationPoint)?.Point ?? XYZ.Zero;
            double d = new XYZ(ptTarget2D.X, ptTarget2D.Y, 0.0).DistanceTo(new XYZ(pBox.X, pBox.Y, 0.0));
            if (d < minCaixa)
            {
                minCaixa = d;
                caixaAlvo = c;
            }
        }
        if (caixaAlvo == null)
        {
            throw new Exception("Caixa Sifonada não encontrada no local do clique.");
        }
        Connector connCaixa = ObterConectorLivreMaisProximo(caixaAlvo, ptTarget2D);
        if (connCaixa == null)
        {
            throw new Exception("A Caixa Sifonada não possui conectores de entrada livres nessa face.");
        }
        XYZ destinoReal = connCaixa.Origin;
        XYZ dirConector = connCaixa.CoordinateSystem.BasisZ;
        double alturaParedePe = JigLancamentoManager.Cfg.AlturaLavatorio / 0.3048;
        double elevacaoLevel = (doc.GetElement(JigLancamentoManager.LevelId) as Level)?.Elevation ?? 0.0;
        double zTopo = elevacaoLevel + alturaParedePe;
        List<XYZ> rota = ComandoLancamentoAutomatico.ResolverRotaLavatorio(ptParedeOriginal, dirFace, destinoReal, dirConector);
        if (rota == null || rota.Count < 2)
        {
            return;
        }
        List<XYZ> ptPiso3D = new List<XYZ>();
        double cotaAtual = destinoReal.Z;
        XYZ ptAtual2D = new XYZ(destinoReal.X, destinoReal.Y, 0.0);
        ptPiso3D.Add(destinoReal);
        double inclinacao = 0.02;
        for (int i = rota.Count - 2; i >= 0; i--)
        {
            XYZ pt2D = new XYZ(rota[i].X, rota[i].Y, 0.0);
            double dist = ptAtual2D.DistanceTo(pt2D);
            cotaAtual += dist * inclinacao;
            ptPiso3D.Add(new XYZ(pt2D.X, pt2D.Y, cotaAtual));
            ptAtual2D = pt2D;
        }
        ptPiso3D.Reverse();
        List<Pipe> tubosPiso = new List<Pipe>();
        for (int i2 = 0; i2 < ptPiso3D.Count - 1; i2++)
        {
            Pipe p = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, ptPiso3D[i2], ptPiso3D[i2 + 1]);
            ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamLav);
            tubosPiso.Add(p);
        }
        for (int i3 = 0; i3 < tubosPiso.Count - 1; i3++)
        {
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tubosPiso[i3], tubosPiso[i3 + 1], ptPiso3D[i3 + 1]);
        }
        XYZ baseVerticalFinal = ptPiso3D.First();
        double cotaTopo = zTopo;
        if (cotaTopo < baseVerticalFinal.Z + 0.3)
        {
            cotaTopo = baseVerticalFinal.Z + 0.3;
        }
        XYZ ptSubidaNaParede = ptParedeOriginal - dirFace * offsetJoelho;
        XYZ topoVerticalFinal = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, cotaTopo);
        Pipe tuboPrumadaPrincipal = null;
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            double avancoMetrosDesvio = 0.21325459317585302;
            double cotaAltoDesvio = elevacaoLevel + 0.18044619422572178;
            double cotaBaixoDesvio = cotaAltoDesvio - avancoMetrosDesvio;
            if (cotaBaixoDesvio < baseVerticalFinal.Z + 275.0 / 762.0)
            {
                double diff = baseVerticalFinal.Z + 275.0 / 762.0 - cotaBaixoDesvio;
                cotaAltoDesvio += diff;
                cotaBaixoDesvio += diff;
            }
            XYZ pBaixoDesvio = new XYZ(baseVerticalFinal.X, baseVerticalFinal.Y, cotaBaixoDesvio);
            XYZ pAltoDesvio = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, cotaAltoDesvio);
            tuboPrumadaPrincipal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, pAltoDesvio);
            Pipe tuboDesvioDiagonal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pAltoDesvio, pBaixoDesvio);
            Pipe tuboPrumadaCurta = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pBaixoDesvio, baseVerticalFinal);
            ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamLav);
            ((Element)tuboDesvioDiagonal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamLav);
            ((Element)tuboPrumadaCurta).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamLav);
            doc.Regenerate();
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tuboDesvioDiagonal, pAltoDesvio);
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboDesvioDiagonal, tuboPrumadaCurta, pBaixoDesvio);
            ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaCurta, tubosPiso[0], baseVerticalFinal);
        }
        else
        {
            tuboPrumadaPrincipal = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, topoVerticalFinal, baseVerticalFinal);
            ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamLav);
            doc.Regenerate();
            if (tubosPiso.Count > 0 && tuboPrumadaPrincipal != null)
            {
                ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tubosPiso[0], baseVerticalFinal);
            }
        }
        Connector cPipeFim = ComandoLancamentoAutomatico.GetConnectorClosestTo(tubosPiso.Last(), ptPiso3D.Last());
        if (cPipeFim != null && !cPipeFim.IsConnected)
        {
            try
            {
                cPipeFim.ConnectTo(connCaixa);
            }
            catch
            {
            }
        }
        XYZ pTopo = topoVerticalFinal;
        XYZ pTocoTopo = pTopo + dirFace * 0.5;
        Pipe tuboTocoTopo = Pipe.Create(doc, JigLancamentoManager.Cfg.SistemaId, JigLancamentoManager.Cfg.TipoTuboEsgotoId, JigLancamentoManager.LevelId, pTopo, pTocoTopo);
        ((Element)tuboTocoTopo).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamLav);
        doc.Regenerate();
        ComandoLancamentoAutomatico.ConectarJoelho(doc, tuboPrumadaPrincipal, tuboTocoTopo, pTopo);
        doc.Regenerate();
        doc.Delete(tuboTocoTopo.Id);
    }

    private FamilyInstance ConectarJuncaoPrumada(Document doc, Pipe prumada, Pipe ramal, XYZ ptQuebra, bool deleteShortPipe = true, double anguloJuncao = 45.0)
    {
        FamilyInstance wye = null;
        try
        {
            Curve cTarget = (prumada.Location as LocationCurve).Curve;
            XYZ vColetor = (cTarget.GetEndPoint(1) - cTarget.GetEndPoint(0)).Normalize();
            XYZ ptQuebraProj = cTarget.Project(ptQuebra).XYZPoint;
            ElementId novoTuboId = PlumbingUtils.BreakCurve(doc, prumada.Id, ptQuebraProj);
            Pipe prumadaParte2 = doc.GetElement(novoTuboId) as Pipe;
            doc.Regenerate();
            XYZ vPerp = vColetor.CrossProduct(XYZ.BasisX).Normalize();
            if (vPerp.IsAlmostEqualTo(XYZ.Zero))
            {
                vPerp = vColetor.CrossProduct(XYZ.BasisY).Normalize();
            }
            Curve cRamal = (ramal.Location as LocationCurve).Curve;
            XYZ ptRamalLonge = ((cRamal.GetEndPoint(0).DistanceTo(ptQuebraProj) > cRamal.GetEndPoint(1).DistanceTo(ptQuebraProj)) ? cRamal.GetEndPoint(0) : cRamal.GetEndPoint(1));
            if (vPerp.DotProduct(ptRamalLonge - ptQuebraProj) < 0.0)
            {
                vPerp = -vPerp;
            }
            double diamRamal = ((Element)ramal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
            Pipe stubY = Pipe.Create(doc, prumada.MEPSystem.GetTypeId(), prumada.GetTypeId(), ObterLevelIdSeguro(doc, prumada), ptQuebraProj, ptQuebraProj + vPerp * 1.0);
            ((Element)stubY).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamal);
            doc.Regenerate();
            Connector cM1 = ComandoLancamentoAutomatico.GetConnectorClosestTo(prumada, ptQuebraProj);
            Connector cM2 = ComandoLancamentoAutomatico.GetConnectorClosestTo(prumadaParte2, ptQuebraProj);
            Connector cStub = ComandoLancamentoAutomatico.GetConnectorClosestTo(stubY, ptQuebraProj);
            if (cM1 != null && cM2 != null && cStub != null)
            {
                Connector cUp = ((cM1.CoordinateSystem.BasisZ.Z > 0.0) ? cM1 : cM2);
                Connector cDown = ((cM1.CoordinateSystem.BasisZ.Z < 0.0) ? cM1 : cM2);
                if (cUp == null)
                {
                    cUp = cM1;
                }
                if (cDown == null)
                {
                    cDown = cM2;
                }
                try
                {
                    wye = doc.Create.NewTeeFitting(cUp, cDown, cStub);
                    doc.Regenerate();
                }
                catch
                {
                }
                if (wye == null || wye.Symbol.Family.Name.ToLower().Contains("joelho") || wye.Symbol.Family.Name.ToLower().Contains("cotovelo") || wye.Symbol.Family.Name.ToLower().Contains("elbow"))
                {
                    if (wye != null)
                    {
                        doc.Delete(wye.Id);
                        doc.Regenerate();
                    }
                    try
                    {
                        wye = doc.Create.NewTeeFitting(cDown, cUp, cStub);
                        doc.Regenerate();
                    }
                    catch
                    {
                    }
                }
            }
            doc.Delete(stubY.Id);
            doc.Regenerate();
            if (wye != null)
            {
                SetarParametroAnguloLocal(wye, anguloJuncao);
                doc.Regenerate();
                Connector connWye = null;
                foreach (Connector c in wye.MEPModel.ConnectorManager.Connectors)
                {
                    XYZ dirC = c.CoordinateSystem.BasisZ.Normalize();
                    if (!dirC.IsAlmostEqualTo(vColetor) && !dirC.IsAlmostEqualTo(-vColetor))
                    {
                        connWye = c;
                        break;
                    }
                }
                if (connWye != null)
                {
                    XYZ currentYDir = connWye.CoordinateSystem.BasisZ.Normalize();
                    XYZ targetYDir = (ptRamalLonge - ptQuebraProj).Normalize();
                    XYZ planeY = (currentYDir - vColetor * currentYDir.DotProduct(vColetor)).Normalize();
                    XYZ planeT = (targetYDir - vColetor * targetYDir.DotProduct(vColetor)).Normalize();
                    if (planeY.GetLength() > 0.01 && planeT.GetLength() > 0.01)
                    {
                        double rotY = planeY.AngleTo(planeT);
                        if (vColetor.CrossProduct(planeY).DotProduct(planeT) < 0.0)
                        {
                            rotY = 0.0 - rotY;
                        }
                        ElementTransformUtils.RotateElement(doc, wye.Id, Line.CreateUnbound(ptQuebraProj, vColetor), rotY);
                    }
                    doc.Regenerate();
                    Connector cRamalConn = ComandoLancamentoAutomatico.GetConnectorClosestTo(ramal, ptQuebraProj);
                    if (cRamalConn != null)
                    {
                        Connector connTargetWye = null;
                        foreach (Connector c2 in wye.MEPModel.ConnectorManager.Connectors)
                        {
                            XYZ dirC2 = c2.CoordinateSystem.BasisZ.Normalize();
                            if (!dirC2.IsAlmostEqualTo(vColetor) && !dirC2.IsAlmostEqualTo(-vColetor))
                            {
                                connTargetWye = c2;
                                break;
                            }
                        }
                        if (connTargetWye != null)
                        {
                            double pipeLen = (ramal.Location as LocationCurve).Curve.Length;
                            if (pipeLen < 3.280839895013123 && deleteShortPipe)
                            {
                                Connector cRamalOutro = ComandoLancamentoAutomatico.GetConnectorClosestTo(ramal, ptRamalLonge);
                                Connector connJoelho = null;
                                FamilyInstance joelho = null;
                                if (cRamalOutro != null)
                                {
                                    foreach (Connector r in cRamalOutro.AllRefs)
                                    {
                                        if (r.Owner.Id != ramal.Id && r.ConnectorType != ConnectorType.Logical)
                                        {
                                            connJoelho = r;
                                            joelho = r.Owner as FamilyInstance;
                                            break;
                                        }
                                    }
                                }
                                doc.Delete(ramal.Id);
                                doc.Regenerate();
                                if (joelho != null)
                                {
                                    foreach (Parameter param in joelho.Parameters)
                                    {
                                        string nome = param.Definition.Name.ToLower();
                                        if (!nome.Contains("lig") || !nome.Contains("conex") || param.IsReadOnly)
                                        {
                                            continue;
                                        }
                                        if (param.StorageType == StorageType.Integer)
                                        {
                                            try
                                            {
                                                param.Set(1);
                                            }
                                            catch
                                            {
                                            }
                                        }
                                        else if (param.StorageType == StorageType.Double)
                                        {
                                            try
                                            {
                                                param.Set(1.0);
                                            }
                                            catch
                                            {
                                            }
                                        }
                                        else if (param.StorageType == StorageType.String)
                                        {
                                            try
                                            {
                                                param.Set("1");
                                            }
                                            catch
                                            {
                                            }
                                        }
                                    }
                                    doc.Regenerate();
                                    Connector connJoelhoAtualizado = null;
                                    foreach (Connector c3 in joelho.MEPModel.ConnectorManager.Connectors)
                                    {
                                        if (!c3.IsConnected && c3.ConnectorType != ConnectorType.Logical)
                                        {
                                            connJoelhoAtualizado = c3;
                                            break;
                                        }
                                    }
                                    if (connJoelhoAtualizado != null)
                                    {
                                        XYZ translation = connTargetWye.Origin - connJoelhoAtualizado.Origin;
                                        ElementTransformUtils.MoveElement(doc, joelho.Id, translation);
                                        doc.Regenerate();
                                        try
                                        {
                                            connJoelhoAtualizado.ConnectTo(connTargetWye);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                            else
                            {
                                try
                                {
                                    LocationCurve lc = ramal.Location as LocationCurve;
                                    XYZ ptStart = ((lc.Curve.GetEndPoint(0).DistanceTo(ptQuebraProj) > lc.Curve.GetEndPoint(1).DistanceTo(ptQuebraProj)) ? lc.Curve.GetEndPoint(0) : lc.Curve.GetEndPoint(1));
                                    lc.Curve = Line.CreateBound(ptStart, connTargetWye.Origin);
                                    doc.Regenerate();
                                }
                                catch
                                {
                                }
                                try
                                {
                                    cRamalConn.ConnectTo(connTargetWye);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return wye;
    }

    private void CorrigirRotacaoJuncaoSeInvertida(Document doc, FamilyInstance wye, Pipe tuboDestino, XYZ pLadoMaquina)
    {
        if (wye == null || tuboDestino == null)
        {
            return;
        }
        try
        {
            Curve cColetor = (tuboDestino.Location as LocationCurve).Curve;
            XYZ vColetor = (cColetor.GetEndPoint(1) - cColetor.GetEndPoint(0)).Normalize();
            Connector connBranch = null;
            foreach (Connector c in wye.MEPModel.ConnectorManager.Connectors)
            {
                XYZ dirC = c.CoordinateSystem.BasisZ.Normalize();
                if (!dirC.IsAlmostEqualTo(vColetor) && !dirC.IsAlmostEqualTo(-vColetor))
                {
                    connBranch = c;
                    break;
                }
            }
            if (connBranch != null)
            {
                XYZ dirAtual = connBranch.CoordinateSystem.BasisZ.Normalize();
                XYZ dirCorreta = (pLadoMaquina - connBranch.Origin).Normalize();
                if (dirAtual.DotProduct(dirCorreta) < 0.0)
                {
                    XYZ pEixoRotacao = cColetor.Project(connBranch.Origin).XYZPoint;
                    ElementTransformUtils.RotateElement(doc, wye.Id, Line.CreateUnbound(pEixoRotacao, vColetor), Math.PI);
                }
            }
        }
        catch
        {
        }
    }

    private void SetarParametroAnguloLocal(FamilyInstance fitting, double graus)
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

    private void AjustarLuvaEConexaoJoelho(FamilyInstance joelho, Pipe tuboMontante, bool ligacaoEmConexao)
    {
        if (joelho?.MEPModel?.ConnectorManager == null || tuboMontante == null)
        {
            return;
        }
        Connector fcMontante = null;
        double minDist = double.MaxValue;
        Connector cMont = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboMontante, (joelho.Location as LocationPoint).Point);
        if (cMont == null)
        {
            return;
        }
        foreach (Connector c in joelho.MEPModel.ConnectorManager.Connectors)
        {
            if (c.ConnectorType != ConnectorType.Logical)
            {
                double d = c.Origin.DistanceTo(cMont.Origin);
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
        Transform T = joelho.GetTotalTransform();
        XYZ dirMontanteLocal = T.Inverse.OfVector(fcMontante.CoordinateSystem.BasisZ);
        int valorAlvo = ((dirMontanteLocal.DotProduct(XYZ.BasisX) > 0.0) ? 1 : 0);
        foreach (Parameter param in joelho.Parameters)
        {
            string nome = param.Definition.Name.ToLower();
            if (nome.Contains("inverter") && nome.Contains("luva") && !param.IsReadOnly && param.StorageType == StorageType.Integer)
            {
                try
                {
                    param.Set(valorAlvo);
                }
                catch
                {
                }
            }
        }
        if (!ligacaoEmConexao)
        {
            return;
        }
        foreach (Parameter param2 in joelho.Parameters)
        {
            string nome2 = param2.Definition.Name.ToLower();
            if (!nome2.Contains("lig") || !nome2.Contains("conex") || param2.IsReadOnly)
            {
                continue;
            }
            if (param2.StorageType == StorageType.Integer)
            {
                try
                {
                    param2.Set(1);
                }
                catch
                {
                }
            }
            else if (param2.StorageType == StorageType.Double)
            {
                try
                {
                    param2.Set(1.0);
                }
                catch
                {
                }
            }
            else if (param2.StorageType == StorageType.String)
            {
                try
                {
                    param2.Set("1");
                }
                catch
                {
                }
            }
        }
    }

    private void EfetuarQuebraEConexao(Document doc, Pipe tuboMain, Pipe ramal, XYZ ptQuebra)
    {
        Curve curvaMain = (tuboMain.Location as LocationCurve).Curve;
        XYZ vColetor = (curvaMain.GetEndPoint(1) - curvaMain.GetEndPoint(0)).Normalize();
        ElementId novoTuboId = PlumbingUtils.BreakCurve(doc, tuboMain.Id, ptQuebra);
        Pipe tuboMainParte2 = doc.GetElement(novoTuboId) as Pipe;
        doc.Regenerate();
        _ = ((Element)tuboMain).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
        double diamRamal = ((Element)ramal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
        Curve curvaRamal = (ramal.Location as LocationCurve).Curve;
        XYZ ptFimRamal = ((curvaRamal.GetEndPoint(0).DistanceTo(ptQuebra) > curvaRamal.GetEndPoint(1).DistanceTo(ptQuebra)) ? curvaRamal.GetEndPoint(0) : curvaRamal.GetEndPoint(1));
        XYZ vStub = (ptFimRamal - ptQuebra).Normalize();
        XYZ vPerp = vColetor.CrossProduct(XYZ.BasisZ).Normalize();
        if (vPerp.DotProduct(vStub) < 0.0)
        {
            vPerp = -vPerp;
        }
        Pipe stubY = Pipe.Create(doc, tuboMain.MEPSystem.GetTypeId(), tuboMain.GetTypeId(), ObterLevelIdSeguro(doc, tuboMain), ptQuebra, ptQuebra + vPerp * 1.0);
        ((Element)stubY).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamal);
        doc.Regenerate();
        FamilyInstance wye = null;
        try
        {
            Connector cM1 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboMain, ptQuebra);
            Connector cM2 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboMainParte2, ptQuebra);
            Connector cStub = ComandoLancamentoAutomatico.GetConnectorClosestTo(stubY, ptQuebra);
            wye = doc.Create.NewTeeFitting(cM1, cM2, cStub);
            doc.Regenerate();
            doc.Delete(stubY.Id);
            doc.Regenerate();
            double rad = Math.PI / 4.0;
            string[] nomes = new string[7] { "Ângulo 1", "Angulo 1", "Angle 1", "Angle", "Branch Angle", "Ângulo", "Angulo" };
            string[] array = nomes;
            foreach (string nome in array)
            {
                Parameter p = wye.LookupParameter(nome);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                {
                    try
                    {
                        p.Set(rad);
                    }
                    catch
                    {
                        continue;
                    }
                    break;
                }
            }
            doc.Regenerate();
            Connector connWyeTemp = ObterConectorDerivacaoDoY(wye, vColetor);
            if (connWyeTemp != null)
            {
                XYZ currentYDirTemp = connWyeTemp.CoordinateSystem.BasisZ.Normalize();
                if (currentYDirTemp.DotProduct(vColetor) * vStub.DotProduct(vColetor) < -0.1)
                {
                    doc.Delete(wye.Id);
                    doc.Regenerate();
                    Pipe stubY2 = Pipe.Create(doc, tuboMain.MEPSystem.GetTypeId(), tuboMain.GetTypeId(), ObterLevelIdSeguro(doc, tuboMain), ptQuebra, ptQuebra + vPerp * 1.0);
                    ((Element)stubY2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamal);
                    doc.Regenerate();
                    wye = doc.Create.NewTeeFitting(cM2, cM1, ComandoLancamentoAutomatico.GetConnectorClosestTo(stubY2, ptQuebra));
                    doc.Regenerate();
                    doc.Delete(stubY2.Id);
                    doc.Regenerate();
                    string[] array2 = nomes;
                    foreach (string nome2 in array2)
                    {
                        Parameter p2 = wye.LookupParameter(nome2);
                        if (p2 != null && !p2.IsReadOnly && p2.StorageType == StorageType.Double)
                        {
                            try
                            {
                                p2.Set(rad);
                            }
                            catch
                            {
                                continue;
                            }
                            break;
                        }
                    }
                    doc.Regenerate();
                }
            }
            Connector connWye = ObterConectorDerivacaoDoY(wye, vColetor);
            if (connWye != null)
            {
                XYZ currentYDir = connWye.CoordinateSystem.BasisZ.Normalize();
                XYZ targetYDir = vStub;
                XYZ planeY = (currentYDir - vColetor * currentYDir.DotProduct(vColetor)).Normalize();
                XYZ planeT = (targetYDir - vColetor * targetYDir.DotProduct(vColetor)).Normalize();
                if (planeY.GetLength() > 0.01 && planeT.GetLength() > 0.01)
                {
                    double angleRot = planeY.AngleTo(planeT);
                    if (angleRot > 0.001)
                    {
                        if (planeY.CrossProduct(planeT).DotProduct(vColetor) < 0.0)
                        {
                            angleRot = 0.0 - angleRot;
                        }
                        ElementTransformUtils.RotateElement(doc, wye.Id, Line.CreateUnbound(ptQuebra, vColetor), angleRot);
                    }
                }
            }
        }
        catch
        {
        }
        if (wye != null)
        {
            Connector connWye2 = ObterConectorDerivacaoDoY(wye, vColetor);
            Connector cRamal = ComandoLancamentoAutomatico.GetConnectorClosestTo(ramal, ptQuebra);
            if (connWye2 != null && cRamal != null)
            {
                try
                {
                    LocationCurve lc = ramal.Location as LocationCurve;
                    Curve c = lc.Curve;
                    XYZ pStart = ((c.GetEndPoint(0).DistanceTo(ptQuebra) > c.GetEndPoint(1).DistanceTo(ptQuebra)) ? c.GetEndPoint(0) : c.GetEndPoint(1));
                    lc.Curve = Line.CreateBound(pStart, connWye2.Origin);
                    doc.Regenerate();
                }
                catch
                {
                }
                try
                {
                    cRamal.ConnectTo(connWye2);
                    return;
                }
                catch
                {
                    return;
                }
            }
            return;
        }
        Connector cMain1 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboMain, ptQuebra);
        Connector cMain2 = ComandoLancamentoAutomatico.GetConnectorClosestTo(tuboMainParte2, ptQuebra);
        Connector cRamal2 = ComandoLancamentoAutomatico.GetConnectorClosestTo(ramal, ptQuebra);
        if (cMain1 == null || cMain2 == null || cRamal2 == null)
        {
            return;
        }
        try
        {
            doc.Create.NewTeeFitting(cMain1, cMain2, cRamal2);
        }
        catch
        {
        }
    }

    private static Connector ObterConectorDerivacaoDoY(FamilyInstance wye, XYZ vColetor)
    {
        if (wye == null || wye.MEPModel == null || wye.MEPModel.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in wye.MEPModel.ConnectorManager.Connectors)
        {
            if (c.Domain == Domain.DomainPiping)
            {
                XYZ dir = c.CoordinateSystem.BasisZ.Normalize();
                if (Math.Abs(dir.DotProduct(vColetor)) < 0.95)
                {
                    return c;
                }
            }
        }
        return null;
    }

    public static Connector ObterConectorCaixa(FamilyInstance fi, bool bloquearHorizontais = false)
    {
        if (fi.MEPModel == null || fi.MEPModel.ConnectorManager == null)
        {
            return null;
        }

        _ = UnitUtils.ConvertToInternalUnits(50.0, UnitTypeId.Millimeters);
        Connector melhor = null;
        foreach (Connector conn in fi.MEPModel.ConnectorManager.Connectors)
        {
            if (conn.Domain != Domain.DomainPiping)
            {
                continue;
            }
            bool isHorizontal = Math.Abs(conn.CoordinateSystem.BasisZ.Z) < 0.5;
            if (bloquearHorizontais && isHorizontal)
            {
                continue;
            }
            if (melhor == null)
            {
                melhor = conn;
                continue;
            }
            bool melhorIsHorizontal = Math.Abs(melhor.CoordinateSystem.BasisZ.Z) < 0.5;
            if (!bloquearHorizontais)
            {
                if (isHorizontal && !melhorIsHorizontal)
                {
                    melhor = conn;
                }
                else if (isHorizontal == melhorIsHorizontal && conn.Radius > melhor.Radius)
                {
                    melhor = conn;
                }
            }
            else if (conn.Radius > melhor.Radius)
            {
                melhor = conn;
            }
        }
        return melhor;
    }

    public static Connector ObterConectorLivreMaisProximo(FamilyInstance fi, XYZ ptReferencia)
    {
        if (fi.MEPModel == null || fi.MEPModel.ConnectorManager == null)
        {
            return null;
        }
        Connector melhor = null;
        double menorDist = double.MaxValue;
        foreach (Connector conn in fi.MEPModel.ConnectorManager.Connectors)
        {
            if (conn.Domain == Domain.DomainPiping && !conn.IsConnected)
            {
                double dist = new XYZ(conn.Origin.X, conn.Origin.Y, 0.0).DistanceTo(new XYZ(ptReferencia.X, ptReferencia.Y, 0.0));
                if (dist < menorDist)
                {
                    menorDist = dist;
                    melhor = conn;
                }
            }
        }
        return melhor;
    }

    public static ElementId ObterLevelIdSeguro(Document doc, Element elemento)
    {
        ElementId lvlId = elemento?.LevelId ?? ElementId.InvalidElementId;
        if (lvlId == ElementId.InvalidElementId)
        {
            if (JigLancamentoManager.LevelId != null && JigLancamentoManager.LevelId != ElementId.InvalidElementId)
            {
                lvlId = JigLancamentoManager.LevelId;
            }
            else if (doc.ActiveView != null && doc.ActiveView.GenLevel != null)
            {
                lvlId = doc.ActiveView.GenLevel.Id;
            }
            else if (new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() is Level lvl)
            {
                lvlId = lvl.Id;
            }
        }
        return lvlId;
    }

    public string GetName()
    {
        return "FinalizarLancamentoAutomatico";
    }
}
