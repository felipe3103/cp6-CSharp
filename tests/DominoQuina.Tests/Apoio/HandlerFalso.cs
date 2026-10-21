using System.Net;
using System.Text;

namespace DominoQuina.Tests.Apoio;

/// <summary>
/// HttpMessageHandler programável: registra as requisições e devolve respostas enfileiradas.
/// </summary>
public sealed class HandlerFalso : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _respostas = new();

    /// <summary>Requisições recebidas, com o corpo já lido.</summary>
    public List<RequisicaoGravada> Requisicoes { get; } = [];

    /// <summary>Enfileira uma resposta com corpo JSON opcional.</summary>
    public HandlerFalso Responder(HttpStatusCode status, string? json = null)
    {
        _respostas.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json ?? "", Encoding.UTF8, "application/json"),
        });
        return this;
    }

    /// <summary>Enfileira uma falha de rede.</summary>
    public HandlerFalso FalharRede()
    {
        _respostas.Enqueue(_ => throw new HttpRequestException("conexão recusada"));
        return this;
    }

    /// <summary>Cria um <see cref="HttpClient"/> que usa este handler.</summary>
    public HttpClient CriarCliente() => new(this) { BaseAddress = new Uri("https://api.teste") };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requisicoes.Add(new RequisicaoGravada(request.Method, request.RequestUri!, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)), corpo));

        if (_respostas.Count == 0) throw new InvalidOperationException($"Resposta não programada para {request.Method} {request.RequestUri}");
        return _respostas.Dequeue()(request);
    }
}

/// <summary>Requisição registrada pelo <see cref="HandlerFalso"/>.</summary>
public sealed record RequisicaoGravada(HttpMethod Metodo, Uri Uri, Dictionary<string, string> Cabecalhos, string? Corpo)
{
    /// <summary>Caminho com query string.</summary>
    public string Caminho => Uri.PathAndQuery;
}
