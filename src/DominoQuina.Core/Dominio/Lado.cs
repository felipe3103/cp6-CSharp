namespace DominoQuina.Core.Dominio;

/// <summary>
/// Ponta da mesa onde a pedra será encaixada. Os valores numéricos são os esperados pela API.
/// </summary>
public enum Lado
{
    /// <summary>Ponta esquerda (<c>lado = 0</c>).</summary>
    Esquerda = 0,

    /// <summary>Ponta direita (<c>lado = 1</c>).</summary>
    Direita = 1,
}
