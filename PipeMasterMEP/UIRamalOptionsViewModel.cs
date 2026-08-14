using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace PipeMasterMEP;

public class UIRamalOptionsViewModel : INotifyPropertyChanged
{
    private bool _isSuave = false;

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

    public bool IsSuave
    {
        get
        {
            return _isSuave;
        }
        set
        {
            _isSuave = value;
            OnPropertyChanged("IsSuave");
            OnPropertyChanged("IsPadrao");
            SaveSettings();
        }
    }

    public bool IsPadrao
    {
        get
        {
            return !_isSuave;
        }
        set
        {
            _isSuave = !value;
            OnPropertyChanged("IsSuave");
            OnPropertyChanged("IsPadrao");
            SaveSettings();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public UIRamalOptionsViewModel()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "PipeMasterMEP");
        _configFilePath = Path.Combine(folder, "tombo_options.txt");
        LoadSettings();
    }

    public void AjustarTema(Autodesk.Revit.DB.Color revitBgColor)
    {
        double brilho = 0.299 * (double)(int)revitBgColor.Red + 0.587 * (double)(int)revitBgColor.Green + 0.114 * (double)(int)revitBgColor.Blue;
        TextColor = ((brilho < 128.0) ? Brushes.White : Brushes.Black);
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
            File.WriteAllText(_configFilePath, _isSuave.ToString());
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
                string text = File.ReadAllText(_configFilePath);
                bool.TryParse(text, out _isSuave);
            }
        }
        catch
        {
        }
    }

    protected void OnPropertyChanged(string name)
    {
        if (this.PropertyChanged != null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
