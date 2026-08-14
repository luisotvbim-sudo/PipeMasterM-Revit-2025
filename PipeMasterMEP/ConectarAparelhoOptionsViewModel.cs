using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class ConectarAparelhoOptionsViewModel : INotifyPropertyChanged
{
    private string _altura = "0.50";

    private string _inclinacao = "2.0";

    private string _trechoMinimo = "0.05";

    private bool _desvioViga = false;

    private string _avancoDesvio = "0.045";

    private Brush _textColor = Brushes.Black;

    private readonly string _configFilePath;

    public Brush TextColor
    {
        get
        {
            return _textColor;
        }
        set
        {
            _textColor = value;
            OnPropertyChanged("TextColor");
        }
    }

    public string Altura
    {
        get
        {
            return _altura;
        }
        set
        {
            _altura = value;
            OnPropertyChanged("Altura");
            SaveSettings();
        }
    }

    public string Inclinacao
    {
        get
        {
            return _inclinacao;
        }
        set
        {
            _inclinacao = value;
            OnPropertyChanged("Inclinacao");
            SaveSettings();
        }
    }

    public string TrechoMinimo
    {
        get
        {
            return _trechoMinimo;
        }
        set
        {
            _trechoMinimo = value;
            OnPropertyChanged("TrechoMinimo");
            SaveSettings();
        }
    }

    public bool DesvioViga
    {
        get
        {
            return _desvioViga;
        }
        set
        {
            _desvioViga = value;
            OnPropertyChanged("DesvioViga");
            SaveSettings();
        }
    }

    public string AvancoDesvio
    {
        get
        {
            return _avancoDesvio;
        }
        set
        {
            _avancoDesvio = value;
            OnPropertyChanged("AvancoDesvio");
            SaveSettings();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ConectarAparelhoOptionsViewModel()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "PipeMasterMEP");
        _configFilePath = Path.Combine(folder, "conectar_options.txt");
        LoadSettings();
    }

    public void AjustarTema(Autodesk.Revit.DB.Color revitBgColor)
    {
        double brilho = 0.299 * (double)(int)revitBgColor.Red + 0.587 * (double)(int)revitBgColor.Green + 0.114 * (double)(int)revitBgColor.Blue;
        if (brilho < 128.0)
        {
            TextColor = Brushes.White;
        }
        else
        {
            TextColor = Brushes.Black;
        }
    }

    private void SaveSettings()
    {
        try
        {
            string directory = Path.GetDirectoryName(_configFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string[] lines = new string[5]
            {
                _altura,
                _inclinacao,
                _trechoMinimo,
                _desvioViga.ToString(),
                _avancoDesvio
            };
            File.WriteAllLines(_configFilePath, lines);
        }
        catch
        {
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string[] lines = File.ReadAllLines(_configFilePath);
                if (lines.Length >= 5)
                {
                    _altura = lines[0];
                    _inclinacao = lines[1];
                    _trechoMinimo = lines[2];
                    bool.TryParse(lines[3], out _desvioViga);
                    _avancoDesvio = lines[4];
                }
            }
        }
        catch
        {
        }
    }

    public double GetAltura()
    {
        return ConverterParaDouble(_altura, 0.5);
    }

    public double GetInclinacao()
    {
        return ConverterParaDouble(_inclinacao, 2.0);
    }

    public double GetTrechoMinimo()
    {
        return ConverterParaDouble(_trechoMinimo, 0.05);
    }

    public double GetAvancoDesvio()
    {
        return ConverterParaDouble(_avancoDesvio, 0.045);
    }

    private double ConverterParaDouble(string texto, double valorPadrao)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return valorPadrao;
        }
        string textoLimpo = texto.Replace(",", ".");
        if (double.TryParse(textoLimpo, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado))
        {
            return resultado;
        }
        return valorPadrao;
    }

    protected void OnPropertyChanged(string name)
    {
        if (this.PropertyChanged != null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
