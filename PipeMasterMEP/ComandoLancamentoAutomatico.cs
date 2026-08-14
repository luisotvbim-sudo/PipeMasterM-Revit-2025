using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoLancamentoAutomatico : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (App.AppCarregado && !VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        GerenciadorPreview.Iniciar();
        JigLancamentoManager.DesmontarJigSeguro();
        try
        {
            JanelaLancamentoAuto janela = new JanelaLancamentoAuto(doc);
            janela.ShowDialog();
            if (!janela.Configuracao.Confirmado)
            {
                return Result.Cancelled;
            }
            ConfigLancamentoAuto cfg = janela.Configuracao;
            ElementId levelId = ObterNivelDaVista(doc, uidoc.ActiveView);
            double zNivel = (doc.GetElement(levelId) as Level)?.Elevation ?? 0.0;
            double zVistaMin = zNivel - 1.6404199475065615;
            double zVistaMax = zNivel + 13.123359580052492;
            try
            {
                if (uidoc.ActiveView is ViewPlan vpr)
                {
                    PlanViewRange vRange = vpr.GetViewRange();
                    ElementId botLvlId = vRange.GetLevelId(PlanViewPlane.BottomClipPlane);
                    ElementId topLvlId = vRange.GetLevelId(PlanViewPlane.TopClipPlane);
                    double botLvlZ = (doc.GetElement(botLvlId) as Level)?.Elevation ?? zNivel;
                    double topLvlZ = (doc.GetElement(topLvlId) as Level)?.Elevation ?? zNivel;
                    zVistaMin = botLvlZ + vRange.GetOffset(PlanViewPlane.BottomClipPlane);
                    zVistaMax = topLvlZ + vRange.GetOffset(PlanViewPlane.TopClipPlane);
                }
            }
            catch
            {
            }
            JigLancamentoManager.ZVistaMin = zVistaMin;
            JigLancamentoManager.ZVistaMax = zVistaMax;
            double zColetor = zNivel + UnitUtils.ConvertToInternalUnits(cfg.ElevacaoColetorMetros, UnitTypeId.Meters);
            double diamVaso = UnitUtils.ConvertToInternalUnits(100.0, UnitTypeId.Millimeters);
            if (cfg.TemVaso)
            {
                JigLancamentoManager.EtapaAtual = JigLancamentoManager.Etapas.Nenhuma;
                XYZ ptAlinhamento = uidoc.Selection.PickPoint(ObjectSnapTypes.Endpoints | ObjectSnapTypes.Nearest | ObjectSnapTypes.Centers, "PipeMaster [1/2]: Clique no ALINHAMENTO CENTRAL do Vaso Sanitario");
                XYZ ptParede = uidoc.Selection.PickPoint(ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Nearest | ObjectSnapTypes.Perpendicular, $"PipeMaster [2/2]: Clique na PAREDE de referencia ({cfg.DistanciaVaso}cm serao calculados automaticamente)");
                XYZ diff = ptParede - ptAlinhamento;
                XYZ dirOrtho = ((Math.Abs(diff.X) >= Math.Abs(diff.Y)) ? new XYZ(Math.Sign(diff.X), 0.0, 0.0) : new XYZ(0.0, Math.Sign(diff.Y), 0.0));
                double offsetPes = cfg.DistanciaVaso / 100.0 / 0.3048;
                XYZ ptVasoFinal = ((Math.Abs(dirOrtho.X) > 0.5) ? new XYZ(ptParede.X - dirOrtho.X * offsetPes, ptAlinhamento.Y, ptAlinhamento.Z) : new XYZ(ptAlinhamento.X, ptParede.Y - dirOrtho.Y * offsetPes, ptAlinhamento.Z));
                double zPreview = zNivel + 3.28084;
                JigLancamentoManager.IniciarJigVaso(commandData.Application, ptVasoFinal, zPreview, cfg, diamVaso, levelId, zNivel, zColetor);
            }
            else
            {
                JigLancamentoManager.Cfg = cfg;
                JigLancamentoManager.ZPreview = zNivel + 3.28084;
                JigLancamentoManager.LevelId = levelId;
                JigLancamentoManager.ZNivel = zNivel;
                JigLancamentoManager.ZColetor = zColetor;
                JigLancamentoManager.EtapaAtual = JigLancamentoManager.Etapas.Nenhuma;
                new FinalizarLancamentoHandler().AvancarParaProximaEtapa(commandData.Application);
            }
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("PipeMaster [M] - Erro", ex.Message + "\n\n" + ex.StackTrace);
            return Result.Failed;
        }
    }

    public static Pipe EncontrarTuboProximo(Document doc, XYZ pt, double boxPe, bool vertical = false)
    {
        Outline outline = new Outline(pt - new XYZ(boxPe, boxPe, 5.0), pt + new XYZ(boxPe, boxPe, 5.0));
        IEnumerable<Pipe> tubos = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).WherePasses(new BoundingBoxIntersectsFilter(outline)).Cast<Pipe>();
        Pipe melhorTubo = null;
        double minDist = double.MaxValue;
        foreach (Pipe t in tubos)
        {
            Curve c = (t.Location as LocationCurve).Curve;
            XYZ p0 = c.GetEndPoint(0);
            XYZ p1 = c.GetEndPoint(1);
            XYZ dir = (p1 - p0).Normalize();
            if ((!vertical || !(Math.Abs(dir.Z) < 0.5)) && (vertical || !(Math.Abs(dir.Z) > 0.5)))
            {
                double dist = DistanciaPontoSegmento2D(pt, p0, p1);
                if (dist < minDist)
                {
                    minDist = dist;
                    melhorTubo = t;
                }
            }
        }
        return melhorTubo;
    }

    public static double DistanciaPontoSegmento2D(XYZ p, XYZ a, XYZ b)
    {
        XYZ p2 = new XYZ(p.X, p.Y, 0.0);
        XYZ a2 = new XYZ(a.X, a.Y, 0.0);
        XYZ b2 = new XYZ(b.X, b.Y, 0.0);
        XYZ ab = b2 - a2;
        double lenSq = ab.DotProduct(ab);
        if (lenSq < 1E-09)
        {
            return p2.DistanceTo(a2);
        }
        double t = Math.Max(0.0, Math.Min(1.0, (p2 - a2).DotProduct(ab) / lenSq));
        return p2.DistanceTo(a2 + ab * t);
    }

    public static (XYZ, XYZ) CalcularProjecao45Graus(XYZ A, XYZ B, double Z)
    {
        double dX = B.X - A.X;
        double dY = B.Y - A.Y;
        double dx = Math.Abs(dX);
        double dy = Math.Abs(dY);
        double sx = ((dX >= 0.0) ? 1 : (-1));
        double sy = ((dY >= 0.0) ? 1 : (-1));
        if (dx < 0.05 || dy < 0.05 || Math.Abs(dx - dy) < 0.05)
        {
            return (new XYZ(B.X, B.Y, Z), new XYZ(B.X, B.Y, Z));
        }
        XYZ intOrtoPrimeiro;
        XYZ intDiagPrimeiro;
        if (dy > dx)
        {
            intOrtoPrimeiro = new XYZ(A.X, B.Y - sy * dx, Z);
            intDiagPrimeiro = new XYZ(B.X, A.Y + sy * dx, Z);
        }
        else
        {
            intOrtoPrimeiro = new XYZ(B.X - sx * dy, A.Y, Z);
            intDiagPrimeiro = new XYZ(A.X + sx * dy, B.Y, Z);
        }
        return (intOrtoPrimeiro, intDiagPrimeiro);
    }

    public static bool RotaVasoViavel(XYZ p1, XYZ corner, XYZ p2, double distRecuo, double minTrecho)
    {
        double d1 = new XYZ(p1.X, p1.Y, 0.0).DistanceTo(new XYZ(corner.X, corner.Y, 0.0));
        double d2 = new XYZ(corner.X, corner.Y, 0.0).DistanceTo(new XYZ(p2.X, p2.Y, 0.0));
        bool temCorner = d1 > 0.05 && d2 > 0.05;
        if (temCorner && d1 < minTrecho)
        {
            return false;
        }
        double dFinal = (temCorner ? d2 : (d1 + d2));
        return dFinal >= distRecuo + minTrecho;
    }

    public static List<List<XYZ>> CalcularRotasRosaDosVentos(XYZ A, XYZ B, double Z, List<XYZ> dirsOrigemFixa = null, bool isCaixaSifonada = false, double diametroMM = 100.0, bool regraDistanciaPorDiametro = false)
    {
        List<List<XYZ>> rotasValidas = new List<List<XYZ>>();
        XYZ P1 = new XYZ(A.X, A.Y, 0.0);
        XYZ P2 = new XYZ(B.X, B.Y, 0.0);
        List<XYZ> dirSaidas = new List<XYZ>();
        if (dirsOrigemFixa != null && dirsOrigemFixa.Count > 0)
        {
            foreach (XYZ d in dirsOrigemFixa)
            {
                dirSaidas.Add(new XYZ(d.X, d.Y, 0.0).Normalize());
            }
        }
        else
        {
            if (Math.Abs(P2.X - P1.X) > 0.05)
            {
                dirSaidas.Add(new XYZ(Math.Sign(P2.X - P1.X), 0.0, 0.0));
            }
            if (Math.Abs(P2.Y - P1.Y) > 0.05)
            {
                dirSaidas.Add(new XYZ(0.0, Math.Sign(P2.Y - P1.Y), 0.0));
            }
            if (dirSaidas.Count == 0)
            {
                return rotasValidas;
            }
        }
        double minDist = 250.0 / 381.0;
        foreach (XYZ dir in dirSaidas)
        {
            XYZ pA = P1 + dir * minDist;
            double dx = P2.X - pA.X;
            double dy = P2.Y - pA.Y;
            double absX = Math.Abs(dx);
            double absY = Math.Abs(dy);
            double signX = Math.Sign(dx);
            if (signX == 0.0)
            {
                signX = 1.0;
            }
            double signY = Math.Sign(dy);
            if (signY == 0.0)
            {
                signY = 1.0;
            }
            List<List<XYZ>> subRotas = new List<List<XYZ>>();
            HashSet<List<XYZ>> rotasCateto = new HashSet<List<XYZ>>();
            double limCM = 19.0;
            if (diametroMM <= 50.0)
            {
                limCM = 16.0;
            }
            else if (diametroMM <= 75.0)
            {
                limCM = 17.0;
            }
            double limiteProximidade = limCM / 100.0 / 0.3048;
            double globalAbsX = Math.Abs(P2.X - P1.X);
            double globalAbsY = Math.Abs(P2.Y - P1.Y);
            double limiteDecisao = limiteProximidade;
            if (regraDistanciaPorDiametro)
            {
                double limite1_6D_cm = 0.16 * diametroMM;
                limiteDecisao = limite1_6D_cm / 100.0 / 0.3048;
            }
            bool proximidade = !isCaixaSifonada && ((!regraDistanciaPorDiametro) ? (globalAbsX < limiteDecisao || globalAbsY < limiteDecisao) : (globalAbsX < limiteDecisao && globalAbsY < limiteDecisao));
            bool rotaDiretaJaAdicionada = false;
            double minTrechoCateto = 0.1;
            if (regraDistanciaPorDiametro)
            {
                minTrechoCateto = 0.16 * diametroMM * Math.Sqrt(2.0) / 100.0 / 0.3048;
            }
            if (!isCaixaSifonada && (proximidade || regraDistanciaPorDiametro))
            {
                subRotas.Add(new List<XYZ> { P1, P2 });
                rotaDiretaJaAdicionada = true;
                double offsetDesvio = limCM / 100.0 / 0.3048;
                if (absX < absY)
                {
                    double xEsq = Math.Min(P1.X, P2.X) - offsetDesvio;
                    double xDir = Math.Max(P1.X, P2.X) + offsetDesvio;
                    double y1Esq = P1.Y + Math.Abs(xEsq - P1.X) * signY;
                    double y2Esq = P2.Y - Math.Abs(xEsq - P2.X) * signY;
                    if (Math.Abs(y1Esq - P1.Y) + Math.Abs(y2Esq - P2.Y) < absY)
                    {
                        subRotas.Add(new List<XYZ>
                        {
                            P1,
                            new XYZ(xEsq, y1Esq, 0.0),
                            new XYZ(xEsq, y2Esq, 0.0),
                            P2
                        });
                    }
                    double y1Dir = P1.Y + Math.Abs(xDir - P1.X) * signY;
                    double y2Dir = P2.Y - Math.Abs(xDir - P2.X) * signY;
                    if (Math.Abs(y1Dir - P1.Y) + Math.Abs(y2Dir - P2.Y) < absY)
                    {
                        subRotas.Add(new List<XYZ>
                        {
                            P1,
                            new XYZ(xDir, y1Dir, 0.0),
                            new XYZ(xDir, y2Dir, 0.0),
                            P2
                        });
                    }
                }
                else
                {
                    double yBai = Math.Min(P1.Y, P2.Y) - offsetDesvio;
                    double yCim = Math.Max(P1.Y, P2.Y) + offsetDesvio;
                    double x1Bai = P1.X + Math.Abs(yBai - P1.Y) * signX;
                    double x2Bai = P2.X - Math.Abs(yBai - P2.Y) * signX;
                    if (Math.Abs(x1Bai - P1.X) + Math.Abs(x2Bai - P2.X) < absX)
                    {
                        subRotas.Add(new List<XYZ>
                        {
                            P1,
                            new XYZ(x1Bai, yBai, 0.0),
                            new XYZ(x2Bai, yBai, 0.0),
                            P2
                        });
                    }
                    double x1Cim = P1.X + Math.Abs(yCim - P1.Y) * signX;
                    double x2Cim = P2.X - Math.Abs(yCim - P2.Y) * signX;
                    if (Math.Abs(x1Cim - P1.X) + Math.Abs(x2Cim - P2.X) < absX)
                    {
                        subRotas.Add(new List<XYZ>
                        {
                            P1,
                            new XYZ(x1Cim, yCim, 0.0),
                            new XYZ(x2Cim, yCim, 0.0),
                            P2
                        });
                    }
                }
            }
            if (!proximidade)
            {
                List<List<XYZ>> tempSubRotas = new List<List<XYZ>>();
                if (absX > absY + 0.01)
                {
                    tempSubRotas.Add(new List<XYZ>
                    {
                        pA,
                        new XYZ(pA.X + absY * signX, pA.Y + absY * signY, 0.0),
                        P2
                    });
                    tempSubRotas.Add(new List<XYZ>
                    {
                        pA,
                        new XYZ(pA.X + (absX - absY) * signX, pA.Y, 0.0),
                        P2
                    });
                }
                else if (absY > absX + 0.01)
                {
                    tempSubRotas.Add(new List<XYZ>
                    {
                        pA,
                        new XYZ(pA.X + absX * signX, pA.Y + absX * signY, 0.0),
                        P2
                    });
                    tempSubRotas.Add(new List<XYZ>
                    {
                        pA,
                        new XYZ(pA.X, pA.Y + (absY - absX) * signY, 0.0),
                        P2
                    });
                }
                else
                {
                    tempSubRotas.Add(new List<XYZ> { pA, P2 });
                }
                foreach (List<XYZ> r in tempSubRotas)
                {
                    if (r.Count < 2)
                    {
                        continue;
                    }
                    XYZ vSeg = (r[1] - r[0]).Normalize();
                    double dot = vSeg.DotProduct(dir);
                    if (!regraDistanciaPorDiametro && Math.Abs(dot) < 0.1)
                    {
                        continue;
                    }
                    double lenR0R1 = ((r.Count >= 2) ? r[0].DistanceTo(r[1]) : 0.0);
                    double lenR1R2 = ((r.Count >= 3) ? r[1].DistanceTo(r[2]) : 0.0);
                    bool isShort = false;
                    if (r.Count == 3)
                    {
                        if (dot < 0.99 && lenR0R1 < minTrechoCateto)
                        {
                            isShort = true;
                        }
                        double minTrechoFinal = (regraDistanciaPorDiametro ? 0.39370078740157477 : minTrechoCateto);
                        if (lenR1R2 < minTrechoFinal)
                        {
                            isShort = true;
                        }
                    }
                    if (!isShort && (r.Count != 2 || !subRotas.Any((List<XYZ> existente) => existente.Count == 2 && existente[0].DistanceTo(r[0]) < 0.05 && existente[1].DistanceTo(r[1]) < 0.05)))
                    {
                        subRotas.Add(r);
                        if (regraDistanciaPorDiametro)
                        {
                            rotasCateto.Add(r);
                        }
                    }
                }
                if (!isCaixaSifonada && !rotaDiretaJaAdicionada)
                {
                    subRotas.Add(new List<XYZ> { P1, P2 });
                }
            }
            foreach (List<XYZ> r2 in subRotas)
            {
                if (r2.Count == 0)
                {
                    continue;
                }
                List<XYZ> rotaCompleta = new List<XYZ>();
                rotaCompleta.Add(P1);
                if (P1.DistanceTo(r2[0]) > 0.01)
                {
                    rotaCompleta.Add(r2[0]);
                }
                for (int i = 1; i < r2.Count; i++)
                {
                    if (r2[i].DistanceTo(r2[i - 1]) > 0.01)
                    {
                        rotaCompleta.Add(r2[i]);
                    }
                }
                List<XYZ> rotaPreLimpa = new List<XYZ> { rotaCompleta[0] };
                for (int i2 = 1; i2 < rotaCompleta.Count - 1; i2++)
                {
                    XYZ v1 = (rotaCompleta[i2] - rotaPreLimpa.Last()).Normalize();
                    XYZ v2 = (rotaCompleta[i2 + 1] - rotaCompleta[i2]).Normalize();
                    if (v1.DistanceTo(v2) > 0.01)
                    {
                        rotaPreLimpa.Add(rotaCompleta[i2]);
                    }
                }
                rotaPreLimpa.Add(rotaCompleta.Last());
                List<XYZ> rotaChanfrada = Chanfrar90Graus(rotaPreLimpa, 0.49212598425196846);
                if (rotaChanfrada == null)
                {
                    continue;
                }
                List<XYZ> rotaLimpa = new List<XYZ> { rotaChanfrada[0] };
                for (int i3 = 1; i3 < rotaChanfrada.Count - 1; i3++)
                {
                    XYZ v3 = (rotaChanfrada[i3] - rotaLimpa.Last()).Normalize();
                    XYZ v4 = (rotaChanfrada[i3 + 1] - rotaChanfrada[i3]).Normalize();
                    if (v3.DistanceTo(v4) > 0.01)
                    {
                        rotaLimpa.Add(rotaChanfrada[i3]);
                    }
                }
                rotaLimpa.Add(rotaChanfrada.Last());
                bool valida = true;
                for (int i4 = 0; i4 < rotaLimpa.Count - 2; i4++)
                {
                    XYZ v5 = (rotaLimpa[i4 + 1] - rotaLimpa[i4]).Normalize();
                    XYZ v6 = (rotaLimpa[i4 + 2] - rotaLimpa[i4 + 1]).Normalize();
                    if (v5.DotProduct(v6) < 0.01)
                    {
                        valida = false;
                        break;
                    }
                }
                if (valida)
                {
                    for (int i5 = 0; i5 < rotaLimpa.Count - 1; i5++)
                    {
                        if (rotaLimpa[i5].DistanceTo(rotaLimpa[i5 + 1]) < 50.0 / 381.0)
                        {
                            valida = false;
                            break;
                        }
                    }
                }
                if (valida && regraDistanciaPorDiametro)
                {
                    double minTrechoFinalPosChanfro = 0.39370078740157477;
                    bool ehCatetoParaRevalidar = rotasCateto.Contains(r2);
                    for (int i6 = 0; i6 < rotaLimpa.Count - 1; i6++)
                    {
                        double minAplicavel = ((i6 == rotaLimpa.Count - 2) ? minTrechoFinalPosChanfro : (ehCatetoParaRevalidar ? minTrechoCateto : (50.0 / 381.0)));
                        double lenSeg = rotaLimpa[i6].DistanceTo(rotaLimpa[i6 + 1]);
                        if (lenSeg < minAplicavel)
                        {
                            valida = false;
                            break;
                        }
                    }
                }
                if (valida)
                {
                    List<XYZ> rotaZ = rotaLimpa.Select((XYZ pt) => new XYZ(pt.X, pt.Y, Z)).ToList();
                    rotasValidas.Add(rotaZ);
                }
            }
        }
        if (rotasValidas.Count == 0 && P1.DistanceTo(P2) > 250.0 / 381.0)
        {
            rotasValidas.Add(new List<XYZ>
            {
                new XYZ(P1.X, P1.Y, Z),
                new XYZ(P2.X, P2.Y, Z)
            });
        }
        if (regraDistanciaPorDiametro && !isCaixaSifonada)
        {
            double gAbsX = Math.Abs(P2.X - P1.X);
            double gAbsY = Math.Abs(P2.Y - P1.Y);
            double gSignX = Math.Sign(P2.X - P1.X);
            if (gSignX == 0.0)
            {
                gSignX = 1.0;
            }
            double gSignY = Math.Sign(P2.Y - P1.Y);
            if (gSignY == 0.0)
            {
                gSignY = 1.0;
            }
            List<XYZ> rOrigemDiagonal = null;
            if (gAbsX > gAbsY + 0.01)
            {
                rOrigemDiagonal = new List<XYZ>
                {
                    P1,
                    new XYZ(P1.X + gAbsY * gSignX, P1.Y + gAbsY * gSignY, 0.0),
                    P2
                };
            }
            else if (gAbsY > gAbsX + 0.01)
            {
                rOrigemDiagonal = new List<XYZ>
                {
                    P1,
                    new XYZ(P1.X + gAbsX * gSignX, P1.Y + gAbsX * gSignY, 0.0),
                    P2
                };
            }
            if (rOrigemDiagonal != null)
            {
                double lenDiag = rOrigemDiagonal[0].DistanceTo(rOrigemDiagonal[1]);
                double lenReto = rOrigemDiagonal[1].DistanceTo(rOrigemDiagonal[2]);
                double minTrechoCatetoOrigem = 0.16 * diametroMM * Math.Sqrt(2.0) / 100.0 / 0.3048;
                double minTrechoFinalOrigem = 0.39370078740157477;
                if (lenDiag >= minTrechoCatetoOrigem && lenReto >= minTrechoFinalOrigem)
                {
                    List<XYZ> rotaPreLimpaOrigem = new List<XYZ> { rOrigemDiagonal[0] };
                    for (int i7 = 1; i7 < rOrigemDiagonal.Count - 1; i7++)
                    {
                        XYZ v7 = (rOrigemDiagonal[i7] - rotaPreLimpaOrigem.Last()).Normalize();
                        XYZ v8 = (rOrigemDiagonal[i7 + 1] - rOrigemDiagonal[i7]).Normalize();
                        if (v7.DistanceTo(v8) > 0.01)
                        {
                            rotaPreLimpaOrigem.Add(rOrigemDiagonal[i7]);
                        }
                    }
                    rotaPreLimpaOrigem.Add(rOrigemDiagonal.Last());
                    List<XYZ> rotaChanfradaOrigem = Chanfrar90Graus(rotaPreLimpaOrigem, 0.49212598425196846);
                    if (rotaChanfradaOrigem != null)
                    {
                        List<XYZ> rotaLimpaOrigem = new List<XYZ> { rotaChanfradaOrigem[0] };
                        for (int i8 = 1; i8 < rotaChanfradaOrigem.Count - 1; i8++)
                        {
                            XYZ v9 = (rotaChanfradaOrigem[i8] - rotaLimpaOrigem.Last()).Normalize();
                            XYZ v10 = (rotaChanfradaOrigem[i8 + 1] - rotaChanfradaOrigem[i8]).Normalize();
                            if (v9.DistanceTo(v10) > 0.01)
                            {
                                rotaLimpaOrigem.Add(rotaChanfradaOrigem[i8]);
                            }
                        }
                        rotaLimpaOrigem.Add(rotaChanfradaOrigem.Last());
                        bool validaOrigem = true;
                        for (int i9 = 0; i9 < rotaLimpaOrigem.Count - 2; i9++)
                        {
                            XYZ v11 = (rotaLimpaOrigem[i9 + 1] - rotaLimpaOrigem[i9]).Normalize();
                            XYZ v12 = (rotaLimpaOrigem[i9 + 2] - rotaLimpaOrigem[i9 + 1]).Normalize();
                            if (v11.DotProduct(v12) < 0.01)
                            {
                                validaOrigem = false;
                                break;
                            }
                        }
                        if (validaOrigem)
                        {
                            for (int i10 = 0; i10 < rotaLimpaOrigem.Count - 1; i10++)
                            {
                                double minAplicavel2 = ((i10 == rotaLimpaOrigem.Count - 2) ? minTrechoFinalOrigem : minTrechoCatetoOrigem);
                                if (rotaLimpaOrigem[i10].DistanceTo(rotaLimpaOrigem[i10 + 1]) < minAplicavel2)
                                {
                                    validaOrigem = false;
                                    break;
                                }
                            }
                        }
                        if (validaOrigem)
                        {
                            rotasValidas.Add(rotaLimpaOrigem.Select((XYZ pt) => new XYZ(pt.X, pt.Y, Z)).ToList());
                        }
                    }
                }
            }
        }
        if (regraDistanciaPorDiametro)
        {
            List<List<XYZ>> rotasUnicas = new List<List<XYZ>>();
            foreach (List<XYZ> rota in rotasValidas)
            {
                if (!rotasUnicas.Any((List<XYZ> existente) => existente.Count == rota.Count && existente.Zip(rota, (XYZ a, XYZ b) => a.DistanceTo(b) < 0.09842519685039369).All((bool igual) => igual)))
                {
                    rotasUnicas.Add(rota);
                }
            }
            rotasValidas = rotasUnicas;
        }
        return regraDistanciaPorDiametro ? rotasValidas.OrderBy((List<XYZ> list) => list.Count).ThenBy((List<XYZ> rota2) => CalcularComprimentoRota(rota2)).ToList() : rotasValidas.OrderBy((List<XYZ> rota2) => CalcularComprimentoRota(rota2)).ToList();
    }

    public static List<XYZ> Chanfrar90Graus(List<XYZ> rota, double d)
    {
        if (rota.Count < 3)
        {
            return rota;
        }
        List<XYZ> novaRota = new List<XYZ>();
        novaRota.Add(rota[0]);
        for (int i = 1; i < rota.Count - 1; i++)
        {
            XYZ prev = novaRota.Last();
            XYZ corner = rota[i];
            XYZ next = rota[i + 1];
            XYZ vIn = (corner - prev).Normalize();
            XYZ vOut = (next - corner).Normalize();
            if (Math.Abs(vIn.DotProduct(vOut)) < 0.05)
            {
                if (prev.DistanceTo(corner) < d + 0.01)
                {
                    return null;
                }
                XYZ p1 = corner - vIn * d;
                XYZ p2 = corner + vOut * d;
                novaRota.Add(p1);
                novaRota.Add(p2);
            }
            else
            {
                novaRota.Add(corner);
            }
        }
        novaRota.Add(rota.Last());
        return novaRota;
    }

    public static double CalcularComprimentoRota(List<XYZ> rota)
    {
        double comp = 0.0;
        for (int i = 0; i < rota.Count - 1; i++)
        {
            comp += rota[i].DistanceTo(rota[i + 1]);
        }
        return comp;
    }

    public static List<XYZ> ResolverRotaChuveiro(XYZ origem, XYZ dirOrigem, XYZ destino, XYZ dirDestino = null)
    {
        double folgaMetros = 0.24606299212598423;
        XYZ dirFim = dirDestino;
        if (dirFim == null)
        {
            XYZ v = destino - origem;
            dirFim = ((!(Math.Abs(v.X) > Math.Abs(v.Y))) ? ((v.Y > 0.0) ? (-XYZ.BasisY) : XYZ.BasisY) : ((v.X > 0.0) ? (-XYZ.BasisX) : XYZ.BasisX));
        }
        dirFim = new XYZ(dirFim.X, dirFim.Y, 0.0).Normalize();
        XYZ dirLivre = new XYZ(Math.Cos(Math.PI / 8.0), Math.Sin(Math.PI / 8.0), 0.0);
        XYZ origemRota = origem;
        if (Math.Abs(dirOrigem.Z) < 0.1)
        {
            origemRota = origem + new XYZ(dirOrigem.X, dirOrigem.Y, 0.0).Normalize() * (75.0 / 508.0);
        }
        XYZ destinoParaRota = new XYZ(destino.X, destino.Y, origemRota.Z);
        ComandoConectarAparelho.Rota rota = ComandoConectarAparelho.TraçarRotaZLimpa(destinoParaRota, dirFim, origemRota, dirLivre, folgaMetros);
        if (rota == null || rota.PontosPiso == null || rota.PontosPiso.Count == 0)
        {
            return null;
        }
        List<XYZ> pontos = new List<XYZ>();
        pontos.Add(destino);
        foreach (XYZ p in rota.PontosPiso)
        {
            pontos.Add(new XYZ(p.X, p.Y, origem.Z));
        }
        pontos.Reverse();
        return pontos;
    }

    public static List<XYZ> ResolverRotaLavatorio(XYZ origem, XYZ dirOrigem, XYZ destino, XYZ dirDestino = null)
    {
        double folgaMetros = 0.19685039370078738;
        double diamLav = UnitUtils.ConvertToInternalUnits(JigLancamentoManager.Cfg.DiametroLavatorio, UnitTypeId.Millimeters);
        double offsetJoelho = diamLav;
        XYZ dirFim = dirDestino;
        if (dirFim == null)
        {
            XYZ delta = destino - origem;
            dirFim = ((Math.Abs(delta.X) > Math.Abs(delta.Y)) ? new XYZ(1.0, 0.0, 0.0) : new XYZ(0.0, 1.0, 0.0));
        }
        dirFim = new XYZ(dirFim.X, dirFim.Y, 0.0).Normalize();
        XYZ destinoParaRota = new XYZ(destino.X, destino.Y, origem.Z);
        ComandoConectarAparelho.Rota rotaLav;
        if (JigLancamentoManager.Cfg.DesviarVigaLavatorio)
        {
            double avancoMetros = 0.21325459317585302;
            rotaLav = ComandoConectarAparelho.RoteamentoDesvioViga(destinoParaRota, dirFim, origem, dirOrigem, avancoMetros, offsetJoelho, folgaMetros);
        }
        else
        {
            rotaLav = ComandoConectarAparelho.RoteamentoZPuro(destinoParaRota, dirFim, origem, dirOrigem, offsetJoelho, folgaMetros);
        }
        if (rotaLav == null)
        {
            return new List<XYZ> { origem, destino };
        }
        List<XYZ> pontos = new List<XYZ>();
        pontos.Add(destino);
        foreach (XYZ p in rotaLav.PontosPiso)
        {
            pontos.Add(new XYZ(p.X, p.Y, origem.Z));
        }
        pontos.Reverse();
        return pontos;
    }

    public static FamilyInstance ConectarJoelho(Document doc, Pipe p1, Pipe p2, XYZ centro)
    {
        Connector c1 = GetConnectorClosestTo(p1, centro);
        Connector c2 = GetConnectorClosestTo(p2, centro);
        if (c1 != null && c2 != null)
        {
            try
            {
                return doc.Create.NewElbowFitting(c1, c2);
            }
            catch
            {
            }
        }
        return null;
    }

    public static Connector GetConnectorClosestTo(Pipe pipe, XYZ point)
    {
        Connector best = null;
        double min = double.MaxValue;
        if (pipe.ConnectorManager == null)
        {
            return null;
        }
        foreach (Connector c in pipe.ConnectorManager.Connectors)
        {
            if (c.ConnectorType != ConnectorType.Logical)
            {
                double d = c.Origin.DistanceTo(point);
                if (d < min)
                {
                    min = d;
                    best = c;
                }
            }
        }
        return best;
    }

    private ElementId ObterNivelDaVista(Document doc, View vista)
    {
        if (vista is ViewPlan { GenLevel: not null } vp)
        {
            return vp.GenLevel.Id;
        }
        if (vista?.LevelId != null && vista.LevelId != ElementId.InvalidElementId)
        {
            return vista.LevelId;
        }
        double zReferencia = 0.0;
        try
        {
            if (vista is ViewPlan vpFallback)
            {
                PlanViewRange viewRange = vpFallback.GetViewRange();
                double cutOffset = viewRange.GetOffset(PlanViewPlane.CutPlane);
                ElementId cutLevelId = viewRange.GetLevelId(PlanViewPlane.CutPlane);
                if (doc.GetElement(cutLevelId) is Level cutLevel)
                {
                    zReferencia = cutLevel.Elevation + cutOffset;
                }
            }
        }
        catch
        {
        }
        List<Level> todos = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
        return todos.OrderBy((Level l) => Math.Abs(l.Elevation - zReferencia)).FirstOrDefault()?.Id ?? ElementId.InvalidElementId;
    }
}
