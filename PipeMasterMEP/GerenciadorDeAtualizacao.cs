using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

public static class GerenciadorDeAtualizacao
{
    private static string UrlVersaoNuvem => $"https://pipemaster.com.br/api/version?t={DateTime.UtcNow.Ticks}";

    public static bool VerificarAtualizacaoObrigatoria()
    {
        try
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            string respostaSite = client.GetStringAsync(UrlVersaoNuvem).GetAwaiter().GetResult().Trim();
            string versaoNuvemStr = "";
            string urlDownload = "https://pipemaster.com.br/login";
            if (respostaSite.StartsWith("{"))
            {
                versaoNuvemStr = Regex.Match(respostaSite, "\"versao_minima\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                string urlExtraida = Regex.Match(respostaSite, "\"url_download\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                if (!string.IsNullOrEmpty(urlExtraida))
                {
                    urlDownload = urlExtraida.Replace("\\/", "/");
                }
            }
            else
            {
                versaoNuvemStr = respostaSite;
            }
            if (string.IsNullOrEmpty(versaoNuvemStr))
            {
                return false;
            }
            Version versaoNuvem = new Version(versaoNuvemStr);
            Version versaoLocal = typeof(GerenciadorDeAtualizacao).Assembly.GetName().Version;
            if (versaoLocal < versaoNuvem)
            {
                TaskDialog td = new TaskDialog("PipeMaster [M] - Atualização Obrigatória")
                {
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                    TitleAutoPrefix = false,
                    MainInstruction = "Uma nova versão do PipeMaster está disponível!",
                    MainContent = $"A sua versão atual ({versaoLocal}) está desatualizada. O acesso foi bloqueado para garantir a estabilidade das ferramentas.\n\nPor favor, atualize para a versão {versaoNuvem} para continuar.",
                    CommonButtons = TaskDialogCommonButtons.Close
                };
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Baixar Nova Versão Agora");
                TaskDialogResult result = td.Show();
                if (result == TaskDialogResult.CommandLink1)
                {
                    Process.Start(new ProcessStartInfo(urlDownload)
                    {
                        UseShellExecute = true
                    });
                }
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }
}
