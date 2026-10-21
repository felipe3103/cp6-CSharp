namespace DominoQuina.Core.Dominio;

/// <summary>
/// Uma jogada que encaixa na mesa atual.
/// </summary>
/// <param name="Pedra">Pedra da mão.</param>
/// <param name="Lado">Ponta onde será encaixada.</param>
/// <param name="PontosEstimados">Pontos que a jogada deve render, segundo <see cref="RegrasDomino.PontosDaJogada"/>.</param>
public sealed record JogadaPossivel(Pedra Pedra, Lado Lado, int PontosEstimados);

/// <summary>
/// O que o participante pode fazer no estado atual.
/// </summary>
/// <param name="Jogadas">Jogadas que encaixam.</param>
/// <param name="PodeComprar">Comprar uma pedra da cava é permitido.</param>
/// <param name="PodePassar">Passar a vez é permitido.</param>
public sealed record AcoesPermitidas(IReadOnlyList<JogadaPossivel> Jogadas, bool PodeComprar, bool PodePassar)
{
    /// <summary>Nenhuma ação (fora da vez ou partida encerrada).</summary>
    public static readonly AcoesPermitidas Nenhuma = new([], false, false);

    /// <summary>Indica se há alguma pedra para jogar.</summary>
    public bool PodeJogar => Jogadas.Count > 0;

    /// <summary>Jogadas possíveis com a pedra informada.</summary>
    public IReadOnlyList<JogadaPossivel> JogadasCom(Pedra pedra) => Jogadas.Where(j => j.Pedra.MesmaPedra(pedra)).ToList();
}

/// <summary>
/// Regras do dominó ponta de quina usadas pelo cliente para destacar jogadas e orientar o jogador autônomo.
/// </summary>
/// <remarks>
/// A API é a autoridade: ela valida a jogada e informa a pontuação oficial.
/// Estas regras servem só para a interface e para a estratégia; nunca substituem a resposta do servidor.
/// </remarks>
public static class RegrasDomino
{
    /// <summary>Pontos ganhos quando a soma das pontas é <paramref name="somaPontas"/>.</summary>
    /// <returns><c>soma ÷ 5</c> se a soma for múltipla de 5 e maior que zero; caso contrário, 0.</returns>
    public static int PontosPorSoma(int somaPontas) =>
        somaPontas > 0 && somaPontas % 5 == 0 ? somaPontas / 5 : 0;

    /// <summary>
    /// Estima os pontos de encaixar <paramref name="pedra"/> na ponta <paramref name="lado"/>.
    /// </summary>
    /// <param name="pontaEsquerda">Ponta esquerda atual; <c>null</c> com a mesa vazia.</param>
    /// <param name="pontaDireita">Ponta direita atual; <c>null</c> com a mesa vazia.</param>
    /// <param name="pedra">Pedra a jogar.</param>
    /// <param name="lado">Ponta escolhida.</param>
    public static int PontosDaJogada(int? pontaEsquerda, int? pontaDireita, Pedra pedra, Lado lado)
    {
        if (pontaEsquerda is null || pontaDireita is null)
        {
            // Abertura: a pedra fica sozinha e as duas pontas são as suas metades.
            return PontosPorSoma(pedra.Soma);
        }

        var novaEsquerda = lado == Lado.Esquerda ? pedra.ValorExpostoAoEncaixar(pontaEsquerda.Value) : pontaEsquerda.Value;
        var novaDireita = lado == Lado.Direita ? pedra.ValorExpostoAoEncaixar(pontaDireita.Value) : pontaDireita.Value;
        return PontosPorSoma(novaEsquerda + novaDireita);
    }

    /// <summary>
    /// Lista as jogadas que encaixam na mesa, respeitando a pedra obrigatória de abertura.
    /// </summary>
    public static IReadOnlyList<JogadaPossivel> JogadasValidas(EstadoJogo estado) =>
        JogadasValidas(estado.Mao, estado.PontaEsquerda, estado.PontaDireita, estado.Mesa.Count == 0, estado.PedraAbertura);

    /// <summary>
    /// Lista as jogadas que encaixam nas pontas informadas.
    /// </summary>
    /// <param name="mao">Pedras disponíveis.</param>
    /// <param name="pontaEsquerda">Ponta esquerda atual.</param>
    /// <param name="pontaDireita">Ponta direita atual.</param>
    /// <param name="mesaVazia">Indica se ainda não há pedra na mesa.</param>
    /// <param name="pedraAbertura">Pedra obrigatória de abertura, se houver.</param>
    public static IReadOnlyList<JogadaPossivel> JogadasValidas(
        IReadOnlyList<Pedra> mao, int? pontaEsquerda, int? pontaDireita, bool mesaVazia, Pedra? pedraAbertura = null)
    {
        var jogadas = new List<JogadaPossivel>();

        if (mesaVazia || pontaEsquerda is null || pontaDireita is null)
        {
            // Na abertura os dois lados são equivalentes; envia-se sempre a esquerda.
            var candidatas = pedraAbertura is { } obrigatoria
                ? mao.Where(p => p.MesmaPedra(obrigatoria))
                : mao;

            foreach (var pedra in candidatas)
                jogadas.Add(new JogadaPossivel(pedra, Lado.Esquerda, PontosPorSoma(pedra.Soma)));

            return jogadas;
        }

        foreach (var pedra in mao)
        {
            if (pedra.Contem(pontaEsquerda.Value))
                jogadas.Add(new JogadaPossivel(pedra, Lado.Esquerda, PontosDaJogada(pontaEsquerda, pontaDireita, pedra, Lado.Esquerda)));

            if (pedra.Contem(pontaDireita.Value))
                jogadas.Add(new JogadaPossivel(pedra, Lado.Direita, PontosDaJogada(pontaEsquerda, pontaDireita, pedra, Lado.Direita)));
        }

        return jogadas;
    }

    /// <summary>
    /// Ações que as regras permitem neste estado. Tela e jogador autônomo usam este método
    /// para nunca enviar um movimento proibido (que, pela regra futura, desconta 1 ponto).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Fora da vez: nenhuma ação.</item>
    /// <item>Abertura: só a pedra obrigatória, quando indicada.</item>
    /// <item>Com encaixe: só jogar; comprar e passar ficam bloqueados.</item>
    /// <item>Sem encaixe: comprar se a cava permitir; passar só sem cava.</item>
    /// </list>
    /// Quando a API informa <see cref="ProximaAcao.Comprar"/> ou <see cref="ProximaAcao.Passar"/>, vale a API.
    /// </remarks>
    public static AcoesPermitidas Permitidas(EstadoJogo estado)
    {
        if (!estado.MinhaVez) return AcoesPermitidas.Nenhuma;

        switch (estado.ProximaAcao)
        {
            case ProximaAcao.Comprar:
                return new AcoesPermitidas([], PodeComprar: true, PodePassar: false);
            case ProximaAcao.Passar:
                return new AcoesPermitidas([], PodeComprar: false, PodePassar: true);
        }

        var jogadas = JogadasValidas(estado);
        if (jogadas.Count > 0) return new AcoesPermitidas(jogadas, PodeComprar: false, PodePassar: false);

        // A API pediu jogada mas nada encaixa pela visão local: segue a regra de compra/passagem.
        var semCava = estado.PedrasNaCava is 0;
        return new AcoesPermitidas([], PodeComprar: !semCava, PodePassar: semCava);
    }

    /// <summary>
    /// Desconto previsto pela regra futura de demora, a partir do tempo gasto para agir.
    /// </summary>
    /// <returns>0 até 10 s; 1 até 30 s; 2 abaixo de 60 s; 3 a partir de 60 s.</returns>
    public static int DescontoPorDemora(TimeSpan tempo) => tempo.TotalSeconds switch
    {
        <= 10 => 0,
        <= 30 => 1,
        < 60 => 2,
        _ => 3,
    };
}
