using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;

namespace DominoQuina.Core.Autonomo;

/// <summary>
/// Escolhe o que o jogador autônomo faz no turno.
/// </summary>
public interface IEstrategia
{
    /// <summary>
    /// Ações candidatas em ordem de preferência. Todas devem ser permitidas pelas regras;
    /// o jogador autônomo tenta a próxima se a API rejeitar a anterior.
    /// </summary>
    /// <returns>Lista vazia quando não é a vez deste participante.</returns>
    IReadOnlyList<Decisao> Candidatas(EstadoJogo estado);
}
