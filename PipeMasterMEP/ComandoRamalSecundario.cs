using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoRamalSecundario : IExternalCommand
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
                return Result.Failed;
            }
            RamalSecundarioViewModel viewModel = new RamalSecundarioViewModel();
            RamalSecundarioOptionsBar optionsControl = new RamalSecundarioOptionsBar
            {
                DataContext = viewModel
            };
            using (TomboOptionsBarSession session = TomboOptionsBarSession.Begin(optionsControl))
            {
                if (session == null)
                {
                    TaskDialog.Show("PipeMaster", "Options Bar indisponível nesta versão do Revit.");
                }
                Reference ref1 = uidoc.Selection.PickObject(ObjectType.Element, new FiltroRamalOuCaixa(), "PipeMaster — 1º Clique: Selecione a Caixa Sifonada ou o Coletor.");
                Reference ref2 = uidoc.Selection.PickObject(ObjectType.Element, new FiltroRamalOuCaixa(), "PipeMaster — 2º Clique: Selecione o outro elemento para fechar a conexão.");
                Element elem1 = doc.GetElement(ref1);
                Element elem2 = doc.GetElement(ref2);
                Reference refRamal = null;
                Reference refColetor = null;
                if (elem1 is FamilyInstance && elem2 is Pipe)
                {
                    refRamal = ref1;
                    refColetor = ref2;
                }
                else if (elem1 is Pipe && elem2 is FamilyInstance)
                {
                    refRamal = ref2;
                    refColetor = ref1;
                }
                else
                {
                    if (!(elem1 is Pipe) || !(elem2 is Pipe))
                    {
                        TaskDialog.Show("PipeMaster", "Selecione pelo menos um Tubo Coletor e uma Caixa Sifonada (ou Ramal).");
                        return Result.Cancelled;
                    }
                    double diam1 = ((Element)(Pipe)elem1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    double diam2 = ((Element)(Pipe)elem2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    if (diam1 < diam2)
                    {
                        refRamal = ref1;
                        refColetor = ref2;
                    }
                    else if (diam2 < diam1)
                    {
                        refRamal = ref2;
                        refColetor = ref1;
                    }
                    else
                    {
                        refRamal = ref1;
                        refColetor = ref2;
                    }
                }
                ConfiguracoesRamal.AlinharComPrimario = viewModel.AlinharComPrimario;
                ConfiguracoesRamal.NivelarTampa = viewModel.NivelarTampa;
                ConfiguracoesRamal.Inclinacao = viewModel.Inclinacao;
                string textoInclinacao = ConfiguracoesRamal.Inclinacao.Replace(",", ".");
                if (!double.TryParse(textoInclinacao, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorInclinacao))
                {
                    valorInclinacao = 2.0;
                }
                double inclinacaoPadrao = valorInclinacao / 100.0;
                bool alinharComPrimario = ConfiguracoesRamal.AlinharComPrimario;
                bool nivelarTampa = ConfiguracoesRamal.NivelarTampa;
                Element elemRamal = doc.GetElement(refRamal);
                if (!(doc.GetElement(refColetor) is Pipe tuboColetor))
                {
                    return Result.Cancelled;
                }
                XYZ pontoCliqueRamal = refRamal.GlobalPoint;
                XYZ pontoCliqueColetor = refColetor.GlobalPoint;
                XYZ pOrigemRamal = null;
                XYZ vDirecaoRamal = null;
                double diamRamalInternal = 125.0 / 762.0;
                List<ElementId> lixoParaDeletar = new List<ElementId>();
                ElementId pecaMontanteId = null;
                FamilyInstance caixaSifonadaEncontrada = null;
                if (elemRamal is Pipe tuboOriginal)
                {
                    caixaSifonadaEncontrada = EncontrarCaixaSifonadaNaRede(tuboOriginal, pontoCliqueRamal, out lixoParaDeletar, out pecaMontanteId);
                    if (caixaSifonadaEncontrada != null)
                    {
                        elemRamal = caixaSifonadaEncontrada;
                        Connector connCaixa = ObterConectorDeSaida(caixaSifonadaEncontrada, pontoCliqueRamal);
                        if (connCaixa != null)
                        {
                            pOrigemRamal = connCaixa.Origin;
                            vDirecaoRamal = connCaixa.CoordinateSystem.BasisZ.Normalize();
                            diamRamalInternal = connCaixa.Radius * 2.0;
                        }
                    }
                    else
                    {
                        Line lCaixa = (tuboOriginal.Location as LocationCurve).Curve as Line;
                        Line lCol = (tuboColetor.Location as LocationCurve).Curve as Line;
                        XYZ pt0 = lCaixa.GetEndPoint(0);
                        XYZ pt1 = lCaixa.GetEndPoint(1);
                        double d0 = pt0.DistanceTo(lCol.Project(pt0).XYZPoint);
                        double d1 = pt1.DistanceTo(lCol.Project(pt1).XYZPoint);
                        pOrigemRamal = ((d0 > d1) ? pt0 : pt1);
                        XYZ pDestino = ((d0 > d1) ? pt1 : pt0);
                        vDirecaoRamal = (pDestino - pOrigemRamal).Normalize();
                        diamRamalInternal = ((Element)tuboOriginal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    }
                }
                else if (elemRamal is FamilyInstance caixaSifonada)
                {
                    Connector melhorConector = ObterConectorDeSaida(caixaSifonada, pontoCliqueRamal);
                    if (melhorConector == null)
                    {
                        return Result.Cancelled;
                    }
                    pOrigemRamal = melhorConector.Origin;
                    vDirecaoRamal = melhorConector.CoordinateSystem.BasisZ.Normalize();
                    diamRamalInternal = melhorConector.Radius * 2.0;
                    caixaSifonadaEncontrada = caixaSifonada;
                }
                Line lColetor = (tuboColetor.Location as LocationCurve).Curve as Line;
                XYZ pJuncaoY_Projetado = ProjetarNaReta(pontoCliqueColetor, lColetor);
                IntersectionResult snapInicial = lColetor.Project(pJuncaoY_Projetado);
                if (snapInicial != null)
                {
                    pJuncaoY_Projetado = snapInicial.XYZPoint;
                }
                XYZ vColetor = lColetor.Direction.Normalize();
                XYZ vRamal2D = new XYZ(vDirecaoRamal.X, vDirecaoRamal.Y, 0.0).Normalize();
                XYZ vColetor2D = new XYZ(vColetor.X, vColetor.Y, 0.0).Normalize();
                double anguloCena = vRamal2D.AngleTo(vColetor2D) * 180.0 / Math.PI;
                if (anguloCena > 90.0)
                {
                    anguloCena = 180.0 - anguloCena;
                }
                bool isCenarioA_Direto = anguloCena >= 30.0 && anguloCena <= 60.0;
                bool isCenarioB_Paralelo = anguloCena < 15.0;
                bool isCenarioC_Perpendicular = anguloCena >= 75.0 && anguloCena <= 90.0;
                double MIN_PIPE_LEN = 0.02;
                using TransactionGroup tg = new TransactionGroup(doc, "PipeMaster: Ramal Secundário");
                tg.Start();
                ElementId sysId = tuboColetor.MEPSystem?.GetTypeId();
                ElementId typeId = tuboColetor.GetTypeId();
                ElementId lvlId = tuboColetor.LevelId;
                if (isCenarioA_Direto)
                {
                    XYZ p1 = new XYZ(pOrigemRamal.X, pOrigemRamal.Y, 0.0);
                    XYZ v1 = vRamal2D;
                    XYZ p2 = new XYZ(lColetor.GetEndPoint(0).X, lColetor.GetEndPoint(0).Y, 0.0);
                    XYZ v2 = vColetor2D;
                    double det = v1.X * v2.Y - v1.Y * v2.X;
                    if (Math.Abs(det) < 0.0001)
                    {
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    double t = ((p2.X - p1.X) * v2.Y - (p2.Y - p1.Y) * v2.X) / det;
                    if (t <= 0.0)
                    {
                        TaskDialog.Show("PipeMaster", "O Ramal está posicionado nas costas do clique do Coletor. Clique numa parte do Coletor que esteja à frente da Caixa.");
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    XYZ pIntersecao2D = p1 + v1 * t;
                    double tColetor = (pIntersecao2D - p2).DotProduct(v2);
                    XYZ pJuncaoY = lColetor.GetEndPoint(0) + vColetor * tColetor;
                    double distRamal = p1.DistanceTo(pIntersecao2D);
                    using Transaction trans = new Transaction(doc, "Modelagem Cenário A");
                    trans.Start();
                    if (lixoParaDeletar.Count > 0)
                    {
                        foreach (ElementId lixoId in lixoParaDeletar)
                        {
                            try
                            {
                                doc.Delete(lixoId);
                            }
                            catch
                            {
                            }
                        }
                        doc.Regenerate();
                    }
                    if (alinharComPrimario)
                    {
                        double zOrigemIdeal = pJuncaoY.Z + distRamal * inclinacaoPadrao;
                        double diferencaZ = zOrigemIdeal - pOrigemRamal.Z;
                        if (Math.Abs(diferencaZ) > 0.001)
                        {
                            if (caixaSifonadaEncontrada != null)
                            {
                                ElementTransformUtils.MoveElement(doc, caixaSifonadaEncontrada.Id, new XYZ(0.0, 0.0, diferencaZ));
                            }
                            pOrigemRamal = new XYZ(pOrigemRamal.X, pOrigemRamal.Y, zOrigemIdeal);
                        }
                    }
                    else
                    {
                        double zJuncaoYIdeal = pOrigemRamal.Z - distRamal * inclinacaoPadrao;
                        double diferencaZCol = zJuncaoYIdeal - pJuncaoY.Z;
                        if (Math.Abs(diferencaZCol) > 0.001)
                        {
                            ElementTransformUtils.MoveElement(doc, tuboColetor.Id, new XYZ(0.0, 0.0, diferencaZCol));
                            pJuncaoY = new XYZ(pJuncaoY.X, pJuncaoY.Y, zJuncaoYIdeal);
                        }
                    }
                    doc.Regenerate();
                    Line lColAtualizada = (tuboColetor.Location as LocationCurve).Curve as Line;
                    IntersectionResult snapFinalA = lColAtualizada.Project(pJuncaoY);
                    if (snapFinalA != null)
                    {
                        pJuncaoY = snapFinalA.XYZPoint;
                    }
                    if (pJuncaoY.DistanceTo(lColAtualizada.GetEndPoint(0)) < 0.15 || pJuncaoY.DistanceTo(lColAtualizada.GetEndPoint(1)) < 0.15)
                    {
                        TaskDialog.Show("PipeMaster - Sem Espaço", "A conexão projetada cai muito perto da extremidade do coletor. Aumente o tubo coletor.");
                        trans.RollBack();
                        return Result.Cancelled;
                    }
                    if (nivelarTampa && caixaSifonadaEncontrada != null)
                    {
                        SetarParametroNivelTampa(caixaSifonadaEncontrada, doc, diamRamalInternal);
                        Connector s = ObterConectorDeSaida(caixaSifonadaEncontrada, pontoCliqueRamal);
                        if (s != null)
                        {
                            pOrigemRamal = s.Origin;
                        }
                    }
                    Pipe ramalTemp = CriarTuboSeguro(doc, sysId, typeId, lvlId, pOrigemRamal, pJuncaoY, diamRamalInternal);
                    doc.Regenerate();
                    ElementId idJus = PlumbingUtils.BreakCurve(doc, tuboColetor.Id, pJuncaoY);
                    Pipe coletorJus = doc.GetElement(idJus) as Pipe;
                    doc.Regenerate();
                    XYZ vPerp = vColetor.CrossProduct(XYZ.BasisZ).Normalize();
                    if (vPerp.DotProduct(pOrigemRamal - pJuncaoY) < 0.0)
                    {
                        vPerp = -vPerp;
                    }
                    Pipe stubY = CriarTuboSeguro(doc, sysId, typeId, lvlId, pJuncaoY, pJuncaoY + vPerp * 1.0, diamRamalInternal);
                    doc.Regenerate();
                    FamilyInstance wye = doc.Create.NewTeeFitting(ConectorMaisProximoGeral(tuboColetor, pJuncaoY), ConectorMaisProximoGeral(coletorJus, pJuncaoY), ConectorMaisProximoGeral(stubY, pJuncaoY));
                    doc.Delete(stubY.Id);
                    doc.Regenerate();
                    SetarParametroAngulo(wye, 45.0);
                    doc.Regenerate();
                    Connector connWye = ObterConectorDerivacaoDoY(wye, vColetor);
                    XYZ currentYDir = connWye.CoordinateSystem.BasisZ.Normalize();
                    XYZ targetYDir = (pOrigemRamal - pJuncaoY).Normalize();
                    XYZ planeY = (currentYDir - vColetor * currentYDir.DotProduct(vColetor)).Normalize();
                    XYZ planeT = (targetYDir - vColetor * targetYDir.DotProduct(vColetor)).Normalize();
                    if (planeY.GetLength() > 0.01 && planeT.GetLength() > 0.01)
                    {
                        double rotY = planeY.AngleTo(planeT);
                        if (vColetor.CrossProduct(planeY).DotProduct(planeT) < 0.0)
                        {
                            rotY = 0.0 - rotY;
                        }
                        ElementTransformUtils.RotateElement(doc, wye.Id, Line.CreateUnbound(pJuncaoY, vColetor), rotY);
                    }
                    doc.Regenerate();
                    doc.Delete(ramalTemp.Id);
                    doc.Regenerate();
                    connWye = ObterConectorDerivacaoDoY(wye, vColetor);
                    if (connWye != null)
                    {
                        if (pOrigemRamal.DistanceTo(connWye.Origin) < MIN_PIPE_LEN)
                        {
                            TaskDialog.Show("PipeMaster - Sem Espaço", "As conexões estão se sobrepondo.\nAfaste a caixa do coletor.");
                            trans.RollBack();
                            return Result.Cancelled;
                        }
                        Pipe ramalFinal = Pipe.Create(doc, sysId, typeId, lvlId, pOrigemRamal, connWye.Origin);
                        ((Element)ramalFinal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamalInternal);
                        doc.Regenerate();
                        Connector cxConn = null;
                        if (caixaSifonadaEncontrada != null)
                        {
                            cxConn = ObterConectorDeSaida(caixaSifonadaEncontrada, pOrigemRamal);
                        }
                        else if (pecaMontanteId != null)
                        {
                            cxConn = ConectorMaisProximoGeral(doc.GetElement(pecaMontanteId), pOrigemRamal);
                        }
                        if (cxConn != null)
                        {
                            ConectorMaisProximoGeral(ramalFinal, pOrigemRamal).ConnectTo(cxConn);
                        }
                        ConectorMaisProximoGeral(ramalFinal, connWye.Origin).ConnectTo(connWye);
                    }
                    trans.Commit();
                }
                else if (isCenarioB_Paralelo)
                {
                    XYZ pJuncaoY2D = new XYZ(pJuncaoY_Projetado.X, pJuncaoY_Projetado.Y, 0.0);
                    XYZ pOrigemRamal2D = new XYZ(pOrigemRamal.X, pOrigemRamal.Y, 0.0);
                    XYZ pProj2D = pOrigemRamal2D + vRamal2D * (pJuncaoY2D - pOrigemRamal2D).DotProduct(vRamal2D);
                    double distLateral = pJuncaoY2D.DistanceTo(pProj2D);
                    XYZ pJoelho2D = pProj2D - vRamal2D * distLateral;
                    if ((pJoelho2D - pOrigemRamal2D).DotProduct(vRamal2D) < 0.0)
                    {
                        TaskDialog.Show("PipeMaster", "Espaço insuficiente para criar o desvio de 45º.");
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    double distL1 = pOrigemRamal2D.DistanceTo(pJoelho2D);
                    double distL2 = pJoelho2D.DistanceTo(pJuncaoY2D);
                    using Transaction t2 = new Transaction(doc, "Modelagem Cenário B");
                    t2.Start();
                    if (lixoParaDeletar.Count > 0)
                    {
                        foreach (ElementId lixoId2 in lixoParaDeletar)
                        {
                            try
                            {
                                doc.Delete(lixoId2);
                            }
                            catch
                            {
                            }
                        }
                        doc.Regenerate();
                    }
                    XYZ pJoelho3D;
                    if (alinharComPrimario)
                    {
                        double zJoelhoIdeal = pJuncaoY_Projetado.Z + distL2 * inclinacaoPadrao;
                        double zOrigemIdeal2 = zJoelhoIdeal + distL1 * inclinacaoPadrao;
                        pJoelho3D = new XYZ(pJoelho2D.X, pJoelho2D.Y, zJoelhoIdeal);
                        double diferencaZ2 = zOrigemIdeal2 - pOrigemRamal.Z;
                        if (Math.Abs(diferencaZ2) > 0.001)
                        {
                            if (caixaSifonadaEncontrada != null)
                            {
                                ElementTransformUtils.MoveElement(doc, caixaSifonadaEncontrada.Id, new XYZ(0.0, 0.0, diferencaZ2));
                            }
                            pOrigemRamal = new XYZ(pOrigemRamal.X, pOrigemRamal.Y, zOrigemIdeal2);
                        }
                    }
                    else
                    {
                        double zJoelhoIdeal2 = pOrigemRamal.Z - distL1 * inclinacaoPadrao;
                        double zJuncaoYIdeal2 = zJoelhoIdeal2 - distL2 * inclinacaoPadrao;
                        pJoelho3D = new XYZ(pJoelho2D.X, pJoelho2D.Y, zJoelhoIdeal2);
                        double diferencaZCol2 = zJuncaoYIdeal2 - pJuncaoY_Projetado.Z;
                        if (Math.Abs(diferencaZCol2) > 0.001)
                        {
                            ElementTransformUtils.MoveElement(doc, tuboColetor.Id, new XYZ(0.0, 0.0, diferencaZCol2));
                            pJuncaoY_Projetado = new XYZ(pJuncaoY_Projetado.X, pJuncaoY_Projetado.Y, zJuncaoYIdeal2);
                        }
                    }
                    doc.Regenerate();
                    Line lColAtualizada2 = (tuboColetor.Location as LocationCurve).Curve as Line;
                    IntersectionResult snapFinalB = lColAtualizada2.Project(pJuncaoY_Projetado);
                    if (snapFinalB != null)
                    {
                        pJuncaoY_Projetado = snapFinalB.XYZPoint;
                    }
                    if (pJuncaoY_Projetado.DistanceTo(lColAtualizada2.GetEndPoint(0)) < 0.15 || pJuncaoY_Projetado.DistanceTo(lColAtualizada2.GetEndPoint(1)) < 0.15)
                    {
                        TaskDialog.Show("PipeMaster - Sem Espaço", "A junção cai muito perto da extremidade do coletor. Aumente o tubo ou clique mais para trás.");
                        t2.RollBack();
                        return Result.Cancelled;
                    }
                    if (nivelarTampa && caixaSifonadaEncontrada != null)
                    {
                        SetarParametroNivelTampa(caixaSifonadaEncontrada, doc, diamRamalInternal);
                        Connector s2 = ObterConectorDeSaida(caixaSifonadaEncontrada, pontoCliqueRamal);
                        if (s2 != null)
                        {
                            pOrigemRamal = s2.Origin;
                        }
                    }
                    if (pOrigemRamal.DistanceTo(pJoelho3D) < MIN_PIPE_LEN)
                    {
                        TaskDialog.Show("PipeMaster - Clique Incorreto", "O ponto clicado no coletor está muito para trás. Não há espaço físico na caixa sifonada para alocar o Joelho 45º.");
                        t2.RollBack();
                        return Result.Cancelled;
                    }
                    Pipe tuboSaida = CriarTuboSeguro(doc, sysId, typeId, lvlId, pOrigemRamal, pJoelho3D, diamRamalInternal);
                    doc.Regenerate();
                    Connector cxConn2 = null;
                    if (caixaSifonadaEncontrada != null)
                    {
                        cxConn2 = ObterConectorDeSaida(caixaSifonadaEncontrada, pOrigemRamal);
                    }
                    else if (pecaMontanteId != null)
                    {
                        cxConn2 = ConectorMaisProximoGeral(doc.GetElement(pecaMontanteId), pOrigemRamal);
                    }
                    if (cxConn2 != null)
                    {
                        ConectorMaisProximoGeral(tuboSaida, pOrigemRamal).ConnectTo(cxConn2);
                    }
                    doc.Regenerate();
                    Pipe chicoteTemp = CriarTuboSeguro(doc, sysId, typeId, lvlId, pJoelho3D, pJuncaoY_Projetado, diamRamalInternal);
                    doc.Regenerate();
                    FamilyInstance joelho = doc.Create.NewElbowFitting(ConectorMaisProximoGeral(tuboSaida, pJoelho3D), ConectorMaisProximoGeral(chicoteTemp, pJoelho3D));
                    if (joelho != null)
                    {
                        SetarParametroBool(joelho, "Inverter Sentido da Luva", valor: true);
                    }
                    doc.Regenerate();
                    ElementId idJus2 = PlumbingUtils.BreakCurve(doc, tuboColetor.Id, pJuncaoY_Projetado);
                    Pipe coletorJus2 = doc.GetElement(idJus2) as Pipe;
                    doc.Regenerate();
                    XYZ vPerp2 = vColetor.CrossProduct(XYZ.BasisZ).Normalize();
                    if (vPerp2.DotProduct(pJoelho3D - pJuncaoY_Projetado) < 0.0)
                    {
                        vPerp2 = -vPerp2;
                    }
                    Pipe stubY2 = CriarTuboSeguro(doc, sysId, typeId, lvlId, pJuncaoY_Projetado, pJuncaoY_Projetado + vPerp2 * 1.0, diamRamalInternal);
                    ((Element)stubY2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamalInternal);
                    doc.Regenerate();
                    FamilyInstance wye2 = doc.Create.NewTeeFitting(ConectorMaisProximoGeral(tuboColetor, pJuncaoY_Projetado), ConectorMaisProximoGeral(coletorJus2, pJuncaoY_Projetado), ConectorMaisProximoGeral(stubY2, pJuncaoY_Projetado));
                    doc.Delete(stubY2.Id);
                    SetarParametroAngulo(wye2, 45.0);
                    doc.Regenerate();
                    Connector connWye2 = ObterConectorDerivacaoDoY(wye2, vColetor);
                    XYZ currentYDir2 = connWye2.CoordinateSystem.BasisZ.Normalize();
                    XYZ targetYDir2 = (pJoelho3D - pJuncaoY_Projetado).Normalize();
                    XYZ planeY2 = (currentYDir2 - vColetor * currentYDir2.DotProduct(vColetor)).Normalize();
                    XYZ planeT2 = (targetYDir2 - vColetor * targetYDir2.DotProduct(vColetor)).Normalize();
                    if (planeY2.GetLength() > 0.01 && planeT2.GetLength() > 0.01)
                    {
                        double rotY2 = planeY2.AngleTo(planeT2);
                        if (vColetor.CrossProduct(planeY2).DotProduct(planeT2) < 0.0)
                        {
                            rotY2 = 0.0 - rotY2;
                        }
                        ElementTransformUtils.RotateElement(doc, wye2.Id, Line.CreateUnbound(pJuncaoY_Projetado, vColetor), rotY2);
                    }
                    doc.Regenerate();
                    doc.Delete(chicoteTemp.Id);
                    doc.Regenerate();
                    connWye2 = ObterConectorDerivacaoDoY(wye2, vColetor);
                    Connector connCotovelo = ObterConectorLivre(joelho);
                    if (connWye2 != null && connCotovelo != null)
                    {
                        double distChicote = connCotovelo.Origin.DistanceTo(connWye2.Origin);
                        XYZ dirChicoteReal = (connWye2.Origin - connCotovelo.Origin).Normalize();
                        XYZ dirChicoteEsperada = (pJuncaoY_Projetado - pJoelho3D).Normalize();
                        if (distChicote < MIN_PIPE_LEN || dirChicoteReal.DotProduct(dirChicoteEsperada) < 0.0)
                        {
                            TaskDialog.Show("PipeMaster - Sem Espaço", "As conexões (Joelho e Junção Y) estão se sobrepondo.\nAumente a distância.");
                            t2.RollBack();
                            return Result.Cancelled;
                        }
                        Pipe chicoteFinal = Pipe.Create(doc, sysId, typeId, lvlId, connCotovelo.Origin, connWye2.Origin);
                        ((Element)chicoteFinal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamalInternal);
                        doc.Regenerate();
                        ConectorMaisProximoGeral(chicoteFinal, connCotovelo.Origin).ConnectTo(connCotovelo);
                        ConectorMaisProximoGeral(chicoteFinal, connWye2.Origin).ConnectTo(connWye2);
                    }
                    t2.Commit();
                }
                else
                {
                    if (!isCenarioC_Perpendicular)
                    {
                        TaskDialog.Show("PipeMaster", "A caixa sifonada está posicionada num ângulo não suportado. Utilize conexões paralelas, de 45º ou perpendiculares ao coletor.");
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    XYZ p3 = new XYZ(pOrigemRamal.X, pOrigemRamal.Y, 0.0);
                    XYZ v3 = vRamal2D;
                    XYZ v4 = vColetor2D;
                    XYZ pJuncaoY2D2 = new XYZ(pJuncaoY_Projetado.X, pJuncaoY_Projetado.Y, 0.0);
                    double rad45 = Math.PI / 4.0;
                    XYZ dir1 = new XYZ(v4.X * Math.Cos(rad45) - v4.Y * Math.Sin(rad45), v4.X * Math.Sin(rad45) + v4.Y * Math.Cos(rad45), 0.0);
                    XYZ dir2 = new XYZ(v4.X * Math.Cos(0.0 - rad45) - v4.Y * Math.Sin(0.0 - rad45), v4.X * Math.Sin(0.0 - rad45) + v4.Y * Math.Cos(0.0 - rad45), 0.0);
                    XYZ vChicote = ((dir1.DotProduct(v3) > dir2.DotProduct(v3)) ? dir1 : dir2);
                    double det2 = v3.X * vChicote.Y - v3.Y * vChicote.X;
                    if (Math.Abs(det2) < 0.0001)
                    {
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    double t_box = ((pJuncaoY2D2.X - p3.X) * vChicote.Y - (pJuncaoY2D2.Y - p3.Y) * vChicote.X) / det2;
                    if (t_box < MIN_PIPE_LEN)
                    {
                        TaskDialog.Show("PipeMaster - Espaço", "O clique no coletor faz o desvio 45º nascer atrás da caixa sifonada. Clique um pouco mais próximo do cruzamento.");
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    XYZ pJoelho2D2 = p3 + v3 * t_box;
                    double distL3 = t_box;
                    double distL4 = pJoelho2D2.DistanceTo(pJuncaoY2D2);
                    if ((pJuncaoY2D2 - pJoelho2D2).DotProduct(v4) < 0.0)
                    {
                        TaskDialog.Show("PipeMaster - Direção", "Por favor, clique no coletor a favor do fluxo (à frente da saída da caixa sifonada).");
                        tg.RollBack();
                        return Result.Cancelled;
                    }
                    using Transaction t3 = new Transaction(doc, "Modelagem Cenário C (Perpendicular)");
                    t3.Start();
                    if (lixoParaDeletar.Count > 0)
                    {
                        foreach (ElementId lixoId3 in lixoParaDeletar)
                        {
                            try
                            {
                                doc.Delete(lixoId3);
                            }
                            catch
                            {
                            }
                        }
                        doc.Regenerate();
                    }
                    XYZ pJoelho3D2;
                    if (alinharComPrimario)
                    {
                        double zJoelhoIdeal3 = pJuncaoY_Projetado.Z + distL4 * inclinacaoPadrao;
                        double zOrigemIdeal3 = zJoelhoIdeal3 + distL3 * inclinacaoPadrao;
                        pJoelho3D2 = new XYZ(pJoelho2D2.X, pJoelho2D2.Y, zJoelhoIdeal3);
                        double diferencaZ3 = zOrigemIdeal3 - pOrigemRamal.Z;
                        if (Math.Abs(diferencaZ3) > 0.001)
                        {
                            if (caixaSifonadaEncontrada != null)
                            {
                                ElementTransformUtils.MoveElement(doc, caixaSifonadaEncontrada.Id, new XYZ(0.0, 0.0, diferencaZ3));
                            }
                            pOrigemRamal = new XYZ(pOrigemRamal.X, pOrigemRamal.Y, zOrigemIdeal3);
                        }
                    }
                    else
                    {
                        double zJoelhoIdeal4 = pOrigemRamal.Z - distL3 * inclinacaoPadrao;
                        double zJuncaoYIdeal3 = zJoelhoIdeal4 - distL4 * inclinacaoPadrao;
                        pJoelho3D2 = new XYZ(pJoelho2D2.X, pJoelho2D2.Y, zJoelhoIdeal4);
                        double diferencaZCol3 = zJuncaoYIdeal3 - pJuncaoY_Projetado.Z;
                        if (Math.Abs(diferencaZCol3) > 0.001)
                        {
                            ElementTransformUtils.MoveElement(doc, tuboColetor.Id, new XYZ(0.0, 0.0, diferencaZCol3));
                            pJuncaoY_Projetado = new XYZ(pJuncaoY_Projetado.X, pJuncaoY_Projetado.Y, zJuncaoYIdeal3);
                        }
                    }
                    doc.Regenerate();
                    Line lColAtualizada3 = (tuboColetor.Location as LocationCurve).Curve as Line;
                    IntersectionResult snapFinalC = lColAtualizada3.Project(pJuncaoY_Projetado);
                    if (snapFinalC != null)
                    {
                        pJuncaoY_Projetado = snapFinalC.XYZPoint;
                    }
                    if (pJuncaoY_Projetado.DistanceTo(lColAtualizada3.GetEndPoint(0)) < 0.15 || pJuncaoY_Projetado.DistanceTo(lColAtualizada3.GetEndPoint(1)) < 0.15)
                    {
                        TaskDialog.Show("PipeMaster - Sem Espaço", "A junção cai muito perto da extremidade do coletor. Aumente o tubo ou clique mais para trás.");
                        t3.RollBack();
                        return Result.Cancelled;
                    }
                    if (nivelarTampa && caixaSifonadaEncontrada != null)
                    {
                        SetarParametroNivelTampa(caixaSifonadaEncontrada, doc, diamRamalInternal);
                        Connector s3 = ObterConectorDeSaida(caixaSifonadaEncontrada, pontoCliqueRamal);
                        if (s3 != null)
                        {
                            pOrigemRamal = s3.Origin;
                        }
                    }
                    Pipe tuboSaida2 = CriarTuboSeguro(doc, sysId, typeId, lvlId, pOrigemRamal, pJoelho3D2, diamRamalInternal);
                    doc.Regenerate();
                    Connector cxConn3 = null;
                    if (caixaSifonadaEncontrada != null)
                    {
                        cxConn3 = ObterConectorDeSaida(caixaSifonadaEncontrada, pOrigemRamal);
                    }
                    else if (pecaMontanteId != null)
                    {
                        cxConn3 = ConectorMaisProximoGeral(doc.GetElement(pecaMontanteId), pOrigemRamal);
                    }
                    if (cxConn3 != null)
                    {
                        ConectorMaisProximoGeral(tuboSaida2, pOrigemRamal).ConnectTo(cxConn3);
                    }
                    doc.Regenerate();
                    Pipe chicoteTemp2 = CriarTuboSeguro(doc, sysId, typeId, lvlId, pJoelho3D2, pJuncaoY_Projetado, diamRamalInternal);
                    doc.Regenerate();
                    FamilyInstance joelho2 = doc.Create.NewElbowFitting(ConectorMaisProximoGeral(tuboSaida2, pJoelho3D2), ConectorMaisProximoGeral(chicoteTemp2, pJoelho3D2));
                    if (joelho2 != null)
                    {
                        SetarParametroBool(joelho2, "Inverter Sentido da Luva", valor: true);
                    }
                    doc.Regenerate();
                    ElementId idJus3 = PlumbingUtils.BreakCurve(doc, tuboColetor.Id, pJuncaoY_Projetado);
                    Pipe coletorJus3 = doc.GetElement(idJus3) as Pipe;
                    doc.Regenerate();
                    XYZ vPerp3 = vColetor.CrossProduct(XYZ.BasisZ).Normalize();
                    if (vPerp3.DotProduct(pJoelho3D2 - pJuncaoY_Projetado) < 0.0)
                    {
                        vPerp3 = -vPerp3;
                    }
                    Pipe stubY3 = CriarTuboSeguro(doc, sysId, typeId, lvlId, pJuncaoY_Projetado, pJuncaoY_Projetado + vPerp3 * 1.0, diamRamalInternal);
                    ((Element)stubY3).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamalInternal);
                    doc.Regenerate();
                    FamilyInstance wye3 = doc.Create.NewTeeFitting(ConectorMaisProximoGeral(tuboColetor, pJuncaoY_Projetado), ConectorMaisProximoGeral(coletorJus3, pJuncaoY_Projetado), ConectorMaisProximoGeral(stubY3, pJuncaoY_Projetado));
                    doc.Delete(stubY3.Id);
                    SetarParametroAngulo(wye3, 45.0);
                    doc.Regenerate();
                    Connector connWye3 = ObterConectorDerivacaoDoY(wye3, vColetor);
                    XYZ currentYDir3 = connWye3.CoordinateSystem.BasisZ.Normalize();
                    XYZ targetYDir3 = (pJoelho3D2 - pJuncaoY_Projetado).Normalize();
                    XYZ planeY3 = (currentYDir3 - vColetor * currentYDir3.DotProduct(vColetor)).Normalize();
                    XYZ planeT3 = (targetYDir3 - vColetor * targetYDir3.DotProduct(vColetor)).Normalize();
                    if (planeY3.GetLength() > 0.01 && planeT3.GetLength() > 0.01)
                    {
                        double rotY3 = planeY3.AngleTo(planeT3);
                        if (vColetor.CrossProduct(planeY3).DotProduct(planeT3) < 0.0)
                        {
                            rotY3 = 0.0 - rotY3;
                        }
                        ElementTransformUtils.RotateElement(doc, wye3.Id, Line.CreateUnbound(pJuncaoY_Projetado, vColetor), rotY3);
                    }
                    doc.Regenerate();
                    doc.Delete(chicoteTemp2.Id);
                    doc.Regenerate();
                    connWye3 = ObterConectorDerivacaoDoY(wye3, vColetor);
                    Connector connCotovelo2 = ObterConectorLivre(joelho2);
                    if (connWye3 != null && connCotovelo2 != null)
                    {
                        double distChicote2 = connCotovelo2.Origin.DistanceTo(connWye3.Origin);
                        XYZ dirChicoteReal2 = (connWye3.Origin - connCotovelo2.Origin).Normalize();
                        XYZ dirChicoteEsperada2 = (pJuncaoY_Projetado - pJoelho3D2).Normalize();
                        if (distChicote2 < MIN_PIPE_LEN || dirChicoteReal2.DotProduct(dirChicoteEsperada2) < 0.0)
                        {
                            TaskDialog.Show("PipeMaster - Sem Espaço", "As conexões (Joelho e Junção Y) estão se sobrepondo.\nAumente a distância.");
                            t3.RollBack();
                            return Result.Cancelled;
                        }
                        Pipe chicoteFinal2 = Pipe.Create(doc, sysId, typeId, lvlId, connCotovelo2.Origin, connWye3.Origin);
                        ((Element)chicoteFinal2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diamRamalInternal);
                        doc.Regenerate();
                        ConectorMaisProximoGeral(chicoteFinal2, connCotovelo2.Origin).ConnectTo(connCotovelo2);
                        ConectorMaisProximoGeral(chicoteFinal2, connWye3.Origin).ConnectTo(connWye3);
                    }
                    t3.Commit();
                }
                tg.Assimilate();
            }
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex2)
        {
            TaskDialog.Show("PipeMaster – Erro de Modelagem", ex2.Message);
            return Result.Failed;
        }
    }

    private FamilyInstance EncontrarCaixaSifonadaNaRede(Pipe startPipe, XYZ clickPoint, out List<ElementId> lixoParaDeletar, out ElementId pecaMontanteId)
    {
        lixoParaDeletar = new List<ElementId>();
        List<ElementId> rastreioTemp = new List<ElementId>();
        pecaMontanteId = null;
        if (startPipe?.ConnectorManager == null)
        {
            return null;
        }
        Connector connUpstream = null;
        double maxDist = -1.0;
        foreach (Connector c in startPipe.ConnectorManager.Connectors)
        {
            if (c.ConnectorType == ConnectorType.End)
            {
                double dist = c.Origin.DistanceTo(clickPoint);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    connUpstream = c;
                }
            }
        }
        if (connUpstream == null)
        {
            return null;
        }
        if (connUpstream.IsConnected)
        {
            foreach (Connector refC in connUpstream.AllRefs)
            {
                if (refC.Owner.Id != startPipe.Id && refC.ConnectorType == ConnectorType.End)
                {
                    pecaMontanteId = refC.Owner.Id;
                    break;
                }
            }
        }
        HashSet<ElementId> visitados = new HashSet<ElementId>();
        Queue<Connector> fila = new Queue<Connector>();
        fila.Enqueue(connUpstream);
        visitados.Add(startPipe.Id);
        rastreioTemp.Add(startPipe.Id);
        while (fila.Count > 0)
        {
            Connector atual = fila.Dequeue();
            if (!atual.IsConnected)
            {
                continue;
            }
            foreach (Connector refC2 in atual.AllRefs)
            {
                if (refC2.Owner.Id == atual.Owner.Id)
                {
                    continue;
                }
                Element vizinho = refC2.Owner;
                if (visitados.Contains(vizinho.Id))
                {
                    continue;
                }
                visitados.Add(vizinho.Id);
                rastreioTemp.Add(vizinho.Id);
                if (vizinho is FamilyInstance { Category: not null } fi)
                {
                    ElementId catId = fi.Category.Id;
                    bool isCaixa = false;
                    if (catId == new ElementId(BuiltInCategory.OST_PlumbingFixtures) || catId == new ElementId(BuiltInCategory.OST_PipeAccessory) || catId == new ElementId(BuiltInCategory.OST_MechanicalEquipment) || catId == new ElementId(BuiltInCategory.OST_GenericModel) || catId == new ElementId(BuiltInCategory.OST_SpecialityEquipment))
                    {
                        isCaixa = true;
                    }
                    else if (catId == new ElementId(BuiltInCategory.OST_PipeFitting))
                    {
                        MEPModel mEPModel = fi.MEPModel;
                        if (mEPModel != null && mEPModel.ConnectorManager?.Connectors.Size >= 3 && (BuscarParametroFlexivel(fi, "ralo") != null || BuscarParametroFlexivel(fi, "sistema") != null || BuscarParametroFlexivel(fi, "caixa") != null))
                        {
                            isCaixa = true;
                        }
                    }
                    if (isCaixa)
                    {
                        FamilyInstance caixaEncontrada = fi;
                        rastreioTemp.Remove(fi.Id);
                        lixoParaDeletar.AddRange(rastreioTemp);
                        return caixaEncontrada;
                    }
                }
                ConnectorManager cm = GetConnectorManager(vizinho);
                if (cm == null)
                {
                    continue;
                }
                foreach (Connector vizConn in cm.Connectors)
                {
                    if (vizConn.Id != refC2.Id && vizConn.ConnectorType == ConnectorType.End && vizConn.IsConnected)
                    {
                        fila.Enqueue(vizConn);
                    }
                }
            }
        }
        lixoParaDeletar.Add(startPipe.Id);
        return null;
    }

    private ConnectorManager GetConnectorManager(Element e)
    {
        if (!(e is Pipe { ConnectorManager: var connectorManager }))
        {
            if (e is FamilyInstance fi)
            {
                return fi.MEPModel?.ConnectorManager;
            }
            return null;
        }
        return connectorManager;
    }

    private Connector ConectorMaisProximoGeral(Element e, XYZ pRef)
    {
        return (from Connector c in GetConnectorManager(e)?.Connectors
                where c.ConnectorType == ConnectorType.End
                orderby c.Origin.DistanceTo(pRef)
                select c).FirstOrDefault();
    }

    private Pipe CriarTuboSeguro(Document doc, ElementId sysId, ElementId typeId, ElementId lvlId, XYZ p1, XYZ p2, double diam)
    {
        if (p1.DistanceTo(p2) <= 0.00835)
        {
            throw new System.InvalidOperationException($"Falta espaço físico para acomodar o tubo de Ø{Math.Round(diam * 304.8)}mm. Devido ao tamanho das conexões deste diâmetro, elas estão se sobrepondo.\n\nTente afastar a caixa sifonada ou clicar mais à frente no coletor.");
        }
        Pipe p3 = Pipe.Create(doc, sysId, typeId, lvlId, p1, p2);
        ((Element)p3).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diam);
        return p3;
    }

    private Connector ObterConectorDeSaida(FamilyInstance fi, XYZ ponto)
    {
        if (fi.MEPModel?.ConnectorManager == null)
        {
            return null;
        }
        Connector melhorConector = null;
        double maiorRaioHorizontal = -1.0;
        foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
        {
            if (c.ConnectorType == ConnectorType.End && Math.Abs(c.CoordinateSystem.BasisZ.Z) < 0.5 && c.Radius > maiorRaioHorizontal)
            {
                maiorRaioHorizontal = c.Radius;
            }
        }
        if (maiorRaioHorizontal <= 0.0)
        {
            double minDist = double.MaxValue;
            foreach (Connector c2 in fi.MEPModel.ConnectorManager.Connectors)
            {
                if (c2.ConnectorType == ConnectorType.End)
                {
                    double dist = c2.Origin.DistanceTo(ponto);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        melhorConector = c2;
                    }
                }
            }
            return melhorConector;
        }
        double distanciaMinima = double.MaxValue;
        foreach (Connector c3 in fi.MEPModel.ConnectorManager.Connectors)
        {
            if (c3.ConnectorType == ConnectorType.End && Math.Abs(c3.CoordinateSystem.BasisZ.Z) < 0.5 && Math.Abs(c3.Radius - maiorRaioHorizontal) < 0.001)
            {
                double dist2 = c3.Origin.DistanceTo(ponto);
                if (dist2 < distanciaMinima)
                {
                    distanciaMinima = dist2;
                    melhorConector = c3;
                }
            }
        }
        return melhorConector;
    }

    private XYZ ProjetarNaReta(XYZ p, Line l)
    {
        XYZ o = l.GetEndPoint(0);
        XYZ d = l.Direction.Normalize();
        return o + d * (p - o).DotProduct(d);
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

    private Connector ObterConectorLivre(FamilyInstance fi)
    {
        if (fi?.MEPModel?.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
        {
            if (!c.IsConnected && c.ConnectorType == ConnectorType.End)
            {
                return c;
            }
        }
        return null;
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

    private Parameter BuscarParametroFlexivel(FamilyInstance fi, string nomeBusca)
    {
        foreach (Parameter p in fi.Parameters)
        {
            if (p.Definition.Name.IndexOf(nomeBusca, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return p;
            }
        }
        return null;
    }

    private void SetarParametroNivelTampa(FamilyInstance fi, Document doc, double diametroSaidaInternal)
    {
        Parameter pElev = ((Element)fi).get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM) ?? ((Element)fi).get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM) ?? ((Element)fi).get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
        if (pElev == null || !pElev.HasValue)
        {
            return;
        }
        double elevacaoBase = pElev.AsDouble();
        if (Math.Abs(elevacaoBase) < 0.0001)
        {
            return;
        }
        if (Math.Abs(diametroSaidaInternal * 304.8 - 50.0) < 5.0)
        {
            Parameter pElevSistema = fi.LookupParameter("Elevação de Sistema") ?? fi.LookupParameter("Elevacao de Sistema") ?? fi.LookupParameter("Elevação do Ralo") ?? fi.LookupParameter("Elevacao do Ralo") ?? BuscarParametroFlexivel(fi, "sistema") ?? BuscarParametroFlexivel(fi, "ralo");
            if (pElevSistema != null && !pElevSistema.IsReadOnly)
            {
                double dezCentimetrosEmPes = 125.0 / 381.0;
                double valorFinal = dezCentimetrosEmPes - elevacaoBase;
                pElevSistema.Set(valorFinal);
            }
            return;
        }
        Parameter paramProlongador = fi.LookupParameter("Prolongador") ?? BuscarParametroFlexivel(fi, "prolongador");
        Parameter paramElevacaoRalo = fi.LookupParameter("Elevação de Ralo") ?? fi.LookupParameter("Elevacao de Ralo") ?? fi.LookupParameter("Elevação do Ralo") ?? BuscarParametroFlexivel(fi, "elevacao");
        if (paramElevacaoRalo == null)
        {
            return;
        }
        List<Tuple<Connector, Connector>> conexoes = new List<Tuple<Connector, Connector>>();
        if (fi.MEPModel?.ConnectorManager != null)
        {
            foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
            {
                if (!c.IsConnected)
                {
                    continue;
                }
                List<Connector> refs = c.AllRefs.Cast<Connector>().ToList();
                foreach (Connector refC in refs)
                {
                    if (refC.Owner.Id != fi.Id && refC.ConnectorType == ConnectorType.End)
                    {
                        conexoes.Add(new Tuple<Connector, Connector>(c, refC));
                        c.DisconnectFrom(refC);
                    }
                }
            }
        }
        if (paramProlongador != null && !paramProlongador.IsReadOnly)
        {
            paramProlongador.Set(1);
        }
        if (!paramElevacaoRalo.IsReadOnly)
        {
            paramElevacaoRalo.Set(-1.0 * elevacaoBase);
        }
        if (!pElev.IsReadOnly)
        {
            pElev.Set(0.0);
        }
        doc.Regenerate();
        foreach (Tuple<Connector, Connector> tuple in conexoes)
        {
            try
            {
                tuple.Item1.ConnectTo(tuple.Item2);
            }
            catch
            {
            }
        }
    }
}
