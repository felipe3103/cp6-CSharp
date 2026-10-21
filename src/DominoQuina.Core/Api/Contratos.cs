using System.Text.Json.Serialization;

namespace DominoQuina.Core.Api;

// Contratos (DTOs) trocados com a API.
//
// Os corpos das requisições seguem exatamente os exemplos dos slides do checkpoint.
// Os nomes dos campos das respostas foram deduzidos do material e devem ser conferidos no
// Swagger (/swagger/v1/swagger.json). Este é o único arquivo que precisa mudar se forem diferentes.
// A serialização usa camelCase e ignora maiúsculas/minúsculas (ver JsonApi).

/// <summary>Corpo de <c>POST /api/identidade/token</c>.</summary>
public sealed record SolicitarTokenRequest(string Email);

/// <summary>Corpo de <c>POST /api/jogadores</c>.</summary>
public sealed record CriarJogadorRequest(string Nome);

/// <summary>Corpo de <c>POST /api/jogadores/{id}/ativar-cliente</c>.</summary>
public sealed record AtivarClienteRequest(string ChaveAtivacao);

/// <summary>Corpo de <c>POST /api/partidas</c>.</summary>
public sealed record CriarPartidaRequest(int PontuacaoAlvo, string JogadorId);

/// <summary>Corpo de <c>POST /api/partidas/{id}/entrar</c> e <c>/iniciar</c>.</summary>
public sealed record ParticipacaoRequest(string JogadorId);

/// <summary>Corpo de <c>/comprar</c> e <c>/passar</c>.</summary>
public sealed record ComandoRequest(string JogadorId, long VersaoEsperada, Guid ChaveIdempotencia);

/// <summary>Corpo de <c>POST /api/partidas/{id}/jogar</c>.</summary>
public sealed record JogarRequest(string JogadorId, long VersaoEsperada, Guid ChaveIdempotencia, int ValorA, int ValorB, int Lado);

/// <summary>Jogador-perfil da conta.</summary>
public sealed class JogadorDto
{
    /// <summary>Identificador (quando a API usa <c>id</c>).</summary>
    public string? Id { get; init; }

    /// <summary>Identificador (quando a API usa <c>jogadorId</c>).</summary>
    public string? JogadorId { get; init; }

    /// <summary>Nome do perfil.</summary>
    public string? Nome { get; init; }

    /// <summary>Identificador efetivo, independentemente do nome do campo.</summary>
    [JsonIgnore]
    public string Identificador => JogadorId ?? Id ?? "";

    /// <summary>Nome para exibição.</summary>
    [JsonIgnore]
    public string NomeExibicao => string.IsNullOrWhiteSpace(Nome) ? Identificador : Nome;
}

/// <summary>Pedra como trafega na API.</summary>
public sealed record PedraDto(int ValorA, int ValorB);

/// <summary>Participante de uma partida.</summary>
public sealed class ParticipanteDto
{
    /// <summary>Perfil do participante.</summary>
    public string? JogadorId { get; init; }

    /// <summary>Nome do perfil.</summary>
    public string? Nome { get; init; }

    /// <summary>Pontuação na partida.</summary>
    public int Pontos { get; init; }

    /// <summary>Alternativa a <see cref="Pontos"/> usada por algumas respostas.</summary>
    public int? Pontuacao { get; init; }
}

/// <summary>Item de <c>GET /api/partidas</c>.</summary>
public sealed class PartidaResumoDto
{
    /// <summary>Identificador (quando a API usa <c>id</c>).</summary>
    public string? Id { get; init; }

    /// <summary>Identificador (quando a API usa <c>partidaId</c>).</summary>
    public string? PartidaId { get; init; }

    /// <summary>Pontuação alvo.</summary>
    public int PontuacaoAlvo { get; init; }

    /// <summary>Situação da partida.</summary>
    public string? Situacao { get; init; }

    /// <summary>Quantidade de participantes, quando informada.</summary>
    public int? QuantidadeJogadores { get; init; }

    /// <summary>Vagas restantes, quando informadas.</summary>
    public int? Vagas { get; init; }

    /// <summary>Participantes, quando informados.</summary>
    public List<ParticipanteDto>? Participantes { get; init; }

    /// <summary>Identificador efetivo.</summary>
    [JsonIgnore]
    public string Identificador => PartidaId ?? Id ?? "";

    /// <summary>Quantos dos dois lugares da mesa estão ocupados.</summary>
    [JsonIgnore]
    public int Ocupacao => QuantidadeJogadores ?? Participantes?.Count ?? (Vagas is { } v ? 2 - v : 1);
}

/// <summary>Estado da partida na visão de um participante.</summary>
public sealed class EstadoPartidaDto
{
    /// <summary>Identificador (quando a API usa <c>id</c>).</summary>
    public string? Id { get; init; }

    /// <summary>Identificador (quando a API usa <c>partidaId</c>).</summary>
    public string? PartidaId { get; init; }

    /// <summary>Versão do estado.</summary>
    public long Versao { get; init; }

    /// <summary>Situação da partida.</summary>
    public string? Situacao { get; init; }

    /// <summary>Rodada atual.</summary>
    public int Rodada { get; init; }

    /// <summary>Turno atual.</summary>
    public int Turno { get; init; }

    /// <summary>Pontuação alvo.</summary>
    public int PontuacaoAlvo { get; init; }

    /// <summary>Próxima ação esperada deste participante.</summary>
    public string? ProximaAcao { get; init; }

    /// <summary>Perfil com a vez.</summary>
    public string? JogadorDaVezId { get; init; }

    /// <summary>Pedra obrigatória de abertura.</summary>
    public PedraDto? PedraAbertura { get; init; }

    /// <summary>Mão deste participante.</summary>
    public List<PedraDto>? Mao { get; init; }

    /// <summary>Pedras jogadas, da esquerda para a direita.</summary>
    public List<PedraDto>? Mesa { get; init; }

    /// <summary>Valor da ponta esquerda.</summary>
    public int? PontaEsquerda { get; init; }

    /// <summary>Valor da ponta direita.</summary>
    public int? PontaDireita { get; init; }

    /// <summary>Quantidade de pedras do adversário.</summary>
    public int QuantidadePedrasAdversario { get; init; }

    /// <summary>Pedras restantes na cava.</summary>
    public int? PedrasNaCava { get; init; }

    /// <summary>Placar por participante.</summary>
    public List<ParticipanteDto>? Placar { get; init; }

    /// <summary>Participantes, usado quando <see cref="Placar"/> não vier.</summary>
    public List<ParticipanteDto>? Participantes { get; init; }

    /// <summary>Perfil vencedor.</summary>
    public string? VencedorId { get; init; }
}

/// <summary>Resposta de <c>GET /api/partidas/{id}/resultado</c>.</summary>
public sealed class ResultadoPartidaDto
{
    /// <summary>Situação final.</summary>
    public string? Situacao { get; init; }

    /// <summary>Perfil vencedor.</summary>
    public string? VencedorId { get; init; }

    /// <summary>Nome do vencedor, quando informado.</summary>
    public string? VencedorNome { get; init; }

    /// <summary>Placar final.</summary>
    public List<ParticipanteDto>? Placar { get; init; }

    /// <summary>Participantes, usado quando <see cref="Placar"/> não vier.</summary>
    public List<ParticipanteDto>? Participantes { get; init; }

    /// <summary>JSON recebido, para exibição.</summary>
    [JsonIgnore]
    public string? JsonBruto { get; set; }
}
