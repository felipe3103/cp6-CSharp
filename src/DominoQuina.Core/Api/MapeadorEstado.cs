using System.Text.Json;
using DominoQuina.Core.Dominio;

namespace DominoQuina.Core.Api;

/// <summary>
/// Converte o JSON de estado recebido da API em <see cref="EstadoJogo"/>.
/// </summary>
public static class MapeadorEstado
{
    /// <summary>
    /// Tenta ler um estado de partida em <paramref name="json"/>.
    /// Aceita o estado na raiz ou embrulhado em <c>{ "estado": { ... } }</c>.
    /// </summary>
    /// <returns>O estado, ou <c>null</c> se o corpo estiver vazio ou não for um estado.</returns>
    public static EstadoJogo? TentarLer(string json, string jogadorId, string? partidaIdPadrao = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        if (JsonApi.TentarObter(raiz, "estado", out var embrulhado) && embrulhado.ValueKind == JsonValueKind.Object)
            raiz = embrulhado;

        if (!JsonApi.TentarObter(raiz, "versao", out _)) return null;

        var dto = raiz.Deserialize<EstadoPartidaDto>(JsonApi.Opcoes)
                  ?? throw new JsonException("Estado da partida vazio.");
        return ParaDominio(dto, jogadorId, partidaIdPadrao, raiz.GetRawText());
    }

    /// <summary>Converte o DTO para o modelo de domínio, completando o que puder ser deduzido.</summary>
    public static EstadoJogo ParaDominio(EstadoPartidaDto dto, string jogadorId, string? partidaIdPadrao = null, string? jsonBruto = null)
    {
        var mao = dto.Mao?.Select(ParaPedra).ToList() ?? [];
        var mesa = OrientadorMesa.Orientar(dto.Mesa?.Select(ParaPedra).ToList() ?? [], dto.PontaEsquerda);

        var pontaEsquerda = dto.PontaEsquerda ?? (mesa.Count > 0 ? mesa[0].ValorA : null);
        var pontaDireita = dto.PontaDireita ?? (mesa.Count > 0 ? mesa[^1].ValorB : null);

        var placar = (dto.Placar ?? dto.Participantes ?? [])
            .Where(p => p.JogadorId is not null)
            .Select(p => new PlacarJogador(
                p.JogadorId!,
                string.IsNullOrWhiteSpace(p.Nome) ? p.JogadorId! : p.Nome!,
                p.Pontuacao ?? p.Pontos,
                string.Equals(p.JogadorId, jogadorId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var situacao = dto.Situacao ?? "";
        var acao = ProximaAcaoTexto.Interpretar(dto.ProximaAcao);
        // O vencedor sozinho só encerra quando a API não disse a próxima ação,
        // para não confundir o vencedor de uma rodada com o fim da partida.
        var encerrada = acao == ProximaAcao.Encerrar
                        || ProximaAcaoTexto.Interpretar(situacao) == ProximaAcao.Encerrar
                        || (acao == ProximaAcao.Desconhecida && !string.IsNullOrEmpty(dto.VencedorId));

        if (encerrada)
        {
            acao = ProximaAcao.Encerrar;
        }
        else if (acao == ProximaAcao.Desconhecida)
        {
            acao = InferirAcao(dto, jogadorId, mao, pontaEsquerda, pontaDireita, mesa.Count == 0);
        }

        return new EstadoJogo
        {
            PartidaId = dto.PartidaId ?? dto.Id ?? partidaIdPadrao ?? "",
            JogadorId = jogadorId,
            Versao = dto.Versao,
            Situacao = situacao,
            Rodada = dto.Rodada,
            Turno = dto.Turno,
            PontuacaoAlvo = dto.PontuacaoAlvo,
            ProximaAcao = acao,
            JogadorDaVezId = dto.JogadorDaVezId,
            PedraAbertura = dto.PedraAbertura is null ? null : ParaPedra(dto.PedraAbertura),
            Mao = mao,
            Mesa = mesa,
            PontaEsquerda = pontaEsquerda,
            PontaDireita = pontaDireita,
            QuantidadePedrasAdversario = dto.QuantidadePedrasAdversario,
            PedrasNaCava = dto.PedrasNaCava,
            Placar = placar,
            VencedorId = dto.VencedorId,
            Encerrada = encerrada,
            JsonBruto = jsonBruto,
        };
    }

    /// <summary>
    /// Usada só quando a API não informa a próxima ação: deduz pela vez e pelas pedras na mão.
    /// </summary>
    private static ProximaAcao InferirAcao(
        EstadoPartidaDto dto, string jogadorId, IReadOnlyList<Pedra> mao, int? esquerda, int? direita, bool mesaVazia)
    {
        if (!string.Equals(dto.JogadorDaVezId, jogadorId, StringComparison.OrdinalIgnoreCase))
            return ProximaAcao.Aguardar;

        if (mesaVazia)
            return ProximaAcao.JogarAbertura;

        var pedraAbertura = dto.PedraAbertura is null ? (Pedra?)null : ParaPedra(dto.PedraAbertura);
        if (RegrasDomino.JogadasValidas(mao, esquerda, direita, mesaVazia, pedraAbertura).Count > 0)
            return ProximaAcao.Jogar;

        return dto.PedrasNaCava is 0 ? ProximaAcao.Passar : ProximaAcao.Comprar;
    }

    private static Pedra ParaPedra(PedraDto dto) => new(dto.ValorA, dto.ValorB);
}
