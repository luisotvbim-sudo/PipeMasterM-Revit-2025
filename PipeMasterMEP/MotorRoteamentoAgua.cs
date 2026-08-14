using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace PipeMasterMEP;

public static class MotorRoteamentoAgua
{
    private class EventoTrilho
    {
        public double Dist;

        public XYZ Pt;

        public int Seg;

        public bool EhDerivacao;

        public bool EhChuveiro;

        public double ZPonto;

        public string Nome;
    }

    public static string GerarRedeAgua(Document doc, SpatialElement ambiente, Transform trfAmbiente, XYZ ptClique, List<PontoConsumoAgua> pontos, ConfigRoteamentoAgua cfg)
    {
        if (pontos == null || pontos.Count == 0)
        {
            throw new Exception("Nenhuma peça selecionada para modelar.");
        }
        List<XYZ> trilho = ConstruirTrilho(ambiente, trfAmbiente, cfg.ZRamal, cfg.RecuoParedePes);
        PrecalcularComprimentos(trilho, out double[] len, out double[] cum, out double L);
        if (L < 1.0)
        {
            throw new Exception("O contorno do ambiente é muito pequeno para rotear.");
        }
        XYZ ptBase;
        int segBase;
        double s0 = ProjetarNoTrilho(ptClique, trilho, len, cum, out ptBase, out segBase);
        string avisoParede = null;
        double distClique = Dist2D(ptClique, ptBase);
        if (distClique > 1.64)
        {
            try
            {
                List<XYZ> trilhoGeo = ConstruirTrilho(ambiente, trfAmbiente, cfg.ZRamal, cfg.RecuoParedePes, usarGeometria: true);
                PrecalcularComprimentos(trilhoGeo, out double[] lenG, out double[] cumG, out double LG);
                if (LG > 1.0)
                {
                    XYZ ptBaseG;
                    int segBaseG;
                    double s0G = ProjetarNoTrilho(ptClique, trilhoGeo, lenG, cumG, out ptBaseG, out segBaseG);
                    double dG = Dist2D(ptClique, ptBaseG);
                    if (dG < distClique - 0.1)
                    {
                        trilho = trilhoGeo;
                        len = lenG;
                        cum = cumG;
                        L = LG;
                        s0 = s0G;
                        ptBase = ptBaseG;
                        segBase = segBaseG;
                        distClique = dG;
                    }
                }
            }
            catch
            {
            }
            if (distClique > 1.64)
            {
                avisoParede = "Atenção: o ponto clicado ficou a " + Math.Round(distClique * 30.48) + " cm do contorno detectado do ambiente. A parede clicada pode não estar delimitando o ambiente — ative 'Delimitar ambiente' na família dessa parede.";
            }
        }
        bool ccw = AreaAssinada2D(trilho) > 0.0;
        List<Tuple<double, EventoTrilho>> pontosNoTrilho = new List<Tuple<double, EventoTrilho>>();
        foreach (PontoConsumoAgua pc in pontos)
        {
            int segPeca;
            double si = ProjetarNoTrilho(pc.Posicao, trilho, len, cum, out _, out segPeca);
            if (Math.Abs(pc.OffsetLateralPes) > 0.001)
            {
                XYZ dirFora = DirecaoParaAmbiente(trilho, segPeca, ccw);
                XYZ dirDireita = dirFora.Negate().CrossProduct(XYZ.BasisZ).Normalize();
                XYZ tLoop = TangenteLoop(trilho, segPeca);
                double deltaS = pc.OffsetLateralPes * dirDireita.DotProduct(tLoop);
                si = ((si + deltaS) % L + L) % L;
            }
            EventoTrilho ev = new EventoTrilho
            {
                EhDerivacao = true,
                ZPonto = pc.ZPonto,
                Nome = pc.Nome,
                EhChuveiro = pc.EhChuveiro
            };
            pontosNoTrilho.Add(Tuple.Create(si, ev));
        }
        bool usarDesvio = cfg.DesviarPeloPiso && cfg.PontoSubidaPiso != null;
        double s2 = 0.0;
        XYZ ptBase2 = null;
        int segBase2 = 0;
        if (usarDesvio)
        {
            s2 = ProjetarNoTrilho(cfg.PontoSubidaPiso, trilho, len, cum, out ptBase2, out segBase2);
        }
        List<EventoTrilho> eventosPos = new List<EventoTrilho>();
        List<EventoTrilho> eventosNeg = new List<EventoTrilho>();
        List<EventoTrilho> eventos2Pos = new List<EventoTrilho>();
        List<EventoTrilho> eventos2Neg = new List<EventoTrilho>();
        foreach (Tuple<double, EventoTrilho> par in pontosNoTrilho)
        {
            double si2 = par.Item1;
            EventoTrilho ev2 = par.Item2;
            if (usarDesvio && DistanciaPerimetro(s2, si2, L) < DistanciaPerimetro(s0, si2, L))
            {
                DistribuirEvento(ev2, s2, si2, L, eventos2Pos, eventos2Neg);
            }
            else
            {
                DistribuirEvento(ev2, s0, si2, L, eventosPos, eventosNeg);
            }
        }
        bool desvioAtivo = usarDesvio && (eventos2Pos.Count > 0 || eventos2Neg.Count > 0);
        int desviados = eventos2Pos.Count + eventos2Neg.Count;
        int tubos = 0;
        int conexoesOk = 0;
        int falhas = 0;
        string avisoJoelho = null;
        Pipe primeiroPos = ((eventosPos.Count > 0) ? MontarRamal(doc, cfg, trilho, len, cum, L, s0, 1, eventosPos, ccw, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho) : null);
        Pipe primeiroNeg = ((eventosNeg.Count > 0) ? MontarRamal(doc, cfg, trilho, len, cum, L, s0, -1, eventosNeg, ccw, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho) : null);
        XYZ dirForaBase = DirecaoParaAmbiente(trilho, segBase, ccw);
        string msgRegistro;
        Pipe prumadaInferior = CriarPrumada(doc, cfg, ptBase, dirForaBase, ref tubos, ref conexoesOk, out msgRegistro);
        Pipe descidaPiso = null;
        if (desvioAtivo)
        {
            Pipe ramal2Pos = ((eventos2Pos.Count > 0) ? MontarRamal(doc, cfg, trilho, len, cum, L, s2, 1, eventos2Pos, ccw, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho) : null);
            Pipe ramal2Neg = ((eventos2Neg.Count > 0) ? MontarRamal(doc, cfg, trilho, len, cum, L, s2, -1, eventos2Neg, ccw, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho) : null);
            XYZ dirFora2 = DirecaoParaAmbiente(trilho, segBase2, ccw);
            descidaPiso = CriarTravessiaPiso(doc, cfg, ptBase, ptBase2, dirForaBase, ref tubos, ref conexoesOk, ref falhas, out Pipe subidaPiso);
            XYZ ptBase2Z = new XYZ(ptBase2.X, ptBase2.Y, cfg.ZRamal);
            MontarJuncaoBase(doc, cfg, ptBase2Z, subidaPiso, null, ramal2Pos, ramal2Neg, dirFora2, ref tubos, ref conexoesOk, ref falhas);
        }
        XYZ ptBaseZ = new XYZ(ptBase.X, ptBase.Y, cfg.ZRamal);
        MontarJuncaoBase(doc, cfg, ptBaseZ, prumadaInferior, descidaPiso, primeiroPos, primeiroNeg, dirForaBase, ref tubos, ref conexoesOk, ref falhas);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(tubos + " tubos e " + conexoesOk + " conexões criados no ambiente.");
        if (falhas > 0)
        {
            sb.AppendLine(falhas + " conexão(ões) não montaram automaticamente — finalize com 'Mover e Conectar'.");
        }
        if (!string.IsNullOrEmpty(msgRegistro))
        {
            sb.AppendLine(msgRegistro);
        }
        if (desvioAtivo)
        {
            sb.AppendLine(desviados + " aparelho(s) alimentado(s) por desvio pelo piso (subida no 2º ponto clicado).");
        }
        if (!string.IsNullOrEmpty(avisoJoelho))
        {
            sb.AppendLine(avisoJoelho);
        }
        if (!string.IsNullOrEmpty(avisoParede))
        {
            sb.AppendLine(avisoParede);
        }
        if (!string.IsNullOrEmpty(cfg.NomeNivel))
        {
            sb.AppendLine("Cotas medidas a partir do nível '" + cfg.NomeNivel + "'.");
        }
        return sb.ToString().Trim();
    }

    private static void MontarJuncaoBase(Document doc, ConfigRoteamentoAgua cfg, XYZ ptoJuncao, Pipe prumada, Pipe desvioPiso, Pipe ramalPos, Pipe ramalNeg, XYZ dirForaBase, ref int tubos, ref int conexoesOk, ref int falhas)
    {
        bool temDesvio = desvioPiso != null;
        bool temPos = ramalPos != null;
        bool temNeg = ramalNeg != null;
        if (!temDesvio)
        {
            if (temPos && temNeg)
            {
                TentarConectar(delegate
                {
                    doc.Create.NewTeeFitting(ConectorEm(ramalPos, ptoJuncao), ConectorEm(ramalNeg, ptoJuncao), ConectorEm(prumada, ptoJuncao));
                }, ref conexoesOk, ref falhas);
            }
            else if (temPos || temNeg)
            {
                TentarConectar(delegate
                {
                    doc.Create.NewElbowFitting(ConectorEm(ramalPos ?? ramalNeg, ptoJuncao), ConectorEm(prumada, ptoJuncao));
                }, ref conexoesOk, ref falhas);
            }
            return;
        }
        if (!temPos && !temNeg)
        {
            TentarConectar(delegate
            {
                ConectorEm(prumada, ptoJuncao).ConnectTo(ConectorEm(desvioPiso, ptoJuncao));
            }, ref conexoesOk, ref falhas);
            return;
        }
        if (temPos != temNeg)
        {
            Pipe ramal = ramalPos ?? ramalNeg;
            TentarConectar(delegate
            {
                doc.Create.NewTeeFitting(ConectorEm(prumada, ptoJuncao), ConectorEm(desvioPiso, ptoJuncao), ConectorEm(ramal, ptoJuncao));
            }, ref conexoesOk, ref falhas);
            return;
        }
        double ponte = 0.49212598425196846;
        XYZ ptPonte = ptoJuncao + dirForaBase * ponte;
        Pipe bridge = CriarTubo(doc, cfg, ptoJuncao, ptPonte, cfg.DiametroRamalPes);
        tubos++;
        TentarConectar(delegate
        {
            doc.Create.NewTeeFitting(ConectorEm(prumada, ptoJuncao), ConectorEm(desvioPiso, ptoJuncao), ConectorEm(bridge, ptoJuncao));
        }, ref conexoesOk, ref falhas);
        TentarConectar(delegate
        {
            doc.Create.NewTeeFitting(ConectorEm(ramalPos, ptPonte), ConectorEm(ramalNeg, ptPonte), ConectorEm(bridge, ptPonte));
        }, ref conexoesOk, ref falhas);
    }

    private static double DistanciaPerimetro(double sRef, double si, double L)
    {
        double d = ((si - sRef) % L + L) % L;
        return Math.Min(d, L - d);
    }

    private static void DistribuirEvento(EventoTrilho ev, double sRef, double si, double L, List<EventoTrilho> pos, List<EventoTrilho> neg)
    {
        double dPos = ((si - sRef) % L + L) % L;
        double dNeg = L - dPos;
        if (dPos <= dNeg)
        {
            ev.Dist = Math.Max(dPos, 0.35);
            pos.Add(ev);
        }
        else
        {
            ev.Dist = Math.Max(dNeg, 0.35);
            neg.Add(ev);
        }
    }

    private static Pipe CriarTravessiaPiso(Document doc, ConfigRoteamentoAgua cfg, XYZ ptBase, XYZ ptBase2, XYZ dirForaBase, ref int tubos, ref int conexoesOk, ref int falhas, out Pipe subida)
    {
        XYZ pBaseTopo = new XYZ(ptBase.X, ptBase.Y, cfg.ZRamal);
        XYZ pBasePiso = new XYZ(ptBase.X, ptBase.Y, cfg.ZPiso);
        XYZ pSubTopo = new XYZ(ptBase2.X, ptBase2.Y, cfg.ZRamal);
        XYZ pSubPiso = new XYZ(ptBase2.X, ptBase2.Y, cfg.ZPiso);
        XYZ fora = new XYZ(dirForaBase.X, dirForaBase.Y, 0.0);
        fora = ((fora.GetLength() > 1E-09) ? fora.Normalize() : XYZ.BasisX);
        XYZ delta = pSubPiso - pBasePiso;
        double comprFora = delta.DotProduct(fora);
        XYZ pCanto = pBasePiso + fora * comprFora;
        Pipe descida = CriarTubo(doc, cfg, pBaseTopo, pBasePiso, cfg.DiametroRamalPes);
        tubos++;
        Pipe anterior = descida;
        XYZ ptAnt = pBasePiso;
        if (Math.Abs(comprFora) > 0.05)
        {
            Pipe travessia = CriarTubo(doc, cfg, pBasePiso, pCanto, cfg.DiametroRamalPes);
            tubos++;
            Pipe a = anterior;
            XYZ pj = ptAnt;
            TentarConectar(delegate
            {
                doc.Create.NewElbowFitting(ConectorEm(a, pj), ConectorEm(travessia, pj));
            }, ref conexoesOk, ref falhas);
            anterior = travessia;
            ptAnt = pCanto;
        }
        if (pCanto.DistanceTo(pSubPiso) > 0.05)
        {
            Pipe corrida = CriarTubo(doc, cfg, pCanto, pSubPiso, cfg.DiametroRamalPes);
            tubos++;
            Pipe a2 = anterior;
            XYZ pj2 = ptAnt;
            TentarConectar(delegate
            {
                doc.Create.NewElbowFitting(ConectorEm(a2, pj2), ConectorEm(corrida, pj2));
            }, ref conexoesOk, ref falhas);
            anterior = corrida;
            ptAnt = pSubPiso;
        }
        Pipe subidaLocal = CriarTubo(doc, cfg, pSubPiso, pSubTopo, cfg.DiametroRamalPes);
        tubos++;
        Pipe aFim = anterior;
        XYZ pjFim = pSubPiso;
        if (anterior == descida)
        {
            TentarConectar(delegate
            {
                ConectorEm(aFim, pjFim).ConnectTo(ConectorEm(subidaLocal, pjFim));
            }, ref conexoesOk, ref falhas);
        }
        else
        {
            TentarConectar(delegate
            {
                doc.Create.NewElbowFitting(ConectorEm(aFim, pjFim), ConectorEm(subidaLocal, pjFim));
            }, ref conexoesOk, ref falhas);
        }
        subida = subidaLocal;
        return descida;
    }

    public static List<PecaAguaDetectada> OrdenarPeloPerimetro(SpatialElement ambiente, Transform trfAmbiente, List<PecaAguaDetectada> pecas)
    {
        if (pecas == null)
        {
            return new List<PecaAguaDetectada>();
        }
        if (pecas.Count < 2)
        {
            return pecas;
        }
        try
        {
            List<XYZ> trilho = ConstruirTrilho(ambiente, trfAmbiente, 0.0, 0.09842519685039369);
            int n = trilho.Count;
            double[] len = new double[n];
            double[] cum = new double[n];
            double L = 0.0;
            for (int i = 0; i < n; i++)
            {
                cum[i] = L;
                len[i] = Dist2D(trilho[i], trilho[(i + 1) % n]);
                L += len[i];
            }
            return pecas.OrderBy((PecaAguaDetectada p) => ProjetarNoTrilho(p.Posicao, trilho, len, cum, out XYZ _, out int _)).ToList();
        }
        catch
        {
            return pecas;
        }
    }

    private static Pipe MontarRamal(Document doc, ConfigRoteamentoAgua cfg, List<XYZ> trilho, double[] len, double[] cum, double L, double s0, int sentido, List<EventoTrilho> derivacoes, bool ccw, ref int tubos, ref int conexoesOk, ref int falhas, ref string avisoJoelho)
    {
        double distMax = derivacoes.Max((EventoTrilho e) => e.Dist);
        List<EventoTrilho> eventos = new List<EventoTrilho>(derivacoes);
        int n = trilho.Count;
        for (int k = 0; k < n; k++)
        {
            double dCanto = ((sentido > 0) ? (((cum[k] - s0) % L + L) % L) : (((s0 - cum[k]) % L + L) % L));
            if (dCanto > 0.01 && dCanto < distMax - 0.01)
            {
                eventos.Add(new EventoTrilho
                {
                    Dist = dCanto,
                    EhDerivacao = false
                });
            }
        }
        List<double> cantos = (from e in eventos
                               where !e.EhDerivacao
                               select e.Dist into d
                               orderby d
                               select d).ToList();
        foreach (EventoTrilho ev in eventos.Where((EventoTrilho e) => e.EhDerivacao))
        {
            foreach (double c in cantos)
            {
                if (Math.Abs(ev.Dist - c) < 0.35)
                {
                    ev.Dist = ((ev.Dist <= c) ? Math.Max(c - 0.35, 0.175) : (c + 0.35));
                }
            }
        }
        eventos = eventos.OrderBy((EventoTrilho e) => e.Dist).ToList();
        foreach (EventoTrilho ev2 in eventos)
        {
            ev2.Pt = PontoNaDistancia(trilho, len, cum, L, s0, ev2.Dist, sentido, out var segEv);
            ev2.Seg = segEv;
        }
        XYZ ptBase = PontoNaDistancia(trilho, len, cum, L, s0, 0.0, sentido);
        List<EventoTrilho> nos = new List<EventoTrilho>();
        XYZ anterior = ptBase;
        foreach (EventoTrilho ev3 in eventos)
        {
            if (ev3.EhDerivacao || !(Dist2D(ev3.Pt, anterior) < 0.05))
            {
                nos.Add(ev3);
                anterior = ev3.Pt;
            }
        }
        if (nos.Count == 0)
        {
            return null;
        }
        List<Pipe> tubosHoriz = new List<Pipe>();
        XYZ ptAtual = ptBase;
        foreach (EventoTrilho ev4 in nos)
        {
            Pipe p = CriarTubo(doc, cfg, ptAtual, ev4.Pt, cfg.DiametroRamalPes);
            tubosHoriz.Add(p);
            tubos++;
            ptAtual = ev4.Pt;
        }
        for (int i = 0; i < nos.Count; i++)
        {
            EventoTrilho ev5 = nos[i];
            Pipe antes = tubosHoriz[i];
            Pipe depois = ((i + 1 < tubosHoriz.Count) ? tubosHoriz[i + 1] : null);
            if (ev5.EhDerivacao)
            {
                XYZ dirFora = DirecaoParaAmbiente(trilho, ev5.Seg, ccw);
                XYZ ptPonto = new XYZ(ev5.Pt.X, ev5.Pt.Y, ev5.ZPonto);
                Pipe vertical = null;
                if (Math.Abs(ev5.ZPonto - cfg.ZRamal) > 0.05)
                {
                    if (cfg.InserirRegistroPressao && ev5.EhChuveiro && ev5.ZPonto > cfg.ZRamal + 0.3 && cfg.ZRegistroPressao > cfg.ZRamal + 0.3 && cfg.ZRegistroPressao < ev5.ZPonto - 0.3)
                    {
                        FamilySymbol simboloP = ((cfg.RegistroPressaoSimboloId != null) ? (doc.GetElement(cfg.RegistroPressaoSimboloId) as FamilySymbol) : null);
                        if (simboloP == null)
                        {
                            simboloP = BuscarSimboloRegistroPressao(doc, cfg.DiametroDescidaPes);
                        }
                        if (simboloP != null && TentarCriarVerticalComValvula(doc, cfg, ev5.Pt, cfg.ZRamal, ev5.ZPonto, cfg.ZRegistroPressao, simboloP, cfg.DiametroDescidaPes, dirFora, ref tubos, ref conexoesOk, out Pipe tBaixo, out Pipe tCima))
                        {
                            vertical = tBaixo;
                            ColocarJoelhoTerminal(doc, cfg, tCima, ptPonto, dirFora, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho);
                        }
                    }
                    if (vertical == null)
                    {
                        vertical = CriarTubo(doc, cfg, ev5.Pt, ptPonto, cfg.DiametroDescidaPes);
                        tubos++;
                        ColocarJoelhoTerminal(doc, cfg, vertical, ptPonto, dirFora, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho);
                    }
                }
                if (depois == null)
                {
                    if (vertical != null)
                    {
                        Pipe vFinal = vertical;
                        Pipe aFinal = antes;
                        TentarConectar(delegate
                        {
                            doc.Create.NewElbowFitting(ConectorEm(aFinal, ev5.Pt), ConectorEm(vFinal, ev5.Pt));
                        }, ref conexoesOk, ref falhas);
                    }
                    else
                    {
                        ColocarJoelhoTerminal(doc, cfg, antes, ev5.Pt, dirFora, ref tubos, ref conexoesOk, ref falhas, ref avisoJoelho);
                    }
                }
                else if (vertical != null)
                {
                    Pipe vMeio = vertical;
                    Pipe aMeio = antes;
                    Pipe dMeio = depois;
                    TentarConectar(delegate
                    {
                        doc.Create.NewTeeFitting(ConectorEm(aMeio, ev5.Pt), ConectorEm(dMeio, ev5.Pt), ConectorEm(vMeio, ev5.Pt));
                    }, ref conexoesOk, ref falhas);
                }
                else
                {
                    ColocarTeTerminal(doc, cfg, antes, depois, ev5.Pt, dirFora, ref conexoesOk, ref falhas, ref avisoJoelho);
                }
            }
            else if (depois != null)
            {
                Pipe aCanto = antes;
                Pipe dCanto2 = depois;
                TentarConectar(delegate
                {
                    doc.Create.NewElbowFitting(ConectorEm(aCanto, ev5.Pt), ConectorEm(dCanto2, ev5.Pt));
                }, ref conexoesOk, ref falhas);
            }
        }
        return tubosHoriz[0];
    }

    private static Pipe CriarPrumada(Document doc, ConfigRoteamentoAgua cfg, XYZ ptBase, XYZ dirFora, ref int tubos, ref int conexoesOk, out string msgRegistro)
    {
        msgRegistro = null;
        XYZ ptTopo = new XYZ(ptBase.X, ptBase.Y, cfg.ZTopoPrumada);
        XYZ ptFundo = new XYZ(ptBase.X, ptBase.Y, cfg.ZRamal);
        bool registroViavel = cfg.InserirRegistro && cfg.ZRegistro > cfg.ZRamal + 0.5 && cfg.ZRegistro < cfg.ZTopoPrumada - 0.5;
        FamilySymbol simbolo = null;
        if (registroViavel)
        {
            if (cfg.RegistroSimboloId != null)
            {
                simbolo = doc.GetElement(cfg.RegistroSimboloId) as FamilySymbol;
            }
            if (simbolo == null)
            {
                simbolo = BuscarSimboloRegistro(doc, cfg.DiametroRamalPes);
            }
        }
        if (cfg.InserirRegistro && registroViavel && simbolo == null)
        {
            msgRegistro = "Nenhuma família de registro (Acessório de Tubo) carregada no projeto — prumada criada sem registro.";
        }
        if (simbolo != null)
        {
            if (TentarCriarVerticalComValvula(doc, cfg, ptBase, cfg.ZTopoPrumada, cfg.ZRamal, cfg.ZRegistro, simbolo, cfg.DiametroRamalPes, dirFora, ref tubos, ref conexoesOk, out Pipe _, out Pipe tuboInf))
            {
                msgRegistro = "Registro '" + simbolo.FamilyName + " - " + simbolo.Name + "' inserido na prumada.";
                return tuboInf;
            }
            msgRegistro = "A família de registro não pôde ser montada automaticamente — prumada criada contínua.";
        }
        Pipe unico = CriarTubo(doc, cfg, ptTopo, ptFundo, cfg.DiametroRamalPes);
        tubos++;
        return unico;
    }

    private static bool TentarCriarVerticalComValvula(Document doc, ConfigRoteamentoAgua cfg, XYZ ptXY, double zA, double zB, double zValvula, FamilySymbol simbolo, double diametro, XYZ dirFora, ref int tubos, ref int conexoesOk, out Pipe tuboLadoA, out Pipe tuboLadoB)
    {
        tuboLadoA = null;
        tuboLadoB = null;
        XYZ ptA = new XYZ(ptXY.X, ptXY.Y, zA);
        XYZ ptB = new XYZ(ptXY.X, ptXY.Y, zB);
        XYZ ptV = new XYZ(ptXY.X, ptXY.Y, zValvula);
        Pipe tA = null;
        Pipe tB = null;
        FamilyInstance valvula = null;
        try
        {
            tA = CriarTubo(doc, cfg, ptA, ptV, diametro);
            tB = CriarTubo(doc, cfg, ptV, ptB, diametro);
            if (!simbolo.IsActive)
            {
                simbolo.Activate();
                doc.Regenerate();
            }
            valvula = CriarInstanciaNoNivel(doc, cfg, ptV, simbolo);
            doc.Regenerate();
            List<Connector> conns = ObterConectoresPiping(valvula);
            if (conns.Count < 2)
            {
                throw new Exception("válvula sem dois conectores hidráulicos");
            }
            XYZ eixo = conns[0].Origin - conns[1].Origin;
            if (eixo.GetLength() < 1E-06)
            {
                eixo = conns[0].CoordinateSystem.BasisZ;
            }
            eixo = eixo.Normalize();
            if (Math.Abs(eixo.DotProduct(XYZ.BasisZ)) < 0.999)
            {
                XYZ eixoRot = eixo.CrossProduct(XYZ.BasisZ);
                if (eixoRot.GetLength() < 1E-06)
                {
                    eixoRot = XYZ.BasisX;
                }
                ElementTransformUtils.RotateElement(doc, valvula.Id, Line.CreateBound(ptV, ptV + eixoRot.Normalize()), eixo.AngleTo(XYZ.BasisZ));
                doc.Regenerate();
                conns = ObterConectoresPiping(valvula);
            }
            XYZ centroCon = (conns[0].Origin + conns[1].Origin) / 2.0;
            XYZ locP = (valvula.Location as LocationPoint)?.Point ?? centroCon;
            XYZ delta = new XYZ(ptV.X - centroCon.X, ptV.Y - centroCon.Y, ptV.Z - locP.Z);
            if (delta.GetLength() > 1E-06)
            {
                ElementTransformUtils.MoveElement(doc, valvula.Id, delta);
                doc.Regenerate();
                conns = ObterConectoresPiping(valvula);
            }
            Transform tfPos = valvula.GetTransform();
            if (dirFora != null)
            {
                XYZ alvo2 = HorizontalOuNulo(dirFora);
                XYZ canopla = HorizontalOuNulo(tfPos.BasisZ);
                if (canopla == null)
                {
                    canopla = HorizontalOuNulo(tfPos.BasisY);
                }
                if (canopla == null)
                {
                    canopla = HorizontalOuNulo(tfPos.BasisX);
                }
                if (alvo2 != null && canopla != null)
                {
                    double ang = Math.Atan2(canopla.CrossProduct(alvo2).Z, canopla.DotProduct(alvo2));
                    if (Math.Abs(ang) > 0.001)
                    {
                        ElementTransformUtils.RotateElement(doc, valvula.Id, Line.CreateBound(ptV, ptV + XYZ.BasisZ), ang);
                        doc.Regenerate();
                        conns = ObterConectoresPiping(valvula);
                    }
                }
            }
            Connector cSup = ((conns[0].Origin.Z >= conns[1].Origin.Z) ? conns[0] : conns[1]);
            Connector cInf = ((cSup == conns[0]) ? conns[1] : conns[0]);
            Connector cLadoA = ((zA >= zB) ? cSup : cInf);
            Connector cLadoB = ((cLadoA == cSup) ? cInf : cSup);
            (tA.Location as LocationCurve).Curve = Line.CreateBound(ptA, cLadoA.Origin);
            (tB.Location as LocationCurve).Curve = Line.CreateBound(cLadoB.Origin, ptB);
            doc.Regenerate();
            ConectorEm(tA, cLadoA.Origin).ConnectTo(cLadoA);
            ConectorEm(tB, cLadoB.Origin).ConnectTo(cLadoB);
            tubos += 2;
            conexoesOk += 2;
            tuboLadoA = tA;
            tuboLadoB = tB;
            return true;
        }
        catch
        {
            try
            {
                if (valvula != null)
                {
                    doc.Delete(valvula.Id);
                }
            }
            catch
            {
            }
            try
            {
                if (tA != null)
                {
                    doc.Delete(tA.Id);
                }
            }
            catch
            {
            }
            try
            {
                if (tB != null)
                {
                    doc.Delete(tB.Id);
                }
            }
            catch
            {
            }
            return false;
        }
    }

    public static FamilySymbol BuscarSimboloRegistro(Document doc, double diametroPes)
    {
        List<FamilySymbol> simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeAccessory).Cast<FamilySymbol>()
            .ToList();
        string bitola = BitolaPolegadas(diametroPes);
        char[] separadores = new char[9] { ' ', '-', '_', '(', ')', '[', ']', ',', ';' };
        FamilySymbol melhor = null;
        int melhorPontos = 0;
        foreach (FamilySymbol s in simbolos)
        {
            string nome = (s.FamilyName + " " + s.Name).ToLower();
            string[] tokens = nome.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
            int pontos = 0;
            if (nome.Contains("registro"))
            {
                pontos += 2;
            }
            if (nome.Contains("gaveta"))
            {
                pontos += 8;
            }
            else if (nome.Contains("esfera"))
            {
                pontos += 3;
            }
            bool ehAF = Enumerable.Contains(tokens, "af") || nome.Contains("água fria") || nome.Contains("agua fria") || Enumerable.Contains(tokens, "fria");
            bool ehAQ = Enumerable.Contains(tokens, "aq") || nome.Contains("quente");
            if (ehAF)
            {
                pontos += 6;
            }
            if (ehAQ)
            {
                pontos -= 20;
            }
            if (nome.Contains("press"))
            {
                pontos -= 15;
            }
            if (bitola != null && nome.Contains(bitola))
            {
                pontos += 3;
            }
            if (pontos > melhorPontos)
            {
                melhorPontos = pontos;
                melhor = s;
            }
        }
        return melhor;
    }

    public static FamilySymbol BuscarSimboloRegistroPressao(Document doc, double diametroPes)
    {
        List<FamilySymbol> simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeAccessory).Cast<FamilySymbol>()
            .ToList();
        string bitola = BitolaPolegadas(diametroPes);
        char[] separadores = new char[9] { ' ', '-', '_', '(', ')', '[', ']', ',', ';' };
        FamilySymbol melhor = null;
        int melhorPontos = 0;
        foreach (FamilySymbol s in simbolos)
        {
            string nome = (s.FamilyName + " " + s.Name).ToLower();
            if (nome.Contains("press"))
            {
                string[] tokens = nome.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                int pontos = 8;
                if (nome.Contains("registro"))
                {
                    pontos += 2;
                }
                bool ehAF = Enumerable.Contains(tokens, "af") || nome.Contains("água fria") || nome.Contains("agua fria") || Enumerable.Contains(tokens, "fria");
                bool ehAQ = Enumerable.Contains(tokens, "aq") || nome.Contains("quente");
                if (ehAF)
                {
                    pontos += 4;
                }
                if (ehAQ)
                {
                    pontos -= 20;
                }
                if (bitola != null && nome.Contains(bitola))
                {
                    pontos += 3;
                }
                if (pontos > melhorPontos)
                {
                    melhorPontos = pontos;
                    melhor = s;
                }
            }
        }
        return melhor;
    }

    private static List<Connector> ObterConectoresPiping(FamilyInstance inst)
    {
        List<Connector> lista = new List<Connector>();
        if (inst.MEPModel != null && inst.MEPModel.ConnectorManager != null)
        {
            foreach (Connector c in inst.MEPModel.ConnectorManager.Connectors)
            {
                if (c.Domain == Domain.DomainPiping)
                {
                    lista.Add(c);
                }
            }
        }
        return lista;
    }

    private static void PrecalcularComprimentos(List<XYZ> trilho, out double[] len, out double[] cum, out double L)
    {
        int n = trilho.Count;
        len = new double[n];
        cum = new double[n];
        L = 0.0;
        for (int i = 0; i < n; i++)
        {
            cum[i] = L;
            len[i] = Dist2D(trilho[i], trilho[(i + 1) % n]);
            L += len[i];
        }
    }

    private static List<XYZ> ConstruirTrilho(SpatialElement ambiente, Transform trf, double zRamal, double recuoPes, bool usarGeometria = false)
    {
        List<XYZ> pts = null;
        try
        {
            if (usarGeometria)
            {
                throw new Exception("forçado contorno geométrico");
            }
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };
            IList<IList<BoundarySegment>> limites = ambiente.GetBoundarySegments(options);
            if (limites != null && limites.Count > 0)
            {
                IList<BoundarySegment> contorno = limites.OrderByDescending((IList<BoundarySegment> l) => l.Sum((BoundarySegment s) => s.GetCurve().Length)).First();
                pts = new List<XYZ>();
                foreach (BoundarySegment seg in contorno)
                {
                    Curve c = seg.GetCurve();
                    if (c is Line)
                    {
                        pts.Add(Achatar(trf.OfPoint(c.GetEndPoint(0)), zRamal));
                        continue;
                    }
                    IList<XYZ> tess = c.Tessellate();
                    for (int i = 0; i < tess.Count - 1; i++)
                    {
                        pts.Add(Achatar(trf.OfPoint(tess[i]), zRamal));
                    }
                }
                pts = LimparVertices(pts);
                if (pts.Count < 3)
                {
                    pts = null;
                }
            }
        }
        catch
        {
            pts = null;
        }
        if (pts == null)
        {
            pts = ContornoPelaGeometria(ambiente, trf, zRamal);
        }
        if (pts == null || pts.Count < 3)
        {
            throw new Exception("Não foi possível ler o contorno do ambiente. Verifique se os elementos que fecham o ambiente estão com 'Delimitar ambiente' ativado.");
        }
        try
        {
            List<Curve> linhas = new List<Curve>();
            for (int i2 = 0; i2 < pts.Count; i2++)
            {
                linhas.Add(Line.CreateBound(pts[i2], pts[(i2 + 1) % pts.Count]));
            }
            CurveLoop loop = CurveLoop.Create(linhas);
            CurveLoop o1 = null;
            CurveLoop o2 = null;
            try
            {
                o1 = CurveLoop.CreateViaOffset(loop, recuoPes, XYZ.BasisZ);
            }
            catch
            {
            }
            try
            {
                o2 = CurveLoop.CreateViaOffset(loop, 0.0 - recuoPes, XYZ.BasisZ);
            }
            catch
            {
            }
            double c2 = o1?.Sum((Curve curve) => curve.Length) ?? (-1.0);
            double c3 = o2?.Sum((Curve curve) => curve.Length) ?? (-1.0);
            CurveLoop maior = ((c2 >= c3) ? o1 : o2);
            if (maior != null)
            {
                List<XYZ> novos = new List<XYZ>();
                foreach (Curve cv in maior)
                {
                    novos.Add(Achatar(cv.GetEndPoint(0), zRamal));
                }
                novos = LimparVertices(novos);
                if (novos.Count >= 3)
                {
                    pts = novos;
                }
            }
        }
        catch
        {
        }
        return pts;
    }

    private static List<XYZ> ContornoPelaGeometria(SpatialElement ambiente, Transform trf, double z)
    {
        try
        {
            GeometryElement geom = ((Element)ambiente).get_Geometry(new Options());
            if (geom == null)
            {
                return null;
            }
            foreach (GeometryObject go in geom)
            {
                Solid solid = go as Solid;
                if (solid == null || solid.Volume <= 0.0)
                {
                    continue;
                }
                foreach (Face face in solid.Faces)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf == null || pf.FaceNormal.Z > -0.9)
                    {
                        continue;
                    }
                    IList<CurveLoop> loops = pf.GetEdgesAsCurveLoops();
                    CurveLoop maior = loops.OrderByDescending((CurveLoop l) => l.Sum((Curve curve) => curve.Length)).FirstOrDefault();
                    if (maior == null)
                    {
                        continue;
                    }
                    List<XYZ> pts = new List<XYZ>();
                    foreach (Curve c in maior)
                    {
                        if (c is Line)
                        {
                            pts.Add(Achatar(trf.OfPoint(c.GetEndPoint(0)), z));
                            continue;
                        }
                        IList<XYZ> tess = c.Tessellate();
                        for (int i = 0; i < tess.Count - 1; i++)
                        {
                            pts.Add(Achatar(trf.OfPoint(tess[i]), z));
                        }
                    }
                    pts = LimparVertices(pts);
                    if (pts.Count < 3)
                    {
                        continue;
                    }
                    return pts;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static List<XYZ> LimparVertices(List<XYZ> pts)
    {
        List<XYZ> semDup = new List<XYZ>();
        foreach (XYZ p in pts)
        {
            if (semDup.Count == 0 || Dist2D(semDup[semDup.Count - 1], p) > 0.01)
            {
                semDup.Add(p);
            }
        }
        if (semDup.Count > 1 && Dist2D(semDup[0], semDup[semDup.Count - 1]) < 0.01)
        {
            semDup.RemoveAt(semDup.Count - 1);
        }
        if (semDup.Count < 3)
        {
            return semDup;
        }
        List<XYZ> final = new List<XYZ>();
        int n = semDup.Count;
        for (int i = 0; i < n; i++)
        {
            XYZ prev = semDup[(i - 1 + n) % n];
            XYZ cur = semDup[i];
            XYZ next = semDup[(i + 1) % n];
            XYZ v1 = cur - prev;
            XYZ v2 = next - cur;
            if (!(v1.GetLength() < 1E-09) && !(v2.GetLength() < 1E-09) && !(v1.Normalize().DotProduct(v2.Normalize()) > 0.9995))
            {
                final.Add(cur);
            }
        }
        return (final.Count >= 3) ? final : semDup;
    }

    private static double ProjetarNoTrilho(XYZ p, List<XYZ> trilho, double[] len, double[] cum, out XYZ ptProj, out int segProj)
    {
        int n = trilho.Count;
        double melhorS = 0.0;
        double melhorDist = double.MaxValue;
        XYZ melhorPt = trilho[0];
        segProj = 0;
        for (int i = 0; i < n; i++)
        {
            XYZ a = trilho[i];
            XYZ b = trilho[(i + 1) % n];
            double lx = b.X - a.X;
            double ly = b.Y - a.Y;
            double l2 = lx * lx + ly * ly;
            double t = ((l2 < 1E-12) ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * lx + (p.Y - a.Y) * ly) / l2)));
            XYZ q = new XYZ(a.X + lx * t, a.Y + ly * t, a.Z);
            double d = (p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y);
            if (d < melhorDist)
            {
                melhorDist = d;
                melhorPt = q;
                melhorS = cum[i] + len[i] * t;
                segProj = i;
            }
        }
        ptProj = melhorPt;
        return melhorS;
    }

    private static XYZ PontoNaDistancia(List<XYZ> trilho, double[] len, double[] cum, double L, double s0, double dist, int sentido)
    {
        return PontoNaDistancia(trilho, len, cum, L, s0, dist, sentido, out _);
    }

    private static XYZ PontoNaDistancia(List<XYZ> trilho, double[] len, double[] cum, double L, double s0, double dist, int sentido, out int seg)
    {
        double s1 = ((sentido > 0) ? ((s0 + dist) % L) : (((s0 - dist) % L + L) % L));
        int n = trilho.Count;
        for (int i = 0; i < n; i++)
        {
            if (s1 <= cum[i] + len[i] + 1E-09)
            {
                double t = ((len[i] < 1E-12) ? 0.0 : ((s1 - cum[i]) / len[i]));
                XYZ a = trilho[i];
                XYZ b = trilho[(i + 1) % n];
                seg = i;
                return new XYZ(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z);
            }
        }
        seg = 0;
        return trilho[0];
    }

    private static void ColocarJoelhoTerminal(Document doc, ConfigRoteamentoAgua cfg, Pipe tuboChegada, XYZ ptFim, XYZ dirFora, ref int tubos, ref int conexoesOk, ref int falhas, ref string avisoJoelho)
    {
        if (tuboChegada == null)
        {
            return;
        }
        Curve curvaOriginal = (tuboChegada.Location as LocationCurve).Curve;
        XYZ e0 = curvaOriginal.GetEndPoint(0);
        XYZ e1 = curvaOriginal.GetEndPoint(1);
        XYZ ptFixo = ((e0.DistanceTo(ptFim) > e1.DistanceTo(ptFim)) ? e0 : e1);
        XYZ dirConector = (ptFixo - ptFim).Normalize();
        List<FamilySymbol> candidatos = BuscarCandidatosJoelhoBucha(doc, cfg.DiametroDescidaPes);
        foreach (FamilySymbol simbolo in candidatos)
        {
            FamilyInstance joelho = null;
            try
            {
                if (!simbolo.IsActive)
                {
                    simbolo.Activate();
                    doc.Regenerate();
                }
                joelho = CriarInstanciaNoNivel(doc, cfg, ptFim, simbolo);
                doc.Regenerate();
                List<Connector> conns = ObterConectoresPiping(joelho);
                if (conns.Count < 2)
                {
                    throw new Exception("joelho sem dois conectores hidráulicos");
                }
                Connector cTubo = (cfg.InverterSentidoBucha ? conns.OrderBy((Connector c) => c.Radius) : conns.OrderByDescending((Connector c) => c.Radius)).First();
                XYZ d1 = cTubo.CoordinateSystem.BasisZ.Normalize();
                double angV = d1.AngleTo(dirConector);
                if (angV > 0.0001)
                {
                    XYZ eixo = d1.CrossProduct(dirConector);
                    if (eixo.GetLength() < 1E-09)
                    {
                        eixo = ObterPerpendicular(d1);
                    }
                    ElementTransformUtils.RotateElement(doc, joelho.Id, Line.CreateBound(ptFim, ptFim + eixo.Normalize()), angV);
                    doc.Regenerate();
                }
                conns = ObterConectoresPiping(joelho);
                Connector cT = PegarConector(conns, dirConector);
                Connector cS = conns.First((Connector c) => c != cT);
                XYZ d2 = cS.CoordinateSystem.BasisZ;
                XYZ d2p = d2 - dirConector * d2.DotProduct(dirConector);
                XYZ dfp = dirFora - dirConector * dirFora.DotProduct(dirConector);
                if (d2p.GetLength() > 1E-06 && dfp.GetLength() > 1E-06)
                {
                    d2p = d2p.Normalize();
                    dfp = dfp.Normalize();
                    double angH = Math.Atan2(d2p.CrossProduct(dfp).DotProduct(dirConector), d2p.DotProduct(dfp));
                    if (Math.Abs(angH) > 0.0001)
                    {
                        ElementTransformUtils.RotateElement(doc, joelho.Id, Line.CreateBound(ptFim, ptFim + dirConector), angH);
                        doc.Regenerate();
                    }
                }
                conns = ObterConectoresPiping(joelho);
                cT = PegarConector(conns, dirConector);
                cS = conns.First((Connector c) => c != cT);
                XYZ canto = cS.Origin + dirFora * (cT.Origin - cS.Origin).DotProduct(dirFora);
                if (canto.DistanceTo(ptFim) > 1E-05)
                {
                    ElementTransformUtils.MoveElement(doc, joelho.Id, ptFim - canto);
                    doc.Regenerate();
                }
                conns = ObterConectoresPiping(joelho);
                cT = PegarConector(conns, dirConector);
                (tuboChegada.Location as LocationCurve).Curve = Line.CreateBound(ptFixo, cT.Origin);
                doc.Regenerate();
                ConectorEm(tuboChegada, cT.Origin).ConnectTo(cT);
                conexoesOk++;
                return;
            }
            catch
            {
                try
                {
                    if (joelho != null)
                    {
                        doc.Delete(joelho.Id);
                    }
                }
                catch
                {
                }
                try
                {
                    (tuboChegada.Location as LocationCurve).Curve = Line.CreateBound(ptFixo, ptFim);
                }
                catch
                {
                }
            }
        }
        if (avisoJoelho == null)
        {
            avisoJoelho = ((candidatos.Count == 0) ? "Família 'Joelho de 90° com Bucha de Latão' (Conexão de Tubo) não encontrada no projeto — joelho padrão aplicado nas saídas." : ("Nenhum tipo do joelho de 90° com bucha de latão bateu com a bitola da descida (" + Math.Round(cfg.DiametroDescidaPes * 304.8) + "mm) — carregue esse tipo no projeto. Joelho padrão aplicado nas saídas."));
        }
        try
        {
            XYZ ptSaida = ptFim + dirFora * (125.0 / 762.0);
            Pipe toco = CriarTubo(doc, cfg, ptFim, ptSaida, cfg.DiametroDescidaPes);
            tubos++;
            Pipe tuboRef = tuboChegada;
            TentarConectar(delegate
            {
                doc.Create.NewElbowFitting(ConectorEm(tuboRef, ptFim), ConectorEm(toco, ptFim));
            }, ref conexoesOk, ref falhas);
        }
        catch
        {
            falhas++;
        }
    }

    private static void ColocarTeTerminal(Document doc, ConfigRoteamentoAgua cfg, Pipe antes, Pipe depois, XYZ ptNo, XYZ dirFora, ref int conexoesOk, ref int falhas, ref string avisoJoelho)
    {
        Curve cAntes = (antes.Location as LocationCurve).Curve;
        XYZ eA0 = cAntes.GetEndPoint(0);
        XYZ eA1 = cAntes.GetEndPoint(1);
        XYZ ptFixoAntes = ((eA0.DistanceTo(ptNo) > eA1.DistanceTo(ptNo)) ? eA0 : eA1);
        Curve cDepois = (depois.Location as LocationCurve).Curve;
        XYZ eD0 = cDepois.GetEndPoint(0);
        XYZ eD1 = cDepois.GetEndPoint(1);
        XYZ ptFixoDepois = ((eD0.DistanceTo(ptNo) > eD1.DistanceTo(ptNo)) ? eD0 : eD1);
        XYZ dirLinha = ptFixoDepois - ptFixoAntes;
        if (dirLinha.GetLength() > 1E-06)
        {
            dirLinha = dirLinha.Normalize();
            List<FamilySymbol> candidatos = BuscarCandidatosTeBucha(doc, cfg.DiametroRamalPes, cfg.DiametroDescidaPes);
            DebugAgua.Log("ColocarTeTerminal em Z=" + Math.Round(ptNo.Z * 30.48) + "cm dirLinha=(" + dirLinha.X.ToString("F2") + "," + dirLinha.Y.ToString("F2") + ") dirFora=(" + dirFora.X.ToString("F2") + "," + dirFora.Y.ToString("F2") + ") — " + candidatos.Count + " candidato(s), ØramalMM=" + Math.Round(cfg.DiametroRamalPes * 304.8) + " ØdescidaMM=" + Math.Round(cfg.DiametroDescidaPes * 304.8));
            foreach (FamilySymbol simbolo in candidatos)
            {
                FamilyInstance te = null;
                try
                {
                    if (!simbolo.IsActive)
                    {
                        simbolo.Activate();
                        doc.Regenerate();
                    }
                    te = CriarInstanciaNoNivel(doc, cfg, ptNo, simbolo);
                    doc.Regenerate();
                    if (!IdentificarPapeisTe(te, out Connector cA, out Connector cB, out Connector cBranch))
                    {
                        DebugAgua.Log("   '" + simbolo.FamilyName + " / " + simbolo.Name + "': IdentificarPapeisTe FALHOU (nConectores=" + ObterConectoresPiping(te).Count + ")");
                        throw new Exception("tê sem par de conectores colineares (não é um tê reto de 3 vias)");
                    }
                    DebugAgua.Log("   '" + simbolo.FamilyName + " / " + simbolo.Name + "': cA=" + Math.Round(2.0 * cA.Radius * 304.8) + "mm cB=" + Math.Round(2.0 * cB.Radius * 304.8) + "mm cBranch=" + Math.Round(2.0 * cBranch.Radius * 304.8) + "mm (alvo reta=" + Math.Round(cfg.DiametroRamalPes * 304.8) + "mm ramo=" + Math.Round(cfg.DiametroDescidaPes * 304.8) + "mm)");
                    DebugAgua.Log("   '" + simbolo.FamilyName + " / " + simbolo.Name + "': conectores antes=" + Math.Round(2.0 * cA.Radius * 304.8) + "mm|" + Math.Round(2.0 * cB.Radius * 304.8) + "mm|" + Math.Round(2.0 * cBranch.Radius * 304.8) + "mm — prosseguindo sem trava de bitola");
                    XYZ eixoReta = cA.CoordinateSystem.BasisZ.Normalize();
                    double angV = eixoReta.AngleTo(dirLinha);
                    if (angV > 0.0001)
                    {
                        XYZ eixoRot = eixoReta.CrossProduct(dirLinha);
                        if (eixoRot.GetLength() < 1E-09)
                        {
                            eixoRot = ObterPerpendicular(eixoReta);
                        }
                        ElementTransformUtils.RotateElement(doc, te.Id, Line.CreateBound(ptNo, ptNo + eixoRot.Normalize()), angV);
                        doc.Regenerate();
                    }
                    if (!IdentificarPapeisTe(te, out cA, out cB, out cBranch))
                    {
                        throw new Exception("papéis perdidos após alinhar");
                    }
                    XYZ dBranchP = cBranch.CoordinateSystem.BasisZ - dirLinha * cBranch.CoordinateSystem.BasisZ.DotProduct(dirLinha);
                    XYZ dForaP = dirFora - dirLinha * dirFora.DotProduct(dirLinha);
                    if (dBranchP.GetLength() > 1E-06 && dForaP.GetLength() > 1E-06)
                    {
                        dBranchP = dBranchP.Normalize();
                        dForaP = dForaP.Normalize();
                        double angH = Math.Atan2(dBranchP.CrossProduct(dForaP).DotProduct(dirLinha), dBranchP.DotProduct(dForaP));
                        if (Math.Abs(angH) > 0.0001)
                        {
                            ElementTransformUtils.RotateElement(doc, te.Id, Line.CreateBound(ptNo, ptNo + dirLinha), angH);
                            doc.Regenerate();
                        }
                    }
                    if (!IdentificarPapeisTe(te, out cA, out cB, out cBranch))
                    {
                        throw new Exception("papéis perdidos após girar");
                    }
                    XYZ proj = cA.Origin + dirLinha * (cBranch.Origin - cA.Origin).DotProduct(dirLinha);
                    if (proj.DistanceTo(ptNo) > 1E-05)
                    {
                        ElementTransformUtils.MoveElement(doc, te.Id, ptNo - proj);
                        doc.Regenerate();
                    }
                    if (!IdentificarPapeisTe(te, out cA, out cB, out cBranch))
                    {
                        throw new Exception("papéis perdidos após posicionar");
                    }
                    Connector cDepoisConn = ((cA.CoordinateSystem.BasisZ.DotProduct(dirLinha) > 0.0) ? cA : cB);
                    Connector cAntesConn = ((cDepoisConn == cA) ? cB : cA);
                    (antes.Location as LocationCurve).Curve = Line.CreateBound(ptFixoAntes, cAntesConn.Origin);
                    (depois.Location as LocationCurve).Curve = Line.CreateBound(cDepoisConn.Origin, ptFixoDepois);
                    doc.Regenerate();
                    ConectorEm(antes, cAntesConn.Origin).ConnectTo(cAntesConn);
                    ConectorEm(depois, cDepoisConn.Origin).ConnectTo(cDepoisConn);
                    conexoesOk += 2;
                    try
                    {
                        doc.Regenerate();
                        double raioRamalPes = cfg.DiametroRamalPes / 2.0;
                        double raioDescidaPes = cfg.DiametroDescidaPes / 2.0;
                        bool resizou = false;
                        Parameter pR1 = te.LookupParameter("Raio Nominal 1");
                        Parameter pR2 = te.LookupParameter("Raio Nominal 2");
                        if (pR1 != null && !pR1.IsReadOnly)
                        {
                            pR1.Set(raioRamalPes);
                            resizou = true;
                        }
                        if (pR2 != null && !pR2.IsReadOnly)
                        {
                            pR2.Set(raioDescidaPes);
                            resizou = true;
                        }
                        if (resizou)
                        {
                            doc.Regenerate();
                            DebugAgua.Log("   resize OK: Raio Nominal 1=" + Math.Round(raioRamalPes * 304.8, 1) + "mm  Raio Nominal 2=" + Math.Round(raioDescidaPes * 304.8, 1) + "mm");
                        }
                        else
                        {
                            Parameter pDiam = ((Element)te).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                            if (pDiam == null || pDiam.IsReadOnly)
                            {
                                pDiam = te.LookupParameter("DN") ?? te.LookupParameter("Nominal Diameter") ?? te.LookupParameter("Diâmetro Nominal");
                            }
                            if (pDiam != null && !pDiam.IsReadOnly)
                            {
                                pDiam.Set(cfg.DiametroRamalPes);
                                doc.Regenerate();
                                DebugAgua.Log("   resize OK via fallback '" + pDiam.Definition.Name + "'");
                            }
                            else
                            {
                                DebugAgua.Log("   resize: parâmetros não encontrados — Tê ficará no tamanho padrão da família");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugAgua.Log("   resize FALHOU: " + ex.Message);
                    }
                    DebugAgua.Log("   OK montou '" + simbolo.FamilyName + " / " + simbolo.Name + "'");
                    return;
                }
                catch (Exception ex2)
                {
                    DebugAgua.Log("   FALHOU '" + simbolo.FamilyName + " / " + simbolo.Name + "': " + ex2.Message);
                    try
                    {
                        if (te != null)
                        {
                            doc.Delete(te.Id);
                        }
                    }
                    catch
                    {
                    }
                    try
                    {
                        (antes.Location as LocationCurve).Curve = Line.CreateBound(ptFixoAntes, ptNo);
                    }
                    catch
                    {
                    }
                    try
                    {
                        (depois.Location as LocationCurve).Curve = Line.CreateBound(ptNo, ptFixoDepois);
                    }
                    catch
                    {
                    }
                }
            }
            DebugAgua.Log("   >> nenhum candidato montou — fallback: emenda sem derivação");
            if (avisoJoelho == null)
            {
                avisoJoelho = ((candidatos.Count == 0) ? "Família 'Tê com Bucha de Latão' (Conexão de Tubo) não encontrada no projeto — pontos na mesma altura do ramal foram apenas emendados, sem derivação para o aparelho." : ("Nenhum tipo do tê com bucha de latão bateu com " + Math.Round(cfg.DiametroRamalPes * 304.8) + "x" + Math.Round(cfg.DiametroRamalPes * 304.8) + "x" + Math.Round(cfg.DiametroDescidaPes * 304.8) + "mm (reta x reta x ramo) — carregue esse tipo no projeto. Pontos na mesma altura do ramal foram apenas emendados."));
            }
        }
        Pipe aEmenda = antes;
        Pipe dEmenda = depois;
        TentarConectar(delegate
        {
            ConectorEm(aEmenda, ptNo).ConnectTo(ConectorEm(dEmenda, ptNo));
        }, ref conexoesOk, ref falhas);
    }

    private static bool IdentificarPapeisTe(FamilyInstance te, out Connector cA, out Connector cB, out Connector cBranch)
    {
        cA = (cB = (cBranch = null));
        List<Connector> conns = ObterConectoresPiping(te);
        if (conns.Count < 3)
        {
            return false;
        }
        double melhorDot = double.MaxValue;
        Connector achouA = null;
        Connector achouB = null;
        for (int i = 0; i < conns.Count; i++)
        {
            for (int j = i + 1; j < conns.Count; j++)
            {
                double dot = conns[i].CoordinateSystem.BasisZ.DotProduct(conns[j].CoordinateSystem.BasisZ);
                if (dot < melhorDot)
                {
                    melhorDot = dot;
                    achouA = conns[i];
                    achouB = conns[j];
                }
            }
        }
        if (melhorDot > -0.5)
        {
            return false;
        }
        cA = achouA;
        cB = achouB;
        cBranch = conns.First((Connector c) => c != achouA && c != achouB);
        return true;
    }

    private static List<FamilySymbol> BuscarCandidatosTeBucha(Document doc, double diametroRetaPes, double diametroRamoPes)
    {
        List<FamilySymbol> simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeFitting).Cast<FamilySymbol>()
            .ToList();
        string bitolaReta = BitolaPolegadas(diametroRetaPes);
        string bitolaRamo = BitolaPolegadas(diametroRamoPes);
        string mmReta = ((int)Math.Round(diametroRetaPes * 304.8)).ToString();
        string mmRamo = ((int)Math.Round(diametroRamoPes * 304.8)).ToString();
        string[] mmConhecidos = new string[9] { "20", "25", "32", "40", "50", "60", "75", "85", "110" };
        char[] separadores = new char[11]
        {
            ' ', '-', '_', 'x', '×', '(', ')', '°', '"', ',',
            ';'
        };
        List<Tuple<FamilySymbol, int>> candidatos = new List<Tuple<FamilySymbol, int>>();
        foreach (FamilySymbol s in simbolos)
        {
            string nome = (s.FamilyName + " " + s.Name).ToLower();
            if (!nome.Contains("bucha") || (!nome.Contains("latão") && !nome.Contains("latao")))
            {
                if (DebugAgua.Ativo && (nome.Contains("te ") || nome.Contains("tê") || nome.StartsWith("te")))
                {
                    DebugAgua.Log("   [Te-like, sem bucha/latão no nome, ignorado] " + s.FamilyName + " / " + s.Name);
                }
                continue;
            }
            string[] tokens = nome.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
            bool ehTe = Enumerable.Contains(tokens, "te") || Enumerable.Contains(tokens, "tê");
            bool ehOutraCoisa = nome.Contains("joelho") || nome.Contains("cotovelo") || nome.Contains("luva") || nome.Contains("uniao") || nome.Contains("união") || nome.Contains("emenda");
            if (!ehTe || ehOutraCoisa)
            {
                DebugAgua.Log("   REJEITADO (ehTe=" + ehTe + " ehOutraCoisa=" + ehOutraCoisa + "): " + s.FamilyName + " / " + s.Name);
                continue;
            }
            int pontos = 10;
            string[] array = mmConhecidos;
            foreach (string mm in array)
            {
                if (Enumerable.Contains(tokens, mm) || Enumerable.Contains(tokens, mm + "mm"))
                {
                    pontos = ((!(mm == mmReta)) ? ((!(mm == mmRamo)) ? (pontos - 4) : (pontos + 6)) : (pontos + 6));
                }
            }
            if (bitolaReta != null && nome.Contains(bitolaReta))
            {
                pontos += 3;
            }
            if (bitolaRamo != null && bitolaRamo != bitolaReta && nome.Contains(bitolaRamo))
            {
                pontos += 3;
            }
            DebugAgua.Log("   candidato: " + s.FamilyName + " / " + s.Name + "  pontos=" + pontos);
            candidatos.Add(Tuple.Create(s, pontos));
        }
        return (from c in candidatos
                orderby c.Item2 descending
                select c.Item1).ToList();
    }

    private static List<FamilySymbol> BuscarCandidatosJoelhoBucha(Document doc, double diametroPes)
    {
        List<FamilySymbol> simbolos = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeFitting).Cast<FamilySymbol>()
            .ToList();
        string bitolaAlvo = BitolaPolegadas(diametroPes);
        string mmAlvo = ((int)Math.Round(diametroPes * 304.8)).ToString();
        string[] mmConhecidos = new string[9] { "20", "25", "32", "40", "50", "60", "75", "85", "110" };
        char[] separadores = new char[11]
        {
            ' ', '-', '_', 'x', '×', '(', ')', '°', '"', ',',
            ';'
        };
        List<Tuple<FamilySymbol, int>> candidatos = new List<Tuple<FamilySymbol, int>>();
        foreach (FamilySymbol s in simbolos)
        {
            string nome = (s.FamilyName + " " + s.Name).ToLower();
            if (!nome.Contains("bucha") || (!nome.Contains("latão") && !nome.Contains("latao")))
            {
                continue;
            }
            bool ehLuva = nome.Contains("luva") || nome.Contains("uniao") || nome.Contains("união") || nome.Contains("emenda") || nome.Contains("conector reto") || nome.Contains(" reta");
            bool ehJoelho = nome.Contains("joelho") || nome.Contains("cotovelo") || nome.Contains("90");
            if (ehLuva || !ehJoelho)
            {
                continue;
            }
            string[] tokens = nome.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
            int pontos = 10;
            if (nome.Contains("90"))
            {
                pontos += 2;
            }
            string[] array = mmConhecidos;
            foreach (string mm in array)
            {
                if (Enumerable.Contains(tokens, mm) || Enumerable.Contains(tokens, mm + "mm"))
                {
                    pontos += ((mm == mmAlvo) ? 5 : (-8));
                }
            }
            if (nome.Contains("1.1/2"))
            {
                pontos += ((bitolaAlvo == "1.1/2") ? 4 : (-8));
            }
            else if (nome.Contains("1/2"))
            {
                pontos += ((bitolaAlvo == "1/2") ? 4 : (-8));
            }
            if (nome.Contains("3/4"))
            {
                pontos += ((bitolaAlvo == "3/4") ? 4 : (-8));
            }
            candidatos.Add(Tuple.Create(s, pontos));
        }
        return (from c in candidatos
                orderby c.Item2 descending
                select c.Item1).ToList();
    }

    private static Connector PegarConector(List<Connector> conns, XYZ dirAlvo)
    {
        Connector melhor = null;
        double melhorDot = double.MinValue;
        foreach (Connector c in conns)
        {
            double dot = c.CoordinateSystem.BasisZ.DotProduct(dirAlvo);
            if (dot > melhorDot)
            {
                melhorDot = dot;
                melhor = c;
            }
        }
        return melhor;
    }

    private static XYZ ObterPerpendicular(XYZ d)
    {
        XYZ p = ((Math.Abs(d.DotProduct(XYZ.BasisZ)) < 0.9) ? d.CrossProduct(XYZ.BasisZ) : d.CrossProduct(XYZ.BasisX));
        return p.Normalize();
    }

    private static XYZ HorizontalOuNulo(XYZ v)
    {
        if (v == null)
        {
            return null;
        }
        XYZ h = new XYZ(v.X, v.Y, 0.0);
        return (h.GetLength() > 1E-06) ? h.Normalize() : null;
    }

    private static XYZ DirecaoParaAmbiente(List<XYZ> trilho, int seg, bool ccw)
    {
        XYZ t = TangenteLoop(trilho, seg);
        XYZ esquerda = new XYZ(0.0 - t.Y, t.X, 0.0);
        return ccw ? esquerda : esquerda.Negate();
    }

    private static XYZ TangenteLoop(List<XYZ> trilho, int seg)
    {
        int n = trilho.Count;
        XYZ t = trilho[(seg + 1) % n] - trilho[seg];
        t = new XYZ(t.X, t.Y, 0.0);
        return (t.GetLength() < 1E-09) ? XYZ.BasisX : t.Normalize();
    }

    private static double AreaAssinada2D(List<XYZ> pts)
    {
        double a = 0.0;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            XYZ p = pts[i];
            XYZ q = pts[(i + 1) % n];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2.0;
    }

    private static string BitolaPolegadas(double diametroPes)
    {
        double mm = diametroPes * 304.8;
        if (mm <= 21.0)
        {
            return "1/2";
        }
        if (mm <= 28.0)
        {
            return "3/4";
        }
        return null;
    }

    private static Pipe CriarTubo(Document doc, ConfigRoteamentoAgua cfg, XYZ p1, XYZ p2, double diametro)
    {
        Pipe p3 = Pipe.Create(doc, cfg.SistemaId, cfg.TipoTuboId, cfg.LevelId, p1, p2);
        try
        {
            ((Element)p3).get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(diametro);
        }
        catch
        {
        }
        return p3;
    }

    private static FamilyInstance CriarInstanciaNoNivel(Document doc, ConfigRoteamentoAgua cfg, XYZ pt, FamilySymbol simbolo)
    {
        Level nivel = ((cfg.LevelId != null) ? (doc.GetElement(cfg.LevelId) as Level) : null);
        return (nivel != null) ? doc.Create.NewFamilyInstance(pt, simbolo, nivel, StructuralType.NonStructural) : doc.Create.NewFamilyInstance(pt, simbolo, StructuralType.NonStructural);
    }

    private static Connector ConectorEm(Pipe tubo, XYZ pt)
    {
        Connector melhor = null;
        double menor = double.MaxValue;
        foreach (Connector c in tubo.ConnectorManager.Connectors)
        {
            double d = c.Origin.DistanceTo(pt);
            if (d < menor)
            {
                menor = d;
                melhor = c;
            }
        }
        return melhor;
    }

    private static void TentarConectar(Action acao, ref int ok, ref int falhas)
    {
        try
        {
            acao();
            ok++;
        }
        catch
        {
            falhas++;
        }
    }

    private static XYZ Achatar(XYZ p, double z)
    {
        return new XYZ(p.X, p.Y, z);
    }

    private static double Dist2D(XYZ a, XYZ b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
