namespace DominoQuina.Core.Api;

/// <summary>
/// Jogador-perfil ativado neste cliente.
/// </summary>
/// <param name="JogadorId">Identificador do perfil na API.</param>
/// <param name="Nome">Nome para exibição.</param>
/// <param name="ChaveAtivacao">Chave de 64 caracteres hexadecimais enviada em <c>X-Jogador-Ativacao</c>.</param>
public sealed record Perfil(string JogadorId, string Nome, string ChaveAtivacao)
{
    /// <summary>Evita que a chave apareça em logs.</summary>
    public override string ToString() => $"Perfil {{ JogadorId = {JogadorId}, Nome = {Nome} }}";
}
