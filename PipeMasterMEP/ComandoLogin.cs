using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace PipeMasterMEP;

[Transaction(TransactionMode.Manual)]
public class ComandoLogin : IExternalCommand
{
    public const string URL_SITE_LOGIN = "https://pipemaster.com.br/auth-plugin?port=5000";

    public const string URL_SITE_SUCESSO = "https://pipemaster.com.br/acesso-liberado";

    public static string URL_SITE_DASHBOARD => $"https://pipemaster.com.br/welcome-plugin?version={typeof(ComandoLogin).Assembly.GetName().Version}";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (TestMode.Enabled)
        {
            TaskDialog.Show("PipeMaster [M]", "Modo de teste ativo. A validação de login está desabilitada somente nesta compilação.");
            return Result.Succeeded;
        }
        if (GerenciadorDeAtualizacao.VerificarAtualizacaoObrigatoria())
        {
            return Result.Cancelled;
        }
        if (SessaoUsuario.Autenticado)
        {
            JanelaLoginWPF janelaUsuario = new JanelaLoginWPF(URL_SITE_DASHBOARD, "PipeMaster Dashboard");
            janelaUsuario.ShowDialog();
            return Result.Succeeded;
        }
        JanelaLoginWPF janelaLogin = new JanelaLoginWPF("https://pipemaster.com.br/auth-plugin?port=5000", "PipeMaster [M] - Autenticação");
        janelaLogin.ShowDialog();
        return Result.Succeeded;
    }
}
