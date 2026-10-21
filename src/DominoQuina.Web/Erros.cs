using DominoQuina.Core.Api;

namespace DominoQuina.Web;

/// <summary>
/// Traduz exceções das chamadas à API em mensagens para a tela.
/// </summary>
public static class Erros
{
    /// <summary>Mensagem amigável para <paramref name="ex"/>.</summary>
    public static string Descrever(Exception ex) => ex switch
    {
        ApiException api => api.MensagemParaUsuario,
        ArgumentException argumento => argumento.Message,
        HttpRequestException http => $"Não foi possível falar com a API ({http.Message}). Verifique a conexão.",
        TaskCanceledException => "A API demorou demais para responder. Tente de novo.",
        _ => ex.Message,
    };
}
