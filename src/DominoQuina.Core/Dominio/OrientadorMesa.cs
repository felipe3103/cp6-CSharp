namespace DominoQuina.Core.Dominio;

/// <summary>
/// Ajusta a orientação das pedras da mesa para que as metades vizinhas coincidam na exibição.
/// </summary>
public static class OrientadorMesa
{
    /// <summary>
    /// Devolve as pedras na mesma ordem, invertendo as que estiverem ao contrário.
    /// </summary>
    /// <param name="mesa">Pedras da ponta esquerda para a direita, como recebidas da API.</param>
    /// <param name="pontaEsquerda">Ponta esquerda informada pela API, usada quando há uma só pedra.</param>
    public static IReadOnlyList<Pedra> Orientar(IReadOnlyList<Pedra> mesa, int? pontaEsquerda = null)
    {
        if (mesa.Count == 0) return [];

        var orientadas = new List<Pedra>(mesa.Count);
        var primeira = mesa[0];

        if (mesa.Count > 1)
        {
            // A metade direita da primeira pedra precisa tocar a segunda.
            var segunda = mesa[1];
            if (!segunda.Contem(primeira.ValorB) && segunda.Contem(primeira.ValorA))
                primeira = primeira.Invertida();
        }
        else if (pontaEsquerda is { } esquerda && primeira.ValorA != esquerda && primeira.ValorB == esquerda)
        {
            primeira = primeira.Invertida();
        }

        orientadas.Add(primeira);

        for (var i = 1; i < mesa.Count; i++)
        {
            var anterior = orientadas[^1].ValorB;
            var pedra = mesa[i];
            if (pedra.ValorA != anterior && pedra.ValorB == anterior)
                pedra = pedra.Invertida();
            orientadas.Add(pedra);
        }

        return orientadas;
    }
}
