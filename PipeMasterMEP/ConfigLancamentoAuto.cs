using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class ConfigLancamentoAuto
{
    public bool Confirmado { get; set; } = false;

    public ElementId SistemaId { get; set; }

    public ElementId TipoTuboEsgotoId { get; set; }

    public double ElevacaoColetorMetros { get; set; }

    public double DiametroLavatorio { get; set; }

    public double AlturaLavatorio { get; set; }

    public double DiametroMaquina { get; set; }

    public double AlturaMaquina { get; set; }

    public bool DesviarVigaLavatorio { get; set; }

    public bool TemVaso { get; set; }

    public bool TemCaixaSifonada { get; set; }

    public bool TemLavatorio { get; set; }

    public bool TemChuveiro { get; set; }

    public bool TemPia { get; set; }

    public bool TemMaquina { get; set; }

    public bool IniciarVentilacao { get; set; } = false;

    public int OpcaoVentilacao { get; set; }

    public double AltVentilacaoCavalete { get; set; } = 0.56;

    public bool RotacaoTe90 { get; set; }

    public bool Joelho45NoChicote { get; set; }

    public bool BloquearConectoresHorizontais { get; set; } = false;

    public int DestinoVaso { get; set; }

    public int DestinoPia { get; set; }

    public int DestinoMaquina { get; set; }

    public int DestinoCaixa { get; set; }

    public bool CaixaIndependente { get; set; }

    public double DistanciaVaso { get; set; }
}
