using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.ExternalService;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoLancamentoAgua : IExternalCommand
{
    public struct POINT
    {
        public int x;

        public int y;
    }

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

#pragma warning disable CS0649
    [StructLayout(LayoutKind.Sequential)]
    private struct MSGNativa
    {
        public nint hwnd;

        public uint message;

        public nint wParam;

        public nint lParam;

        public uint time;

        public POINT pt;
    }
#pragma warning restore CS0649

    private nint _hookClique = IntPtr.Zero;

    private HookProc _hookProc;

    private bool _cliquePendente;

    private POINT _ptCliqueHook;

    private int _vistaEsq;

    private int _vistaTopo;

    private int _vistaDir;

    private int _vistaFundo;

    private UIApplication _uiapp;

    private UIDocument _uidoc;

    private Document _doc;

    private RoomHoverServer _hoverServer;

    private UIView _uiview;

    private MultiServerService _ms;

    private double _zSonda;

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (App.AppCarregado && !VerificadorDeSessao.PermissaoConcedida())
        {
            return Result.Cancelled;
        }
        _uiapp = commandData.Application;
        _uidoc = _uiapp.ActiveUIDocument;
        _doc = _uidoc.Document;
        if (_doc.ActiveView.ViewType != ViewType.FloorPlan && _doc.ActiveView.ViewType != ViewType.EngineeringPlan)
        {
            TaskDialog.Show("PipeMaster", "Por favor, utilize este comando em uma vista de Planta Baixa.");
            return Result.Cancelled;
        }
        _uiview = _uidoc.GetOpenUIViews().FirstOrDefault((UIView v) => v.ViewId == _doc.ActiveView.Id);
        _zSonda = (_doc.ActiveView.GenLevel?.ProjectElevation ?? 0.0) + 3.28;
        _hoverServer = new RoomHoverServer();
        _ms = ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService) as MultiServerService;
        IList<Guid> activeIds = _ms.GetActiveServerIds();
        foreach (Guid id in activeIds)
        {
            if (id == _hoverServer.GetServerId())
            {
                _ms.RemoveServer(id);
            }
        }
        _ms.AddServer(_hoverServer);
        IList<Guid> active = _ms.GetActiveServerIds();
        active.Add(_hoverServer.GetServerId());
        _ms.SetActiveServers(active);
        AtualizarRetanguloVista();
        try
        {
            _hookProc = HookClique;
            _hookClique = SetWindowsHookEx(3, _hookProc, IntPtr.Zero, GetCurrentThreadId());
        }
        catch
        {
            _hookClique = IntPtr.Zero;
        }
        _uiapp.Idling += Uiapp_Idling;
        return Result.Succeeded;
    }

    private void AtualizarRetanguloVista()
    {
        try
        {
            Rectangle rect = _uiview.GetWindowRectangle();
            _vistaEsq = rect.Left;
            _vistaTopo = rect.Top;
            _vistaDir = rect.Right;
            _vistaFundo = rect.Bottom;
        }
        catch
        {
        }
    }

    private nint HookClique(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == 1)
        {
            try
            {
                MSGNativa msg = (MSGNativa)Marshal.PtrToStructure(lParam, typeof(MSGNativa));
                if ((msg.message == 513 || msg.message == 515 || msg.message == 514) && msg.pt.x >= _vistaEsq && msg.pt.x <= _vistaDir && msg.pt.y >= _vistaTopo && msg.pt.y <= _vistaFundo)
                {
                    if (msg.message != 514)
                    {
                        _ptCliqueHook = msg.pt;
                        _cliquePendente = true;
                    }
                    msg.message = 0u;
                    Marshal.StructureToPtr(msg, lParam, fDeleteOld: false);
                }
            }
            catch
            {
            }
        }
        return CallNextHookEx(_hookClique, nCode, wParam, lParam);
    }

    private void RemoverHookClique()
    {
        if (_hookClique != IntPtr.Zero)
        {
            try
            {
                UnhookWindowsHookEx(_hookClique);
            }
            catch
            {
            }
            _hookClique = IntPtr.Zero;
            _hookProc = null;
        }
    }

    private void Uiapp_Idling(object sender, IdlingEventArgs e)
    {
        e.SetRaiseWithoutDelay();
        if ((GetAsyncKeyState(27) & 0x8000) != 0)
        {
            EncerrarHover();
            return;
        }
        bool clicou = false;
        POINT clickPt = default(POINT);
        if (_cliquePendente)
        {
            _cliquePendente = false;
            clickPt = _ptCliqueHook;
            clicou = true;
        }
        else if ((GetAsyncKeyState(1) & 0x8000) != 0)
        {
            GetCursorPos(out clickPt);
            clicou = true;
        }
        if (clicou)
        {
            XYZ xyzClick0 = ScreenToXYZ(_uiview, clickPt);
            XYZ xyzClick1 = new XYZ(xyzClick0.X, xyzClick0.Y, _zSonda);
            PararIdling();
            _uidoc.Selection.SetElementIds(new List<ElementId>());
            try
            {
                IniciarFluxoPrincipal(xyzClick1);
                return;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro PipeMaster", ex.Message);
                return;
            }
            finally
            {
                RemoverServidor();
            }
        }
        AtualizarRetanguloVista();
        GetCursorPos(out var hoverPt);
        XYZ xyz = ScreenToXYZ(_uiview, hoverPt);
        Tuple<SpatialElement, Transform, RevitLinkInstance> amb = ObterAmbienteOuEspacoInfo(_doc, new XYZ(xyz.X, xyz.Y, _zSonda));
        if (amb.Item1 == null)
        {
            amb = ObterAmbienteOuEspacoInfo(_doc, new XYZ(xyz.X, xyz.Y, xyz.Z + 1.0));
        }
        if (amb.Item1 != null)
        {
            Mesh mesh = CriarMalhaAmbiente(amb.Item1);
            if (mesh != null)
            {
                _hoverServer.UpdateRoomMesh(mesh, amb.Item2);
            }
        }
        else
        {
            _hoverServer.Clear();
        }
        _uidoc.RefreshActiveView();
    }

    private void PararIdling()
    {
        RemoverHookClique();
        _uiapp.Idling -= Uiapp_Idling;
        if (_hoverServer != null)
        {
            _hoverServer.LimparAmbiente();
        }
        _uidoc.RefreshActiveView();
    }

    private void RemoverServidor()
    {
        if (_hoverServer == null)
        {
            return;
        }
        _hoverServer.Clear();
        _uidoc.RefreshActiveView();
        if (_ms != null)
        {
            Guid serverId = _hoverServer.GetServerId();
            if (_ms.IsRegisteredServerId(serverId))
            {
                IList<Guid> active = _ms.GetActiveServerIds();
                if (active.Contains(serverId))
                {
                    active.Remove(serverId);
                    _ms.SetActiveServers(active);
                }
                _ms.RemoveServer(serverId);
            }
        }
        _hoverServer = null;
    }

    private void EncerrarHover()
    {
        if (_hoverServer != null)
        {
            PararIdling();
            RemoverServidor();
        }
    }

    private XYZ ScreenToXYZ(UIView uiview, POINT screenPt)
    {
        Rectangle rect = uiview.GetWindowRectangle();
        IList<XYZ> corners = uiview.GetZoomCorners();
        XYZ p1 = corners[0];
        XYZ p2 = corners[1];
        double xRatio = (double)(screenPt.x - rect.Left) / (double)(rect.Right - rect.Left);
        double yRatio = (double)(rect.Bottom - screenPt.y) / (double)(rect.Bottom - rect.Top);
        double x = p1.X + (p2.X - p1.X) * xRatio;
        double y = p1.Y + (p2.Y - p1.Y) * yRatio;
        return new XYZ(x, y, (p1.Z + p2.Z) / 2.0);
    }

    private Mesh CriarMalhaAmbiente(SpatialElement ambiente)
    {
        GeometryElement geomElem = ((Element)ambiente).get_Geometry(new Options());
        if (geomElem != null)
        {
            foreach (GeometryObject geomObj in geomElem)
            {
                if (!(geomObj is Solid { Volume: > 0.0 } solid))
                {
                    continue;
                }
                foreach (Face face in solid.Faces)
                {
                    if (face is PlanarFace pf && pf.FaceNormal.Z < -0.9)
                    {
                        return face.Triangulate();
                    }
                }
            }
        }
        return null;
    }

    private void IniciarFluxoPrincipal(XYZ clickPt)
    {
        DebugAgua.Iniciar();
        Tuple<SpatialElement, Transform, RevitLinkInstance> ambInfo = ObterAmbienteOuEspacoInfo(_doc, new XYZ(clickPt.X, clickPt.Y, clickPt.Z + 1.0));
        SpatialElement room = ambInfo.Item1;
        if (room == null)
        {
            try
            {
                XYZ ptNativo = _uidoc.Selection.PickPoint("PipeMaster: clique DENTRO do ambiente desejado");
                clickPt = new XYZ(ptNativo.X, ptNativo.Y, _zSonda);
                ambInfo = ObterAmbienteOuEspacoInfo(_doc, new XYZ(clickPt.X, clickPt.Y, clickPt.Z + 1.0));
                room = ambInfo.Item1;
            }
            catch
            {
                return;
            }
            if (room == null)
            {
                TaskDialog dlgCriar = new TaskDialog("PipeMaster — Nenhum ambiente encontrado");
                dlgCriar.MainInstruction = "Nenhum ambiente (Room/Space) foi encontrado neste ponto.";
                dlgCriar.MainContent = "O vínculo de arquitetura não possui ambientes (Rooms/Spaces) criados.\n\nO PipeMaster vai tentar criar o ambiente automaticamente.\nSe não conseguir detectar os limites do cômodo, você poderá desenhar o retângulo do ambiente manualmente.\n\nO ambiente criado será mantido no projeto ao final.";
                dlgCriar.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                dlgCriar.DefaultButton = TaskDialogResult.Yes;
                if (dlgCriar.Show() != TaskDialogResult.Yes)
                {
                    return;
                }
                DebugAgua.Log("=== FASE: criação automática de Room (fallback, sem ambiente no ponto) ===");
                room = TentarCriarRoomAutomatico(_doc, clickPt, out ambInfo);
                DebugAgua.Log("Estr.A (Room Bounding + NewRoom): " + ((room == null) ? "não criou" : ("criou area=" + Math.Round(room.Area * 0.092903, 2) + " m²")));
                if (room != null && RoomTamanhoExcessivo(room))
                {
                    DebugAgua.Log("Estr.A: Room excessivo (cobriu o pavimento) — descartado, indo para contorno automático (Estr.B)");
                    try
                    {
                        using Transaction tDel = new Transaction(_doc, "PipeMaster - Remover Room Excessivo");
                        tDel.Start();
                        _doc.Delete(room.Id);
                        tDel.Commit();
                    }
                    catch
                    {
                    }
                    room = null;
                    ambInfo = new Tuple<SpatialElement, Transform, RevitLinkInstance>(null, Transform.Identity, null);
                }
                if (room == null)
                {
                    room = TentarCriarRoomComContorno(_doc, clickPt, out ambInfo);
                    if (room != null && RoomTamanhoExcessivo(room))
                    {
                        DebugAgua.Log("Estr.B: contorno excessivo — descartado, indo para PickBox (Estr.C)");
                        try
                        {
                            using Transaction tDel2 = new Transaction(_doc, "PipeMaster - Remover Contorno Excessivo");
                            tDel2.Start();
                            _doc.Delete(room.Id);
                            tDel2.Commit();
                        }
                        catch
                        {
                        }
                        room = null;
                        ambInfo = new Tuple<SpatialElement, Transform, RevitLinkInstance>(null, Transform.Identity, null);
                    }
                }
                if (room == null)
                {
                    TaskDialog.Show("PipeMaster — Delimite o ambiente", "Arraste um retângulo sobre o CONTORNO DO CÔMODO onde deseja lançar.\n\nUse o botão esquerdo do mouse para iniciar e libere para confirmar.");
                    PickedBox caixa;
                    try
                    {
                        caixa = _uidoc.Selection.PickBox(PickBoxStyle.Enclosing, "PipeMaster: ARRASTE sobre o cômodo para definir o ambiente");
                    }
                    catch
                    {
                        return;
                    }
                    room = TentarCriarRoomComRetangulo(_doc, clickPt, caixa.Min.X, caixa.Min.Y, caixa.Max.X, caixa.Max.Y, out ambInfo);
                    if (room == null)
                    {
                        TaskDialog.Show("PipeMaster — Não foi possível criar o ambiente", "O ponto do clique ficou fora do retângulo desenhado, ou não foi possível criar o ambiente nessa área.\n\nTente novamente clicando DENTRO da área e desenhando o retângulo a partir dos cantos do cômodo.");
                        return;
                    }
                }
            }
        }
        List<PecaAguaDetectada> pecasEncontradas = CapturarPecasInteligente(_doc, clickPt, out string nomeAmb);
        pecasEncontradas = MotorRoteamentoAgua.OrdenarPeloPerimetro(room, ambInfo.Item2, pecasEncontradas);
        LancamentoAguaViewModel viewModel = new LancamentoAguaViewModel(_doc, nomeAmb, pecasEncontradas);
        Action<PecaAguaDetectada> destacar = delegate (PecaAguaDetectada peca)
        {
            try
            {
                if (_hoverServer != null)
                {
                    _hoverServer.MostrarMarcador(peca.Posicao);
                    _uidoc.RefreshActiveView();
                }
            }
            catch
            {
            }
        };
        Action limpar = delegate
        {
            try
            {
                if (_hoverServer != null)
                {
                    _hoverServer.LimparMarcador();
                    _uidoc.RefreshActiveView();
                }
            }
            catch
            {
            }
        };
        JanelaLancamentoAgua janela;
        while (true)
        {
            janela = new JanelaLancamentoAgua(viewModel, destacar, limpar);
            janela.ShowDialog();
            if (!janela.SolicitarSelecaoJanela)
            {
                break;
            }
            SelecionarAparelhosPorJanela(viewModel);
        }
        if (!janela.Result)
        {
            return;
        }
        viewModel.SalvarCache();
        List<PecaAguaItemViewModel> selecionadas = viewModel.Pecas.Where((PecaAguaItemViewModel p) => p.Selecionada).ToList();
        if (selecionadas.Count == 0)
        {
            return;
        }
        DebugAgua.Log("=== FASE: importação + roteamento ===");
        if (viewModel.UsarVinculo && viewModel.ImportarDoVinculo)
        {
            MapeamentoAparelhosViewModel vmMap = viewModel.ObterMapeamentoViewModel();
            ImportadorAparelhos.ResultadoImportacao resultImp = null;
            List<PecaAguaDetectada> importadas = new List<PecaAguaDetectada>();
            try
            {
                using Transaction tImp = new Transaction(_doc, "PipeMaster - Importar Aparelhos");
                tImp.Start();
                resultImp = ImportadorAparelhos.Importar(_doc, room, ambInfo.Item3, vmMap, out importadas);
                tImp.Commit();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("PipeMaster - Importação", "Falha ao importar aparelhos: " + ex.Message);
            }
            foreach (PecaAguaItemViewModel sel in selecionadas)
            {
                if (sel.Origem != null && sel.Origem.IsDoVinculo)
                {
                    PecaAguaDetectada equivalente = importadas.OrderBy((PecaAguaDetectada i) => i.Posicao.DistanceTo(sel.Origem.Posicao)).FirstOrDefault();
                    if (equivalente != null && equivalente.Posicao.DistanceTo(sel.Origem.Posicao) < 0.3)
                    {
                        sel.Origem.Instancia = equivalente.Instancia;
                        sel.Origem.Nome = equivalente.Nome;
                        sel.Origem.IsDoVinculo = false;
                    }
                }
            }
            if (resultImp != null && (resultImp.TotalImportados == 0 || resultImp.Avisos.Count > 0))
            {
                string msg = resultImp.TotalImportados + " aparelho(s) importado(s), " + resultImp.TotalIgnorados + " ignorado(s).";
                if (resultImp.Avisos.Count > 0)
                {
                    msg = msg + "\n\n" + string.Join("\n", resultImp.Avisos.Take(10));
                }
                else if (resultImp.TotalImportados == 0)
                {
                    msg += "\n\nVerifique se você mapeou as famílias no botão 'Mapear Famílias...' para uma família do projeto (diferente de '-- não importar --').";
                }
                TaskDialog.Show("PipeMaster - Importação", msg);
            }
        }
        XYZ ptRegistro;
        try
        {
            ptRegistro = _uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Perpendicular, "PipeMaster: clique na PAREDE onde ficará a prumada com o registro");
        }
        catch
        {
            return;
        }
        XYZ ptSubidaPiso = null;
        if (viewModel.DesviarPeloPiso)
        {
            try
            {
                ptSubidaPiso = _uidoc.Selection.PickPoint(ObjectSnapTypes.Nearest | ObjectSnapTypes.Perpendicular, "PipeMaster: clique na PAREDE OPOSTA onde a tubulação sobe do piso (travessia)");
            }
            catch
            {
                return;
            }
        }
        Level nivel = _doc.ActiveView.GenLevel;
        if (nivel == null)
        {
            double zRef = ptRegistro.Z;
            nivel = (from Level l in new FilteredElementCollector(_doc).OfClass(typeof(Level))
                     orderby Math.Abs(l.ProjectElevation - zRef)
                     select l).FirstOrDefault();
        }
        if (nivel == null)
        {
            TaskDialog.Show("PipeMaster", "Nenhum nível encontrado no projeto ativo para referenciar as alturas.");
            return;
        }
        ElementId levelId = nivel.Id;
        double zNivel = nivel.ProjectElevation;
        ConfigRoteamentoAgua cfg = new ConfigRoteamentoAgua
        {
            SistemaId = viewModel.SistemaSelecionado.Id,
            TipoTuboId = viewModel.TipoTuboSelecionado.Id,
            LevelId = levelId,
            ZRamal = zNivel + viewModel.AlturaRamal / 0.3048,
            ZTopoPrumada = zNivel + viewModel.AlturaPrumada / 0.3048,
            ZRegistro = zNivel + viewModel.AlturaRegistro / 0.3048,
            InserirRegistro = viewModel.InserirRegistro,
            RegistroSimboloId = viewModel.FamiliaRegistroSelecionada?.Id,
            InserirRegistroPressao = (viewModel.InserirRegistroPressao && viewModel.TemChuveiroSelecionado),
            RegistroPressaoSimboloId = viewModel.FamiliaRegistroPressaoSelecionada?.Id,
            ZRegistroPressao = zNivel + viewModel.AlturaRegistroPressao / 0.3048,
            DiametroRamalPes = viewModel.DiametroRamal / 304.8,
            DiametroDescidaPes = viewModel.DiametroDescida / 304.8,
            RecuoParedePes = viewModel.RecuoParede / 100.0 / 0.3048,
            NomeNivel = nivel.Name,
            InverterSentidoBucha = viewModel.InverterSentidoBucha,
            DesviarPeloPiso = viewModel.DesviarPeloPiso,
            ZPiso = zNivel + viewModel.AlturaPiso / 0.3048,
            PontoSubidaPiso = ptSubidaPiso
        };
        List<PontoConsumoAgua> pontos = selecionadas.Select((PecaAguaItemViewModel p) => new PontoConsumoAgua
        {
            Posicao = p.Origem.Posicao,
            ZPonto = zNivel + p.AlturaPonto / 0.3048,
            Nome = p.NomeExibicao,
            OffsetLateralPes = p.OffsetCm / 100.0 / 0.3048,
            EhChuveiro = (p.TipoSelecionado == "Chuveiro")
        }).ToList();
        using Transaction t = new Transaction(_doc, "PipeMaster - Lançamento de Água");
        t.Start();
        try
        {
            string resumo = MotorRoteamentoAgua.GerarRedeAgua(_doc, room, ambInfo.Item2, ptRegistro, pontos, cfg);
            t.Commit();
            if (viewModel.UsarVinculo && viewModel.ImportarDoVinculo)
            {
                using Transaction t2 = new Transaction(_doc, "PipeMaster - Alinhar Famílias");
                t2.Start();
                foreach (PecaAguaItemViewModel sel2 in selecionadas)
                {
                    if (sel2.Origem == null || sel2.Origem.Instancia == null || sel2.Origem.IsDoVinculo)
                    {
                        continue;
                    }
                    FamilyInstance fixture = sel2.Origem.Instancia;
                    ConnectorManager mgr = fixture.MEPModel?.ConnectorManager;
                    DebugAgua.Log("ALINHAR '" + sel2.NomeExibicao + "' (" + fixture.Symbol.FamilyName + ") pos=" + FmtPtCmd((fixture.Location as LocationPoint)?.Point) + ((mgr == null) ? " SEM ConnectorManager" : ""));
                    if (mgr == null)
                    {
                        continue;
                    }
                    Connector connAgua = SelecionarConectorAgua(mgr);
                    DebugAgua.Log("   conectorAgua=" + ((connAgua == null) ? "NAO ENCONTRADO" : (FmtPtCmd(connAgua.Origin) + " Ø" + Math.Round(2.0 * connAgua.Radius * 304.8) + "mm sist=" + connAgua.PipeSystemType)));
                    if (connAgua == null)
                    {
                        continue;
                    }
                    XYZ pontoJoelho = new XYZ(sel2.Origem.Posicao.X, sel2.Origem.Posicao.Y, zNivel + sel2.AlturaPonto / 0.3048);
                    List<FamilyInstance> fittings = (from FamilyInstance familyInstance in new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_PipeFitting).OfClass(typeof(FamilyInstance))
                                                     where familyInstance.Location is LocationPoint locationPoint && locationPoint.Point.DistanceTo(pontoJoelho) < 3.0
                                                     select familyInstance).ToList();
                    Connector alvo = null;
                    double minDist = double.MaxValue;
                    foreach (FamilyInstance f in fittings)
                    {
                        ConnectorManager fMgr = f.MEPModel?.ConnectorManager;
                        if (fMgr == null)
                        {
                            continue;
                        }
                        foreach (Connector c in fMgr.Connectors)
                        {
                            if (!c.IsConnected && c.Domain == Domain.DomainPiping)
                            {
                                double d = c.Origin.DistanceTo(pontoJoelho);
                                if (d < minDist && d < 3.0)
                                {
                                    minDist = d;
                                    alvo = c;
                                }
                            }
                        }
                    }
                    DebugAgua.Log("   " + fittings.Count + " conexão(ões) num raio de ~90cm do ponto do joelho " + FmtPtCmd(pontoJoelho) + "; alvo=" + ((alvo == null) ? "NENHUM conector aberto" : (FmtPtCmd(alvo.Origin) + " dist3D=" + Math.Round(minDist * 30.48) + "cm")));
                    if (alvo == null)
                    {
                        continue;
                    }
                    XYZ locF = (fixture.Location as LocationPoint)?.Point;
                    if (locF != null)
                    {
                        double dAtual = Dist2D(connAgua.Origin, alvo.Origin);
                        XYZ connFlip = new XYZ(2.0 * locF.X - connAgua.Origin.X, 2.0 * locF.Y - connAgua.Origin.Y, connAgua.Origin.Z);
                        double dFlip = Dist2D(connFlip, alvo.Origin);
                        if (dAtual > 0.66 && dFlip < dAtual - 0.66)
                        {
                            try
                            {
                                ElementTransformUtils.RotateElement(_doc, fixture.Id, Line.CreateBound(locF, locF + XYZ.BasisZ), Math.PI);
                                _doc.Regenerate();
                                connAgua = SelecionarConectorAgua(fixture.MEPModel?.ConnectorManager);
                                DebugAgua.Log("   estava de costas — girado 180° (conector agora " + FmtPtCmd(connAgua?.Origin) + ")");
                            }
                            catch (Exception ex2)
                            {
                                DebugAgua.Log("   falha ao girar 180°: " + ex2.Message);
                            }
                        }
                    }
                    if (connAgua == null)
                    {
                        continue;
                    }
                    XYZ mov3D = alvo.Origin - connAgua.Origin;
                    if (mov3D.GetLength() > 0.001)
                    {
                        DebugAgua.Log("   movendo em 3D: Δ=(" + Math.Round(mov3D.X * 30.48) + "cm, " + Math.Round(mov3D.Y * 30.48) + "cm, " + Math.Round(mov3D.Z * 30.48) + "cm)");
                        try
                        {
                            ElementTransformUtils.MoveElement(_doc, fixture.Id, mov3D);
                            _doc.Regenerate();
                        }
                        catch (Exception ex3)
                        {
                            DebugAgua.Log("   falha ao mover: " + ex3.Message);
                        }
                    }
                    Connector aguaPos = SelecionarConectorAgua(fixture.MEPModel?.ConnectorManager);
                    if (aguaPos != null && !aguaPos.IsConnected && !alvo.IsConnected)
                    {
                        try
                        {
                            aguaPos.ConnectTo(alvo);
                            DebugAgua.Log("   CONECTADO ao joelho.");
                        }
                        catch (Exception ex4)
                        {
                            DebugAgua.Log("   falha ao conectar: " + ex4.Message);
                        }
                    }
                    else if (aguaPos != null)
                    {
                        DebugAgua.Log("   sem ConnectTo (residual=" + Math.Round(aguaPos.Origin.DistanceTo(alvo.Origin) * 30.48) + "cm, aguaPos.IsConnected=" + aguaPos.IsConnected + ", alvo.IsConnected=" + alvo.IsConnected + ")");
                    }
                }
                t2.Commit();
            }
            new JanelaSucessoPremium("Lançamento de Água", resumo).ShowDialog();
        }
        catch (Exception ex5)
        {
            if (t.GetStatus() == TransactionStatus.Started)
            {
                t.RollBack();
            }
            TaskDialog.Show("PipeMaster - Erro", "Falha ao rotear: " + ex5.Message);
        }
    }

    private static double Dist2D(XYZ a, XYZ b)
    {
        if (a == null || b == null)
        {
            return double.MaxValue;
        }
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string FmtPtCmd(XYZ p)
    {
        return (p == null) ? "(null)" : string.Format(CultureInfo.InvariantCulture, "({0:F2}m, {1:F2}m, z={2:F2}m)", p.X * 0.3048, p.Y * 0.3048, p.Z * 0.3048);
    }

    private void SelecionarAparelhosPorJanela(LancamentoAguaViewModel vm)
    {
        try
        {
            PickedBox box = _uidoc.Selection.PickBox(PickBoxStyle.Crossing, "PipeMaster: arraste uma janela sobre os aparelhos a marcar");
            if (box != null && box.Min != null && box.Max != null)
            {
                double minX = Math.Min(box.Min.X, box.Max.X);
                double maxX = Math.Max(box.Min.X, box.Max.X);
                double minY = Math.Min(box.Min.Y, box.Max.Y);
                double maxY = Math.Max(box.Min.Y, box.Max.Y);
                vm.MarcarPecasNaCaixa(minX, minY, maxX, maxY);
            }
        }
        catch
        {
        }
    }

    private static Connector SelecionarConectorAgua(ConnectorManager mgr)
    {
        Connector af = null;
        Connector aq = null;
        Connector generico = null;
        double menorRaio = double.MaxValue;
        foreach (Connector c in mgr.Connectors)
        {
            if (c.Domain != Domain.DomainPiping)
            {
                continue;
            }
            PipeSystemType st = c.PipeSystemType;
            if (st == PipeSystemType.Sanitary || st == PipeSystemType.Vent)
            {
                continue;
            }
            switch (st)
            {
                case PipeSystemType.DomesticColdWater:
                    if (af == null)
                    {
                        af = c;
                    }
                    continue;
                case PipeSystemType.DomesticHotWater:
                    if (aq == null)
                    {
                        aq = c;
                    }
                    continue;
            }
            double raio = c.Radius;
            if (raio < menorRaio)
            {
                menorRaio = raio;
                generico = c;
            }
        }
        return af ?? aq ?? generico;
    }

    private List<PecaAguaDetectada> CapturarPecasInteligente(Document doc, XYZ cliqueOriginal, out string nomeAmbienteDetectado)
    {
        nomeAmbienteDetectado = "Radar de Proximidade";
        List<PecaAguaBruta> pecasBrutas = new List<PecaAguaBruta>();
        XYZ pontoBusca = new XYZ(cliqueOriginal.X, cliqueOriginal.Y, cliqueOriginal.Z + 1.0);
        Tuple<SpatialElement, Transform, RevitLinkInstance> ambInfo = ObterAmbienteOuEspacoInfo(doc, pontoBusca);
        SpatialElement ambiente = ambInfo.Item1;
        Transform trfAmb = ambInfo.Item2;
        ElementMulticategoryFilter catFilter = new ElementMulticategoryFilter(new List<BuiltInCategory>
        {
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_SpecialityEquipment,
            BuiltInCategory.OST_GenericModel
        });
        IEnumerable<RevitLinkInstance> links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
        if (ambiente != null)
        {
            nomeAmbienteDetectado = ambiente.Name;
            BoundingBoxXYZ bb = ((Element)ambiente).get_BoundingBox((View)null);
            if (bb != null)
            {
                Outline outline = GetTransformedOutline(bb.Min, bb.Max, trfAmb);
                BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);
                IEnumerable<FamilyInstance> pecasAtivas = new FilteredElementCollector(doc).WherePasses(catFilter).WherePasses(filter).OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(delegate (FamilyInstance f)
                    {
                        if (!(f.Location is LocationPoint locationPoint))
                        {
                            return false;
                        }
                        if (PontoNoAmbiente(ambiente, trfAmb, locationPoint.Point))
                        {
                            return true;
                        }
                        BoundingBoxXYZ boundingBoxXYZ = ((Element)f).get_BoundingBox((View)null);
                        return boundingBoxXYZ != null && PontoNoAmbiente(ambiente, trfAmb, (boundingBoxXYZ.Min + boundingBoxXYZ.Max) / 2.0);
                    });
                foreach (FamilyInstance p in pecasAtivas)
                {
                    pecasBrutas.Add(new PecaAguaBruta
                    {
                        Instancia = p,
                        Transformacao = Transform.Identity,
                        IsDoVinculo = false
                    });
                }
                foreach (RevitLinkInstance link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc == null)
                    {
                        continue;
                    }
                    Transform t = link.GetTotalTransform();
                    Outline linkOutline = GetTransformedOutline(outline.MinimumPoint, outline.MaximumPoint, t.Inverse);
                    BoundingBoxIntersectsFilter linkFilter = new BoundingBoxIntersectsFilter(linkOutline);
                    IEnumerable<FamilyInstance> pecasLink = new FilteredElementCollector(linkDoc).WherePasses(catFilter).WherePasses(linkFilter).OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(delegate (FamilyInstance f)
                        {
                            if (!(f.Location is LocationPoint locationPoint))
                            {
                                return false;
                            }
                            XYZ ptAtivo = t.OfPoint(locationPoint.Point);
                            if (PontoNoAmbiente(ambiente, trfAmb, ptAtivo))
                            {
                                return true;
                            }
                            BoundingBoxXYZ boundingBoxXYZ = ((Element)f).get_BoundingBox((View)null);
                            return boundingBoxXYZ != null && PontoNoAmbiente(ambiente, trfAmb, t.OfPoint((boundingBoxXYZ.Min + boundingBoxXYZ.Max) / 2.0));
                        });
                    foreach (FamilyInstance p2 in pecasLink)
                    {
                        pecasBrutas.Add(new PecaAguaBruta
                        {
                            Instancia = p2,
                            Transformacao = t,
                            IsDoVinculo = true
                        });
                    }
                }
            }
        }
        if (pecasBrutas.Count == 0)
        {
            double raioPes = 16.404199475065617;
            Outline outlineRadar = new Outline(new XYZ(cliqueOriginal.X - raioPes, cliqueOriginal.Y - raioPes, cliqueOriginal.Z - 3.0), new XYZ(cliqueOriginal.X + raioPes, cliqueOriginal.Y + raioPes, cliqueOriginal.Z + 9.0));
            BoundingBoxIntersectsFilter filterRadar = new BoundingBoxIntersectsFilter(outlineRadar);
            IEnumerable<FamilyInstance> pecasAtivas2 = new FilteredElementCollector(doc).WherePasses(catFilter).WherePasses(filterRadar).OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();
            foreach (FamilyInstance p3 in pecasAtivas2)
            {
                pecasBrutas.Add(new PecaAguaBruta
                {
                    Instancia = p3,
                    Transformacao = Transform.Identity,
                    IsDoVinculo = false
                });
            }
            foreach (RevitLinkInstance link2 in links)
            {
                Document linkDoc2 = link2.GetLinkDocument();
                if (linkDoc2 == null)
                {
                    continue;
                }
                Transform t2 = link2.GetTotalTransform();
                Outline linkOutline2 = GetTransformedOutline(outlineRadar.MinimumPoint, outlineRadar.MaximumPoint, t2.Inverse);
                BoundingBoxIntersectsFilter linkFilter2 = new BoundingBoxIntersectsFilter(linkOutline2);
                IEnumerable<FamilyInstance> pecasLink2 = new FilteredElementCollector(linkDoc2).WherePasses(catFilter).WherePasses(linkFilter2).OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>();
                foreach (FamilyInstance p4 in pecasLink2)
                {
                    pecasBrutas.Add(new PecaAguaBruta
                    {
                        Instancia = p4,
                        Transformacao = t2,
                        IsDoVinculo = true
                    });
                }
            }
        }
        List<PecaAguaDetectada> listaRefinada = new List<PecaAguaDetectada>();
        foreach (PecaAguaBruta pb in pecasBrutas)
        {
            FamilyInstance aparelho = pb.Instancia;
            if (!(aparelho.Location is LocationPoint loc))
            {
                continue;
            }
            string nomeTipo = (aparelho.Name ?? "").ToLower();
            string nomeSoFamilia = (aparelho.Symbol.FamilyName ?? "").ToLower();
            string nomeFamilia = (nomeTipo + " " + nomeSoFamilia).ToLower();
            bool ehLoucaForte = nomeFamilia.Contains("cuba") || nomeFamilia.Contains("pia") || nomeFamilia.Contains("lavat") || nomeFamilia.Contains("vaso") || nomeFamilia.Contains("bacia") || nomeFamilia.Contains("sanit") || nomeFamilia.Contains("chuveiro") || nomeFamilia.Contains("ducha") || nomeFamilia.Contains("mict") || nomeFamilia.Contains("tanque") || nomeFamilia.Contains("higien");
            if ((!ehLoucaForte && (nomeFamilia.Contains("ralo") || nomeFamilia.Contains("sifonada") || nomeFamilia.Contains("gordura") || nomeFamilia.Contains("inspe") || nomeFamilia.Contains("barra") || nomeFamilia.Contains("apoio") || nomeFamilia.Contains("dispenser") || nomeFamilia.Contains("toalha") || nomeFamilia.Contains("espelho") || nomeFamilia.Contains("papeleira") || nomeFamilia.Contains("cabide") || nomeFamilia.Contains("saboneteira") || nomeFamilia.Contains("lixeira") || nomeFamilia.Contains("secador") || nomeFamilia.Contains("gabinete") || nomeFamilia.Contains("componente") || nomeFamilia.Contains("tampo") || nomeFamilia.Contains("bancada") || nomeFamilia.Contains("acabamento") || nomeFamilia.Contains("porta") || nomeFamilia.Contains("box") || nomeFamilia.Contains("papel") || nomeFamilia.Contains("lixo") || nomeFamilia.Contains("televis") || nomeFamilia.Contains("tv") || nomeFamilia.Contains("prateleira") || nomeFamilia.Contains("nicho") || nomeFamilia.Contains("cortina") || nomeFamilia.Contains("janela"))) || (!ehLoucaForte && (nomeSoFamilia.Contains("branco") || nomeSoFamilia.Contains("branca") || nomeSoFamilia.Contains("preto") || nomeSoFamilia.Contains("tampa") || nomeSoFamilia.Contains("assento") || nomeSoFamilia.Contains("cega"))))
            {
                continue;
            }
            XYZ posicaoReal = pb.Transformacao.OfPoint(loc.Point);
            bool temAF = false;
            bool temAQ = false;
            if (aparelho.MEPModel != null && aparelho.MEPModel.ConnectorManager != null)
            {
                foreach (Connector conn in aparelho.MEPModel.ConnectorManager.Connectors)
                {
                    if (conn.Domain == Domain.DomainPiping)
                    {
                        if (conn.PipeSystemType == PipeSystemType.DomesticColdWater)
                        {
                            temAF = true;
                        }
                        if (conn.PipeSystemType == PipeSystemType.DomesticHotWater)
                        {
                            temAQ = true;
                        }
                    }
                }
            }
            if (!temAF && !temAQ)
            {
                if (nomeFamilia.Contains("vaso") || nomeFamilia.Contains("bacia") || nomeFamilia.Contains("sanit") || nomeFamilia.Contains("mict") || nomeFamilia.Contains("válvula") || nomeFamilia.Contains("valvula") || nomeFamilia.Contains("descarga") || nomeFamilia.Contains("miolo"))
                {
                    temAF = true;
                }
                else if (nomeFamilia.Contains("lavat") || nomeFamilia.Contains("pia") || nomeFamilia.Contains("chuveiro") || nomeFamilia.Contains("ducha") || nomeFamilia.Contains("misturador") || nomeFamilia.Contains("torneira") || nomeFamilia.Contains("bebedouro") || nomeFamilia.Contains("filtro") || nomeFamilia.Contains("máquina") || nomeFamilia.Contains("maquina") || nomeFamilia.Contains("tanque"))
                {
                    temAF = true;
                    temAQ = true;
                }
                else
                {
                    if (aparelho.Category != null && aparelho.Category.Id != new ElementId(BuiltInCategory.OST_PlumbingFixtures))
                    {
                        continue;
                    }
                    temAF = true;
                }
            }
            if (temAF || temAQ)
            {
                listaRefinada.Add(new PecaAguaDetectada
                {
                    Instancia = aparelho,
                    Nome = aparelho.Name,
                    Posicao = posicaoReal,
                    RequerAguaFria = temAF,
                    RequerAguaQuente = temAQ,
                    IsDoVinculo = pb.IsDoVinculo
                });
            }
        }
        return listaRefinada.OrderBy((PecaAguaDetectada pecaAguaDetectada) => pecaAguaDetectada.Nome).ToList();
    }

    private bool PontoNoAmbiente(SpatialElement ambiente, Transform trfAmb, XYZ ptAtivo)
    {
        try
        {
            XYZ ptLocal = trfAmb.Inverse.OfPoint(ptAtivo);
            XYZ ptLocalAcima = new XYZ(ptLocal.X, ptLocal.Y, ptLocal.Z + 1.0);
            if (ambiente is Room r)
            {
                return r.IsPointInRoom(ptLocal) || r.IsPointInRoom(ptLocalAcima);
            }
            if (ambiente is Space s)
            {
                return s.IsPointInSpace(ptLocal) || s.IsPointInSpace(ptLocalAcima);
            }
        }
        catch
        {
        }
        return false;
    }

    private Tuple<SpatialElement, Transform, RevitLinkInstance> ObterAmbienteOuEspacoInfo(Document doc, XYZ pt)
    {
        Room r = doc.GetRoomAtPoint(pt);
        if (r != null)
        {
            return new Tuple<SpatialElement, Transform, RevitLinkInstance>(r, Transform.Identity, null);
        }
        Space s = doc.GetSpaceAtPoint(pt);
        if (s != null)
        {
            return new Tuple<SpatialElement, Transform, RevitLinkInstance>(s, Transform.Identity, null);
        }
        IEnumerable<RevitLinkInstance> links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
        foreach (RevitLinkInstance link in links)
        {
            Document linkDoc = link.GetLinkDocument();
            if (linkDoc != null)
            {
                Transform t = link.GetTotalTransform();
                XYZ ptLink = t.Inverse.OfPoint(pt);
                Room rLink = linkDoc.GetRoomAtPoint(ptLink);
                if (rLink != null)
                {
                    return new Tuple<SpatialElement, Transform, RevitLinkInstance>(rLink, t, link);
                }
                Space sLink = linkDoc.GetSpaceAtPoint(ptLink);
                if (sLink != null)
                {
                    return new Tuple<SpatialElement, Transform, RevitLinkInstance>(sLink, t, link);
                }
            }
        }
        return new Tuple<SpatialElement, Transform, RevitLinkInstance>(null, Transform.Identity, null);
    }

    private SpatialElement TentarCriarRoomAutomatico(Document doc, XYZ pt, out Tuple<SpatialElement, Transform, RevitLinkInstance> ambInfoOut)
    {
        ambInfoOut = new Tuple<SpatialElement, Transform, RevitLinkInstance>(null, Transform.Identity, null);
        Level nivel = (from Level l in new FilteredElementCollector(doc).OfClass(typeof(Level))
                       orderby Math.Abs(l.ProjectElevation - pt.Z)
                       select l).FirstOrDefault();
        if (nivel == null)
        {
            return null;
        }
        try
        {
            using Transaction t1 = new Transaction(doc, "PipeMaster - Room Bounding");
            t1.Start();
            DefinirRoomBoundingNosVinculos(doc, 1);
            t1.Commit();
        }
        catch
        {
        }
        Room novoRoom = null;
        try
        {
            using Transaction t2 = new Transaction(doc, "PipeMaster - Criar Ambiente Automático");
            t2.Start();
            novoRoom = doc.Create.NewRoom(nivel, new UV(pt.X, pt.Y));
            doc.Regenerate();
            if (novoRoom != null && novoRoom.Area < 0.01)
            {
                doc.Delete(novoRoom.Id);
                novoRoom = null;
            }
            if (novoRoom == null)
            {
                t2.RollBack();
                return null;
            }
            novoRoom.Name = "Ambiente PipeMaster";
            t2.Commit();
        }
        catch
        {
            return null;
        }
        Room confirmado = novoRoom;
        try
        {
            Room r = doc.GetRoomAtPoint(new XYZ(pt.X, pt.Y, pt.Z + 1.0));
            if (r != null)
            {
                confirmado = r;
            }
        }
        catch
        {
        }
        ambInfoOut = new Tuple<SpatialElement, Transform, RevitLinkInstance>(confirmado, Transform.Identity, null);
        return confirmado;
    }

    private static int DefinirRoomBoundingNosVinculos(Document doc, int valor)
    {
        int alterados = 0;
        List<RevitLinkInstance> links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
        foreach (RevitLinkInstance link in links)
        {
            if (valor == 1)
            {
                Document ld = link.GetLinkDocument();
                if (ld == null || !new FilteredElementCollector(ld).OfClass(typeof(Wall)).Any())
                {
                    continue;
                }
            }
            Element[] array = new Element[2]
            {
                doc.GetElement(link.GetTypeId()),
                link
            };
            foreach (Element el in array)
            {
                if (el == null)
                {
                    continue;
                }
                foreach (Parameter p in el.Parameters)
                {
                    string nome = p.Definition.Name.ToLower();
                    if (!nome.Contains("room bounding") && !nome.Contains("limite de ambiente") && (!nome.Contains("bounding") || !nome.Contains("room")) && (!nome.Contains("delimita") || !nome.Contains("ambiente")))
                    {
                        continue;
                    }
                    try
                    {
                        if (!p.IsReadOnly && p.StorageType == StorageType.Integer && p.AsInteger() != valor)
                        {
                            p.Set(valor);
                            alterados++;
                            DebugAgua.Log("   RB '" + p.Definition.Name + "' -> " + valor + " (" + el.GetType().Name + ")");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugAgua.Log("   RB set falhou: " + ex.Message);
                    }
                }
            }
        }
        return alterados;
    }

    private SpatialElement TentarCriarRoomComRetangulo(Document doc, XYZ ptClique, double xMin, double yMin, double xMax, double yMax, out Tuple<SpatialElement, Transform, RevitLinkInstance> ambInfoOut)
    {
        ambInfoOut = new Tuple<SpatialElement, Transform, RevitLinkInstance>(null, Transform.Identity, null);
        Level nivel = (from Level l in new FilteredElementCollector(doc).OfClass(typeof(Level))
                       orderby Math.Abs(l.ProjectElevation - ptClique.Z)
                       select l).FirstOrDefault();
        if (nivel == null)
        {
            return null;
        }
        ViewPlan vista = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().FirstOrDefault((ViewPlan v) => v.GenLevel != null && v.GenLevel.Id == nivel.Id && v.ViewType == ViewType.FloorPlan && !v.IsTemplate);
        if (vista == null)
        {
            return null;
        }
        Room novoRoom = null;
        try
        {
            using Transaction t = new Transaction(doc, "PipeMaster - Criar Ambiente Manual");
            t.Start();
            double z = nivel.ProjectElevation;
            if (xMin > xMax)
            {
                double tmp = xMin;
                xMin = xMax;
                xMax = tmp;
            }
            if (yMin > yMax)
            {
                double tmp2 = yMin;
                yMin = yMax;
                yMax = tmp2;
            }
            DebugAgua.Log("ROOM: caixa desenhada (PickBox) x=[" + Mt(xMin) + " .. " + Mt(xMax) + "] y=[" + Mt(yMin) + " .. " + Mt(yMax) + "]  (" + Mt(xMax - xMin) + " x " + Mt(yMax - yMin) + ")");
            double[] snapped = SnapBordaAParedes(doc, xMin, yMin, xMax, yMax);
            xMin = snapped[0];
            yMin = snapped[1];
            xMax = snapped[2];
            yMax = snapped[3];
            DebugAgua.Log("ROOM: retângulo pós-snap x=[" + Mt(xMin) + " .. " + Mt(xMax) + "] y=[" + Mt(yMin) + " .. " + Mt(yMax) + "]  (" + Mt(xMax - xMin) + " x " + Mt(yMax - yMin) + ")");
            double ptX = Math.Max(xMin + 0.1, Math.Min(xMax - 0.1, ptClique.X));
            double ptY = Math.Max(yMin + 0.1, Math.Min(yMax - 0.1, ptClique.Y));
            DebugAgua.Log("ROOM: Delimitação de ambientes LIGADA em " + DefinirRoomBoundingNosVinculos(doc, 1) + " vínculo(s) de arquitetura");
            LimparSeparationLinesNaRegiao(doc, xMin, yMin, xMax, yMax);
            XYZ[] corners = new XYZ[4]
            {
                new XYZ(xMin, yMin, z),
                new XYZ(xMax, yMin, z),
                new XYZ(xMax, yMax, z),
                new XYZ(xMin, yMax, z)
            };
            SketchPlane plano = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0.0, 0.0, z)));
            CurveArray curvas = new CurveArray();
            for (int i = 0; i < 4; i++)
            {
                curvas.Append(Line.CreateBound(corners[i], corners[(i + 1) % 4]));
            }
            doc.Create.NewRoomBoundaryLines(plano, curvas, vista);
            doc.Regenerate();
            novoRoom = CriarRoomNaFaseDaVista(doc, nivel, vista, new XYZ(ptX, ptY, z));
            DebugAgua.Log("ROOM: NewRoom em (" + Mt(ptX) + ", " + Mt(ptY) + ")  area=" + ((novoRoom == null) ? "null" : (Math.Round(novoRoom.Area * 0.092903, 2) + " m²")));
            if (novoRoom != null && novoRoom.Area < 0.01)
            {
                DebugAgua.Log("ROOM: area ~0 — descartado (paredes não fecharam o loop nesse retângulo)");
                doc.Delete(novoRoom.Id);
                novoRoom = null;
            }
            if (novoRoom == null)
            {
                t.RollBack();
                return null;
            }
            novoRoom.Name = "Ambiente PipeMaster";
            t.Commit();
            DebugAgua.Log("ROOM: criado OK e commitado.");
        }
        catch (Exception ex)
        {
            DebugAgua.Log("ROOM: EXCEÇÃO ao criar: " + ex.Message);
            return null;
        }
        Room confirmado = novoRoom;
        try
        {
            Room r = doc.GetRoomAtPoint(new XYZ(ptClique.X, ptClique.Y, ptClique.Z + 1.0));
            if (r != null)
            {
                confirmado = r;
            }
        }
        catch
        {
        }
        ambInfoOut = new Tuple<SpatialElement, Transform, RevitLinkInstance>(confirmado, Transform.Identity, null);
        return confirmado;
    }

    private SpatialElement TentarCriarRoomComContorno(Document doc, XYZ ptClique, out Tuple<SpatialElement, Transform, RevitLinkInstance> ambInfoOut)
    {
        ambInfoOut = new Tuple<SpatialElement, Transform, RevitLinkInstance>(null, Transform.Identity, null);
        Level nivel = (from Level l in new FilteredElementCollector(doc).OfClass(typeof(Level))
                       orderby Math.Abs(l.ProjectElevation - ptClique.Z)
                       select l).FirstOrDefault();
        if (nivel == null)
        {
            return null;
        }
        ViewPlan vista = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().FirstOrDefault((ViewPlan v) => v.GenLevel != null && v.GenLevel.Id == nivel.Id && v.ViewType == ViewType.FloorPlan && !v.IsTemplate);
        if (vista == null)
        {
            return null;
        }
        double z = nivel.ProjectElevation;
        List<XYZ> poligono = TracarContornoAmbiente(doc, ptClique, z);
        if (poligono == null || poligono.Count < 4)
        {
            DebugAgua.Log("CONTORNO: não fechou um polígono válido — usando retângulo (Estr.C)");
            return null;
        }
        DebugAgua.Log("CONTORNO: polígono com " + poligono.Count + " vértices — criando Room");
        Room novoRoom = null;
        try
        {
            using Transaction t = new Transaction(doc, "PipeMaster - Ambiente por Contorno");
            t.Start();
            DebugAgua.Log("CONTORNO: Delimitação de ambientes LIGADA em " + DefinirRoomBoundingNosVinculos(doc, 1) + " vínculo(s) de arquitetura");
            double cminX = poligono.Min((XYZ xYZ) => xYZ.X);
            double cmaxX = poligono.Max((XYZ xYZ) => xYZ.X);
            double cminY = poligono.Min((XYZ xYZ) => xYZ.Y);
            double cmaxY = poligono.Max((XYZ xYZ) => xYZ.Y);
            LimparSeparationLinesNaRegiao(doc, cminX, cminY, cmaxX, cmaxY);
            SketchPlane plano = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0.0, 0.0, z)));
            CurveArray curvas = new CurveArray();
            int m = poligono.Count;
            for (int i = 0; i < m; i++)
            {
                XYZ p1 = new XYZ(poligono[i].X, poligono[i].Y, z);
                XYZ p2 = new XYZ(poligono[(i + 1) % m].X, poligono[(i + 1) % m].Y, z);
                if (p1.DistanceTo(p2) > 0.02)
                {
                    curvas.Append(Line.CreateBound(p1, p2));
                }
            }
            doc.Create.NewRoomBoundaryLines(plano, curvas, vista);
            doc.Regenerate();
            novoRoom = CriarRoomNaFaseDaVista(doc, nivel, vista, ptClique);
            DebugAgua.Log("CONTORNO: NewRoom area=" + ((novoRoom == null) ? "null" : (Math.Round(novoRoom.Area * 0.092903, 2) + " m²")));
            if (novoRoom != null && novoRoom.Area < 0.01)
            {
                doc.Delete(novoRoom.Id);
                novoRoom = null;
            }
            if (novoRoom == null)
            {
                t.RollBack();
                return null;
            }
            novoRoom.Name = "Ambiente PipeMaster";
            t.Commit();
            DebugAgua.Log("CONTORNO: Room criado OK e commitado.");
        }
        catch (Exception ex)
        {
            DebugAgua.Log("CONTORNO: EXCEÇÃO ao criar Room: " + ex.Message);
            return null;
        }
        Room confirmado = novoRoom;
        try
        {
            Room r = doc.GetRoomAtPoint(new XYZ(ptClique.X, ptClique.Y, ptClique.Z + 1.0));
            if (r != null)
            {
                confirmado = r;
            }
        }
        catch
        {
        }
        ambInfoOut = new Tuple<SpatialElement, Transform, RevitLinkInstance>(confirmado, Transform.Identity, null);
        return confirmado;
    }

    private static List<XYZ> TracarContornoAmbiente(Document doc, XYZ clickPt, double z)
    {
        List<Tuple<Document, Transform>> pares = new List<Tuple<Document, Transform>>();
        pares.Add(Tuple.Create(doc, Transform.Identity));
        foreach (RevitLinkInstance lk in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            Document ld = lk.GetLinkDocument();
            if (ld != null)
            {
                pares.Add(Tuple.Create(ld, lk.GetTotalTransform()));
            }
        }
        List<double[]> segs = new List<double[]>();
        foreach (Tuple<Document, Transform> pr in pares)
        {
            foreach (Wall parede in new FilteredElementCollector(pr.Item1).OfClass(typeof(Wall)).Cast<Wall>())
            {
                if (!(parede.Location is LocationCurve { Curve: var cv }))
                {
                    continue;
                }
                XYZ a = pr.Item2.OfPoint(cv.GetEndPoint(0));
                XYZ b = pr.Item2.OfPoint(cv.GetEndPoint(1));
                if (Math.Abs((a.Z + b.Z) * 0.5 - z) > 4.0)
                {
                    continue;
                }
                double w = 0.24;
                try
                {
                    w = parede.Width;
                }
                catch
                {
                }
                double half = w * 0.5;
                XYZ desloc = XYZ.Zero;
                try
                {
                    int refLine = ((Element)parede).get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.AsInteger() ?? 0;
                    double off = 0.0;
                    if (refLine == 3 || refLine == 5)
                    {
                        off = w * 0.5;
                    }
                    else if (refLine == 2 || refLine == 4)
                    {
                        off = (0.0 - w) * 0.5;
                    }
                    XYZ nrm = pr.Item2.OfVector(parede.Orientation);
                    XYZ nrmH = new XYZ(nrm.X, nrm.Y, 0.0);
                    if (Math.Abs(off) > 1E-09 && nrmH.GetLength() > 1E-09)
                    {
                        desloc = nrmH.Normalize().Multiply(off);
                    }
                }
                catch
                {
                }
                a += desloc;
                b += desloc;
                if (cv is Line)
                {
                    segs.Add(new double[5] { a.X, a.Y, b.X, b.Y, half });
                    continue;
                }
                XYZ prev = a;
                for (int k = 1; k <= 8; k++)
                {
                    XYZ q = pr.Item2.OfPoint(cv.Evaluate((double)k / 8.0, normalized: true)) + desloc;
                    segs.Add(new double[5] { prev.X, prev.Y, q.X, q.Y, half });
                    prev = q;
                }
            }
        }
        if (segs.Count < 3)
        {
            return null;
        }
        int n = 401;
        double x0 = clickPt.X - 32.8;
        double y0 = clickPt.Y - 32.8;
        bool[,] paredeCel = new bool[n, n];
        foreach (double[] s in segs)
        {
            double ax = s[0];
            double ay = s[1];
            double bx = s[2];
            double by = s[3];
            double half2 = Math.Max(s[4], 0.14);
            double margem = half2 + 0.164;
            int iMin = (int)((Math.Min(ax, bx) - margem - x0) / 0.164);
            int iMax = (int)((Math.Max(ax, bx) + margem - x0) / 0.164);
            int jMin = (int)((Math.Min(ay, by) - margem - y0) / 0.164);
            int jMax = (int)((Math.Max(ay, by) + margem - y0) / 0.164);
            if (iMin < 0)
            {
                iMin = 0;
            }
            if (jMin < 0)
            {
                jMin = 0;
            }
            if (iMax >= n)
            {
                iMax = n - 1;
            }
            if (jMax >= n)
            {
                jMax = n - 1;
            }
            for (int i = iMin; i <= iMax; i++)
            {
                for (int j = jMin; j <= jMax; j++)
                {
                    if (!paredeCel[i, j])
                    {
                        double cx = x0 + ((double)i + 0.5) * 0.164;
                        double cy = y0 + ((double)j + 0.5) * 0.164;
                        if (DistPtSeg2D(cx, cy, ax, ay, bx, by) <= half2)
                        {
                            paredeCel[i, j] = true;
                        }
                    }
                }
            }
        }
        int ci = (int)((clickPt.X - x0) / 0.164);
        int cj = (int)((clickPt.Y - y0) / 0.164);
        if (ci < 1 || ci >= n - 1 || cj < 1 || cj >= n - 1)
        {
            return null;
        }
        if (paredeCel[ci, cj])
        {
            bool achou = false;
            for (int r = 1; r <= 8; r++)
            {
                if (achou)
                {
                    break;
                }
                for (int di = -r; di <= r; di++)
                {
                    if (achou)
                    {
                        break;
                    }
                    for (int dj = -r; dj <= r; dj++)
                    {
                        if (achou)
                        {
                            break;
                        }
                        int ii = ci + di;
                        int jj = cj + dj;
                        if (ii > 0 && ii < n - 1 && jj > 0 && jj < n - 1 && !paredeCel[ii, jj])
                        {
                            ci = ii;
                            cj = jj;
                            achou = true;
                        }
                    }
                }
            }
            if (!achou)
            {
                return null;
            }
        }
        bool[,] cheio = new bool[n, n];
        Stack<int> pilha = new Stack<int>();
        pilha.Push(ci * n + cj);
        cheio[ci, cj] = true;
        bool vazou = false;
        long area = 0L;
        int[] dii = new int[4] { 1, -1, 0, 0 };
        int[] djj = new int[4] { 0, 0, 1, -1 };
        while (pilha.Count > 0)
        {
            int cur = pilha.Pop();
            int i2 = cur / n;
            int j2 = cur % n;
            area++;
            if (i2 <= 0 || i2 >= n - 1 || j2 <= 0 || j2 >= n - 1)
            {
                vazou = true;
                break;
            }
            for (int l = 0; l < 4; l++)
            {
                int ni = i2 + dii[l];
                int nj = j2 + djj[l];
                if (!cheio[ni, nj] && !paredeCel[ni, nj])
                {
                    cheio[ni, nj] = true;
                    pilha.Push(ni * n + nj);
                }
            }
        }
        if (vazou)
        {
            DebugAgua.Log("CONTORNO: preenchimento vazou (cômodo não fechou)");
            return null;
        }
        double areaM2 = (double)area * 0.164 * 0.164 * 0.092903;
        if (areaM2 < 0.5 || areaM2 > 80.0)
        {
            DebugAgua.Log("CONTORNO: área " + Math.Round(areaM2, 1) + " m² fora do razoável");
            return null;
        }
        Dictionary<long, long> prox = new Dictionary<long, long>();
        for (int m = 0; m < n; m++)
        {
            for (int num = 0; num < n; num++)
            {
                if (cheio[m, num])
                {
                    bool right = m + 1 < n && cheio[m + 1, num];
                    bool left = m - 1 >= 0 && cheio[m - 1, num];
                    bool top = num + 1 < n && cheio[m, num + 1];
                    bool bot = num - 1 >= 0 && cheio[m, num - 1];
                    if (!right)
                    {
                        prox[(long)(m + 1) * (long)(n + 1) + num] = (long)(m + 1) * (long)(n + 1) + (num + 1);
                    }
                    if (!left)
                    {
                        prox[(long)m * (long)(n + 1) + (num + 1)] = (long)m * (long)(n + 1) + num;
                    }
                    if (!top)
                    {
                        prox[(long)(m + 1) * (long)(n + 1) + (num + 1)] = (long)m * (long)(n + 1) + (num + 1);
                    }
                    if (!bot)
                    {
                        prox[(long)m * (long)(n + 1) + num] = (long)(m + 1) * (long)(n + 1) + num;
                    }
                }
            }
        }
        HashSet<long> visitado = new HashSet<long>();
        List<XYZ> melhor = null;
        double melhorArea = -1.0;
        foreach (long chave in prox.Keys)
        {
            if (visitado.Contains(chave))
            {
                continue;
            }
            List<long> loop = new List<long>();
            long atual = chave;
            int guarda = 0;
            while (!visitado.Contains(atual) && prox.ContainsKey(atual) && guarda++ < prox.Count + 5)
            {
                visitado.Add(atual);
                loop.Add(atual);
                atual = prox[atual];
            }
            if (loop.Count < 4)
            {
                continue;
            }
            List<XYZ> pts = new List<XYZ>(loop.Count);
            foreach (long kkey in loop)
            {
                int cx2 = (int)(kkey / (n + 1));
                int cy2 = (int)(kkey % (n + 1));
                pts.Add(new XYZ(x0 + (double)cx2 * 0.164, y0 + (double)cy2 * 0.164, z));
            }
            double ar = Math.Abs(AreaPoligono2D(pts));
            if (ar > melhorArea)
            {
                melhorArea = ar;
                melhor = pts;
            }
        }
        if (melhor == null)
        {
            return null;
        }
        melhor = MesclarColineares(melhor);
        melhor = SimplificarDP(melhor, 0.25);
        melhor = MesclarColineares(melhor);
        if (melhor.Count < 4 || melhor.Count > 200)
        {
            return null;
        }
        melhor = SnapContornoAParedes(melhor, segs);
        melhor = MesclarColineares(melhor);
        StringBuilder sbC = new StringBuilder("CONTORNO vértices (m):");
        foreach (XYZ pv in melhor)
        {
            sbC.Append(" (" + Math.Round(pv.X * 0.3048, 2) + "," + Math.Round(pv.Y * 0.3048, 2) + ")");
        }
        DebugAgua.Log(sbC.ToString());
        return melhor;
    }

    private static void LimparSeparationLinesNaRegiao(Document doc, double minX, double minY, double maxX, double maxY)
    {
        try
        {
            double m = 0.5;
            List<ElementId> ids = (from el in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RoomSeparationLines).WhereElementIsNotElementType().Where(delegate (Element el)
                {
                    Curve curve = ((el is CurveElement curveElement) ? curveElement.GeometryCurve : null);
                    if (curve == null)
                    {
                        return false;
                    }
                    XYZ xYZ = curve.Evaluate(0.5, normalized: true);
                    return xYZ.X >= minX - m && xYZ.X <= maxX + m && xYZ.Y >= minY - m && xYZ.Y <= maxY + m;
                })
                                   select el.Id).ToList();
            DebugAgua.Log("ROOM: separation lines pré-existentes na região = " + ids.Count);
            if (ids.Count > 0)
            {
                doc.Delete(ids);
                doc.Regenerate();
                DebugAgua.Log("ROOM: apagadas " + ids.Count + " Room Separation Lines pré-existentes (evita loop quebrado)");
            }
        }
        catch (Exception ex)
        {
            DebugAgua.Log("ROOM: falha ao limpar separation lines: " + ex.Message);
        }
    }

    private static Room CriarRoomNaFaseDaVista(Document doc, Level nivel, ViewPlan vista, XYZ ptDentro)
    {
        Phase fase = null;
        try
        {
            Parameter pp = ((Element)vista).get_Parameter(BuiltInParameter.VIEW_PHASE);
            if (pp != null)
            {
                fase = doc.GetElement(pp.AsElementId()) as Phase;
            }
        }
        catch
        {
        }
        DebugAgua.Log("ROOM: vista='" + vista.Name + "' fase='" + ((fase != null) ? fase.Name : "?") + "'");
        if (fase != null)
        {
            try
            {
                PlanTopology pt = doc.get_PlanTopology(nivel, fase);
                XYZ ptTest = new XYZ(ptDentro.X, ptDentro.Y, nivel.ProjectElevation + 1.0);
                int circuitos = 0;
                int jaLocalizados = 0;
                foreach (PlanCircuit circ in pt.Circuits)
                {
                    circuitos++;
                    if (circ.IsRoomLocated)
                    {
                        jaLocalizados++;
                        DebugAgua.Log("   circ#" + circuitos + " JÁ TEM ROOM (IsRoomLocated=true) — pulado sem testar o ponto");
                        continue;
                    }
                    Room r = null;
                    try
                    {
                        r = doc.Create.NewRoom(null, circ);
                    }
                    catch
                    {
                        r = null;
                    }
                    if (r == null)
                    {
                        continue;
                    }
                    doc.Regenerate();
                    double ar = 0.0;
                    bool in3d = false;
                    bool in2d = false;
                    XYZ loc = null;
                    try
                    {
                        ar = r.Area;
                        loc = (r.Location as LocationPoint)?.Point;
                        if (ar > 0.01)
                        {
                            try
                            {
                                in3d = r.IsPointInRoom(ptTest);
                            }
                            catch
                            {
                            }
                            in2d = PontoDentroDoRoom2D(r, ptDentro);
                        }
                    }
                    catch
                    {
                    }
                    DebugAgua.Log("   circ#" + circuitos + " area=" + Math.Round(ar * 0.092903, 2) + "m² loc=" + ((loc != null) ? ("(" + Math.Round(loc.X * 0.3048, 2) + "," + Math.Round(loc.Y * 0.3048, 2) + ")") : "?") + " in3D=" + in3d + " in2D=" + in2d);
                    if (ar > 0.01 && (in3d || in2d))
                    {
                        DebugAgua.Log("ROOM: criado via PlanTopology (fase='" + fase.Name + "') area=" + Math.Round(ar * 0.092903, 2) + " m²");
                        return r;
                    }
                    try
                    {
                        doc.Delete(r.Id);
                    }
                    catch
                    {
                    }
                }
                DebugAgua.Log("ROOM: PlanTopology fase='" + fase.Name + "' varreu " + circuitos + " circuito(s) (" + jaLocalizados + " já com Room), nenhum dos livres continha o ponto");
            }
            catch (Exception ex)
            {
                DebugAgua.Log("ROOM: falha PlanTopology: " + ex.Message);
            }
        }
        Room rr = doc.Create.NewRoom(nivel, new UV(ptDentro.X, ptDentro.Y));
        doc.Regenerate();
        DebugAgua.Log("ROOM: fallback NewRoom(Level,UV) area=" + ((rr == null) ? "null" : (Math.Round(rr.Area * 0.092903, 2) + " m²")));
        return rr;
    }

    private static bool PontoDentroDoRoom2D(Room r, XYZ pt)
    {
        try
        {
            IList<IList<BoundarySegment>> loops = r.GetBoundarySegments(new SpatialElementBoundaryOptions());
            if (loops == null || loops.Count == 0)
            {
                return false;
            }
            IList<BoundarySegment> contorno = loops.OrderByDescending((IList<BoundarySegment> l) => l.Sum((BoundarySegment s) => s.GetCurve().Length)).First();
            List<XYZ> pts = new List<XYZ>();
            foreach (BoundarySegment seg in contorno)
            {
                Curve c = seg.GetCurve();
                if (c is Line)
                {
                    pts.Add(c.GetEndPoint(0));
                    continue;
                }
                foreach (XYZ tp in c.Tessellate())
                {
                    pts.Add(tp);
                }
            }
            return PontoEmPoligono2D(pts, pt);
        }
        catch
        {
            return false;
        }
    }

    private static bool PontoEmPoligono2D(List<XYZ> poly, XYZ pt)
    {
        bool dentro = false;
        int n = poly.Count;
        int i = 0;
        int j = n - 1;
        while (i < n)
        {
            double xi = poly[i].X;
            double yi = poly[i].Y;
            double xj = poly[j].X;
            double yj = poly[j].Y;
            if (yi > pt.Y != yj > pt.Y && pt.X < (xj - xi) * (pt.Y - yi) / (yj - yi + 1E-12) + xi)
            {
                dentro = !dentro;
            }
            j = i++;
        }
        return dentro;
    }

    private static List<XYZ> SnapContornoAParedes(List<XYZ> poli, List<double[]> segs)
    {
        if (poli == null || poli.Count < 3 || segs == null || segs.Count == 0)
        {
            return poli;
        }
        if (AreaPoligono2D(poli) < 0.0)
        {
            poli.Reverse();
        }
        int n = poli.Count;
        List<XYZ> res = new List<XYZ>(poli);
        for (int i = 0; i < n; i++)
        {
            XYZ A = poli[i];
            XYZ B = poli[(i + 1) % n];
            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.16)
            {
                continue;
            }
            double inx = (0.0 - dy) / len;
            double iny = dx / len;
            bool vertical = Math.Abs(dx) < 0.3 && Math.Abs(dy) > 0.3;
            bool horizontal = Math.Abs(dy) < 0.3 && Math.Abs(dx) > 0.3;
            if (vertical)
            {
                double Xe = (A.X + B.X) * 0.5;
                double yLo = Math.Min(A.Y, B.Y);
                double yHi = Math.Max(A.Y, B.Y);
                double melhorFace = double.NaN;
                double melhorD = 0.82;
                foreach (double[] s in segs)
                {
                    if (Math.Abs(s[2] - s[0]) > 0.3)
                    {
                        continue;
                    }
                    double Xw = (s[0] + s[2]) * 0.5;
                    double sYlo = Math.Min(s[1], s[3]);
                    double sYhi = Math.Max(s[1], s[3]);
                    if (!(sYhi < yLo - 0.5) && !(sYlo > yHi + 0.5))
                    {
                        double face = Xw + (double)Math.Sign(inx) * s[4];
                        double d = Math.Abs(face - Xe);
                        if (d < melhorD)
                        {
                            melhorD = d;
                            melhorFace = face;
                        }
                    }
                }
                if (!double.IsNaN(melhorFace))
                {
                    res[i] = new XYZ(melhorFace, res[i].Y, res[i].Z);
                    res[(i + 1) % n] = new XYZ(melhorFace, res[(i + 1) % n].Y, res[(i + 1) % n].Z);
                }
            }
            else
            {
                if (!horizontal)
                {
                    continue;
                }
                double Ye = (A.Y + B.Y) * 0.5;
                double xLo = Math.Min(A.X, B.X);
                double xHi = Math.Max(A.X, B.X);
                double melhorFace2 = double.NaN;
                double melhorD2 = 0.82;
                foreach (double[] s2 in segs)
                {
                    if (Math.Abs(s2[3] - s2[1]) > 0.3)
                    {
                        continue;
                    }
                    double Yw = (s2[1] + s2[3]) * 0.5;
                    double sXlo = Math.Min(s2[0], s2[2]);
                    double sXhi = Math.Max(s2[0], s2[2]);
                    if (!(sXhi < xLo - 0.5) && !(sXlo > xHi + 0.5))
                    {
                        double face2 = Yw + (double)Math.Sign(iny) * s2[4];
                        double d2 = Math.Abs(face2 - Ye);
                        if (d2 < melhorD2)
                        {
                            melhorD2 = d2;
                            melhorFace2 = face2;
                        }
                    }
                }
                if (!double.IsNaN(melhorFace2))
                {
                    res[i] = new XYZ(res[i].X, melhorFace2, res[i].Z);
                    res[(i + 1) % n] = new XYZ(res[(i + 1) % n].X, melhorFace2, res[(i + 1) % n].Z);
                }
            }
        }
        return res;
    }

    private static double DistPtSeg2D(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double len2 = dx * dx + dy * dy;
        double t = ((len2 > 1E-12) ? (((px - ax) * dx + (py - ay) * dy) / len2) : 0.0);
        if (t < 0.0)
        {
            t = 0.0;
        }
        else if (t > 1.0)
        {
            t = 1.0;
        }
        double cx = ax + t * dx;
        double cy = ay + t * dy;
        double ex = px - cx;
        double ey = py - cy;
        return Math.Sqrt(ex * ex + ey * ey);
    }

    private static double AreaPoligono2D(List<XYZ> p)
    {
        double s = 0.0;
        for (int i = 0; i < p.Count; i++)
        {
            XYZ a = p[i];
            XYZ b = p[(i + 1) % p.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return s * 0.5;
    }

    private static List<XYZ> MesclarColineares(List<XYZ> anel)
    {
        int m = anel.Count;
        if (m < 3)
        {
            return anel;
        }
        List<XYZ> res = new List<XYZ>();
        for (int i = 0; i < m; i++)
        {
            XYZ prev = anel[(i - 1 + m) % m];
            XYZ cur = anel[i];
            XYZ nxt = anel[(i + 1) % m];
            double d1x = cur.X - prev.X;
            double d1y = cur.Y - prev.Y;
            double d2x = nxt.X - cur.X;
            double d2y = nxt.Y - cur.Y;
            double cross = d1x * d2y - d1y * d2x;
            double dot = d1x * d2x + d1y * d2y;
            if (!(Math.Abs(cross) < 1E-06) || !(dot > 0.0))
            {
                res.Add(cur);
            }
        }
        return res;
    }

    private static List<XYZ> SimplificarDP(List<XYZ> pts, double tol)
    {
        if (pts.Count < 4)
        {
            return pts;
        }
        int far = 0;
        double dmax = -1.0;
        for (int i = 1; i < pts.Count; i++)
        {
            double d = pts[0].DistanceTo(pts[i]);
            if (d > dmax)
            {
                dmax = d;
                far = i;
            }
        }
        List<XYZ> a = pts.GetRange(0, far + 1);
        List<XYZ> b = new List<XYZ>();
        for (int j = far; j < pts.Count; j++)
        {
            b.Add(pts[j]);
        }
        b.Add(pts[0]);
        List<XYZ> ra = DPrec(a, tol);
        List<XYZ> rb = DPrec(b, tol);
        List<XYZ> res = new List<XYZ>(ra);
        for (int k = 1; k < rb.Count - 1; k++)
        {
            res.Add(rb[k]);
        }
        return res;
    }

    private static List<XYZ> DPrec(List<XYZ> pts, double tol)
    {
        if (pts.Count < 3)
        {
            return new List<XYZ>(pts);
        }
        double dmax = 0.0;
        int idx = 0;
        XYZ a = pts[0];
        XYZ b = pts[pts.Count - 1];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            double d = DistPtSeg2D(pts[i].X, pts[i].Y, a.X, a.Y, b.X, b.Y);
            if (d > dmax)
            {
                dmax = d;
                idx = i;
            }
        }
        if (dmax > tol)
        {
            List<XYZ> left = DPrec(pts.GetRange(0, idx + 1), tol);
            List<XYZ> right = DPrec(pts.GetRange(idx, pts.Count - idx), tol);
            List<XYZ> res = new List<XYZ>(left);
            res.RemoveAt(res.Count - 1);
            res.AddRange(right);
            return res;
        }
        return new List<XYZ> { a, b };
    }

    private static double[] SnapBordaAParedes(Document doc, double xMin, double yMin, double xMax, double yMax)
    {
        double dxMin = 3.28;
        double dyMin = 3.28;
        double dxMax = 3.28;
        double dyMax = 3.28;
        double locXMin = xMin;
        double locYMin = yMin;
        double locXMax = xMax;
        double locYMax = yMax;
        double espXMin = 0.0;
        double espYMin = 0.0;
        double espXMax = 0.0;
        double espYMax = 0.0;
        try
        {
            IEnumerable<RevitLinkInstance> links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
            foreach (RevitLinkInstance link in links)
            {
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null)
                {
                    continue;
                }
                Transform trf = link.GetTotalTransform();
                IEnumerable<Wall> paredes = new FilteredElementCollector(linkDoc).OfClass(typeof(Wall)).Cast<Wall>();
                foreach (Wall parede in paredes)
                {
                    if (!(parede.Location is LocationCurve lc))
                    {
                        continue;
                    }
                    XYZ s = trf.OfPoint(lc.Curve.GetEndPoint(0));
                    XYZ e = trf.OfPoint(lc.Curve.GetEndPoint(1));
                    double ddx = Math.Abs(e.X - s.X);
                    double ddy = Math.Abs(e.Y - s.Y);
                    double len = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (len < 0.5)
                    {
                        continue;
                    }
                    double esp = 0.0;
                    try
                    {
                        esp = parede.Width;
                    }
                    catch
                    {
                    }
                    bool isVertical = ddy > ddx * (1.0 / Math.Tan(0.17));
                    bool isHorizontal = ddx > ddy * (1.0 / Math.Tan(0.17));
                    if (isVertical)
                    {
                        double wyMin = Math.Min(s.Y, e.Y);
                        double wyMax = Math.Max(s.Y, e.Y);
                        if (!(wyMax < yMin - 1.0) && !(wyMin > yMax + 1.0))
                        {
                            double wx = (s.X + e.X) * 0.5;
                            double dLeft = Math.Abs(wx - xMin);
                            if (dLeft < dxMin)
                            {
                                dxMin = dLeft;
                                locXMin = wx;
                                espXMin = esp;
                            }
                            double dRight = Math.Abs(wx - xMax);
                            if (dRight < dxMax)
                            {
                                dxMax = dRight;
                                locXMax = wx;
                                espXMax = esp;
                            }
                        }
                    }
                    else
                    {
                        if (!isHorizontal)
                        {
                            continue;
                        }
                        double wxMin = Math.Min(s.X, e.X);
                        double wxMax = Math.Max(s.X, e.X);
                        if (!(wxMax < xMin - 1.0) && !(wxMin > xMax + 1.0))
                        {
                            double wy = (s.Y + e.Y) * 0.5;
                            double dBot = Math.Abs(wy - yMin);
                            if (dBot < dyMin)
                            {
                                dyMin = dBot;
                                locYMin = wy;
                                espYMin = esp;
                            }
                            double dTop = Math.Abs(wy - yMax);
                            if (dTop < dyMax)
                            {
                                dyMax = dTop;
                                locYMax = wy;
                                espYMax = esp;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
        double snapXMin = ((dxMin < 3.28) ? (locXMin + espXMin * 0.5) : xMin);
        double snapXMax = ((dxMax < 3.28) ? (locXMax - espXMax * 0.5) : xMax);
        double snapYMin = ((dyMin < 3.28) ? (locYMin + espYMin * 0.5) : yMin);
        double snapYMax = ((dyMax < 3.28) ? (locYMax - espYMax * 0.5) : yMax);
        DebugAgua.Log("   SNAP xMin(esq): " + ((dxMin < 3.28) ? ("parede a " + Math.Round(dxMin * 30.48) + "cm, centro=" + Mt(locXMin) + " esp=" + Math.Round(espXMin * 304.8) + "mm -> face=" + Mt(snapXMin)) : ("sem parede em ~1m (mantém " + Mt(xMin) + ")")));
        DebugAgua.Log("   SNAP xMax(dir): " + ((dxMax < 3.28) ? ("parede a " + Math.Round(dxMax * 30.48) + "cm, centro=" + Mt(locXMax) + " esp=" + Math.Round(espXMax * 304.8) + "mm -> face=" + Mt(snapXMax)) : ("sem parede em ~1m (mantém " + Mt(xMax) + ")")));
        DebugAgua.Log("   SNAP yMin(inf): " + ((dyMin < 3.28) ? ("parede a " + Math.Round(dyMin * 30.48) + "cm, centro=" + Mt(locYMin) + " esp=" + Math.Round(espYMin * 304.8) + "mm -> face=" + Mt(snapYMin)) : ("sem parede em ~1m (mantém " + Mt(yMin) + ")")));
        DebugAgua.Log("   SNAP yMax(sup): " + ((dyMax < 3.28) ? ("parede a " + Math.Round(dyMax * 30.48) + "cm, centro=" + Mt(locYMax) + " esp=" + Math.Round(espYMax * 304.8) + "mm -> face=" + Mt(snapYMax)) : ("sem parede em ~1m (mantém " + Mt(yMax) + ")")));
        if (snapXMin >= snapXMax)
        {
            DebugAgua.Log("   SNAP: X invertido pós-snap — revertido para o desenhado");
            snapXMin = xMin;
            snapXMax = xMax;
        }
        if (snapYMin >= snapYMax)
        {
            DebugAgua.Log("   SNAP: Y invertido pós-snap — revertido para o desenhado");
            snapYMin = yMin;
            snapYMax = yMax;
        }
        return new double[4] { snapXMin, snapYMin, snapXMax, snapYMax };
    }

    private static string Mt(double ft)
    {
        return (ft * 0.3048).ToString("F2", CultureInfo.InvariantCulture) + "m";
    }

    private static bool RoomTamanhoExcessivo(SpatialElement room)
    {
        try
        {
            BoundingBoxXYZ bb = ((Element)room)?.get_BoundingBox((View)null);
            if (bb == null)
            {
                return false;
            }
            double w = Math.Abs(bb.Max.X - bb.Min.X);
            double h = Math.Abs(bb.Max.Y - bb.Min.Y);
            return w > 65.6 || h > 65.6;
        }
        catch
        {
            return false;
        }
    }

    private Outline GetTransformedOutline(XYZ min, XYZ max, Transform t)
    {
        List<XYZ> corners = new List<XYZ>
        {
            new XYZ(min.X, min.Y, min.Z),
            new XYZ(min.X, min.Y, max.Z),
            new XYZ(min.X, max.Y, min.Z),
            new XYZ(min.X, max.Y, max.Z),
            new XYZ(max.X, min.Y, min.Z),
            new XYZ(max.X, min.Y, max.Z),
            new XYZ(max.X, max.Y, min.Z),
            new XYZ(max.X, max.Y, max.Z)
        };
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double minZ = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        double maxZ = double.MinValue;
        foreach (XYZ c in corners)
        {
            XYZ pt = t.OfPoint(c);
            if (pt.X < minX)
            {
                minX = pt.X;
            }
            if (pt.Y < minY)
            {
                minY = pt.Y;
            }
            if (pt.Z < minZ)
            {
                minZ = pt.Z;
            }
            if (pt.X > maxX)
            {
                maxX = pt.X;
            }
            if (pt.Y > maxY)
            {
                maxY = pt.Y;
            }
            if (pt.Z > maxZ)
            {
                maxZ = pt.Z;
            }
        }
        return new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
    }
}
