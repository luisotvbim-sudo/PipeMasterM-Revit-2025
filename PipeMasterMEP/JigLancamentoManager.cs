using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace PipeMasterMEP;

public static class JigLancamentoManager
{
    public enum Etapas
    {
        Nenhuma,
        Vaso_Ponto2,
        Vaso_Ponto3,
        Vaso_Parede,
        CaixaSifonada,
        Caixa_Destino,
        Caixa_EscolherRota,
        Pia_Destino,
        Pia_EscolherRota,
        Maquina_Destino,
        Maquina_EscolherRota,
        Lavatorio_Caixa,
        Chuveiro_Caixa,
        Ventilacao_SelecionarColuna,
        Ventilacao_EscolherDirecaoCavalete,
        Ventilacao_LigarRamal,
        Ventilacao_EscolherRota
    }

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    private struct POINT
    {
        public int X;

        public int Y;
    }

    public static Etapas EtapaAtual = Etapas.Nenhuma;

    public static XYZ Pt1;

    public static XYZ Pt2;

    public static XYZ IntA;

    public static XYZ IntB;

    public static XYZ PtClickFinal;

    public static XYZ PtMouseRota;

    public static XYZ CentroPrumada = null;

    public static XYZ Pt2_Temp;

    public static XYZ IntA_Temp;

    public static XYZ IntB_Temp;

    public static List<List<XYZ>> RotasPia = null;

    public static List<List<XYZ>> RotasPia_Temp = null;

    public static double ZPreview;

    public static double ZColetor;

    public static double ZNivel;

    public static double DiamVaso;

    public static double ZVistaMin = double.MinValue;

    public static double ZVistaMax = double.MaxValue;

    public static ConfigLancamentoAuto Cfg;

    public static ElementId LevelId;

    public static int RotaEscolhida = 0;

    public static XYZ PtAlinhamentoVaso;

    public static XYZ DirPreviewParede;
    public const double DIST_RECUO_JUNCAO_MAQUINA_PES = 275.0 / 762.0;

    public const double FOLGA_MINIMA_JOELHO_PES = 125.0 / 762.0;

    public const double FOLGA_MINIMA_ROTA_CATETO_PES = 25.0 / 762.0;

    public const double TOLERANCIA_PERPENDICULAR_GRAUS = 15.0;

    public const double MIN_TRECHO_RETO_CAIXA_PES = 0.25;

    public const double MIN_AVANCO_JUNCAO_PES = 0.25;

    public static Pipe TuboDestino;

    public static Connector ConectorCaixa;

    public static XYZ DirCaixa;

    public static XYZ PtParedeLavatorio;

    public static XYZ DirParedeLavatorio;

    public static XYZ PtParedePia;

    public static XYZ DirParedePia;

    public static XYZ PtParedeMaquina;

    public static XYZ DirParedeMaquina;

    public static Connector ConectorChuveiro;

    public static XYZ PtSaidaChuveiro;

    public static XYZ DirSaidaChuveiro;

    public static HashSet<ElementId> IdsCriadosNestaSessao = new HashSet<ElementId>();

    private static List<Pipe> _candidatosTuboPrincipal;

    private static List<FamilyInstance> _candidatosCaixas;

    private static UIApplication _uiapp;

    private static ExternalEvent _exEvent;

    private static FinalizarLancamentoHandler _handler;

    private static volatile bool _jigAtivo = false;

    private static bool _bloquearProximoUp = false;

    private static LowLevelMouseProc _proc = HookCallback;

    private static nint _hookID = IntPtr.Zero;

    private static void PrepararCandidatos(Document doc, XYZ pt, double raioPe = 15.0)
    {
        double zMin = ZVistaMin;
        double zMax = ZVistaMax;
        Outline outTubos = new Outline(new XYZ(pt.X - raioPe, pt.Y - raioPe, zMin), new XYZ(pt.X + raioPe, pt.Y + raioPe, zMax));
        _candidatosTuboPrincipal = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).WherePasses(new BoundingBoxIntersectsFilter(outTubos)).Cast<Pipe>()
            .Where(delegate (Pipe p)
            {
                LocationCurve locationCurve = p.Location as LocationCurve;
                if (locationCurve?.Curve == null)
                {
                    return false;
                }
                XYZ xYZ = (locationCurve.Curve.GetEndPoint(1) - locationCurve.Curve.GetEndPoint(0)).Normalize();
                if (Math.Abs(xYZ.Z) >= 0.5)
                {
                    return false;
                }
                double num = (locationCurve.Curve.GetEndPoint(0).Z + locationCurve.Curve.GetEndPoint(1).Z) / 2.0;
                return num >= zMin && num <= zMax;
            })
            .ToList();
        Outline outline = new Outline(new XYZ(pt.X - raioPe, pt.Y - raioPe, zMin), new XYZ(pt.X + raioPe, pt.Y + raioPe, zMax));
        _candidatosCaixas = (from FamilyInstance fi in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WherePasses(new BoundingBoxIntersectsFilter(outline))
                             where fi.Category != null && (fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PlumbingFixtures)) || fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_MechanicalEquipment)) || fi.Category.Id.Equals(new ElementId(BuiltInCategory.OST_PipeAccessory)))
                             select fi).Where(delegate (FamilyInstance fi)
                         {
                             Parameter parameter = ((Element)fi).get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                             if (parameter != null && parameter.AsElementId() != ElementId.InvalidElementId)
                             {
                                 return parameter.AsElementId() == LevelId;
                             }
                             return fi.Location is LocationPoint locationPoint && locationPoint.Point.Z >= zMin && locationPoint.Point.Z <= zMax;
                         }).ToList();
    }

    public static void IniciarJigVentilacao(UIApplication uiapp, XYZ ptCV)
    {
        Pt1 = ptCV;
        EtapaAtual = Etapas.Ventilacao_LigarRamal;
        _uiapp = uiapp;
        MontarJigNativo();
    }

    public static void IniciarJigVentilacaoCavalete(UIApplication uiapp, XYZ ptCV)
    {
        Pt1 = ptCV;
        EtapaAtual = Etapas.Ventilacao_EscolherDirecaoCavalete;
        _uiapp = uiapp;
        MontarJigNativo();
    }

    public static void IniciarJigVaso(UIApplication uiapp, XYZ pt1, double zPreview, ConfigLancamentoAuto cfg, double diamVaso, ElementId levelId, double zNivel, double zColetor)
    {
        EtapaAtual = Etapas.Vaso_Ponto2;
        _uiapp = uiapp;
        Pt1 = pt1;
        ZPreview = zPreview;
        Cfg = cfg;
        DiamVaso = diamVaso;
        LevelId = levelId;
        ZNivel = zNivel;
        ZColetor = zColetor;
        IdsCriadosNestaSessao.Clear();
        MontarJigNativo();
    }

    public static void IniciarJigCaixa(UIApplication uiapp, XYZ ptCaixa)
    {
        _uiapp = uiapp;
        Pt1 = ptCaixa;
        Pt2_Temp = null;
        IntA_Temp = null;
        IntB_Temp = null;
        TuboDestino = null;
        if (Cfg != null)
        {
            double zNivel = (uiapp.ActiveUIDocument.Document.GetElement(LevelId) as Level)?.Elevation ?? 0.0;
            ZPreview = zNivel + UnitUtils.ConvertToInternalUnits(Cfg.ElevacaoColetorMetros, UnitTypeId.Meters);
        }
        if (Cfg != null && Cfg.CaixaIndependente && Cfg.DestinoCaixa != 3)
        {
            EtapaAtual = Etapas.Caixa_Destino;
        }
        else
        {
            EtapaAtual = Etapas.CaixaSifonada;
            if (uiapp.ActiveUIDocument != null)
            {
                PrepararCandidatos(uiapp.ActiveUIDocument.Document, ptCaixa);
            }
        }
        MontarJigNativo();
    }

    public static void IniciarJigLavatorio(UIApplication uiapp, XYZ ptParede, XYZ dirFace)
    {
        EtapaAtual = Etapas.Lavatorio_Caixa;
        _uiapp = uiapp;
        PtParedeLavatorio = ptParede;
        DirParedeLavatorio = dirFace;
        if (uiapp.ActiveUIDocument != null)
        {
            PrepararCandidatos(uiapp.ActiveUIDocument.Document, ptParede);
        }
        MontarJigNativo();
    }

    public static void IniciarJigPia(UIApplication uiapp, XYZ ptParede, XYZ dirFace)
    {
        double offsetBase = 125.0 / 762.0;
        PtParedePia = ptParede;
        DirParedePia = dirFace;
        XYZ ptBase = ptParede - dirFace * offsetBase;
        Pt1 = new XYZ(ptBase.X, ptBase.Y, ZPreview);
        Pt2_Temp = null;
        IntA_Temp = null;
        IntB_Temp = null;
        TuboDestino = null;
        EtapaAtual = Etapas.Pia_Destino;
        _uiapp = uiapp;
        MontarJigNativo();
    }

    public static void IniciarJigMaquina(UIApplication uiapp, XYZ ptParede, XYZ dirFace)
    {
        double offsetBase = 125.0 / 762.0;
        PtParedeMaquina = ptParede;
        DirParedeMaquina = dirFace;
        XYZ ptBase = ptParede - dirFace * offsetBase;
        Pt1 = new XYZ(ptBase.X, ptBase.Y, ZPreview);
        Pt2_Temp = null;
        IntA_Temp = null;
        IntB_Temp = null;
        TuboDestino = null;
        EtapaAtual = Etapas.Maquina_Destino;
        _uiapp = uiapp;
        MontarJigNativo();
    }

    public static void IniciarJigChuveiro(UIApplication uiapp, XYZ ptSaida, XYZ dirSaida)
    {
        EtapaAtual = Etapas.Chuveiro_Caixa;
        _uiapp = uiapp;
        PtSaidaChuveiro = ptSaida;
        DirSaidaChuveiro = dirSaida;
        if (uiapp.ActiveUIDocument != null)
        {
            PrepararCandidatos(uiapp.ActiveUIDocument.Document, ptSaida);
        }
        MontarJigNativo();
    }

    public static void MontarJigNativo()
    {
        _jigAtivo = true;
        _bloquearProximoUp = false;
        if (_handler == null)
        {
            _handler = new FinalizarLancamentoHandler();
            _exEvent = ExternalEvent.Create(_handler);
        }
        if (_hookID == IntPtr.Zero)
        {
            _hookID = SetHook(_proc);
        }
        try
        {
            _uiapp.Idling -= Uiapp_Idling;
        }
        catch
        {
        }
        _uiapp.Idling += Uiapp_Idling;
    }

    private static void Uiapp_Idling(object sender, IdlingEventArgs e)
    {
        if (!_jigAtivo)
        {
            return;
        }
        if ((GetAsyncKeyState(27) & 0x8000) != 0)
        {
            _jigAtivo = false;
            DesmontarJigSeguro();
            return;
        }
        GetCursorPos(out var cursorTela);
        XYZ ptModelo = null;
        UIDocument uidoc = _uiapp.ActiveUIDocument;
        if (uidoc == null)
        {
            return;
        }
        UIView uiv = uidoc.GetOpenUIViews().FirstOrDefault((UIView v) => v.ViewId == uidoc.ActiveView.Id);
        if (uiv != null)
        {
            IList<XYZ> corners = uiv.GetZoomCorners();
            Rectangle winRect = uiv.GetWindowRectangle();
            int rLeft = winRect.Left;
            int rTop = winRect.Top;
            int rWidth = winRect.Right - winRect.Left;
            int rHeight = winRect.Bottom - winRect.Top;
            if (rWidth > 0 && rHeight > 0)
            {
                double tx = (double)(cursorTela.X - rLeft) / (double)rWidth;
                double ty = (double)(cursorTela.Y - rTop) / (double)rHeight;
                ptModelo = new XYZ(corners[0].X + tx * (corners[1].X - corners[0].X), corners[0].Y + (1.0 - ty) * (corners[1].Y - corners[0].Y), ZPreview);
            }
        }
        if (ptModelo == null)
        {
            return;
        }
        if (EtapaAtual == Etapas.Vaso_Parede)
        {
            XYZ ptAlign = new XYZ(PtAlinhamentoVaso.X, PtAlinhamentoVaso.Y, ZPreview);
            XYZ diff = new XYZ(ptModelo.X - ptAlign.X, ptModelo.Y - ptAlign.Y, 0.0);
            double distTotal = diff.GetLength();
            if (distTotal > 0.05)
            {
                XYZ ptFimPreview = (Pt2_Temp = ptAlign + (DirPreviewParede = ((!(Math.Abs(diff.X) >= Math.Abs(diff.Y))) ? new XYZ(0.0, Math.Sign(diff.Y), 0.0) : new XYZ(Math.Sign(diff.X), 0.0, 0.0))).Multiply(distTotal));
                GerenciadorPreview.Server.SetRotaLivre(new List<XYZ> { ptAlign, ptFimPreview });
                _uiapp.ActiveUIDocument.RefreshActiveView();
            }
        }
        else if (EtapaAtual == Etapas.Vaso_Ponto2)
        {
            XYZ ptTeste = ptModelo;
            if (Cfg.DestinoVaso == 1 || Cfg.DestinoVaso == 2)
            {
                Pipe tubo = ComandoLancamentoAutomatico.EncontrarTuboProximo(_uiapp.ActiveUIDocument.Document, ptModelo, 1.0, vertical: true);
                if (tubo != null)
                {
                    Curve c = (tubo.Location as LocationCurve).Curve;
                    XYZ ptExatoNoTubo = c.Project(ptModelo).XYZPoint;
                    ptTeste = new XYZ(ptExatoNoTubo.X, ptExatoNoTubo.Y, ZPreview);
                    TuboDestino = tubo;
                }
                else
                {
                    TuboDestino = null;
                }
                XYZ p1 = new XYZ(Pt1.X, Pt1.Y, ZPreview);
                XYZ p2 = new XYZ(ptTeste.X, ptTeste.Y, ZPreview);
                (XYZ, XYZ) tuple = ComandoLancamentoAutomatico.CalcularProjecao45Graus(Pt1, ptTeste, ZPreview);
                XYZ intA = tuple.Item1;
                XYZ intB = tuple.Item2;
                double distRecuo = 200.0 / 381.0;
                double minTrechoReto = 0.39370078740157477;
                int rotaNova = 1;
                if (intA.DistanceTo(intB) > 0.1)
                {
                    double dA = Math.Min(ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, p1, intA), ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, intA, p2));
                    double dB = Math.Min(ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, p1, intB), ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, intB, p2));
                    bool viavelA = ComandoLancamentoAutomatico.RotaVasoViavel(p1, intA, p2, distRecuo, minTrechoReto);
                    bool viavelB = ComandoLancamentoAutomatico.RotaVasoViavel(p1, intB, p2, distRecuo, minTrechoReto);
                    if (!viavelA && !viavelB)
                    {
                        rotaNova = -1;
                    }
                    else
                    {
                        rotaNova = ((dA <= dB) ? 1 : 2);
                        if (rotaNova == 1 && !viavelA)
                        {
                            rotaNova = 2;
                        }
                        else if (rotaNova == 2 && !viavelB)
                        {
                            rotaNova = 1;
                        }
                    }
                }
                GerenciadorPreview.Server.SetRotas(p1, intA, intB, p2);
                GerenciadorPreview.Server.SetRotaAtiva(rotaNova);
                RotaEscolhida = rotaNova;
                _uiapp.ActiveUIDocument.RefreshActiveView();
                Pt2_Temp = ptTeste;
                IntA_Temp = intA;
                IntB_Temp = intB;
            }
            else
            {
                double desvioX = Math.Abs(ptTeste.X - Pt1.X);
                double desvioY = Math.Abs(ptTeste.Y - Pt1.Y);
                if (desvioX < 0.3)
                {
                    ptTeste = new XYZ(Pt1.X, ptTeste.Y, ptTeste.Z);
                }
                else if (desvioY < 0.3)
                {
                    ptTeste = new XYZ(ptTeste.X, Pt1.Y, ptTeste.Z);
                }
                var (intA2, intB2) = ComandoLancamentoAutomatico.CalcularProjecao45Graus(Pt1, ptTeste, ZPreview);
                GerenciadorPreview.Server.SetRotas(new XYZ(Pt1.X, Pt1.Y, ZPreview), intA2, intB2, new XYZ(ptTeste.X, ptTeste.Y, ZPreview));
                GerenciadorPreview.Server.SetRotaAtiva(1);
                _uiapp.ActiveUIDocument.RefreshActiveView();
                Pt2_Temp = ptTeste;
                IntA_Temp = intA2;
                IntB_Temp = intB2;
            }
        }
        else if (EtapaAtual == Etapas.Vaso_Ponto3)
        {
            double distRecuo2 = 200.0 / 381.0;
            double minTrechoReto2 = 0.39370078740157477;
            bool viavelA2 = ComandoLancamentoAutomatico.RotaVasoViavel(Pt1, IntA, Pt2, distRecuo2, minTrechoReto2);
            bool viavelB2 = ComandoLancamentoAutomatico.RotaVasoViavel(Pt1, IntB, Pt2, distRecuo2, minTrechoReto2);
            int rotaNova2 = 1;
            if (!viavelA2 && !viavelB2)
            {
                rotaNova2 = -1;
            }
            else
            {
                double dVerde = Math.Min(ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, Pt1, IntA), ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, IntA, Pt2));
                double dLaranja = Math.Min(ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, Pt1, IntB), ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, IntB, Pt2));
                rotaNova2 = ((dVerde <= dLaranja) ? 1 : 2);
                if (rotaNova2 == 1 && !viavelA2)
                {
                    rotaNova2 = 2;
                }
                else if (rotaNova2 == 2 && !viavelB2)
                {
                    rotaNova2 = 1;
                }
            }
            if (GerenciadorPreview.Server.RotaAtiva != rotaNova2)
            {
                GerenciadorPreview.Server.SetRotaAtiva(rotaNova2);
                RotaEscolhida = rotaNova2;
                _uiapp.ActiveUIDocument.RefreshActiveView();
            }
        }
        else if (EtapaAtual == Etapas.Pia_Destino || EtapaAtual == Etapas.Maquina_Destino)
        {
            XYZ ptTeste2 = ptModelo;
            int destinoAtivo = ((EtapaAtual == Etapas.Pia_Destino) ? Cfg.DestinoPia : Cfg.DestinoMaquina);
            if (destinoAtivo == 1 || destinoAtivo == 2)
            {
                Pipe tubo2 = ComandoLancamentoAutomatico.EncontrarTuboProximo(_uiapp.ActiveUIDocument.Document, ptModelo, 1.0, vertical: true);
                if (tubo2 != null)
                {
                    Curve c2 = (tubo2.Location as LocationCurve).Curve;
                    XYZ ptExatoNoTubo2 = c2.Project(ptModelo).XYZPoint;
                    ptTeste2 = new XYZ(ptExatoNoTubo2.X, ptExatoNoTubo2.Y, ZPreview);
                    TuboDestino = tubo2;
                }
                else
                {
                    TuboDestino = null;
                }
            }
            RotasPia_Temp = new List<List<XYZ>>
            {
                new List<XYZ>
                {
                    new XYZ(Pt1.X, Pt1.Y, ZPreview),
                    ptTeste2
                }
            };
            GerenciadorPreview.Server.SetRotasLivres(RotasPia_Temp);
            GerenciadorPreview.Server.SetRotaAtiva(0);
            _uiapp.ActiveUIDocument.RefreshActiveView();
            Pt2_Temp = ptTeste2;
        }
        else if (EtapaAtual == Etapas.Pia_EscolherRota || EtapaAtual == Etapas.Maquina_EscolherRota)
        {
            if (RotasPia == null || RotasPia.Count <= 0)
            {
                return;
            }
            int melhorRota = 0;
            double menorDist = double.MaxValue;
            for (int i = 0; i < RotasPia.Count; i++)
            {
                List<XYZ> rota = RotasPia[i];
                double minDistSeg = double.MaxValue;
                for (int j = 0; j < rota.Count - 1; j++)
                {
                    double d = ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, rota[j], rota[j + 1]);
                    if (d < minDistSeg)
                    {
                        minDistSeg = d;
                    }
                }
                if (i == 0 && minDistSeg < 0.3)
                {
                    minDistSeg -= 0.05;
                }
                if (minDistSeg < menorDist)
                {
                    menorDist = minDistSeg;
                    melhorRota = i;
                }
            }
            if (GerenciadorPreview.Server.RotaAtiva != melhorRota)
            {
                GerenciadorPreview.Server.SetRotaAtiva(melhorRota);
                RotaEscolhida = melhorRota;
                _uiapp.ActiveUIDocument.RefreshActiveView();
            }
        }
        else if (EtapaAtual == Etapas.Caixa_Destino)
        {
            XYZ ptTeste3 = ptModelo;
            if (Cfg.DestinoCaixa == 1 || Cfg.DestinoCaixa == 2 || Cfg.DestinoCaixa == 3)
            {
                bool querVertical = Cfg.DestinoCaixa == 1 || Cfg.DestinoCaixa == 2;
                Pipe tubo3 = ComandoLancamentoAutomatico.EncontrarTuboProximo(_uiapp.ActiveUIDocument.Document, ptModelo, 1.0, querVertical);
                if (tubo3 != null)
                {
                    Curve c3 = (tubo3.Location as LocationCurve).Curve;
                    XYZ ptExato = c3.Project(ptModelo).XYZPoint;
                    ptTeste3 = new XYZ(ptExato.X, ptExato.Y, ZPreview);
                    TuboDestino = tubo3;
                }
                else
                {
                    TuboDestino = null;
                }
            }
            XYZ ptOrigem = new XYZ(Pt1.X, Pt1.Y, ZPreview);
            XYZ dirConector = DirCaixa;
            List<XYZ> dirsOrigem = new List<XYZ>();
            if (dirConector == null || dirConector.IsZeroLength() || Math.Abs(dirConector.Z) > 0.9)
            {
                dirsOrigem.Add(new XYZ(1.0, 0.0, 0.0));
                dirsOrigem.Add(new XYZ(-1.0, 0.0, 0.0));
                dirsOrigem.Add(new XYZ(0.0, 1.0, 0.0));
                dirsOrigem.Add(new XYZ(0.0, -1.0, 0.0));
            }
            else
            {
                dirsOrigem.Add(new XYZ(dirConector.X, dirConector.Y, 0.0).Normalize());
            }
            List<List<XYZ>> tempRoutes = ComandoLancamentoAutomatico.CalcularRotasRosaDosVentos(ptOrigem, ptTeste3, ZPreview, dirsOrigem, isCaixaSifonada: true);
            if (tempRoutes.Count > 0)
            {
                RotasPia_Temp = new List<List<XYZ>> { tempRoutes[0] };
            }
            else
            {
                RotasPia_Temp = new List<List<XYZ>>
                {
                    new List<XYZ> { ptOrigem, ptTeste3 }
                };
            }
            GerenciadorPreview.Server.SetRotasLivres(RotasPia_Temp);
            GerenciadorPreview.Server.SetRotaAtiva(0);
            _uiapp.ActiveUIDocument.RefreshActiveView();
            Pt2_Temp = ptTeste3;
        }
        else if (EtapaAtual == Etapas.Caixa_EscolherRota)
        {
            if (RotasPia == null || RotasPia.Count == 0)
            {
                return;
            }
            int melhorRota2 = 0;
            double menorDist2 = double.MaxValue;
            for (int i2 = 0; i2 < RotasPia.Count; i2++)
            {
                List<XYZ> rota2 = RotasPia[i2];
                double minDistSeg2 = double.MaxValue;
                for (int j2 = 0; j2 < rota2.Count - 1; j2++)
                {
                    double d2 = ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, rota2[j2], rota2[j2 + 1]);
                    if (d2 < minDistSeg2)
                    {
                        minDistSeg2 = d2;
                    }
                }
                if (i2 == 0 && minDistSeg2 < 0.3)
                {
                    minDistSeg2 -= 0.05;
                }
                if (minDistSeg2 < menorDist2)
                {
                    menorDist2 = minDistSeg2;
                    melhorRota2 = i2;
                }
            }
            if (GerenciadorPreview.Server.RotaAtiva != melhorRota2)
            {
                GerenciadorPreview.Server.SetRotaAtiva(melhorRota2);
                RotaEscolhida = melhorRota2;
                _uiapp.ActiveUIDocument.RefreshActiveView();
            }
        }
        else if (EtapaAtual == Etapas.CaixaSifonada)
        {
            Pipe tuboMain = null;
            double minDist = double.MaxValue;
            if (_candidatosTuboPrincipal != null)
            {
                foreach (Pipe t in _candidatosTuboPrincipal)
                {
                    Curve c4 = (t.Location as LocationCurve).Curve;
                    double dist = ComandoLancamentoAutomatico.DistanciaPontoSegmento2D(ptModelo, c4.GetEndPoint(0), c4.GetEndPoint(1));
                    if (dist < minDist && dist < 15.0)
                    {
                        minDist = dist;
                        tuboMain = t;
                    }
                }
            }
            if (tuboMain == null)
            {
                return;
            }
            Curve curvaMain = (tuboMain.Location as LocationCurve).Curve;
            XYZ pStart = curvaMain.GetEndPoint(0);
            XYZ pEnd = curvaMain.GetEndPoint(1);
            XYZ proj2D = ProjetarPontoNaLinhaInfinita2D(ptModelo, pStart, pEnd);
            XYZ pStart2D = new XYZ(pStart.X, pStart.Y, 0.0);
            XYZ pEnd2D = new XYZ(pEnd.X, pEnd.Y, 0.0);
            double distTotal2D = pStart2D.DistanceTo(pEnd2D);
            double zExato = pStart.Z;
            if (distTotal2D > 0.001)
            {
                double t2 = (proj2D - pStart2D).DotProduct(pEnd2D - pStart2D) / (distTotal2D * distTotal2D);
                zExato = pStart.Z + t2 * (pEnd.Z - pStart.Z);
            }
            XYZ ptExatoNoTubo3 = new XYZ(proj2D.X, proj2D.Y, zExato);
            XYZ ptExatoZPreview = new XYZ(ptExatoNoTubo3.X, ptExatoNoTubo3.Y, ZPreview);
            bool conectorVertical = DirCaixa == null || Math.Abs(DirCaixa.Z) > 0.9;
            XYZ ptElbow = ptExatoZPreview;
            if (conectorVertical)
            {
                (XYZ, XYZ) tuple3 = ComandoLancamentoAutomatico.CalcularProjecao45Graus(new XYZ(Pt1.X, Pt1.Y, ZPreview), ptExatoZPreview, ZPreview);
                XYZ intA3 = tuple3.Item1;
                XYZ intB3 = tuple3.Item2;
                double d3 = Pt1.DistanceTo(intA3) + intA3.DistanceTo(ptExatoZPreview);
                double d4 = Pt1.DistanceTo(intB3) + intB3.DistanceTo(ptExatoZPreview);
                ptElbow = ((d3 <= d4) ? intA3 : intB3);
            }
            else
            {
                XYZ dirCaixa2D = new XYZ(DirCaixa.X, DirCaixa.Y, 0.0).Normalize();
                XYZ dirMain = new XYZ(pEnd.X - pStart.X, pEnd.Y - pStart.Y, 0.0).Normalize();
                XYZ intRetas = IntersecaoReta(new XYZ(Pt1.X, Pt1.Y, 0.0), dirCaixa2D, pStart2D, dirMain);
                bool isStraight = false;
                double angSaida = dirCaixa2D.AngleTo(dirMain) * 180.0 / Math.PI;
                bool saidaPerpendicular = Math.Abs(angSaida - 90.0) <= 15.0;
                if (intRetas != null && !saidaPerpendicular && (Math.Abs(angSaida - 45.0) < 1.0 || Math.Abs(angSaida - 135.0) < 1.0 || proj2D.DistanceTo(intRetas) < 0.8) && (intRetas - new XYZ(Pt1.X, Pt1.Y, 0.0)).DotProduct(dirCaixa2D) > 0.0)
                {
                    isStraight = true;
                }
                if (isStraight)
                {
                    double zNovo = pStart.Z;
                    if (distTotal2D > 0.001)
                    {
                        double t3 = (intRetas - pStart2D).DotProduct(pEnd2D - pStart2D) / (distTotal2D * distTotal2D);
                        zNovo = pStart.Z + t3 * (pEnd.Z - pStart.Z);
                    }
                    ptExatoNoTubo3 = new XYZ(intRetas.X, intRetas.Y, zNovo);
                    ptExatoZPreview = new XYZ(intRetas.X, intRetas.Y, ZPreview);
                    ptElbow = ptExatoZPreview;
                }
                else
                {
                    if (saidaPerpendicular && intRetas != null)
                    {
                        XYZ ptCx2D = new XYZ(Pt1.X, Pt1.Y, 0.0);
                        XYZ cruz2D = new XYZ(intRetas.X, intRetas.Y, 0.0);
                        double tCruz = (cruz2D - ptCx2D).DotProduct(dirCaixa2D);
                        if (tCruz > 0.5)
                        {
                            double sMouse = (new XYZ(proj2D.X, proj2D.Y, 0.0) - cruz2D).DotProduct(dirMain);
                            double sSign = ((!(Math.Abs(pEnd.Z - pStart.Z) > 0.001)) ? ((sMouse >= 0.0) ? 1.0 : (-1.0)) : ((pEnd.Z < pStart.Z) ? 1.0 : (-1.0)));
                            double sAbs = Math.Max(0.25, Math.Min(Math.Abs(sMouse), tCruz - 0.25));
                            XYZ projClamped = cruz2D + dirMain * (sAbs * sSign);
                            double tCol = (projClamped - pStart2D).DotProduct(dirMain);
                            double zJ = pStart.Z;
                            if (distTotal2D > 0.001)
                            {
                                zJ = pStart.Z + tCol / distTotal2D * (pEnd.Z - pStart.Z);
                            }
                            ptExatoNoTubo3 = new XYZ(projClamped.X, projClamped.Y, zJ);
                            ptExatoZPreview = new XYZ(projClamped.X, projClamped.Y, ZPreview);
                        }
                    }
                    XYZ invMain = -dirMain;
                    double cos45 = Math.Cos(Math.PI / 4.0);
                    double sin45 = Math.Sin(Math.PI / 4.0);
                    XYZ v45_A = new XYZ(invMain.X * cos45 - invMain.Y * sin45, invMain.X * sin45 + invMain.Y * cos45, 0.0).Normalize();
                    XYZ v45_B = new XYZ(invMain.X * cos45 - invMain.Y * (0.0 - sin45), invMain.X * (0.0 - sin45) + invMain.Y * cos45, 0.0).Normalize();
                    XYZ intA4 = IntersecaoReta(Pt1, dirCaixa2D, ptExatoZPreview, v45_A);
                    XYZ intB4 = IntersecaoReta(Pt1, dirCaixa2D, ptExatoZPreview, v45_B);
                    double tA = ((intA4 != null) ? (intA4 - Pt1).DotProduct(dirCaixa2D) : (-1.0));
                    double tB = ((intB4 != null) ? (intB4 - Pt1).DotProduct(dirCaixa2D) : (-1.0));
                    ptElbow = ((tA > 0.0 && tB > 0.0) ? ((tA < tB) ? intA4 : intB4) : ((tA > 0.0) ? intA4 : ((!(tB > 0.0)) ? ptExatoZPreview : intB4)));
                    ptElbow = new XYZ(ptElbow.X, ptElbow.Y, ZPreview);
                }
            }
            XYZ Pt1_2D = new XYZ(Pt1.X, Pt1.Y, ZPreview);
            XYZ vIn_prev = (ptElbow - Pt1_2D).Normalize();
            XYZ vOut_prev = (ptExatoZPreview - ptElbow).Normalize();
            double anguloCorner_prev = vIn_prev.AngleTo(vOut_prev);
            if (Math.Abs(anguloCorner_prev - Math.PI / 2.0) < 0.05 && Pt1_2D.DistanceTo(ptElbow) > 0.1 && ptElbow.DistanceTo(ptExatoZPreview) > 0.1)
            {
                double d1_prev = Pt1_2D.DistanceTo(ptElbow);
                double d2_prev = ptElbow.DistanceTo(ptExatoZPreview);
                double dChanfro_prev = (d1_prev + d2_prev) / (2.0 + 2.0 * Math.Sqrt(2.0));
                dChanfro_prev = Math.Min(dChanfro_prev, Math.Min(d1_prev * 0.45, d2_prev * 0.45));
                XYZ p2a = ptElbow - vIn_prev * dChanfro_prev;
                XYZ p2b = ptElbow + vOut_prev * dChanfro_prev;
                GerenciadorPreview.Server.SetRotaLivre(new List<XYZ> { Pt1, p2a, p2b, ptExatoZPreview });
            }
            else
            {
                GerenciadorPreview.Server.SetRotas(Pt1, ptElbow, ptElbow, ptExatoZPreview);
                GerenciadorPreview.Server.SetRotaAtiva(1);
            }
            Pt2 = ptExatoNoTubo3;
            IntA = ptElbow;
            IntB = ptElbow;
            TuboDestino = tuboMain;
            RotaEscolhida = 1;
            _uiapp.ActiveUIDocument.RefreshActiveView();
        }
        else if (EtapaAtual == Etapas.Lavatorio_Caixa)
        {
            XYZ target2D = new XYZ(ptModelo.X, ptModelo.Y, ZPreview);
            FamilyInstance caixaAlvo = null;
            double minCaixa = double.MaxValue;
            if (_candidatosCaixas != null)
            {
                foreach (FamilyInstance c5 in _candidatosCaixas)
                {
                    XYZ pBox = (c5.Location as LocationPoint)?.Point ?? XYZ.Zero;
                    double d5 = new XYZ(ptModelo.X, ptModelo.Y, 0.0).DistanceTo(new XYZ(pBox.X, pBox.Y, 0.0));
                    if (d5 < minCaixa && d5 < 4.0)
                    {
                        minCaixa = d5;
                        caixaAlvo = c5;
                    }
                }
            }
            XYZ dirConectorCaixa = null;
            if (caixaAlvo != null)
            {
                Connector conn = FinalizarLancamentoHandler.ObterConectorLivreMaisProximo(caixaAlvo, ptModelo);
                if (conn != null)
                {
                    target2D = new XYZ(conn.Origin.X, conn.Origin.Y, ZPreview);
                    dirConectorCaixa = conn.CoordinateSystem.BasisZ;
                }
            }
            List<XYZ> rota3 = ComandoLancamentoAutomatico.ResolverRotaLavatorio(PtParedeLavatorio, DirParedeLavatorio, target2D, dirConectorCaixa);
            if (rota3 != null)
            {
                GerenciadorPreview.Server.SetRotaLivre(rota3);
                Pt2 = target2D;
                _uiapp.ActiveUIDocument.RefreshActiveView();
            }
        }
        else if (EtapaAtual == Etapas.Chuveiro_Caixa)
        {
            XYZ target2D2 = new XYZ(ptModelo.X, ptModelo.Y, ZPreview);
            FamilyInstance caixaAlvo2 = null;
            double minCaixa2 = double.MaxValue;
            if (_candidatosCaixas != null)
            {
                foreach (FamilyInstance c6 in _candidatosCaixas)
                {
                    XYZ pBox2 = (c6.Location as LocationPoint)?.Point ?? XYZ.Zero;
                    double d6 = new XYZ(ptModelo.X, ptModelo.Y, 0.0).DistanceTo(new XYZ(pBox2.X, pBox2.Y, 0.0));
                    if (d6 < minCaixa2 && d6 < 4.0)
                    {
                        minCaixa2 = d6;
                        caixaAlvo2 = c6;
                    }
                }
            }
            XYZ dirConectorCaixa2 = null;
            if (caixaAlvo2 != null)
            {
                Connector conn2 = FinalizarLancamentoHandler.ObterConectorLivreMaisProximo(caixaAlvo2, ptModelo);
                if (conn2 != null)
                {
                    target2D2 = new XYZ(conn2.Origin.X, conn2.Origin.Y, ZPreview);
                    dirConectorCaixa2 = conn2.CoordinateSystem.BasisZ;
                }
            }
            List<XYZ> rota4 = ComandoLancamentoAutomatico.ResolverRotaChuveiro(PtSaidaChuveiro, DirSaidaChuveiro, target2D2, dirConectorCaixa2);
            if (rota4 != null)
            {
                GerenciadorPreview.Server.SetRotaLivre(rota4);
                Pt2 = target2D2;
                _uiapp.ActiveUIDocument.RefreshActiveView();
            }
        }
        else if (EtapaAtual == Etapas.Ventilacao_EscolherDirecaoCavalete)
        {
            Pt2 = ptModelo;
            double dx = Pt2.X - Pt1.X;
            double dy = Pt2.Y - Pt1.Y;
            double angle = Math.Atan2(dy, dx);
            double snappedAngle = Math.Round(angle / (Math.PI / 8.0)) * (Math.PI / 8.0);
            double cosA = Math.Cos(snappedAngle);
            double sinA = Math.Sin(snappedAngle);
            if (Math.Abs(cosA) < 0.001)
            {
                cosA = 0.0;
            }
            if (Math.Abs(sinA) < 0.001)
            {
                sinA = 0.0;
            }
            if (Math.Abs(Math.Abs(cosA) - 1.0) < 0.001)
            {
                cosA = Math.Sign(cosA);
            }
            if (Math.Abs(Math.Abs(sinA) - 1.0) < 0.001)
            {
                sinA = Math.Sign(sinA);
            }
            XYZ dirCaixa = new XYZ(cosA, sinA, 0.0).Normalize();
            double offsetPrev = 0.377296;
            if (TuboDestino != null)
            {
                double diamPrumada = ((Element)TuboDestino).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? (125.0 / 381.0);
                offsetPrev = 0.23786089238845143 + diamPrumada / 2.0;
                Curve cTub = (TuboDestino.Location as LocationCurve)?.Curve;
                if (cTub != null && Math.Abs((cTub.GetEndPoint(1) - cTub.GetEndPoint(0)).Normalize().Z) < 0.5)
                {
                    offsetPrev = 0.344488;
                }
            }
            XYZ posCavalete = Pt1 + dirCaixa * offsetPrev;
            GerenciadorPreview.Server.SetCavaletePreview(Pt1, posCavalete);
            _uiapp.ActiveUIDocument.RefreshActiveView();
        }
        else
        {
            if (EtapaAtual != Etapas.Ventilacao_LigarRamal && EtapaAtual != Etapas.Ventilacao_EscolherRota)
            {
                return;
            }
            XYZ target2D3 = ((EtapaAtual == Etapas.Ventilacao_LigarRamal) ? new XYZ(ptModelo.X, ptModelo.Y, ZPreview) : new XYZ(PtClickFinal.X, PtClickFinal.Y, ZPreview));
            XYZ dirPipe = new XYZ(1.0, 0.0, 0.0);
            Pipe tuboPreview = ComandoLancamentoAutomatico.EncontrarTuboProximo(_uiapp.ActiveUIDocument.Document, target2D3, 1.0, vertical: true);
            if (tuboPreview != null)
            {
                Curve cTub2 = (tuboPreview.Location as LocationCurve)?.Curve;
                if (cTub2 != null)
                {
                    dirPipe = (cTub2.GetEndPoint(1) - cTub2.GetEndPoint(0)).Normalize();
                }
            }
            XYZ dirPipeXY = new XYZ(dirPipe.X, dirPipe.Y, 0.0);
            dirPipe = ((!dirPipeXY.IsAlmostEqualTo(XYZ.Zero)) ? dirPipeXY.Normalize() : new XYZ(1.0, 0.0, 0.0));
            List<XYZ> rotaPreview = new List<XYZ>();
            XYZ ptStubFim2D;
            if (Cfg.RotacaoTe90)
            {
                XYZ dirBranch2D = new XYZ(0.0, 0.0, 1.0);
                double length = 0.393701;
                ptStubFim2D = target2D3 + dirBranch2D * length;
                rotaPreview = new List<XYZ> { target2D3, ptStubFim2D, Pt1 };
            }
            else
            {
                XYZ vecCV = new XYZ(Pt1.X - target2D3.X, Pt1.Y - target2D3.Y, 0.0);
                XYZ vecMouse = new XYZ(ptModelo.X - target2D3.X, ptModelo.Y - target2D3.Y, 0.0);
                XYZ perpPipe = new XYZ(0.0 - dirPipe.Y, dirPipe.X, 0.0).Normalize();
                if (vecMouse.DotProduct(perpPipe) < 0.0)
                {
                    perpPipe = -perpPipe;
                }
                double distPerp = vecCV.DotProduct(perpPipe);
                double lReq = distPerp * Math.Sqrt(2.0);
                double length2 = 0.328084;
                XYZ dirBranch2D2 = perpPipe;
                if (Cfg.Joelho45NoChicote)
                {
                    length2 = 0.393701;
                    double signDirPrev = Math.Sign(vecCV.DotProduct(dirPipe));
                    if (signDirPrev == 0.0)
                    {
                        signDirPrev = 1.0;
                    }
                    dirBranch2D2 = new XYZ((signDirPrev * dirPipe.X + perpPipe.X) * 0.707106, (signDirPrev * dirPipe.Y + perpPipe.Y) * 0.707106, 0.0).Normalize();
                }
                else if (distPerp > 0.0 && lReq <= 3.28)
                {
                    length2 = Math.Max(lReq, 0.164042);
                }
                else if (distPerp <= 0.0)
                {
                    length2 = 0.492126;
                }
                ptStubFim2D = target2D3 + dirBranch2D2 * length2;
                XYZ ptFimChicote2D = ptStubFim2D;
                List<XYZ> baseRota = new List<XYZ> { target2D3, ptStubFim2D };
                XYZ vecToBase = new XYZ(Pt1.X - ptFimChicote2D.X, Pt1.Y - ptFimChicote2D.Y, 0.0);
                XYZ dirParallel = ((dirPipe.DotProduct(vecToBase) > 0.0) ? dirPipe : (-dirPipe));
                double distParallel = vecToBase.DotProduct(dirParallel);
                XYZ ptCorner2D = ptFimChicote2D + dirParallel * distParallel;
                rotaPreview = new List<XYZ>(baseRota);
                rotaPreview.Add(ptCorner2D);
                rotaPreview.Add(Pt1);
            }
            if (EtapaAtual == Etapas.Ventilacao_EscolherRota)
            {
                XYZ pRef = ptStubFim2D;
                double dx2 = Math.Abs(Pt1.X - pRef.X);
                double dy2 = Math.Abs(Pt1.Y - pRef.Y);
                bool isOpcaoV0Te90 = Cfg.OpcaoVentilacao == 0 && Cfg.RotacaoTe90;
                double limiteUnico = (isOpcaoV0Te90 ? 0.29527 : 0.05);
                if (dx2 >= limiteUnico && dy2 >= limiteUnico)
                {
                    XYZ pCorner1 = new XYZ(pRef.X, Pt1.Y, pRef.Z);
                    XYZ pCorner2 = new XYZ(Pt1.X, pRef.Y, pRef.Z);
                    double minD = Math.Min(dx2, dy2);
                    if ((!Cfg.RotacaoTe90 && Cfg.Joelho45NoChicote) || (isOpcaoV0Te90 && minD >= 0.29527 && minD < 0.45931))
                    {
                        XYZ pDir = new XYZ(0.0 - dirPipe.Y, dirPipe.X, 0.0).Normalize();
                        if (new XYZ(Pt1.X - target2D3.X, Pt1.Y - target2D3.Y, 0.0).DotProduct(pDir) < 0.0)
                        {
                            pDir = -pDir;
                        }
                        List<XYZ> validCorners = new List<XYZ>();
                        XYZ D1 = new XYZ(pDir.X * 0.707106781 - pDir.Y * 0.707106781, pDir.X * 0.707106781 + pDir.Y * 0.707106781, 0.0);
                        XYZ D2 = new XYZ(pDir.X * 0.707106781 + pDir.Y * 0.707106781, (0.0 - pDir.X) * 0.707106781 + pDir.Y * 0.707106781, 0.0);
                        XYZ delta = new XYZ(Pt1.X - pRef.X, Pt1.Y - pRef.Y, 0.0);
                        double cross1 = pDir.X * D1.Y - pDir.Y * D1.X;
                        if (Math.Abs(cross1) > 0.001)
                        {
                            double t4 = (delta.X * D1.Y - delta.Y * D1.X) / cross1;
                            double u1 = (pDir.X * delta.Y - pDir.Y * delta.X) / cross1;
                            if (t4 >= 0.05 && u1 >= 0.05)
                            {
                                validCorners.Add(pRef + pDir * t4);
                            }
                        }
                        double cross2 = pDir.X * D2.Y - pDir.Y * D2.X;
                        if (Math.Abs(cross2) > 0.001)
                        {
                            double t5 = (delta.X * D2.Y - delta.Y * D2.X) / cross2;
                            double u2 = (pDir.X * delta.Y - pDir.Y * delta.X) / cross2;
                            if (t5 >= 0.05 && u2 >= 0.05)
                            {
                                validCorners.Add(pRef + pDir * t5);
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
                                fallbackCorners.Add(pRef + D1 * offset_d1);
                            }
                        }
                        double crossD2_pDir = D2.X * pDir.Y - D2.Y * pDir.X;
                        if (Math.Abs(crossD2_pDir) > 0.001)
                        {
                            double offset_d3 = (delta.X * pDir.Y - delta.Y * pDir.X) / crossD2_pDir;
                            double offset_d4 = (D2.X * delta.Y - D2.Y * delta.X) / crossD2_pDir;
                            if (offset_d3 >= 0.05 && offset_d4 >= 0.05)
                            {
                                fallbackCorners.Add(pRef + D2 * offset_d3);
                            }
                        }
                        validCorners.AddRange(fallbackCorners);
                        if (validCorners.Count > 0)
                        {
                            pCorner1 = validCorners[0];
                            pCorner2 = ((validCorners.Count > 1) ? validCorners[1] : validCorners[0]);
                        }
                        else
                        {
                            double proj = new XYZ(Pt1.X - pRef.X, Pt1.Y - pRef.Y, 0.0).DotProduct(pDir);
                            if (proj < 0.05)
                            {
                                proj = 0.05;
                            }
                            pCorner1 = pRef + pDir * proj;
                            pCorner2 = pCorner1;
                        }
                    }
                    List<XYZ> baseRota2 = new List<XYZ> { target2D3, ptStubFim2D };
                    if (!Cfg.RotacaoTe90 && Cfg.Joelho45NoChicote)
                    {
                        baseRota2.Add(pRef);
                    }
                    List<XYZ> r0 = new List<XYZ>(baseRota2) { Pt1 };
                    List<XYZ> r1 = new List<XYZ>(baseRota2) { pCorner1, Pt1 };
                    List<XYZ> r2 = new List<XYZ>(baseRota2) { pCorner2, Pt1 };
                    XYZ pMid = new XYZ((pRef.X + Pt1.X) / 2.0, (pRef.Y + Pt1.Y) / 2.0, pRef.Z);
                    double d7 = ptModelo.DistanceTo(pMid);
                    double d8 = ptModelo.DistanceTo(pCorner1);
                    double d9 = ptModelo.DistanceTo(pCorner2);
                    int rotaNova3 = 0;
                    if (d8 <= d7 && d8 <= d9)
                    {
                        rotaNova3 = 1;
                    }
                    else if (d9 <= d7 && d9 < d8)
                    {
                        rotaNova3 = 2;
                    }
                    if (!Cfg.RotacaoTe90 && Cfg.Joelho45NoChicote && rotaNova3 == 0)
                    {
                        rotaNova3 = 1;
                    }
                    List<List<XYZ>> todasAsRotas = new List<List<XYZ>> { r0, r1, r2 };
                    GerenciadorPreview.Server.SetRotasLivres(todasAsRotas);
                    GerenciadorPreview.Server.SetRotaAtiva(rotaNova3);
                    RotaEscolhida = rotaNova3;
                }
                else
                {
                    GerenciadorPreview.Server.SetRotasLivres(new List<List<XYZ>> { rotaPreview });
                    GerenciadorPreview.Server.SetRotaAtiva(0);
                    RotaEscolhida = 0;
                }
            }
            else
            {
                GerenciadorPreview.Server.SetRotasLivres(new List<List<XYZ>> { rotaPreview });
                GerenciadorPreview.Server.SetRotaAtiva(0);
            }
            Pt2 = ptModelo;
            _uiapp.ActiveUIDocument.RefreshActiveView();
        }
    }

    private static nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == 513 && _jigAtivo)
            {
                _bloquearProximoUp = true;
                if (EtapaAtual == Etapas.Vaso_Parede)
                {
                    XYZ ptParede = Pt2_Temp ?? PtAlinhamentoVaso;
                    XYZ dirEntrada = DirPreviewParede ?? new XYZ(1.0, 0.0, 0.0);
                    XYZ ptVasoFinal;
                    if (Math.Abs(dirEntrada.X) > 0.5)
                    {
                        double xVaso = ptParede.X - dirEntrada.X * 0.9842519685039369;
                        ptVasoFinal = new XYZ(xVaso, PtAlinhamentoVaso.Y, PtAlinhamentoVaso.Z);
                    }
                    else
                    {
                        double yVaso = ptParede.Y - dirEntrada.Y * 0.9842519685039369;
                        ptVasoFinal = new XYZ(PtAlinhamentoVaso.X, yVaso, PtAlinhamentoVaso.Z);
                    }
                    Pt1 = ptVasoFinal;
                    Pt2_Temp = null;
                    IntA_Temp = null;
                    IntB_Temp = null;
                    EtapaAtual = Etapas.Vaso_Ponto2;
                    return 1;
                }
                if (EtapaAtual == Etapas.Vaso_Ponto2)
                {
                    if ((Cfg.DestinoVaso == 1 || Cfg.DestinoVaso == 2) && TuboDestino == null)
                    {
                        return 1;
                    }
                    if (RotaEscolhida == -1)
                    {
                        return 1;
                    }
                    Pt2 = Pt2_Temp ?? Pt1;
                    IntA = IntA_Temp ?? Pt1;
                    IntB = IntB_Temp ?? Pt1;
                    EtapaAtual = Etapas.Vaso_Ponto3;
                    if (RotaEscolhida != 1 && RotaEscolhida != 2)
                    {
                        RotaEscolhida = 1;
                    }
                    GerenciadorPreview.Server.SetRotaAtiva(RotaEscolhida);
                    return 1;
                }
                if (EtapaAtual == Etapas.Vaso_Ponto3)
                {
                    if (RotaEscolhida == -1)
                    {
                        return 1;
                    }
                    _jigAtivo = false;
                    _exEvent.Raise();
                    return 1;
                }
                if (EtapaAtual == Etapas.Pia_Destino || EtapaAtual == Etapas.Maquina_Destino)
                {
                    int destinoAtivo = ((EtapaAtual == Etapas.Pia_Destino) ? Cfg.DestinoPia : Cfg.DestinoMaquina);
                    if ((destinoAtivo == 1 || destinoAtivo == 2) && TuboDestino == null)
                    {
                        return 1;
                    }
                    Pt2 = Pt2_Temp ?? Pt1;
                    XYZ dirFace = ((EtapaAtual == Etapas.Pia_Destino) ? DirParedePia : DirParedeMaquina);
                    bool regraDistanciaPorDiametro = EtapaAtual == Etapas.Maquina_Destino && Cfg.DestinoMaquina == 2;
                    List<XYZ> dirsOrigem = null;
                    if (dirFace != null)
                    {
                        XYZ dirPerp = new XYZ(dirFace.X, dirFace.Y, 0.0).Normalize();
                        dirsOrigem = new List<XYZ> { dirPerp };
                        dirsOrigem.Add(new XYZ(dirPerp.X - dirPerp.Y, dirPerp.X + dirPerp.Y, 0.0).Normalize());
                        dirsOrigem.Add(new XYZ(dirPerp.X + dirPerp.Y, 0.0 - dirPerp.X + dirPerp.Y, 0.0).Normalize());
                        if (regraDistanciaPorDiametro)
                        {
                            dirsOrigem.Add(new XYZ(0.0 - dirPerp.Y, dirPerp.X, 0.0));
                            dirsOrigem.Add(new XYZ(dirPerp.Y, 0.0 - dirPerp.X, 0.0));
                        }
                    }
                    double diamAparelho = ((EtapaAtual == Etapas.Pia_Destino) ? Cfg.DiametroLavatorio : Cfg.DiametroMaquina);
                    RotasPia = ComandoLancamentoAutomatico.CalcularRotasRosaDosVentos(Pt1, Pt2, ZPreview, dirsOrigem, isCaixaSifonada: false, diamAparelho, regraDistanciaPorDiametro);
                    EtapaAtual = ((EtapaAtual == Etapas.Pia_Destino) ? Etapas.Pia_EscolherRota : Etapas.Maquina_EscolherRota);
                    GerenciadorPreview.Server.SetRotasLivres(RotasPia);
                    GerenciadorPreview.Server.SetRotaAtiva(0);
                    RotaEscolhida = 0;
                    return 1;
                }
                if (EtapaAtual == Etapas.Pia_EscolherRota || EtapaAtual == Etapas.Maquina_EscolherRota)
                {
                    _jigAtivo = false;
                    _exEvent.Raise();
                    return 1;
                }
                if (EtapaAtual == Etapas.Caixa_Destino)
                {
                    if ((Cfg.DestinoCaixa == 1 || Cfg.DestinoCaixa == 2 || Cfg.DestinoCaixa == 3) && TuboDestino == null)
                    {
                        return 1;
                    }
                    Pt2 = Pt2_Temp ?? Pt1;
                    XYZ dirConector = DirCaixa;
                    List<XYZ> dirsOrigem2 = new List<XYZ>();
                    if (dirConector == null || dirConector.IsZeroLength() || Math.Abs(dirConector.Z) > 0.9)
                    {
                        dirsOrigem2.Add(new XYZ(1.0, 0.0, 0.0));
                        dirsOrigem2.Add(new XYZ(-1.0, 0.0, 0.0));
                        dirsOrigem2.Add(new XYZ(0.0, 1.0, 0.0));
                        dirsOrigem2.Add(new XYZ(0.0, -1.0, 0.0));
                    }
                    else
                    {
                        dirConector = new XYZ(dirConector.X, dirConector.Y, 0.0).Normalize();
                        dirsOrigem2.Add(dirConector);
                    }
                    RotasPia = ComandoLancamentoAutomatico.CalcularRotasRosaDosVentos(new XYZ(Pt1.X, Pt1.Y, ZPreview), Pt2, ZPreview, dirsOrigem2, isCaixaSifonada: true);
                    EtapaAtual = Etapas.Caixa_EscolherRota;
                    GerenciadorPreview.Server.SetRotasLivres(RotasPia);
                    GerenciadorPreview.Server.SetRotaAtiva(0);
                    RotaEscolhida = 0;
                    return 1;
                }
                if (EtapaAtual == Etapas.Ventilacao_EscolherDirecaoCavalete)
                {
                    CentroPrumada = Pt1;
                    double dx = Pt2.X - Pt1.X;
                    double dy = Pt2.Y - Pt1.Y;
                    double angle = Math.Atan2(dy, dx);
                    double snappedAngle = Math.Round(angle / (Math.PI / 8.0)) * (Math.PI / 8.0);
                    double cosA = Math.Cos(snappedAngle);
                    double sinA = Math.Sin(snappedAngle);
                    if (Math.Abs(cosA) < 0.001)
                    {
                        cosA = 0.0;
                    }
                    if (Math.Abs(sinA) < 0.001)
                    {
                        sinA = 0.0;
                    }
                    if (Math.Abs(Math.Abs(cosA) - 1.0) < 0.001)
                    {
                        cosA = Math.Sign(cosA);
                    }
                    if (Math.Abs(Math.Abs(sinA) - 1.0) < 0.001)
                    {
                        sinA = Math.Sign(sinA);
                    }
                    DirCaixa = new XYZ(cosA, sinA, 0.0).Normalize();
                    double diamPrumada = 125.0 / 762.0;
                    if (TuboDestino != null)
                    {
                        diamPrumada = ((Element)TuboDestino).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble();
                    }
                    double offsetClick = 0.23786089238845143 + diamPrumada / 2.0;
                    Pt1 += DirCaixa * offsetClick;
                    EtapaAtual = Etapas.Ventilacao_LigarRamal;
                    return 1;
                }
                if (EtapaAtual == Etapas.Ventilacao_LigarRamal)
                {
                    PtClickFinal = Pt2;
                    EtapaAtual = Etapas.Ventilacao_EscolherRota;
                    return 1;
                }
                if (EtapaAtual == Etapas.Ventilacao_EscolherRota)
                {
                    PtMouseRota = Pt2;
                    _jigAtivo = false;
                    _exEvent.Raise();
                    return 1;
                }
                if (EtapaAtual == Etapas.CaixaSifonada)
                {
                    if (TuboDestino == null)
                    {
                        return 1;
                    }
                    if (IntA == null)
                    {
                        IntA = Pt2;
                    }
                    if (IntB == null)
                    {
                        IntB = Pt2;
                    }
                    _jigAtivo = false;
                    _exEvent.Raise();
                    return 1;
                }
                _jigAtivo = false;
                _exEvent.Raise();
                return 1;
            }
            if (wParam == 514 && _bloquearProximoUp)
            {
                _bloquearProximoUp = false;
                return 1;
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    public static XYZ IntersecaoReta(XYZ p1, XYZ dir1, XYZ p2, XYZ dir2)
    {
        double det = dir1.X * dir2.Y - dir1.Y * dir2.X;
        if (Math.Abs(det) < 1E-06)
        {
            return null;
        }
        return p1 + dir1 * (((p2.X - p1.X) * dir2.Y - (p2.Y - p1.Y) * dir2.X) / det);
    }

    public static void DesmontarJigSeguro()
    {
        _jigAtivo = false;
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
        try
        {
            if (_uiapp != null)
            {
                _uiapp.Idling -= Uiapp_Idling;
            }
        }
        catch
        {
        }
        if (_uiapp?.ActiveUIDocument != null)
        {
            GerenciadorPreview.Server.Clear();
            _uiapp.ActiveUIDocument.RefreshActiveView();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetModuleHandle(string lpModuleName);

    private static nint SetHook(LowLevelMouseProc proc)
    {
        using Process curProcess = Process.GetCurrentProcess();
        ProcessModule curModule = curProcess.MainModule;
        try
        {
            return SetWindowsHookEx(14, proc, GetModuleHandle(curModule.ModuleName), 0u);
        }
        finally
        {
            ((IDisposable)curModule)?.Dispose();
        }
    }

    private static XYZ ProjetarPontoNaLinhaInfinita2D(XYZ pt, XYZ linhaInicio, XYZ linhaFim)
    {
        XYZ p = new XYZ(pt.X, pt.Y, 0.0);
        XYZ a = new XYZ(linhaInicio.X, linhaInicio.Y, 0.0);
        XYZ b = new XYZ(linhaFim.X, linhaFim.Y, 0.0);
        XYZ ab = b - a;
        if (ab.DotProduct(ab) < 1E-09)
        {
            return a;
        }
        return a + ab * ((p - a).DotProduct(ab) / ab.DotProduct(ab));
    }
}
