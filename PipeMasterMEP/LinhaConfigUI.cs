using System.Collections.Generic;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class LinhaConfigUI
{
    public double Diametro;

    public CheckBox ChkAtivo;

    public TextBox TxtInclinacao;

    public Button BtnSelecionar;

    public TextBlock TxtStatus;

    public List<CurveElement> LinhasSelecionadas = new List<CurveElement>();
}
