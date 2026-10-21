namespace DominoQuina.Core.Api;

/// <summary>
/// Rotas da API do Dominó Ponta de Quina.
/// </summary>
/// <remarks>
/// As rotas de identidade, ativação e partidas vêm do material do checkpoint.
/// As rotas de listagem e criação de jogadores-perfil não aparecem nos slides; confira no Swagger
/// (<c>/swagger</c>) e ajuste aqui se forem diferentes.
/// </remarks>
public static class RotasApi
{
    /// <summary>URL base padrão da API.</summary>
    public const string UrlBasePadrao = "https://domino-quina-atbkg6ckcwbpd5gz.westus3-01.azurewebsites.net";

    /// <summary>Cabeçalho que identifica este cliente para o perfil ativado.</summary>
    public const string CabecalhoAtivacao = "X-Jogador-Ativacao";

    /// <summary><c>POST</c>: solicita o envio do token por e-mail.</summary>
    public const string Token = "/api/identidade/token";

    /// <summary><c>GET</c> lista e <c>POST</c> cria jogadores-perfil da conta.</summary>
    public const string Jogadores = "/api/jogadores";

    /// <summary><c>POST</c>: ativa o perfil neste cliente.</summary>
    public static string AtivarCliente(string jogadorId) => $"/api/jogadores/{Uri.EscapeDataString(jogadorId)}/ativar-cliente";

    /// <summary><c>DELETE</c>: libera o perfil para outro cliente.</summary>
    public static string DesativarCliente(string jogadorId) => $"/api/jogadores/{Uri.EscapeDataString(jogadorId)}/desativar-cliente";

    /// <summary><c>GET</c> lista e <c>POST</c> cria partidas.</summary>
    public const string Partidas = "/api/partidas";

    /// <summary><c>GET</c>: estado da partida na visão do perfil.</summary>
    public static string Partida(string partidaId, string jogadorId) =>
        $"{Base(partidaId)}?jogadorId={Uri.EscapeDataString(jogadorId)}";

    /// <summary><c>POST</c>: ingressa na partida.</summary>
    public static string Entrar(string partidaId) => $"{Base(partidaId)}/entrar";

    /// <summary><c>POST</c>: inicia a partida e distribui as pedras.</summary>
    public static string Iniciar(string partidaId) => $"{Base(partidaId)}/iniciar";

    /// <summary><c>POST</c>: joga uma pedra.</summary>
    public static string Jogar(string partidaId) => $"{Base(partidaId)}/jogar";

    /// <summary><c>POST</c>: compra uma pedra da cava.</summary>
    public static string Comprar(string partidaId) => $"{Base(partidaId)}/comprar";

    /// <summary><c>POST</c>: passa a vez.</summary>
    public static string Passar(string partidaId) => $"{Base(partidaId)}/passar";

    /// <summary><c>GET</c>: estado novo desde <paramref name="versaoConhecida"/> (200) ou nada (204).</summary>
    public static string Atualizacoes(string partidaId, string jogadorId, long versaoConhecida) =>
        $"{Base(partidaId)}/atualizacoes?jogadorId={Uri.EscapeDataString(jogadorId)}&versaoConhecida={versaoConhecida}";

    /// <summary><c>GET</c>: resultado da partida.</summary>
    public static string Resultado(string partidaId, string jogadorId) =>
        $"{Base(partidaId)}/resultado?jogadorId={Uri.EscapeDataString(jogadorId)}";

    private static string Base(string partidaId) => $"{Partidas}/{Uri.EscapeDataString(partidaId)}";
}
