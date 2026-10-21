using DominoQuina.Core.Dominio;

namespace DominoQuina.Core.Jogo;

/// <summary>Tipo de ação do turno.</summary>
public enum TipoDecisao
{
    /// <summary>Jogar uma pedra.</summary>
    Jogar,

    /// <summary>Comprar da cava.</summary>
    Comprar,

    /// <summary>Passar a vez.</summary>
    Passar,
}

/// <summary>
/// Uma ação de turno escolhida pela pessoa ou pelo jogador autônomo.
/// </summary>
/// <param name="Tipo">Tipo da ação.</param>
/// <param name="Pedra">Pedra, quando <see cref="TipoDecisao.Jogar"/>.</param>
/// <param name="Lado">Ponta, quando <see cref="TipoDecisao.Jogar"/>.</param>
public sealed record Decisao(TipoDecisao Tipo, Pedra? Pedra = null, Lado Lado = Lado.Esquerda)
{
    /// <summary>Jogar <paramref name="pedra"/> em <paramref name="lado"/>.</summary>
    public static Decisao Jogar(Pedra pedra, Lado lado) => new(TipoDecisao.Jogar, pedra, lado);

    /// <summary>Comprar uma pedra.</summary>
    public static readonly Decisao Comprar = new(TipoDecisao.Comprar);

    /// <summary>Passar a vez.</summary>
    public static readonly Decisao Passar = new(TipoDecisao.Passar);

    /// <inheritdoc />
    public override string ToString() => Tipo switch
    {
        TipoDecisao.Jogar => $"jogar {Pedra} na {(Lado == Lado.Esquerda ? "esquerda" : "direita")}",
        TipoDecisao.Comprar => "comprar",
        _ => "passar",
    };
}
