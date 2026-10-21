namespace DominoQuina.Core.Dominio;

/// <summary>
/// Próxima ação que a API espera deste participante.
/// </summary>
public enum ProximaAcao
{
    /// <summary>Não é a vez deste participante (ou a partida ainda não começou).</summary>
    Aguardar = 0,

    /// <summary>É a vez deste participante e ele deve jogar a pedra obrigatória de abertura.</summary>
    JogarAbertura = 1,

    /// <summary>É a vez deste participante e há pedra para encaixar.</summary>
    Jogar = 2,

    /// <summary>É a vez deste participante, não há encaixe e a cava permite compra.</summary>
    Comprar = 3,

    /// <summary>É a vez deste participante, não há encaixe nem cava: ele deve passar.</summary>
    Passar = 4,

    /// <summary>A partida terminou.</summary>
    Encerrar = 5,

    /// <summary>A API enviou um valor que este cliente não reconhece.</summary>
    Desconhecida = 99,
}

/// <summary>
/// Conversão tolerante do texto enviado pela API para <see cref="ProximaAcao"/>.
/// </summary>
public static class ProximaAcaoTexto
{
    /// <summary>
    /// Interpreta textos como <c>"JogarAbertura"</c>, <c>"JOGAR_ABERTURA"</c>, <c>"jogar-abertura"</c>
    /// ou o número do enum.
    /// </summary>
    public static ProximaAcao Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return ProximaAcao.Desconhecida;

        if (int.TryParse(texto, out var numero))
        {
            return Enum.IsDefined(typeof(ProximaAcao), numero) ? (ProximaAcao)numero : ProximaAcao.Desconhecida;
        }

        var normalizado = new string(texto.Where(char.IsLetter).ToArray()).ToLowerInvariant();

        // A ordem importa: "jogarabertura" contém "jogar".
        if (normalizado.Contains("abertura")) return ProximaAcao.JogarAbertura;
        if (normalizado.Contains("jogar")) return ProximaAcao.Jogar;
        if (normalizado.Contains("compr")) return ProximaAcao.Comprar;
        if (normalizado.Contains("pass")) return ProximaAcao.Passar;
        if (normalizado.Contains("encerr") || normalizado.Contains("finaliz") || normalizado.Contains("fim"))
            return ProximaAcao.Encerrar;
        if (normalizado.Contains("aguard") || normalizado.Contains("esper")) return ProximaAcao.Aguardar;

        return ProximaAcao.Desconhecida;
    }

    /// <summary>Texto amigável para exibir na tela.</summary>
    public static string Descrever(ProximaAcao acao) => acao switch
    {
        ProximaAcao.Aguardar => "Aguardar",
        ProximaAcao.JogarAbertura => "Jogar abertura",
        ProximaAcao.Jogar => "Jogar",
        ProximaAcao.Comprar => "Comprar",
        ProximaAcao.Passar => "Passar",
        ProximaAcao.Encerrar => "Encerrada",
        _ => "Desconhecida",
    };
}
