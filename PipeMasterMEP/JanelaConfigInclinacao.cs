using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeMasterMEP;

public partial class JanelaConfigInclinacao : Window, IComponentConnector
{
    private ObservableCollection<RegraInclinacao> _regrasAtuais;

    private Document _doc;

    public JanelaConfigInclinacao(Document doc)
    {
        InitializeComponent();
        _doc = doc;
        _regrasAtuais = new ObservableCollection<RegraInclinacao>();
        dgRegras.ItemsSource = _regrasAtuais;
        chkNivelar.IsChecked = MemoriaInclinacao.NivelarTampaCaixas;
        chkNivelar.Checked += delegate
        {
            MemoriaInclinacao.NivelarTampaCaixas = true;
        };
        chkNivelar.Unchecked += delegate
        {
            MemoriaInclinacao.NivelarTampaCaixas = false;
        };
        CarregarSistemasDoProjeto();
    }

    private void MoverJanela_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void CarregarSistemasDoProjeto()
    {
        List<string> sistemas = (from n in (from PipingSystemType s in new FilteredElementCollector(_doc).OfClass(typeof(PipingSystemType))
                                            select s.Name).Distinct()
                                 orderby n
                                 select n).ToList();
        cmbSistemas.ItemsSource = sistemas;
        if (sistemas.Count > 0)
        {
            cmbSistemas.SelectedIndex = 0;
        }
    }

    private void CmbSistemas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        string sistemaSel = cmbSistemas.SelectedItem as string;
        if (string.IsNullOrEmpty(sistemaSel))
        {
            return;
        }
        _regrasAtuais.Clear();
        if (!MemoriaInclinacao.RegrasPorSistema.ContainsKey(sistemaSel))
        {
            return;
        }
        foreach (KeyValuePair<int, double> kvp in MemoriaInclinacao.RegrasPorSistema[sistemaSel].OrderByDescending((KeyValuePair<int, double> x) => x.Key))
        {
            _regrasAtuais.Add(new RegraInclinacao
            {
                Diametro = kvp.Key,
                InclinacaoPorcentagem = kvp.Value * 100.0
            });
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        string sistemaSel = cmbSistemas.SelectedItem as string;
        if (string.IsNullOrEmpty(sistemaSel))
        {
            return;
        }
        Dictionary<int, double> dic = new Dictionary<int, double>();
        foreach (RegraInclinacao regra in _regrasAtuais)
        {
            if (regra.Diametro > 0)
            {
                dic[regra.Diametro] = regra.InclinacaoPorcentagem / 100.0;
            }
        }
        MemoriaInclinacao.RegrasPorSistema[sistemaSel] = dic;
        MemoriaInclinacao.Salvar();
        Close();
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
