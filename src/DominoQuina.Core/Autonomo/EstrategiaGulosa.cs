using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;

namespace DominoQuina.Core.Autonomo;

/// <summary>
/// Estratégia gulosa para o dominó ponta de quina.
/// </summary>
/// <remarks>
/// Entre as jogadas que encaixam, prefere, nesta ordem:
/// <list type="number">
/// <item>a que faz mais pontos agora (soma das pontas múltipla de 5);</item>
/// <item>a que não entrega pontos fáceis ao adversário na resposta seguinte;</item>
/// <item>duplas, que são mais difíceis de encaixar depois;</item>
/// <item>a pedra de maior soma, para reduzir os pontos presos na mão se a mesa travar.</item>
/// </list>
/// Sem encaixe, compra (se a cava permitir) ou passa, conforme <see cref="RegrasDomino.Permitidas"/>.
/// </remarks>
public sealed class EstrategiaGulosa : IEstrategia
{
    /// <inheritdoc />
    public IReadOnlyList<Decisao> Candidatas(EstadoJogo estado)
    {
        var permitidas = RegrasDomino.Permitidas(estado);

        if (permitidas.PodeJogar)
        {
            return permitidas.Jogadas
                .OrderByDescending(j => j.PontosEstimados)
                .ThenBy(j => MelhorRespostaPossivel(estado, j))
                .ThenByDescending(j => j.Pedra.EhDupla)
                .ThenByDescending(j => j.Pedra.Soma)
                .Select(j => Decisao.Jogar(j.Pedra, j.Lado))
                .ToList();
        }

        if (permitidas.PodeComprar) return [Decisao.Comprar];
        if (permitidas.PodePassar) return [Decisao.Passar];
        return [];
    }

    /// <summary>
    /// Maior pontuação que qualquer pedra (0 a 6) encaixada depois desta jogada poderia fazer.
    /// Não conhece a mão do adversário: considera o pior caso.
    /// </summary>
    private static int MelhorRespostaPossivel(EstadoJogo estado, JogadaPossivel jogada)
    {
        int esquerda, direita;
        if (estado.PontaEsquerda is not { } e || estado.PontaDireita is not { } d)
        {
            (esquerda, direita) = (jogada.Pedra.ValorA, jogada.Pedra.ValorB);
        }
        else
        {
            esquerda = jogada.Lado == Lado.Esquerda ? jogada.Pedra.ValorExpostoAoEncaixar(e) : e;
            direita = jogada.Lado == Lado.Direita ? jogada.Pedra.ValorExpostoAoEncaixar(d) : d;
        }

        var melhor = 0;
        for (var valor = 0; valor <= 6; valor++)
        {
            // Uma pedra [esquerda|valor] deixa a ponta esquerda em "valor"; idem à direita.
            melhor = Math.Max(melhor, RegrasDomino.PontosPorSoma(valor + direita));
            melhor = Math.Max(melhor, RegrasDomino.PontosPorSoma(esquerda + valor));
        }

        return melhor;
    }
}
