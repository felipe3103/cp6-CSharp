using System.Security.Cryptography;

namespace DominoQuina.Core.Seguranca;

/// <summary>
/// Gera a chave aleatória que identifica este cliente para um jogador-perfil.
/// </summary>
public static class GeradorChaveAtivacao
{
    /// <summary>Tamanho da chave em caracteres hexadecimais.</summary>
    public const int Tamanho = 64;

    /// <summary>Gera 32 bytes criptograficamente aleatórios em 64 caracteres hexadecimais minúsculos.</summary>
    public static string Gerar() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(Tamanho / 2));

    /// <summary>Indica se o texto tem o formato de chave aceito pela API.</summary>
    public static bool EhValida(string? chave) =>
        chave is { Length: Tamanho } && chave.All(Uri.IsHexDigit);
}
