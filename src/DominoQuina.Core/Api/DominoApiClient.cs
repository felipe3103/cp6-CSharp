using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DominoQuina.Core.Dominio;

namespace DominoQuina.Core.Api;

/// <summary>
/// Cliente HTTP da API do Dominó Ponta de Quina.
/// </summary>
/// <remarks>
/// Cada método corresponde a uma rota de <see cref="RotasApi"/>. Respostas fora de 2xx viram
/// <see cref="ApiException"/>. O cliente não guarda estado de partida: isso é papel de
/// <see cref="Jogo.SessaoPartida"/>.
/// </remarks>
public sealed class DominoApiClient
{
    private readonly HttpClient _http;

    /// <summary>Cria o cliente. O <paramref name="http"/> deve ter <c>BaseAddress</c> configurado.</summary>
    public DominoApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Token Bearer enviado nas rotas protegidas.</summary>
    public string? Token { get; set; }

    // ----- Identidade e perfis -----

    /// <summary>Pede que a API envie o token ao e-mail. A primeira solicitação cria a conta.</summary>
    /// <remarks>A resposta esperada é <c>202 Accepted</c>, sem o token no corpo.</remarks>
    public async Task SolicitarTokenAsync(string email, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Token, corpo: new SolicitarTokenRequest(email), autenticar: false);
        await EnviarAsync(requisicao, ct);
    }

    /// <summary>Lista os jogadores-perfil da conta autenticada.</summary>
    public async Task<IReadOnlyList<JogadorDto>> ListarJogadoresAsync(CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, RotasApi.Jogadores);
        var resposta = await EnviarAsync(requisicao, ct);
        return JsonApi.LerLista<JogadorDto>(resposta.Corpo);
    }

    /// <summary>Cria um jogador-perfil na conta autenticada.</summary>
    public async Task<JogadorDto> CriarJogadorAsync(string nome, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Jogadores, corpo: new CriarJogadorRequest(nome));
        var resposta = await EnviarAsync(requisicao, ct);
        return Desserializar<JogadorDto>(resposta.Corpo);
    }

    /// <summary>
    /// Ativa o perfil neste cliente com a chave informada. Repetir com a mesma chave é seguro.
    /// </summary>
    public async Task AtivarClienteAsync(string jogadorId, string chaveAtivacao, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.AtivarCliente(jogadorId), corpo: new AtivarClienteRequest(chaveAtivacao));
        await EnviarAsync(requisicao, ct);
    }

    /// <summary>Libera o perfil para ser ativado em outro cliente.</summary>
    public async Task DesativarClienteAsync(Perfil perfil, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Delete, RotasApi.DesativarCliente(perfil.JogadorId), perfil);
        await EnviarAsync(requisicao, ct);
    }

    // ----- Partidas -----

    /// <summary>Lista as partidas disponíveis.</summary>
    public async Task<IReadOnlyList<PartidaResumoDto>> ListarPartidasAsync(Perfil? perfil = null, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, RotasApi.Partidas, perfil);
        var resposta = await EnviarAsync(requisicao, ct);
        return JsonApi.LerLista<PartidaResumoDto>(resposta.Corpo);
    }

    /// <summary>Cria uma partida com o perfil como primeiro participante.</summary>
    /// <returns>Identificador da partida criada.</returns>
    public async Task<string> CriarPartidaAsync(Perfil perfil, int pontuacaoAlvo, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Partidas, perfil, new CriarPartidaRequest(pontuacaoAlvo, perfil.JogadorId));
        var resposta = await EnviarAsync(requisicao, ct);
        return ExtrairIdPartida(resposta)
               ?? throw new InvalidOperationException("A API criou a partida mas não informou o identificador.");
    }

    /// <summary>Ingressa o perfil na partida. A API revalida a vaga.</summary>
    public async Task EntrarPartidaAsync(Perfil perfil, string partidaId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Entrar(partidaId), perfil, new ParticipacaoRequest(perfil.JogadorId));
        await EnviarAsync(requisicao, ct);
    }

    /// <summary>Inicia a partida (exige duas participações).</summary>
    /// <returns>O estado confirmado, se a resposta trouxer um.</returns>
    public async Task<EstadoJogo?> IniciarPartidaAsync(Perfil perfil, string partidaId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Iniciar(partidaId), perfil, new ParticipacaoRequest(perfil.JogadorId));
        var resposta = await EnviarAsync(requisicao, ct);
        return MapeadorEstado.TentarLer(resposta.Corpo, perfil.JogadorId, partidaId);
    }

    /// <summary>Obtém a visão atual da partida para o perfil.</summary>
    public async Task<EstadoJogo> ObterEstadoAsync(Perfil perfil, string partidaId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, RotasApi.Partida(partidaId, perfil.JogadorId), perfil);
        var resposta = await EnviarAsync(requisicao, ct);
        return MapeadorEstado.TentarLer(resposta.Corpo, perfil.JogadorId, partidaId)
               ?? throw new InvalidOperationException("A API não devolveu um estado de partida reconhecível.");
    }

    /// <summary>
    /// Consulta se há estado mais novo que <paramref name="versaoConhecida"/>.
    /// </summary>
    /// <returns>O novo estado (200) ou <c>null</c> se nada mudou (204).</returns>
    public async Task<EstadoJogo?> ObterAtualizacoesAsync(Perfil perfil, string partidaId, long versaoConhecida, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, RotasApi.Atualizacoes(partidaId, perfil.JogadorId, versaoConhecida), perfil);
        var resposta = await EnviarAsync(requisicao, ct);
        return resposta.Status == HttpStatusCode.NoContent
            ? null
            : MapeadorEstado.TentarLer(resposta.Corpo, perfil.JogadorId, partidaId);
    }

    /// <summary>Joga uma pedra na ponta indicada.</summary>
    /// <param name="perfil">Perfil ativo.</param>
    /// <param name="partidaId">Partida.</param>
    /// <param name="versaoEsperada">Versão do estado em que a decisão foi tomada.</param>
    /// <param name="chaveIdempotencia">UUID da intenção; repetir a mesma chave não duplica a jogada.</param>
    /// <param name="pedra">Pedra da mão.</param>
    /// <param name="lado">Ponta da mesa.</param>
    /// <param name="ct">Cancelamento.</param>
    /// <returns>O estado confirmado, se a resposta trouxer um.</returns>
    public async Task<EstadoJogo?> JogarAsync(
        Perfil perfil, string partidaId, long versaoEsperada, Guid chaveIdempotencia, Pedra pedra, Lado lado, CancellationToken ct = default)
    {
        var corpo = new JogarRequest(perfil.JogadorId, versaoEsperada, chaveIdempotencia, pedra.ValorA, pedra.ValorB, (int)lado);
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Jogar(partidaId), perfil, corpo);
        var resposta = await EnviarAsync(requisicao, ct);
        return MapeadorEstado.TentarLer(resposta.Corpo, perfil.JogadorId, partidaId);
    }

    /// <summary>Compra uma pedra da cava.</summary>
    /// <returns>O estado confirmado, se a resposta trouxer um.</returns>
    public async Task<EstadoJogo?> ComprarAsync(Perfil perfil, string partidaId, long versaoEsperada, Guid chaveIdempotencia, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Comprar(partidaId), perfil,
            new ComandoRequest(perfil.JogadorId, versaoEsperada, chaveIdempotencia));
        var resposta = await EnviarAsync(requisicao, ct);
        return MapeadorEstado.TentarLer(resposta.Corpo, perfil.JogadorId, partidaId);
    }

    /// <summary>Passa a vez. Só é aceito sem cava e sem pedra compatível.</summary>
    /// <returns>O estado confirmado, se a resposta trouxer um.</returns>
    public async Task<EstadoJogo?> PassarAsync(Perfil perfil, string partidaId, long versaoEsperada, Guid chaveIdempotencia, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Post, RotasApi.Passar(partidaId), perfil,
            new ComandoRequest(perfil.JogadorId, versaoEsperada, chaveIdempotencia));
        var resposta = await EnviarAsync(requisicao, ct);
        return MapeadorEstado.TentarLer(resposta.Corpo, perfil.JogadorId, partidaId);
    }

    /// <summary>Obtém o resultado da partida.</summary>
    public async Task<ResultadoPartidaDto> ObterResultadoAsync(Perfil perfil, string partidaId, CancellationToken ct = default)
    {
        using var requisicao = Criar(HttpMethod.Get, RotasApi.Resultado(partidaId, perfil.JogadorId), perfil);
        var resposta = await EnviarAsync(requisicao, ct);
        var resultado = Desserializar<ResultadoPartidaDto>(resposta.Corpo);
        resultado.JsonBruto = resposta.Corpo;
        return resultado;
    }

    // ----- Infraestrutura -----

    private HttpRequestMessage Criar(HttpMethod metodo, string rota, Perfil? perfil = null, object? corpo = null, bool autenticar = true)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);

        if (autenticar && !string.IsNullOrWhiteSpace(Token))
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        if (perfil is not null)
            requisicao.Headers.Add(RotasApi.CabecalhoAtivacao, perfil.ChaveAtivacao);

        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo, corpo.GetType(), options: JsonApi.Opcoes);

        return requisicao;
    }

    private async Task<RespostaApi> EnviarAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        using var resposta = await _http.SendAsync(requisicao, ct);
        var corpo = await resposta.Content.ReadAsStringAsync(ct);

        if (!resposta.IsSuccessStatusCode)
            throw ApiException.Criar(resposta, corpo);

        return new RespostaApi(resposta.StatusCode, corpo, resposta.Headers.Location);
    }

    private static T Desserializar<T>(string json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();

        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        // Algumas APIs embrulham o recurso em { "dados": {...} } ou similar.
        if (raiz.ValueKind == JsonValueKind.Object && raiz.EnumerateObject().Count() == 1
            && raiz.EnumerateObject().First().Value.ValueKind == JsonValueKind.Object)
        {
            raiz = raiz.EnumerateObject().First().Value;
        }

        return raiz.Deserialize<T>(JsonApi.Opcoes) ?? new T();
    }

    private static string? ExtrairIdPartida(RespostaApi resposta)
    {
        if (!string.IsNullOrWhiteSpace(resposta.Corpo))
        {
            using var documento = JsonDocument.Parse(resposta.Corpo);
            var raiz = documento.RootElement;

            if (raiz.ValueKind == JsonValueKind.String) return raiz.GetString();

            if (JsonApi.TentarObter(raiz, "estado", out var estado) && estado.ValueKind == JsonValueKind.Object)
                raiz = estado;

            if (JsonApi.TentarObter(raiz, "partida", out var partida) && partida.ValueKind == JsonValueKind.Object)
                raiz = partida;

            if (JsonApi.LerTexto(raiz, "partidaId", "id") is { Length: > 0 } id) return id;
        }

        // 201 Created costuma trazer /api/partidas/{id} no cabeçalho Location.
        return resposta.Location?.OriginalString.TrimEnd('/').Split('/').LastOrDefault();
    }

    private sealed record RespostaApi(HttpStatusCode Status, string Corpo, Uri? Location);
}
