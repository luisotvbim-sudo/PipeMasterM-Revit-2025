using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoConectarAparelho : IExternalCommand
{
    public class Rota
    {
        public List<XYZ> PontosPiso;

        public XYZ PointBasePrumada;

        public double DistanciaTotal;

        public int NumeroCurvas;
    }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (!VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        ConectarAparelhoOptionsViewModel viewModel = new ConectarAparelhoOptionsViewModel();
        viewModel.AjustarTema(commandData.Application.Application.BackgroundColor);
        ConectarAparelhoOptionsBar optionsControl = new ConectarAparelhoOptionsBar
        {
            DataContext = viewModel
        };
        using OptionsBarSession session = OptionsBarSession.Begin(optionsControl);
        if (session == null)
        {
            TaskDialog.Show("PipeMaster [M]", "Aviso: Não foi possível injetar a interface na Options Bar. Usando valores padrão.");
        }
        try
        {
            Reference refCaixa = uidoc.Selection.PickObject(ObjectType.Element, new FiltroCaixaSifonadaNova(), "PipeMaster [M]: 1. Selecione a Caixa Sifonada CLICANDO PERTO da saída desejada...");
            if (!(doc.GetElement(refCaixa) is FamilyInstance { MEPModel: not null } caixaSifonada) || caixaSifonada.MEPModel.ConnectorManager == null)
            {
                return Result.Cancelled;
            }
            XYZ ptCliqueCaixa = refCaixa.GlobalPoint;
            XYZ ptCliqueCaixa2D = new XYZ(ptCliqueCaixa.X, ptCliqueCaixa.Y, 0.0);
            Connector melhorConector = null;
            double menorDistanciaClique = double.MaxValue;
            foreach (Connector c in caixaSifonada.MEPModel.ConnectorManager.Connectors)
            {
                if (!c.IsConnected && c.ConnectorType == ConnectorType.End)
                {
                    double distAoClique = new XYZ(c.Origin.X, c.Origin.Y, 0.0).DistanceTo(ptCliqueCaixa2D);
                    if (distAoClique < menorDistanciaClique)
                    {
                        menorDistanciaClique = distAoClique;
                        melhorConector = c;
                    }
                }
            }
            if (melhorConector == null)
            {
                return Result.Failed;
            }
            XYZ ptCliqueParede = uidoc.Selection.PickPoint("PipeMaster [M]: 2. Clique no PONTO EXATO da parede (onde a FACE do joelho ficará)...");
            XYZ ptParede2D = new XYZ(ptCliqueParede.X, ptCliqueParede.Y, 0.0);
            XYZ ptRosaDosVentos = uidoc.Selection.PickPoint("PipeMaster [M]: 3. ROSA DOS VENTOS: Clique na direção para fora da parede...");
            XYZ ptRosa2D = new XYZ(ptRosaDosVentos.X, ptRosaDosVentos.Y, 0.0);
            XYZ vetorDirecao = ptRosa2D - ptParede2D;
            if (vetorDirecao.GetLength() < 0.01)
            {
                return Result.Failed;
            }
            XYZ dirFaceJoelho = GetNearestOrthogonal(vetorDirecao);
            double userAltura = viewModel.GetAltura();
            double userInclinacao = viewModel.GetInclinacao() / 100.0;
            double userTrechoMinimo = viewModel.GetTrechoMinimo();
            bool userDesvioViga = viewModel.DesvioViga;
            double userAvancoDesvio = viewModel.GetAvancoDesvio();
            ElementId sysId = null;
            foreach (Connector conn in caixaSifonada.MEPModel.ConnectorManager.Connectors)
            {
                if (conn.MEPSystem != null)
                {
                    sysId = conn.MEPSystem.GetTypeId();
                    break;
                }
            }
            if (sysId == null)
            {
                PipingSystemType matchingSys = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault((PipingSystemType x) => x.SystemClassification == MEPSystemClassification.Sanitary);
                if (matchingSys != null)
                {
                    sysId = matchingSys.Id;
                }
            }
            if (sysId == null)
            {
                sysId = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).FirstElementId();
            }
            ElementId pipeTypeId = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).FirstElementId();
            double diametro = melhorConector.Radius * 2.0;
            Level nivel = (doc.GetElement(caixaSifonada.LevelId) as Level) ?? doc.ActiveView.GenLevel ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
            double elevacaoNivel = nivel.Elevation;
            double offsetJoelho = diametro;
            using Transaction t = new Transaction(doc, "PipeMaster: Conectar Aparelho");
            t.Start();
            SubTransaction st = new SubTransaction(doc);
            try
            {
                st.Start();
                XYZ dO = ptParede2D + new XYZ(0.0, 0.0, 100.0);
                Pipe pD1 = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, dO, dO + new XYZ(0.0, 0.0, 2.0));
                Pipe pD2 = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, dO + new XYZ(0.0, 0.0, 2.0), dO + new XYZ(0.0, 0.0, 2.0) + dirFaceJoelho.Multiply(2.0));
                ((Element)pD1).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                ((Element)pD2).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                doc.Regenerate();
                Connector cD1 = ObterConectorMaisProximo(pD1, dO + new XYZ(0.0, 0.0, 2.0));
                Connector cD2 = ObterConectorMaisProximo(pD2, dO + new XYZ(0.0, 0.0, 2.0));
                FamilyInstance dummyElbow = doc.Create.NewElbowFitting(cD1, cD2);
                doc.Regenerate();
                if (dummyElbow != null)
                {
                    BoundingBoxXYZ bb = ((Element)dummyElbow).get_BoundingBox((View)null);
                    if (bb != null)
                    {
                        XYZ center = (dummyElbow.Location as LocationPoint).Point;
                        offsetJoelho = ((!(Math.Abs(dirFaceJoelho.X) > 0.5)) ? ((dirFaceJoelho.Y > 0.0) ? (bb.Max.Y - center.Y) : (center.Y - bb.Min.Y)) : ((dirFaceJoelho.X > 0.0) ? (bb.Max.X - center.X) : (center.X - bb.Min.X)));
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
            XYZ O = new XYZ(melhorConector.Origin.X, melhorConector.Origin.Y, 0.0);
            XYZ D = new XYZ(melhorConector.CoordinateSystem.BasisZ.X, melhorConector.CoordinateSystem.BasisZ.Y, 0.0).Normalize();
            double folgaMetros = userTrechoMinimo / 0.3048;
            Rota melhorRota = null;
            if (userDesvioViga)
            {
                double avancoMetros = userAvancoDesvio / 0.3048;
                melhorRota = RoteamentoDesvioViga(O, D, ptParede2D, dirFaceJoelho, avancoMetros, offsetJoelho, folgaMetros);
            }
            else
            {
                melhorRota = RoteamentoZPuro(O, D, ptParede2D, dirFaceJoelho, offsetJoelho, folgaMetros);
            }
            if (melhorRota == null)
            {
                t.RollBack();
                TaskDialog.Show("PipeMaster [M]", "Impossível traçar rota limpa (ortogonal/45º) com o espaço disponível. Tente clicar em outra saída ou modifique o layout.");
                return Result.Failed;
            }
            List<XYZ> ptPiso3D = new List<XYZ>();
            double cotaAtual = melhorConector.Origin.Z;
            XYZ ptAtual2D = new XYZ(melhorConector.Origin.X, melhorConector.Origin.Y, 0.0);
            ptPiso3D.Add(melhorConector.Origin);
            foreach (XYZ pt2D in melhorRota.PontosPiso)
            {
                double dist = ptAtual2D.DistanceTo(pt2D);
                cotaAtual += dist * userInclinacao;
                ptPiso3D.Add(new XYZ(pt2D.X, pt2D.Y, cotaAtual));
                ptAtual2D = pt2D;
            }
            List<Pipe> tubosPiso = new List<Pipe>();
            for (int i = 0; i < ptPiso3D.Count - 1; i++)
            {
                Pipe p = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, ptPiso3D[i], ptPiso3D[i + 1]);
                ((Element)p).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                tubosPiso.Add(p);
            }
            XYZ ptSubidaNaParede = ptParede2D - dirFaceJoelho.Multiply(offsetJoelho);
            XYZ baseVerticalFinal = ptPiso3D.Last();
            double cotaTopo = elevacaoNivel + userAltura / 0.3048;
            if (cotaTopo < baseVerticalFinal.Z + 0.3)
            {
                cotaTopo = baseVerticalFinal.Z + 0.3;
            }
            XYZ topoVerticalFinal = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, cotaTopo);
            Pipe tuboPrumadaPrincipal = null;
            Pipe tuboPrumadaCurta = null;
            Pipe tuboDesvioDiagonal = null;
            if (userDesvioViga)
            {
                double avancoMetrosDesvio = userAvancoDesvio / 0.3048;
                double cotaAltoDesvio = elevacaoNivel + 25.0 / 762.0;
                double cotaBaixoDesvio = cotaAltoDesvio - avancoMetrosDesvio;
                if (cotaBaixoDesvio < baseVerticalFinal.Z + 125.0 / 381.0)
                {
                    double diff = baseVerticalFinal.Z + 125.0 / 381.0 - cotaBaixoDesvio;
                    cotaAltoDesvio += diff;
                    cotaBaixoDesvio += diff;
                }
                XYZ pBaixoDesvio = new XYZ(melhorRota.PointBasePrumada.X, melhorRota.PointBasePrumada.Y, cotaBaixoDesvio);
                XYZ pAltoDesvio = new XYZ(ptSubidaNaParede.X, ptSubidaNaParede.Y, cotaAltoDesvio);
                tuboPrumadaCurta = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, baseVerticalFinal, pBaixoDesvio);
                tuboDesvioDiagonal = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, pBaixoDesvio, pAltoDesvio);
                tuboPrumadaPrincipal = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, pAltoDesvio, topoVerticalFinal);
                ((Element)tuboPrumadaCurta).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                ((Element)tuboDesvioDiagonal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
                ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
            }
            else
            {
                tuboPrumadaPrincipal = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, baseVerticalFinal, topoVerticalFinal);
                ((Element)tuboPrumadaPrincipal).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
            }
            XYZ tocoFinalPos = topoVerticalFinal + dirFaceJoelho.Multiply(0.5);
            Pipe tuboTemporarioAlinhamento = Pipe.Create(doc, sysId, pipeTypeId, nivel.Id, topoVerticalFinal, tocoFinalPos);
            ((Element)tuboTemporarioAlinhamento).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
            doc.Regenerate();
            ObterConectorMaisProximo(tubosPiso[0], ptPiso3D[0]).ConnectTo(melhorConector);
            for (int i2 = 0; i2 < tubosPiso.Count - 1; i2++)
            {
                doc.Create.NewElbowFitting(ObterConectorMaisProximo(tubosPiso[i2], ptPiso3D[i2 + 1]), ObterConectorMaisProximo(tubosPiso[i2 + 1], ptPiso3D[i2 + 1]));
            }
            Connector cBasePisoF = ObterConectorMaisProximo(tubosPiso.Last(), baseVerticalFinal);
            Connector cBasePrumaF = ObterConectorMaisProximo(userDesvioViga ? tuboPrumadaCurta : tuboPrumadaPrincipal, baseVerticalFinal);
            doc.Create.NewElbowFitting(cBasePisoF, cBasePrumaF);
            if (userDesvioViga)
            {
                XYZ pBaixo = (tuboPrumadaCurta.Location as LocationCurve).Curve.GetEndPoint(1);
                XYZ pAlto = (tuboDesvioDiagonal.Location as LocationCurve).Curve.GetEndPoint(1);
                doc.Create.NewElbowFitting(ObterConectorMaisProximo(tuboPrumadaCurta, pBaixo), ObterConectorMaisProximo(tuboDesvioDiagonal, pBaixo));
                doc.Create.NewElbowFitting(ObterConectorMaisProximo(tuboDesvioDiagonal, pAlto), ObterConectorMaisProximo(tuboPrumadaPrincipal, pAlto));
            }
            doc.Create.NewElbowFitting(ObterConectorMaisProximo(tuboPrumadaPrincipal, topoVerticalFinal), ObterConectorMaisProximo(tuboTemporarioAlinhamento, topoVerticalFinal));
            doc.Regenerate();
            doc.Delete(tuboTemporarioAlinhamento.Id);
            t.Commit();
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex2)
        {
            TaskDialog.Show("PipeMaster [M]", "Falha inesperada ao modelar as conexões.\n" + ex2.Message);
            return Result.Failed;
        }
    }

    public static Rota RoteamentoZPuro(XYZ O, XYZ D, XYZ ptParede2D, XYZ dirFaceJoelho, double offsetJoelho, double folgaMetros)
    {
        XYZ ptTargetPrumada = ptParede2D - dirFaceJoelho.Multiply(offsetJoelho);
        return TraçarRotaZLimpa(O, D, ptTargetPrumada, dirFaceJoelho, folgaMetros, permitirPerpendicular: true);
    }

    public static Rota RoteamentoDesvioViga(XYZ O, XYZ D, XYZ ptParede2D, XYZ dirFaceJoelho, double avancoMetrosDesvio, double offsetJoelho, double folgaMetros)
    {
        XYZ ptTargetPrumadaAfastada = ptParede2D - dirFaceJoelho.Multiply(offsetJoelho) + dirFaceJoelho.Multiply(avancoMetrosDesvio);
        return TraçarRotaZLimpa(O, D, ptTargetPrumadaAfastada, dirFaceJoelho, folgaMetros, permitirPerpendicular: true);
    }

    public static Rota TraçarRotaZLimpa(XYZ O, XYZ D, XYZ TargetFinal2D, XYZ dirFaceJoelho, double folgaMetros, bool permitirPerpendicular = false)
    {
        List<Rota> rotasValidas = new List<Rota>();
        XYZ V_final = TargetFinal2D - O;
        if (Math.Abs(V_final.Normalize().CrossProduct(D).GetLength()) < 0.05 && V_final.DotProduct(D) > folgaMetros && IsFinalValid(D))
        {
            rotasValidas.Add(new Rota
            {
                PontosPiso = new List<XYZ> { TargetFinal2D },
                PointBasePrumada = TargetFinal2D,
                DistanciaTotal = V_final.GetLength(),
                NumeroCurvas = 0
            });
        }
        XYZ[] array = GetNextDirs(D);
        foreach (XYZ A in array)
        {
            if (IsFinalValid(A))
            {
                double[] t = Solve(D, A, V_final);
                if (t != null && t[0] >= folgaMetros && t[1] >= folgaMetros)
                {
                    rotasValidas.Add(new Rota
                    {
                        PontosPiso = new List<XYZ>
                        {
                            O + D.Multiply(t[0]),
                            TargetFinal2D
                        },
                        PointBasePrumada = TargetFinal2D,
                        DistanciaTotal = t[0] + t[1],
                        NumeroCurvas = 1
                    });
                }
            }
        }
        XYZ[] array2 = GetNextDirs(D);
        foreach (XYZ M in array2)
        {
            XYZ[] array3 = GetNextDirs(M);
            foreach (XYZ A2 in array3)
            {
                if (IsFinalValid(A2))
                {
                    double[] t_2_1 = Solve(M, A2, V_final - D.Multiply(folgaMetros));
                    if (t_2_1 != null && t_2_1[0] >= folgaMetros && t_2_1[1] >= folgaMetros)
                    {
                        rotasValidas.Add(new Rota
                        {
                            PontosPiso = new List<XYZ>
                            {
                                O + D.Multiply(folgaMetros),
                                O + D.Multiply(folgaMetros) + M.Multiply(t_2_1[0]),
                                TargetFinal2D
                            },
                            PointBasePrumada = TargetFinal2D,
                            DistanciaTotal = folgaMetros + t_2_1[0] + t_2_1[1],
                            NumeroCurvas = 2
                        });
                    }
                    double[] t_2_2 = Solve(D, M, V_final - A2.Multiply(folgaMetros));
                    if (t_2_2 != null && t_2_2[0] >= folgaMetros && t_2_2[1] >= folgaMetros)
                    {
                        rotasValidas.Add(new Rota
                        {
                            PontosPiso = new List<XYZ>
                            {
                                O + D.Multiply(t_2_2[0]),
                                O + D.Multiply(t_2_2[0]) + M.Multiply(t_2_2[1]),
                                TargetFinal2D
                            },
                            PointBasePrumada = TargetFinal2D,
                            DistanciaTotal = t_2_2[0] + t_2_2[1] + folgaMetros,
                            NumeroCurvas = 2
                        });
                    }
                }
            }
        }
        XYZ[] array4 = GetNextDirs(D);
        foreach (XYZ M2 in array4)
        {
            XYZ[] array5 = GetNextDirs(M2);
            foreach (XYZ M3 in array5)
            {
                XYZ[] array6 = GetNextDirs(M3);
                foreach (XYZ A3 in array6)
                {
                    if (IsFinalValid(A3))
                    {
                        XYZ fixedPath1 = D.Multiply(folgaMetros) + M2.Multiply(folgaMetros);
                        double[] t_3_1 = Solve(M3, A3, V_final - fixedPath1);
                        if (t_3_1 != null && t_3_1[0] >= folgaMetros && t_3_1[1] >= folgaMetros)
                        {
                            rotasValidas.Add(new Rota
                            {
                                PontosPiso = new List<XYZ>
                                {
                                    O + D.Multiply(folgaMetros),
                                    O + D.Multiply(folgaMetros) + M2.Multiply(folgaMetros),
                                    O + fixedPath1 + M3.Multiply(t_3_1[0]),
                                    TargetFinal2D
                                },
                                PointBasePrumada = TargetFinal2D,
                                DistanciaTotal = folgaMetros * 2.0 + t_3_1[0] + t_3_1[1],
                                NumeroCurvas = 3
                            });
                        }
                        XYZ fixedPath2 = M2.Multiply(folgaMetros) + A3.Multiply(folgaMetros);
                        double[] t_3_2 = Solve(D, M3, V_final - fixedPath2);
                        if (t_3_2 != null && t_3_2[0] >= folgaMetros && t_3_2[1] >= folgaMetros)
                        {
                            rotasValidas.Add(new Rota
                            {
                                PontosPiso = new List<XYZ>
                                {
                                    O + D.Multiply(t_3_2[0]),
                                    O + D.Multiply(t_3_2[0]) + M2.Multiply(folgaMetros),
                                    O + D.Multiply(t_3_2[0]) + M2.Multiply(folgaMetros) + M3.Multiply(t_3_2[1]),
                                    TargetFinal2D
                                },
                                PointBasePrumada = TargetFinal2D,
                                DistanciaTotal = t_3_2[0] + folgaMetros + t_3_2[1] + folgaMetros,
                                NumeroCurvas = 3
                            });
                        }
                    }
                }
            }
        }
        XYZ[] array7 = GetNextDirs(D);
        foreach (XYZ M4 in array7)
        {
            XYZ[] array8 = GetNextDirs(M4);
            foreach (XYZ M5 in array8)
            {
                XYZ[] array9 = GetNextDirs(M5);
                foreach (XYZ M6 in array9)
                {
                    XYZ[] array10 = GetNextDirs(M6);
                    foreach (XYZ A4 in array10)
                    {
                        if (IsFinalValid(A4))
                        {
                            XYZ fixedPath3 = D.Multiply(folgaMetros) + M4.Multiply(folgaMetros) + M5.Multiply(folgaMetros);
                            double[] t_4 = Solve(M6, A4, V_final - fixedPath3);
                            if (t_4 != null && t_4[0] >= folgaMetros && t_4[1] >= folgaMetros)
                            {
                                rotasValidas.Add(new Rota
                                {
                                    PontosPiso = new List<XYZ>
                                    {
                                        O + D.Multiply(folgaMetros),
                                        O + D.Multiply(folgaMetros) + M4.Multiply(folgaMetros),
                                        O + D.Multiply(folgaMetros) + M4.Multiply(folgaMetros) + M5.Multiply(folgaMetros),
                                        O + fixedPath3 + M6.Multiply(t_4[0]),
                                        TargetFinal2D
                                    },
                                    PointBasePrumada = TargetFinal2D,
                                    DistanciaTotal = folgaMetros * 3.0 + t_4[0] + t_4[1],
                                    NumeroCurvas = 4
                                });
                            }
                        }
                    }
                }
            }
        }
        if (rotasValidas.Count == 0)
        {
            return null;
        }
        return (from r in rotasValidas
                orderby r.NumeroCurvas, r.DistanciaTotal
                select r).First();
        static XYZ[] GetNextDirs(XYZ currentDir)
        {
            return new XYZ[2]
            {
                RotateZ(currentDir, 45.0),
                RotateZ(currentDir, -45.0)
            };
        }
        bool IsFinalValid(XYZ xYZ)
        {
            if (permitirPerpendicular)
            {
                return true;
            }
            return Math.Abs(xYZ.DotProduct(dirFaceJoelho)) > 0.05;
        }
        static double[] Solve(XYZ xYZ, XYZ xYZ2, XYZ V)
        {
            double det = xYZ.X * xYZ2.Y - xYZ.Y * xYZ2.X;
            if (Math.Abs(det) < 1E-09)
            {
                return null;
            }
            double t2 = (V.X * xYZ2.Y - V.Y * xYZ2.X) / det;
            double t3 = (xYZ.X * V.Y - xYZ.Y * V.X) / det;
            return new double[2] { t2, t3 };
        }
    }

    public static XYZ RotateZ(XYZ v, double angleDegrees)
    {
        return new XYZ(v.X * Math.Cos(angleDegrees * Math.PI / 180.0) - v.Y * Math.Sin(angleDegrees * Math.PI / 180.0), v.X * Math.Sin(angleDegrees * Math.PI / 180.0) + v.Y * Math.Cos(angleDegrees * Math.PI / 180.0), 0.0).Normalize();
    }

    private XYZ GetNearestOrthogonal(XYZ v)
    {
        return (Math.Abs(v.X) > Math.Abs(v.Y)) ? new XYZ(Math.Sign(v.X), 0.0, 0.0) : new XYZ(0.0, Math.Sign(v.Y), 0.0);
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
            if (c.Origin.DistanceTo(alvo) < menorDist)
            {
                menorDist = c.Origin.DistanceTo(alvo);
                melhor = c;
            }
        }
        return melhor;
    }
}
