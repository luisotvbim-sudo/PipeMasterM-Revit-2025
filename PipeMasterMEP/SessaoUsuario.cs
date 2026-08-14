namespace PipeMasterMEP;

public static class SessaoUsuario
{
    public static bool Autenticado { get; set; } = false;

    public static string Token { get; set; } = string.Empty;

    public static string SessionId { get; set; } = string.Empty;

    public static string Email { get; set; } = string.Empty;

    public static void Deslogar()
    {
        Autenticado = false;
        Token = string.Empty;
        SessionId = string.Empty;
        Email = string.Empty;
    }
}
