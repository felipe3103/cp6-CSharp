using DominoQuina.Core.Api;
using DominoQuina.Core.Dominio;

namespace DominoQuina.Tests.Api;

public class MapeadorEstadoTests
{
    [Fact]
    public void Mapeia_estado_completo()
    {
        const string json = """
            { "partidaId": "p1", "versao": 9, "situacao": "EmAndamento", "rodada": 2, "turno": 5, "pontuacaoAlvo": 100,
              "proximaAcao": "Comprar", "jogadorDaVezId": "eu",
              "mao": [ { "valorA": 2, "valorB": 3 } ],
              "mesa": [ { "valorA": 6, "valorB": 6 }, { "valorA": 1, "valorB": 6 } ],
              "quantidadePedrasAdversario": 4, "pedrasNaCava": 2,
              "placar": [ { "jogadorId": "eu", "nome": "Ana", "pontos": 12 }, { "jogadorId": "ele", "nome": "Bia", "pontos": 7 } ] }
            """;

        var estado = MapeadorEstado.TentarLer(json, "eu")!;

        Assert.Equal(9, estado.Versao);
        Assert.Equal(ProximaAcao.Comprar, estado.ProximaAcao);
        Assert.True(estado.MinhaVez);
        Assert.Equal([new Pedra(6, 6), new Pedra(6, 1)], estado.Mesa); // orientada
        Assert.Equal(6, estado.PontaEsquerda);
        Assert.Equal(1, estado.PontaDireita);
        Assert.Equal(12, estado.MeuPlacar!.Pontos);
        Assert.Equal("Bia", estado.PlacarAdversario!.Nome);
        Assert.Equal(4, estado.QuantidadePedrasAdversario);
        Assert.False(estado.Encerrada);
    }

    [Fact]
    public void Corpo_sem_versao_nao_e_estado() =>
        Assert.Null(MapeadorEstado.TentarLer("""{ "id": "p1" }""", "eu"));

    [Fact]
    public void Sem_proxima_acao_deduz_pela_vez()
    {
        const string json = """{ "versao": 1, "jogadorDaVezId": "eu", "mao": [ { "valorA": 1, "valorB": 2 } ], "mesa": [] }""";

        Assert.Equal(ProximaAcao.JogarAbertura, MapeadorEstado.TentarLer(json, "eu")!.ProximaAcao);
        Assert.Equal(ProximaAcao.Aguardar, MapeadorEstado.TentarLer(json, "outro")!.ProximaAcao);
    }

    [Fact]
    public void Situacao_encerrada_marca_fim_da_partida()
    {
        var estado = MapeadorEstado.TentarLer("""{ "versao": 40, "situacao": "Encerrada", "vencedorId": "eu" }""", "eu")!;

        Assert.True(estado.Encerrada);
        Assert.False(estado.MinhaVez);
        Assert.Equal(ProximaAcao.Encerrar, estado.ProximaAcao);
    }

    [Fact]
    public void Vencedor_de_rodada_nao_encerra_se_a_API_pede_jogada()
    {
        var estado = MapeadorEstado.TentarLer("""{ "versao": 12, "proximaAcao": "Jogar", "vencedorId": "ele" }""", "eu")!;

        Assert.False(estado.Encerrada);
    }
}
