using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;

namespace DominoQuina.Core.Autonomo;

/// <summary>
/// Joga sozinho usando uma <see cref="IEstrategia"/> sobre uma <see cref="SessaoPartida"/>.
/// </summary>
/// <remarks>
/// Age assim que recebe a vez, para não sofrer desconto por demora. Se a API rejeitar uma ação,
/// tenta a próxima candidata da mesma versão do estado em vez de repetir o erro.
/// </remarks>
public sealed class JogadorAutonomo
{
    private readonly SessaoPartida _sessao;
    private readonly IEstrategia _estrategia;
    private readonly TimeSpan _intervaloConsulta;
    private readonly HashSet<Decisao> _rejeitadas = [];
    private long _versaoDasRejeicoes = -1;

    /// <summary>Cria o jogador.</summary>
    /// <param name="sessao">Sessão da partida.</param>
    /// <param name="estrategia">Estratégia; o padrão é <see cref="EstrategiaGulosa"/>.</param>
    /// <param name="intervaloConsulta">Espera entre consultas quando nada muda.</param>
    public JogadorAutonomo(SessaoPartida sessao, IEstrategia? estrategia = null, TimeSpan? intervaloConsulta = null)
    {
        _sessao = sessao;
        _estrategia = estrategia ?? new EstrategiaGulosa();
        _intervaloConsulta = intervaloConsulta ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Mensagens do que o jogador está fazendo.</summary>
    public event Action<string>? Registro;

    /// <summary>
    /// Executa no máximo uma ação, se for a vez deste perfil.
    /// </summary>
    /// <returns>O resultado da ação, ou <c>null</c> se não havia o que fazer.</returns>
    public async Task<ResultadoAcao?> JogarTurnoAsync(CancellationToken ct = default)
    {
        if (_sessao.Estado is not { MinhaVez: true } estado) return null;

        if (estado.Versao != _versaoDasRejeicoes)
        {
            _rejeitadas.Clear();
            _versaoDasRejeicoes = estado.Versao;
        }

        var decisao = _estrategia.Candidatas(estado).FirstOrDefault(d => !_rejeitadas.Contains(d));
        if (decisao is null) return null;

        Registro?.Invoke($"Autônomo: {decisao}");
        var resultado = await _sessao.ExecutarAsync(decisao, ct);

        if (resultado.Aceita)
        {
            Registro?.Invoke("Autônomo: ação confirmada.");
        }
        else
        {
            _rejeitadas.Add(decisao);
            Registro?.Invoke($"Autônomo: rejeitada. {resultado.Mensagem}");
        }

        return resultado;
    }

    /// <summary>
    /// Acompanha a partida e joga cada turno até ela terminar.
    /// </summary>
    /// <returns>O estado final.</returns>
    public async Task<EstadoJogo> JogarAteOFimAsync(CancellationToken ct = default)
    {
        if (_sessao.Estado is null) await _sessao.CarregarAsync(ct);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (_sessao.Estado!.Encerrada) return _sessao.Estado;

            if (await JogarTurnoAsync(ct) is { Aceita: true }) continue;

            if (!await _sessao.AtualizarAsync(ct))
                await Task.Delay(_intervaloConsulta, ct);
        }
    }
}
