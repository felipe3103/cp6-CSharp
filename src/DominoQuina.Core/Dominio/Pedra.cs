namespace DominoQuina.Core.Dominio;

/// <summary>
/// Uma pedra de dominó com dois valores entre 0 e 6.
/// </summary>
/// <remarks>
/// A ordem dos valores importa apenas para a exibição na mesa: <c>[2|5]</c> e <c>[5|2]</c>
/// são a mesma pedra, o que pode ser verificado com <see cref="MesmaPedra"/>.
/// </remarks>
/// <param name="ValorA">Valor da primeira metade (esquerda ou superior).</param>
/// <param name="ValorB">Valor da segunda metade (direita ou inferior).</param>
public readonly record struct Pedra(int ValorA, int ValorB)
{
    /// <summary>Indica se as duas metades têm o mesmo valor (carroça).</summary>
    public bool EhDupla => ValorA == ValorB;

    /// <summary>Soma dos dois valores da pedra.</summary>
    public int Soma => ValorA + ValorB;

    /// <summary>Indica se alguma das metades tem o valor informado.</summary>
    public bool Contem(int valor) => ValorA == valor || ValorB == valor;

    /// <summary>Retorna a mesma pedra com as metades trocadas.</summary>
    public Pedra Invertida() => new(ValorB, ValorA);

    /// <summary>Compara duas pedras ignorando a orientação.</summary>
    public bool MesmaPedra(Pedra outra) =>
        (ValorA == outra.ValorA && ValorB == outra.ValorB) ||
        (ValorA == outra.ValorB && ValorB == outra.ValorA);

    /// <summary>
    /// Valor que fica exposto na ponta depois de encaixar esta pedra numa ponta de valor <paramref name="ponta"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A pedra não encaixa na ponta.</exception>
    public int ValorExpostoAoEncaixar(int ponta)
    {
        if (ValorA == ponta) return ValorB;
        if (ValorB == ponta) return ValorA;
        throw new InvalidOperationException($"A pedra {this} não encaixa na ponta {ponta}.");
    }

    /// <inheritdoc />
    public override string ToString() => $"[{ValorA}|{ValorB}]";
}
