using System.Net;
using System.Text.Json;

namespace DominoQuina.Core.Api;

/// <summary>
/// Rejeição enviada pela API (qualquer resposta HTTP fora da faixa 2xx).
/// </summary>
/// <remarks>
/// Uma ação que termina nesta exceção <b>não</b> foi aplicada: o cliente deve mostrar
/// <see cref="MensagemParaUsuario"/> e continuar exibindo o estado confirmado anterior.
/// </remarks>
public sealed class ApiException : Exception
{
    /// <summary>Código de erro da API para perfil ativo em outro cliente.</summary>
    public const string CodigoJogadorJaAtivo = "JOGADOR_JA_ATIVO";

    /// <summary>Cria a exceção a partir dos dados da resposta.</summary>
    public ApiException(HttpStatusCode statusCode, string? codigo, string? detalhe, TimeSpan? aguardarPor = null, string? corpoBruto = null)
        : base(MontarMensagem(statusCode, codigo, detalhe))
    {
        StatusCode = statusCode;
        Codigo = codigo;
        Detalhe = detalhe;
        AguardarPor = aguardarPor;
        CorpoBruto = corpoBruto;
    }

    /// <summary>Status HTTP recebido.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Código de erro da API, quando informado (ex.: <c>JOGADOR_JA_ATIVO</c>).</summary>
    public string? Codigo { get; }

    /// <summary>Descrição do erro enviada pela API.</summary>
    public string? Detalhe { get; }

    /// <summary>Tempo de espera indicado pela API em respostas 429.</summary>
    public TimeSpan? AguardarPor { get; }

    /// <summary>Corpo original da resposta.</summary>
    public string? CorpoBruto { get; }

    /// <summary>O perfil está ativo em outro cliente (409 <c>JOGADOR_JA_ATIVO</c>).</summary>
    public bool JogadorJaAtivo => string.Equals(Codigo, CodigoJogadorJaAtivo, StringComparison.OrdinalIgnoreCase);

    /// <summary>A versão enviada não é mais a atual (409 por conflito de versão).</summary>
    public bool ConflitoDeVersao => StatusCode == HttpStatusCode.Conflict && !JogadorJaAtivo;

    /// <summary>Explicação para a pessoa que está jogando, seguindo a tabela de rejeições do checkpoint.</summary>
    public string MensagemParaUsuario
    {
        get
        {
            var detalhe = string.IsNullOrWhiteSpace(Detalhe) ? "" : $" Detalhe da API: {Detalhe}";
            return StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Token ausente, inválido ou revogado. Solicite um novo token por e-mail." + detalhe,
                HttpStatusCode.Forbidden => "Este usuário ou perfil não tem permissão para esta ação." + detalhe,
                HttpStatusCode.Conflict when JogadorJaAtivo =>
                    "Este perfil já está ativo em outro cliente. Libere-o no cliente atual (Desativar) antes de usá-lo aqui." + detalhe,
                HttpStatusCode.Conflict => "O estado da partida mudou antes da sua ação (conflito de versão). A mesa foi atualizada com o estado oficial." + detalhe,
                HttpStatusCode.UnprocessableEntity => "Ação rejeitada: jogada, encaixe, turno ou passagem inválida. A partida não avançou." + detalhe,
                HttpStatusCode.TooManyRequests => AguardarPor is { } espera
                    ? $"Limite de solicitação de token atingido. Aguarde {Math.Ceiling(espera.TotalSeconds)} s e tente de novo." + detalhe
                    : "Limite de solicitação de token atingido. Aguarde alguns instantes e tente de novo." + detalhe,
                HttpStatusCode.NotFound => "Recurso não encontrado na API." + detalhe,
                _ => $"A API respondeu {(int)StatusCode} ({StatusCode})." + detalhe,
            };
        }
    }

    /// <summary>Monta a exceção lendo código, detalhe e espera do corpo e dos cabeçalhos.</summary>
    public static ApiException Criar(HttpResponseMessage resposta, string corpo)
    {
        string? codigo = null;
        string? detalhe = null;
        TimeSpan? aguardar = resposta.Headers.RetryAfter?.Delta
            ?? (resposta.Headers.RetryAfter?.Date is { } data ? data - DateTimeOffset.UtcNow : null);

        try
        {
            if (!string.IsNullOrWhiteSpace(corpo))
            {
                using var documento = JsonDocument.Parse(corpo);
                var raiz = documento.RootElement;
                codigo = JsonApi.LerTexto(raiz, "codigo", "code", "erro", "error");
                detalhe = JsonApi.LerTexto(raiz, "mensagem", "message", "detail", "detalhe", "title");
                detalhe = AnexarErrosDeValidacao(raiz, detalhe);

                if (aguardar is null && JsonApi.LerTexto(raiz, "aguardarSegundos", "retryAfter", "segundos") is { } s
                    && double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var segundos))
                {
                    aguardar = TimeSpan.FromSeconds(segundos);
                }
            }
        }
        catch (JsonException)
        {
            detalhe = corpo.Length > 300 ? corpo[..300] : corpo;
        }

        if (codigo is null && corpo.Contains(CodigoJogadorJaAtivo, StringComparison.OrdinalIgnoreCase))
            codigo = CodigoJogadorJaAtivo;

        return new ApiException(resposta.StatusCode, codigo, detalhe, aguardar, corpo);
    }

    private static string? AnexarErrosDeValidacao(JsonElement raiz, string? detalhe)
    {
        // ProblemDetails de validação do ASP.NET: { "errors": { "campo": ["mensagem"] } }
        if (!JsonApi.TentarObter(raiz, "errors", out var erros) || erros.ValueKind != JsonValueKind.Object)
            return detalhe;

        var mensagens = erros.EnumerateObject()
            .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array
                ? p.Value.EnumerateArray().Select(m => $"{p.Name}: {m}")
                : [$"{p.Name}: {p.Value}"]);

        var texto = string.Join("; ", mensagens);
        return string.IsNullOrEmpty(detalhe) ? texto : $"{detalhe} ({texto})";
    }

    private static string MontarMensagem(HttpStatusCode status, string? codigo, string? detalhe) =>
        $"HTTP {(int)status}{(codigo is null ? "" : $" {codigo}")}{(detalhe is null ? "" : $": {detalhe}")}";
}
