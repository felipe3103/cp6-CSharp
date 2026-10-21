using System.Net;
using DominoQuina.Core.Api;
using DominoQuina.Core.Dominio;

namespace DominoQuina.Core.Jogo;

/// <summary>
/// Acompanha uma partida para um perfil: guarda o último estado confirmado e envia comandos.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Só aplica estados com versão mais nova que a atual.</item>
/// <item>Todo comando leva <c>versaoEsperada</c> e um UUID novo de idempotência por intenção.</item>
/// <item>Falhas de rede repetem o mesmo comando com a <b>mesma</b> chave: a API não duplica a jogada.</item>
/// <item>409 de versão recarrega o estado oficial; 422 mantém o estado, porque a partida não avançou.</item>
/// </list>
/// Os acessos à API são serializados, então a tela e o jogador autônomo podem compartilhar a sessão.
/// </remarks>
public sealed class SessaoPartida
{
    private readonly DominoApiClient _api;
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly int _tentativasRede;
    private readonly TimeSpan _esperaEntreTentativas;

    /// <summary>Cria a sessão.</summary>
    /// <param name="api">Cliente da API.</param>
    /// <param name="perfil">Perfil ativo neste cliente.</param>
    /// <param name="partidaId">Partida acompanhada.</param>
    /// <param name="tentativasRede">Total de tentativas de um comando diante de falha de rede.</param>
    /// <param name="esperaEntreTentativas">Espera base entre tentativas (cresce a cada tentativa).</param>
    public SessaoPartida(DominoApiClient api, Perfil perfil, string partidaId, int tentativasRede = 3, TimeSpan? esperaEntreTentativas = null)
    {
        _api = api;
        Perfil = perfil;
        PartidaId = partidaId;
        _tentativasRede = Math.Max(1, tentativasRede);
        _esperaEntreTentativas = esperaEntreTentativas ?? TimeSpan.FromMilliseconds(300);
    }

    /// <summary>Perfil dono da visão.</summary>
    public Perfil Perfil { get; }

    /// <summary>Partida acompanhada.</summary>
    public string PartidaId { get; }

    /// <summary>Último estado confirmado pela API.</summary>
    public EstadoJogo? Estado { get; private set; }

    /// <summary>Disparado sempre que um estado mais novo é aplicado.</summary>
    public event Action<EstadoJogo>? EstadoAlterado;

    /// <summary>Busca a visão completa da partida.</summary>
    public async Task<EstadoJogo> CarregarAsync(CancellationToken ct = default)
    {
        await _trava.WaitAsync(ct);
        try
        {
            await RecarregarAsync(ct);
            return Estado!;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>
    /// Pergunta à API se há estado novo (200) ou não (204).
    /// </summary>
    /// <returns><c>true</c> se um estado mais novo foi aplicado.</returns>
    public async Task<bool> AtualizarAsync(CancellationToken ct = default)
    {
        await _trava.WaitAsync(ct);
        try
        {
            if (Estado is null)
            {
                await RecarregarAsync(ct);
                return true;
            }

            var novo = await _api.ObterAtualizacoesAsync(Perfil, PartidaId, Estado.Versao, ct);
            return novo is not null && Aplicar(novo);
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Inicia a partida (exige duas participações).</summary>
    public Task<ResultadoAcao> IniciarAsync(CancellationToken ct = default) =>
        ExecutarAsync((_, _) => _api.IniciarPartidaAsync(Perfil, PartidaId, ct), ct);

    /// <summary>Joga <paramref name="pedra"/> na ponta <paramref name="lado"/>.</summary>
    public Task<ResultadoAcao> JogarAsync(Pedra pedra, Lado lado, CancellationToken ct = default) =>
        ExecutarAsync((versao, chave) => _api.JogarAsync(Perfil, PartidaId, versao, chave, pedra, lado, ct), ct);

    /// <summary>Compra uma pedra da cava.</summary>
    public Task<ResultadoAcao> ComprarAsync(CancellationToken ct = default) =>
        ExecutarAsync((versao, chave) => _api.ComprarAsync(Perfil, PartidaId, versao, chave, ct), ct);

    /// <summary>Passa a vez.</summary>
    public Task<ResultadoAcao> PassarAsync(CancellationToken ct = default) =>
        ExecutarAsync((versao, chave) => _api.PassarAsync(Perfil, PartidaId, versao, chave, ct), ct);

    /// <summary>Executa uma decisão de turno.</summary>
    public Task<ResultadoAcao> ExecutarAsync(Decisao decisao, CancellationToken ct = default) => decisao.Tipo switch
    {
        TipoDecisao.Jogar => JogarAsync(decisao.Pedra ?? throw new ArgumentException("Jogada sem pedra.", nameof(decisao)), decisao.Lado, ct),
        TipoDecisao.Comprar => ComprarAsync(ct),
        _ => PassarAsync(ct),
    };

    private async Task<ResultadoAcao> ExecutarAsync(Func<long, Guid, Task<EstadoJogo?>> comando, CancellationToken ct)
    {
        await _trava.WaitAsync(ct);
        try
        {
            var versao = Estado?.Versao ?? 0;
            var chaveIdempotencia = Guid.NewGuid(); // uma chave por intenção, reaproveitada nas novas tentativas

            for (var tentativa = 1; ; tentativa++)
            {
                try
                {
                    var confirmado = await comando(versao, chaveIdempotencia);
                    if (confirmado is null)
                        await RecarregarAsync(ct);
                    else
                        Aplicar(confirmado);

                    return ResultadoAcao.Sucesso(Estado!);
                }
                catch (ApiException ex) when (ex.ConflitoDeVersao)
                {
                    await RecarregarAsync(ct);
                    return ResultadoAcao.Falha(ex, Estado);
                }
                catch (ApiException ex) when (!EhFalhaTransitoria(ex, ct) || tentativa >= _tentativasRede)
                {
                    return ResultadoAcao.Falha(ex, Estado);
                }
                catch (Exception ex) when (EhFalhaTransitoria(ex, ct) && tentativa < _tentativasRede)
                {
                    await Task.Delay(_esperaEntreTentativas * tentativa, ct);
                }
            }
        }
        finally
        {
            _trava.Release();
        }
    }

    private async Task RecarregarAsync(CancellationToken ct)
    {
        var estado = await _api.ObterEstadoAsync(Perfil, PartidaId, ct);
        Aplicar(estado);
    }

    /// <summary>Aplica o estado se for mais novo que o atual.</summary>
    private bool Aplicar(EstadoJogo novo)
    {
        if (Estado is not null && novo.Versao <= Estado.Versao) return false;

        Estado = novo;
        EstadoAlterado?.Invoke(novo);
        return true;
    }

    private static bool EhFalhaTransitoria(Exception ex, CancellationToken ct) => ex switch
    {
        HttpRequestException => true,
        TaskCanceledException => !ct.IsCancellationRequested, // tempo limite do HttpClient
        ApiException api => api.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout,
        _ => false,
    };
}
