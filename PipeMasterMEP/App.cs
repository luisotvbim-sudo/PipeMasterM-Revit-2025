using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB.ExternalService;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Windows;

namespace PipeMasterMEP;

public class App : IExternalApplication
{
    private static readonly (string CmdId, string Namespace, string Icon16, string Icon32, string Tooltip)[] _botoes = new (string, string, string, string, string)[6]
    {
        ("cmdSubir45", "PipeMasterMEP.ComandoSubir45", "Subir45_16.png", "Subir45.png", "Subir 45° / Desvio vertical ascendente de 45°"),
        ("cmdDescer45", "PipeMasterMEP.ComandoDescer45", "Descer45_16.png", "Descer45.png", "Descer 45° / Desvio vertical descendente de 45°"),
        ("cmdSubir90", "PipeMasterMEP.ComandoSubir90", "Subir90_16.png", "Subir90.png", "Subir 90° / Desvio vertical ascendente de 90°"),
        ("cmdDescer90", "PipeMasterMEP.ComandoDescer90", "Descer90_16.png", "Descer90.png", "Descer 90° / Desvio vertical descendente de 90°"),
        ("cmdVirarEsq", "PipeMasterMEP.ComandoVirarEsquerda", "VirarEsquerda_16.png", "VirarEsquerda.png", "Virar Esquerda / Desvio horizontal para a esquerda"),
        ("cmdVirarDir", "PipeMasterMEP.ComandoVirarDireita", "VirarDireita_16.png", "VirarDireita.png", "Virar Direita / Desvio horizontal para a direita")
    };

    private string _assemblyPath;

    private string _pastaIcones;

    private Autodesk.Revit.UI.RibbonPanel _panelModelagem;

    private bool _patchAplicado = false;

    public static bool AppCarregado { get; private set; } = false;

    public static PreviewRotasServer PreviewServer { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        AppCarregado = true;
        PreviewServer = new PreviewRotasServer();
        if (ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService) is MultiServerService mss)
        {
            mss.AddServer(PreviewServer);
            IList<Guid> ativos = mss.GetActiveServerIds();
            ativos.Add(PreviewServer.GetServerId());
            mss.SetActiveServers(ativos);
        }
        _assemblyPath = Assembly.GetExecutingAssembly().Location;
        _pastaIcones = Path.Combine(Path.GetDirectoryName(_assemblyPath), "..", "Icones");
        try
        {
            application.CreateRibbonTab("PipeMaster [M]");
        }
        catch
        {
        }
        Autodesk.Revit.UI.RibbonPanel panelAcesso = application.CreateRibbonPanel("PipeMaster [M]", "Acesso");
        string textoLogin = TestMode.Enabled ? "Modo\nTeste" : "Login";
        PushButton btnLogin = panelAcesso.AddItem(new PushButtonData("cmdLogin", textoLogin, _assemblyPath, "PipeMasterMEP.ComandoLogin")) as PushButton;
        CarregarIconeGrande(btnLogin, "acesso.png");
        _panelModelagem = application.CreateRibbonPanel("PipeMaster [M]", "Roteamento & Conexões");
        PushButtonData dataCriarTubo = new PushButtonData("cmdCriarTubo", "Criar\nTubo", _assemblyPath, "PipeMasterMEP.ComandoCriarTubo")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataCriarTubo.ToolTip = "Cria um tubo a partir de uma conexão, acessório ou peça hidrossanitária.";
        PushButton btnGerar = _panelModelagem.AddItem(dataCriarTubo) as PushButton;
        CarregarIconeGrande(btnGerar, "Icone Criar Tubo.png");
        _panelModelagem.AddSeparator();
        AdicionarBotoesNativosBloqueados(_panelModelagem);
        _panelModelagem.AddSeparator();
        PushButtonData dataTomboColetor = new PushButtonData("cmdTomboColetor", "Ramal Superior\ncom Junção", _assemblyPath, "PipeMasterMEP.ComandoTomboColetor")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataTomboColetor.ToolTip = "Cria um ramal de queda entre um ramal e um coletor predial.";
        dataTomboColetor.LongDescription = "Selecione o tubo principal (Coletor) e o tubo superior (Ramal). O PipeMaster M irá conectar as redes gerando automaticamente a peça de Junção em 45 graus, nivelando tudo pela geratriz superior e absorvendo a inclinação do sistema.";
        PushButtonData dataRamalJuncao = new PushButtonData("cmdRamalJuncao", "Ramal\nHallef", _assemblyPath, "PipeMasterMEP.ComandoRamalComJuncao")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataRamalJuncao.ToolTip = "Cria um uma junção rotacionada e um ramal de queda para conexão com ramal secundário.";
        dataRamalJuncao.LongDescription = "Clique na tubulação do Ramal Primário exatamente na posição onde se deseja criar a junção rotacionada. Clique na direção onde deseja que o Ramal seja criado. O comando alinha a geometria e insere automaticamente uma junção em Y acoplada a um joelho de 45° para garantir o traçado esquadrejado e o fluxo ideal do esgoto.";
        PushButtonData dataRamalSecundario = new PushButtonData("cmdRamalSecundario", "Ramal\nSecundário", _assemblyPath, "PipeMasterMEP.ComandoRamalSecundario")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataRamalSecundario.ToolTip = "Cria/Conecta um ramal secundário/primário a um ramal primário";
        dataRamalSecundario.LongDescription = "Selecione um dos tubos ou caixa sifonada e em seguida clique no tubo que irá ser conectado. O sistema calcula a cota de intersecção, alonga o tubo na inclinação correta e insere a peça de acoplamento apropriada sem distorcer o sistema hidráulico.";
        PushButtonData dataConectarAparelho = new PushButtonData("cmdConectarAparelho", "Conectar\nAparelho", _assemblyPath, "PipeMasterMEP.ComandoConectarAparelho")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataConectarAparelho.ToolTip = "Cria a prumada de saída de uma peça sanitária até uma caixa sifonada.";
        dataConectarAparelho.LongDescription = "Clique no conector da caixa sifonada e no alinhamento da parede da peça hidrossanitária. O comando descerá a prumada vertical e traçará o tubo de conexão horizontal automaticamente criando os joelhos e luvas necessárias.";
        PushButtonData dataRamalAutomatico = new PushButtonData("cmdRamalAutomatico", "Ramal em\nLinhas (Beta)", _assemblyPath, "PipeMasterMEP.ComandoLinhasParaEsgoto")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataRamalAutomatico.ToolTip = "Transforma um desenho em linhas (Estudo Preliminar) em uma rede 3D completa.";
        dataRamalAutomatico.LongDescription = "Instruções:\nConfigure diâmetros e inclinações no painel do PipeMaster.\nClique em 'Selecionar' para pintar as linhas correspondentes a cada bitola.\nSelecione as eventuais prumadas de 90º para Vasos Sanitários.\nAo confirmar, o PipeMaster M converte o tracejado numa malha 3D conectada, nivelada pela geratriz superior e roteada com precisão.";
        PushButtonData dataLancamentoAutomatico = new PushButtonData("cmdLancamentoAutomatico", "Lançamento Automático\nde Esgoto", _assemblyPath, "PipeMasterMEP.ComandoLancamentoAutomatico")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataLancamentoAutomatico.ToolTip = "Traçado automatizado e assistido de Caixas Sifonadas, Vasos, Pias, Máquinas, Ralos e Ventilação.";
        dataLancamentoAutomatico.LongDescription = "Instruções:\n1. Defina elevações, bitolas e aparelhos ativos no painel.\n2. Siga as instruções no rodapé do Revit para lançar cada aparelho.\n3. Explore o traçado 3D dinâmico em tempo real movendo o mouse.\n4. O PipeMaster irá gerar as tubulações, joelhos, luvas e reduções automaticamente ao final dos cliques.\n\nNota: Algumas opções (como Ventilação e Vaso Sanitário) estão em fase de calibração (Beta).";
        PushButtonData dataLancamentoAgua = new PushButtonData("cmdLancamentoAgua", "Lançamento Automático\nde Água (Beta)", _assemblyPath, "PipeMasterMEP.ComandoLancamentoAgua")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataLancamentoAgua.ToolTip = "Modelagem automática de Água Fria/Quente a partir do ambiente: prumada, registro, ramal embutido na parede e descidas para cada peça.";
        dataLancamentoAgua.LongDescription = "Instruções:\n1. Passe o mouse sobre a planta — o ambiente acende em roxo — e clique para selecionar.\n2. Confira o checklist de peças detectadas, os tipos e as alturas dos pontos.\n3. Defina sistema, tipo de tubo, diâmetros e alturas (prumada, registro e ramal).\n4. Clique na parede onde ficará a prumada com o registro.\nO PipeMaster gera a prumada, o registro, o ramal embutido na parede com joelhos e tês, e as descidas/subidas até cada ponto de consumo.";
        CarregarIconeGrande(_panelModelagem.AddItem(dataTomboColetor) as PushButton, "Ramal Superior com Junção.png");
        CarregarIconeGrande(_panelModelagem.AddItem(dataRamalJuncao) as PushButton, "hallef.png");
        CarregarIconeGrande(_panelModelagem.AddItem(dataRamalSecundario) as PushButton, "Ramal Secundario.png");
        CarregarIconeGrande(_panelModelagem.AddItem(dataConectarAparelho) as PushButton, "ConectarAparelho.png");
        CarregarIconeGrande(_panelModelagem.AddItem(dataRamalAutomatico) as PushButton, "Ramal Automático.png");
        CarregarIconeGrande(_panelModelagem.AddItem(dataLancamentoAutomatico) as PushButton, "Lançamento Automático.png");
        CarregarIconeGrande(_panelModelagem.AddItem(dataLancamentoAgua) as PushButton, "LançamentoAgua.png");
        string ttAlign3D = "Projeta e rotaciona um tubo/conexão para o eixo de referência no espaço 3D.";
        string ldAlign3D = "Instruções:\nSelecione o tubo de REFERÊNCIA (eixo mestre fixo).\nSelecione o tubo ou conexão MÓVEL.\nO elemento será deslocado ortogonalmente de encontro à linha mestre. A operação corrige o ângulo da peça e preserva o comprimento original do tubo, evitando o rompimento das conexões adjacentes.";
        string ttAlignBranch = "Alinhar a extremidade de um ramal ao eixo do tubo principal.";
        string ldAlignBranch = "Selecione o tubo mestre e em seguida a tubulação do ramal. O PipeMaster alinhará ramal até cruzar o ponto exato da geratriz do coletor, preparando o traçado para receber a conexão.";
        string ttInclinar = "Aplica ou corrige as inclinações para toda uma rede de esgoto existente.";
        string ldInclinar = "Selecione o(s) tubo(s) e pressione o comando alinhar. O PipeMaster fará a varredura a montante na topologia hidráulica e aplicará as taxas de caimento configuradas no painel. O comando realinha joelhos, junções e reduções excêntricas sem quebrar a rede.";
        string ttMoveConnect = "Desloca um tubo ou conexão e o acopla diretamente a outro elemento.";
        string ldMoveConnect = "Selecione primeiro o conector fixo e o depois o elemento que será movido. O comando arrasta o segundo elemento até a posição do primeiro e une os conectores.";
        Autodesk.Revit.UI.RibbonPanel panelModifyHack = application.CreateRibbonPanel("PipeMaster [M]", "PipeMaster [M]");
        PushButtonData dataInclinarRedeCtx = new PushButtonData("cmdInclinarRedeCtx", "Inclinar\nSistema", _assemblyPath, "PipeMasterMEP.ComandoInclinarRede")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttInclinar,
            LongDescription = ldInclinar
        };
        PushButtonData dataAlign3DCtx = new PushButtonData("cmdAlign3DCtx", "Alinhamento\nTridimensional", _assemblyPath, "PipeMasterMEP.ComandoAlign3D")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttAlign3D,
            LongDescription = ldAlign3D
        };
        PushButtonData dataAlignBranchCtx = new PushButtonData("cmdAlignBranchCtx", "Alinhar\nRamal", _assemblyPath, "PipeMasterMEP.ComandoAlignBranch")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttAlignBranch,
            LongDescription = ldAlignBranch
        };
        PushButtonData dataMoveConnectCtx = new PushButtonData("cmdMoveConnectCtx", "Mover e\nConectar", _assemblyPath, "PipeMasterMEP.ComandoMoveAndConnect")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttMoveConnect,
            LongDescription = ldMoveConnect
        };
        CarregarIconeGrande(panelModifyHack.AddItem(dataInclinarRedeCtx) as PushButton, "Inclinar Sistema.png");
        CarregarIconeGrande(panelModifyHack.AddItem(dataAlign3DCtx) as PushButton, "Alinhar Tridimensional.png");
        CarregarIconeGrande(panelModifyHack.AddItem(dataAlignBranchCtx) as PushButton, "align branch.png");
        CarregarIconeGrande(panelModifyHack.AddItem(dataMoveConnectCtx) as PushButton, "Mover e Conectar.png");
        Autodesk.Revit.UI.RibbonPanel panelAlinhar = application.CreateRibbonPanel("PipeMaster [M]", "Alinhar");
        PushButtonData dataAlign3D = new PushButtonData("cmdAlign3D", "Alinhamento\nTridimensional", _assemblyPath, "PipeMasterMEP.ComandoAlign3D")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttAlign3D,
            LongDescription = ldAlign3D
        };
        PushButtonData dataAlignBranch = new PushButtonData("cmdAlignBranch", "Alinhar\nRamal", _assemblyPath, "PipeMasterMEP.ComandoAlignBranch")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttAlignBranch,
            LongDescription = ldAlignBranch
        };
        PushButtonData dataAlignBranchPerp = new PushButtonData("cmdAlignBranchPerp", "Alinhar\nPerpendicular", _assemblyPath, "PipeMasterMEP.ComandoAlignBranchPerp")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataAlignBranchPerp.ToolTip = "Alinha e rotaciona um tubo selecionado para exatos 90° da rede principal.";
        dataAlignBranchPerp.LongDescription = "Selecione o tubo mestre (referência) e depois o tubo móvel. O comando forçará uma angulação perfeitamente ortogonal, destravando erros de traçado e permitindo o encaixe seguro de Tês sanitários paralelos aos eixos X/Y.";
        CarregarIconeGrande(panelAlinhar.AddItem(dataAlign3D) as PushButton, "Alinhar Tridimensional.png");
        CarregarIconeGrande(panelAlinhar.AddItem(dataAlignBranch) as PushButton, "align branch.png");
        CarregarIconeGrande(panelAlinhar.AddItem(dataAlignBranchPerp) as PushButton, "align branch 2.png");
        panelAlinhar.AddSeparator();
        PushButtonData dataInclinarRede = new PushButtonData("cmdInclinarRede", "Inclinar\nSistema", _assemblyPath, "PipeMasterMEP.ComandoInclinarRede")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttInclinar,
            LongDescription = ldInclinar
        };
        PushButtonData dataConfigInclinacao = new PushButtonData("cmdConfigInclinacao", "Configurar\nInclinação", _assemblyPath, "PipeMasterMEP.ConfigInclinacao")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataConfigInclinacao.ToolTip = "Abre as preferências globais de declividade da rede hidrossanitária.";
        dataConfigInclinacao.LongDescription = "Defina as porcentagens de inclinação padrão vinculadas a cada diâmetro de tubo (Ex: 1% para 100mm, 2% para 50mm). Estes parâmetros orientarão as declividades gerados pelo comando Inclinar Sistema.";
        CarregarIconeGrande(panelAlinhar.AddItem(dataInclinarRede) as PushButton, "Inclinar Sistema.png");
        CarregarIconeGrande(panelAlinhar.AddItem(dataConfigInclinacao) as PushButton, "Configurar Inclinação.png");
        panelAlinhar.AddSeparator();
        PushButtonData dataRotacionar180 = new PushButtonData("cmdRotacionar180", "Rotacionar\n180°", _assemblyPath, "PipeMasterMEP.ComandoRotacionar180")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataRotacionar180.ToolTip = "Rotaciona uma conexão selecionada em exatos 180 graus.";
        dataRotacionar180.LongDescription = "Selecione uma peça hidrossanitária (ex: joelho, tê, luva) para rotacioná-la instantaneamente em 180° em torno do seu eixo de fluxo. Ideal para corrigir o lado de caimento de caixas sifonadas ou virar joelhos sem perder a conectividade com os tubos.";
        PushButtonData dataRotacionar181 = new PushButtonData("cmdRotacionar45", "Rotacionar\n45°", _assemblyPath, "PipeMasterMEP.ComandoRotacionar45")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataRotacionar181.ToolTip = "Rotaciona uma conexão selecionada em exatos de 45 graus.";
        dataRotacionar181.LongDescription = "Selecione uma conexão para aplicar uma rotação matemática de 45° no seu próprio eixo. Ferramenta essencial para ajustar a angulação de joelhos e saídas de ramais que precisam trabalhar na diagonal do projeto.";
        PushButtonData dataRotacionarConexao = new PushButtonData("cmdRotacionarConexao", "Rotacionar\nConexão", _assemblyPath, "PipeMasterMEP.ComandoRotacionarConexao")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataRotacionarConexao.ToolTip = "Gira a peça no seu próprio eixo de acordo com ângulo desejado.";
        dataRotacionarConexao.LongDescription = "Selecione a conexão que deseja ajustar. O comando rotaciona o componente mantendo seu ponto de inserção fixo, permitindo apontar a saída da peça para a direção correta antes de traçar o próximo trecho de tubo.";
        CarregarIconeGrande(panelAlinhar.AddItem(dataRotacionar180) as PushButton, "Rotacionar 180.png");
        CarregarIconeGrande(panelAlinhar.AddItem(dataRotacionar181) as PushButton, "Rotacionar 45.png");
        CarregarIconeGrande(panelAlinhar.AddItem(dataRotacionarConexao) as PushButton, "Rotacionar Conexão.png");
        Autodesk.Revit.UI.RibbonPanel panelConectar = application.CreateRibbonPanel("PipeMaster [M]", "Conectar e Desconectar");
        PushButtonData dataMoveConnect = new PushButtonData("cmdMoveConnect", "Mover e\nConectar", _assemblyPath, "PipeMasterMEP.ComandoMoveAndConnect")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin",
            ToolTip = ttMoveConnect,
            LongDescription = ldMoveConnect
        };
        PushButtonData dataMoveAlignConnect = new PushButtonData("cmdMoveAlignConnect", "Mover, Alinhar\ne Conectar", _assemblyPath, "PipeMasterMEP.ComandoMoverAlinharConectar")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataMoveAlignConnect.ToolTip = "Realiza o alinhamento ortogonal, o deslocamento e a conexão em uma única ação.";
        dataMoveAlignConnect.LongDescription = "A solução definitiva para fechamento de redes. Selecione a referência mestre e o elemento móvel. O PipeMaster calcula o alinhamento ideal preservando as inclinações, desloca a peça para o eixo e solda os conectores instantaneamente.";
        CarregarIconeGrande(panelConectar.AddItem(dataMoveConnect) as PushButton, "Mover e Conectar.png");
        CarregarIconeGrande(panelConectar.AddItem(dataMoveAlignConnect) as PushButton, "Mover Conectar e Alinhar.png");
        panelConectar.AddSeparator();
        PushButtonData dataDesconectar = new PushButtonData("cmdDesconectar", "Desconectar\nElemento", _assemblyPath, "PipeMasterMEP.ComandoDesconectar")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataDesconectar.ToolTip = "Separa um tubo ou conexão da rede selecionada.";
        dataDesconectar.LongDescription = "Clique em um elemento para romper fisicamente com o segundo elemento selecionado. Ferramenta crucial para isolar um trecho da tubulação antes de realizar modificações complexas no roteamento sem que o Revit arraste a rede inteira junto.";
        PushButtonData dataDeletarSistema = new PushButtonData("cmdDeletarSistema", "Excluir\nSistema", _assemblyPath, "PipeMasterMEP.ComandoDeletarSistema")
        {
            AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
        };
        dataDeletarSistema.ToolTip = "Apaga rapidamente os sistemas inseridos em tubulações.";
        dataDeletarSistema.LongDescription = "Selecione um tubo ou peça. O comando rastreará de forma inteligente a topologia conectada a partir daquele ponto e removerá o sistema de tubulação atual.";
        CarregarIconeGrande(panelConectar.AddItem(dataDesconectar) as PushButton, "Desconectar Elemento.png");
        CarregarIconeGrande(panelConectar.AddItem(dataDeletarSistema) as PushButton, "Excluir Sistema.png");
        application.Idling += AplicarPatchAdWindowsNoIdling;
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private void AdicionarBotoesNativosBloqueados(Autodesk.Revit.UI.RibbonPanel panel)
    {
        panel.AddStackedItems(Criar(0), Criar(1));
        panel.AddStackedItems(Criar(2), Criar(3));
        panel.AddStackedItems(Criar(4), Criar(5));
        PushButtonData Criar(int idx)
        {
            (string CmdId, string Namespace, string Icon16, string Icon32, string Tooltip) tuple = _botoes[idx];
            string cmdId = tuple.CmdId;
            string ns = tuple.Namespace;
            string icon16 = tuple.Icon16;
            string tooltip = tuple.Tooltip;
            PushButtonData d = new PushButtonData(cmdId, cmdId, _assemblyPath, ns)
            {
                ToolTip = tooltip,
                AvailabilityClassName = "PipeMasterMEP.BloqueioDeLogin"
            };
            string p = Path.Combine(_pastaIcones, icon16);
            if (File.Exists(p))
            {
                try
                {
                    d.Image = new BitmapImage(new Uri(p, UriKind.Absolute));
                }
                catch
                {
                }
            }
            return d;
        }
    }

    private void AplicarPatchAdWindowsNoIdling(object sender, IdlingEventArgs e)
    {
        if (_patchAplicado)
        {
            return;
        }
        try
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
            {
                return;
            }
            RibbonTab myTab = ribbon.Tabs.FirstOrDefault((RibbonTab t) => t.Title == "PipeMaster [M]" || t.Id.Contains("PipeMaster [M]"));
            if (myTab == null)
            {
                return;
            }
            RibbonTab modifyTab = ribbon.Tabs.FirstOrDefault((RibbonTab t) => t.Id == "Modify");
            if (modifyTab != null)
            {
                Autodesk.Windows.RibbonPanel panelToMove = myTab.Panels.FirstOrDefault((Autodesk.Windows.RibbonPanel p) => p.Source.Title == "PipeMaster [M]");
                if (panelToMove != null)
                {
                    myTab.Panels.Remove(panelToMove);
                    modifyTab.Panels.Add(panelToMove);
                }
            }
            if (modifyTab != null)
            {
                string[] hackIds = new string[4] { "cmdInclinarRedeCtx", "cmdAlign3DCtx", "cmdAlignBranchCtx", "cmdMoveConnectCtx" };
                string[] hackImagens = new string[4] { "Inclinar Sistema.png", "Alinhar Tridimensional.png", "align branch.png", "Mover e Conectar.png" };
                for (int i = 0; i < hackIds.Length; i++)
                {
                    string idRevit = "CustomCtrl_%CustomCtrl_%PipeMaster [M]%PipeMaster [M]%" + hackIds[i];
                    Autodesk.Windows.RibbonButton rbBtn = EncontrarBotaoAdWindows(modifyTab, idRevit);
                    if (rbBtn == null)
                    {
                        continue;
                    }
                    rbBtn.Size = RibbonItemSize.Large;
                    rbBtn.ShowText = true;
                    string caminhoIcone = Path.Combine(_pastaIcones, hackImagens[i]);
                    if (File.Exists(caminhoIcone))
                    {
                        try
                        {
                            BitmapImage bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(caminhoIcone, UriKind.Absolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            ((Freezable)bmp).Freeze();
                            rbBtn.LargeImage = bmp;
                            rbBtn.Image = bmp;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            bool tudoPatchado = true;
            (string, string, string, string, string)[] botoes = _botoes;
            for (int num = 0; num < botoes.Length; num++)
            {
                (string, string, string, string, string) tuple = botoes[num];
                string cmdId = tuple.Item1;
                string icon32 = tuple.Item4;
                string tooltip = tuple.Item5;
                string idRevit2 = "CustomCtrl_%CustomCtrl_%PipeMaster [M]%Roteamento & Conexões%" + cmdId;
                Autodesk.Windows.RibbonButton rbBtn2 = EncontrarBotaoAdWindows(myTab, idRevit2);
                if (rbBtn2 == null)
                {
                    tudoPatchado = false;
                    continue;
                }
                rbBtn2.Size = RibbonItemSize.Large;
                rbBtn2.ShowText = false;
                string caminhoIcone2 = Path.Combine(_pastaIcones, icon32);
                if (File.Exists(caminhoIcone2))
                {
                    try
                    {
                        BitmapImage bmp2 = new BitmapImage();
                        bmp2.BeginInit();
                        bmp2.UriSource = new Uri(caminhoIcone2, UriKind.Absolute);
                        bmp2.CacheOption = BitmapCacheOption.OnLoad;
                        bmp2.EndInit();
                        ((Freezable)bmp2).Freeze();
                        rbBtn2.LargeImage = bmp2;
                        rbBtn2.Image = bmp2;
                    }
                    catch
                    {
                    }
                }
                rbBtn2.ToolTip = tooltip;
            }
            if (tudoPatchado)
            {
                _patchAplicado = true;
                if (sender is UIControlledApplication uiApp)
                {
                    uiApp.Idling -= AplicarPatchAdWindowsNoIdling;
                }
            }
        }
        catch
        {
        }
    }

    private static Autodesk.Windows.RibbonButton EncontrarBotaoAdWindows(RibbonTab tab, string idAlvo)
    {
        foreach (Autodesk.Windows.RibbonPanel panel in tab.Panels)
        {
            Autodesk.Windows.RibbonButton encontrado = BuscarRecursivo(panel.Source.Items, idAlvo);
            if (encontrado != null)
            {
                return encontrado;
            }
        }
        return null;
    }

    private static Autodesk.Windows.RibbonButton BuscarRecursivo(RibbonItemCollection items, string idAlvo)
    {
        foreach (Autodesk.Windows.RibbonItem item in items)
        {
            if (item is Autodesk.Windows.RibbonButton rb && rb.Id == idAlvo)
            {
                return rb;
            }
            if (item is RibbonSplitButton rsb)
            {
                Autodesk.Windows.RibbonButton r = BuscarRecursivo(rsb.Items, idAlvo);
                if (r != null)
                {
                    return r;
                }
            }
            if (item is RibbonRowPanel rrp)
            {
                Autodesk.Windows.RibbonButton r2 = BuscarRecursivo(rrp.Items, idAlvo);
                if (r2 != null)
                {
                    return r2;
                }
            }
        }
        return null;
    }

    private void CarregarIconeGrande(PushButton botao, string nomeArquivo)
    {
        if (botao == null)
        {
            return;
        }
        string caminho = Path.Combine(_pastaIcones, nomeArquivo);
        if (!File.Exists(caminho))
        {
            return;
        }
        try
        {
            botao.LargeImage = new BitmapImage(new Uri(caminho, UriKind.Absolute));
        }
        catch
        {
        }
    }
}
