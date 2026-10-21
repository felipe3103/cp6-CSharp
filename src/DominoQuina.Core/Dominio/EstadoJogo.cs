namespace DominoQuina.Core.Dominio;

/// <summary>
/// Visão de uma partida para um participante, já confirmada pela API.
/// </summary>
/// <remarks>
/// Contém somente o que o participante pode ver: a própria mão, a mesa e a quantidade
/// de pedras do adversário. A mão do adversário nunca chega a este objeto.
/// </remarks>
public sealed record EstadoJogo
{
    /// <summary>Identificador da partida.</summary>
    public required string PartidaId { get; init; }

    /// <summary>Perfil dono desta visão.</summary>
    public required string JogadorId { get; init; }

    /// <summary>Versão do estado. Comandos enviam este valor como <c>versaoEsperada</c>.</summary>
    public long Versao { get; init; }

    /// <summary>Situação textual informada pela API (ex.: aguardando jogadores, em andamento).</summary>
    public string Situacao { get; init; } = "";

    /// <summary>Número da rodada atual.</summary>
    public int Rodada { get; init; }

    /// <summary>Número do turno atual.</summary>
    public int Turno { get; init; }

    /// <summary>Pontuação que encerra a partida.</summary>
    public int PontuacaoAlvo { get; init; }

    /// <summary>Próxima ação esperada deste participante.</summary>
    public ProximaAcao ProximaAcao { get; init; }

    /// <summary>Perfil que está com a vez, quando informado.</summary>
    public string? JogadorDaVezId { get; init; }

    /// <summary>Pedra obrigatória de abertura, quando indicada pela API.</summary>
    public Pedra? PedraAbertura { get; init; }

    /// <summary>Pedras na mão deste participante.</summary>
    public IReadOnlyList<Pedra> Mao { get; init; } = [];

    /// <summary>Pedras jogadas, da ponta esquerda para a direita.</summary>
    public IReadOnlyList<Pedra> Mesa { get; init; } = [];

    /// <summary>Valor exposto na ponta esquerda, ou <c>null</c> com a mesa vazia.</summary>
    public int? PontaEsquerda { get; init; }

    /// <summary>Valor exposto na ponta direita, ou <c>null</c> com a mesa vazia.</summary>
    public int? PontaDireita { get; init; }

    /// <summary>Quantidade de pedras na mão do adversário.</summary>
    public int QuantidadePedrasAdversario { get; init; }

    /// <summary>Pedras restantes na cava, quando informado.</summary>
    public int? PedrasNaCava { get; init; }

    /// <summary>Placar de cada participante.</summary>
    public IReadOnlyList<PlacarJogador> Placar { get; init; } = [];

    /// <summary>Perfil vencedor, quando a partida termina.</summary>
    public string? VencedorId { get; init; }

    /// <summary>Indica se a partida terminou.</summary>
    public bool Encerrada { get; init; }

    /// <summary>JSON recebido da API, mantido para depuração na tela.</summary>
    public string? JsonBruto { get; init; }

    /// <summary>Indica se a API espera uma ação deste participante agora.</summary>
    public bool MinhaVez => !Encerrada && ProximaAcao is
        ProximaAcao.JogarAbertura or ProximaAcao.Jogar or ProximaAcao.Comprar or ProximaAcao.Passar;

    /// <summary>Indica se as pedras já foram distribuídas.</summary>
    public bool Iniciada => Mao.Count > 0 || Mesa.Count > 0 || MinhaVez || Encerrada;

    /// <summary>Placar deste participante, se presente.</summary>
    public PlacarJogador? MeuPlacar => Placar.FirstOrDefault(p => p.EhVoce);

    /// <summary>Placar do adversário, se presente.</summary>
    public PlacarJogador? PlacarAdversario => Placar.FirstOrDefault(p => !p.EhVoce);
}

/// <summary>
/// Pontuação de um participante na partida.
/// </summary>
/// <param name="JogadorId">Perfil do participante.</param>
/// <param name="Nome">Nome do perfil, quando informado.</param>
/// <param name="Pontos">Pontos oficiais informados pela API.</param>
/// <param name="EhVoce">Indica se é o perfil ativo neste cliente.</param>
public sealed record PlacarJogador(string JogadorId, string Nome, int Pontos, bool EhVoce);
