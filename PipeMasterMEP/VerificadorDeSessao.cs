using System;
using System.Net.Http;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

public static class VerificadorDeSessao
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static bool PermissaoConcedida()
    {
        if (TestMode.Enabled)
        {
            return true;
        }
        if (!SessaoUsuario.Autenticado)
        {
            return false;
        }
        try
        {
            string url = $"https://pipemaster.com.br/api/verify-session?email={Uri.EscapeDataString(SessaoUsuario.Email)}&session_id={Uri.EscapeDataString(SessaoUsuario.SessionId)}&t={DateTime.UtcNow.Ticks}";
            Task<HttpResponseMessage> responseTask = _httpClient.GetAsync(url);
            responseTask.Wait();
            HttpResponseMessage response = responseTask.Result;
            if (response.IsSuccessStatusCode)
            {
                Task<string> contentTask = response.Content.ReadAsStringAsync();
                contentTask.Wait();
                string json = contentTask.Result;
                if (json.Contains("\"valid\":true") || json.Contains("\"valid\": true"))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            TaskDialog.Show("PipeMaster [M]", "Falha de conexão com o servidor de validação. Verifique sua internet.");
            return false;
        }
        TaskDialog.Show("PipeMaster [M]", "Sessão expirada. Sua conta foi acessada em outro computador. Faça login novamente.");
        SessaoUsuario.Deslogar();
        return false;
    }
}
