using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace PipeMasterMEP;

public class JanelaLoginWPF : Window
{
    private WebView2 _browser;

    public JanelaLoginWPF(string url, string tituloJanela)
    {
        base.Title = tituloJanela;
        base.Width = 1000.0;
        base.Height = 700.0;
        base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        base.ResizeMode = ResizeMode.NoResize;
        base.Background = PipeMasterTheme.Brush(PipeMasterTheme.Background);
        _browser = new WebView2();
        base.Content = _browser;
        InicializarNavegador(url);
    }

    private async void InicializarNavegador(string url)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userDataFolder = Path.Combine(appData, "PipeMasterMEP", "WebView2Cache");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _browser.EnsureCoreWebView2Async(env);
            _browser.CoreWebView2.NavigationStarting += InterceptarNavegacao;
            _browser.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            MessageBox.Show("Erro ao inicializar o navegador: " + ex2.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private void InterceptarNavegacao(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        string urlDestino = e.Uri.ToString();
        if (!urlDestino.StartsWith("http://localhost:5000/"))
        {
            return;
        }
        e.Cancel = true;
        if (urlDestino.Contains("token="))
        {
            string token = ExtrairParametroQuery(urlDestino, "token");
            string sessionId = ExtrairParametroQuery(urlDestino, "session_id");
            string email = ExtrairParametroQuery(urlDestino, "email");
            if (!string.IsNullOrEmpty(token))
            {
                SessaoUsuario.Token = token;
                SessaoUsuario.SessionId = sessionId;
                SessaoUsuario.Email = email;
                AutenticarEFecharComAtraso();
            }
        }
        else if (urlDestino.Contains("logout"))
        {
            SessaoUsuario.Deslogar();
            ((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
            {
                base.Title = "PipeMaster [M] - Autenticação";
                _browser.Source = new Uri("https://pipemaster.com.br/auth-plugin?port=5000");
            });
        }
    }

    private string ExtrairParametroQuery(string url, string nomeParametro)
    {
        try
        {
            Uri uri = new Uri(url);
            string query = uri.Query;
            if (string.IsNullOrEmpty(query))
            {
                return string.Empty;
            }
            string[] pares = query.TrimStart('?').Split('&');
            string[] array = pares;
            foreach (string par in array)
            {
                string[] chaveValor = par.Split('=');
                if (chaveValor.Length != 0 && chaveValor[0].Equals(nomeParametro, StringComparison.OrdinalIgnoreCase))
                {
                    return (chaveValor.Length > 1) ? Uri.UnescapeDataString(chaveValor[1]) : string.Empty;
                }
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    private async void AutenticarEFecharComAtraso()
    {
        SessaoUsuario.Autenticado = true;
        _browser.Source = new Uri("https://pipemaster.com.br/acesso-liberado");
        await Task.Delay(5000);
        try
        {
            Close();
        }
        catch
        {
        }
    }
}
