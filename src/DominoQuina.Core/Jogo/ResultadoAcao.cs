using DominoQuina.Core.Api;
using DominoQuina.Core.Dominio;

namespace DominoQuina.Core.Jogo;

/// <summary>
/// Resultado de um comando enviado à API.
/// </summary>
/// <param name="Aceita">A API confirmou a ação.</param>
/// <param name="Estado">Estado confirmado depois da ação (ou o estado oficial, numa rejeição por versão).</param>
/// <param name="Erro">Rejeição da API, quando <paramref name="Aceita"/> é falso.</param>
public sealed record ResultadoAcao(bool Aceita, EstadoJogo? Estado, ApiException? Erro)
{
    /// <summary>Ação confirmada.</summary>
    public static ResultadoAcao Sucesso(EstadoJogo estado) => new(true, estado, null);

    /// <summary>Ação rejeitada; a partida não avançou por causa dela.</summary>
    public static ResultadoAcao Falha(ApiException erro, EstadoJogo? estado) => new(false, estado, erro);

    /// <summary>Texto para mostrar na tela.</summary>
    public string Mensagem => Aceita ? "Ação confirmada pela API." : Erro?.MensagemParaUsuario ?? "Ação rejeitada.";
}
