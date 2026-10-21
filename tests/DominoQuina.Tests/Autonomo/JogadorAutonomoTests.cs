using System.Net;
using System.Text.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Autonomo;
using DominoQuina.Core.Jogo;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Autonomo;

public class JogadorAutonomoTests
{
    private static readonly Perfil Perfil = new("eu", "Bot", new string('c', 64));
    private readonly HandlerFalso _handler = new();
    private readonly SessaoPartida _sessao;
    private readonly JogadorAutonomo _jogador;

    public JogadorAutonomoTests()
    {
        var api = new DominoApiClient(_handler.CriarCliente()) { Token = "tok" };
        _sessao = new SessaoPartida(api, Perfil, "p1", esperaEntreTentativas: TimeSpan.Zero);
        _jogador = new JogadorAutonomo(_sessao, intervaloConsulta: TimeSpan.Zero);
    }

    private const string MinhaVez = """
        { "versao": 5, "proximaAcao": "Jogar", "pedrasNaCava": 4,
          "mao": [ { "valorA": 1, "valorB": 4 }, { "valorA": 1, "valorB": 2 } ],
          "mesa": [ { "valorA": 6, "valorB": 1 } ] }
        """;

    [Fact]
    public async Task Fora_da_vez_nao_envia_nada()
    {
        _handler.Responder(HttpStatusCode.OK, """{ "versao": 5, "proximaAcao": "Aguardar", "mao": [], "mesa": [] }""");
        await _sessao.CarregarAsync();

        Assert.Null(await _jogador.JogarTurnoAsync());
        Assert.Single(_handler.Requisicoes);
    }

    [Fact]
    public async Task Na_vez_joga_a_melhor_pedra()
    {
        _handler.Responder(HttpStatusCode.OK, MinhaVez)
                .Responder(HttpStatusCode.OK, """{ "versao": 6, "proximaAcao": "Aguardar" }""");
        await _sessao.CarregarAsync();

        var resultado = await _jogador.JogarTurnoAsync();

        Assert.True(resultado!.Aceita);
        var corpo = JsonDocument.Parse(_handler.Requisicoes[1].Corpo!).RootElement;
        Assert.Equal(1, corpo.GetProperty("valorA").GetInt32());
        Assert.Equal(4, corpo.GetProperty("valorB").GetInt32());
        Assert.Equal(1, corpo.GetProperty("lado").GetInt32());
    }

    [Fact]
    public async Task Rejeitada_tenta_a_proxima_candidata_e_nao_repete_o_erro()
    {
        _handler.Responder(HttpStatusCode.OK, MinhaVez)
                .Responder(HttpStatusCode.UnprocessableEntity, "{}")
                .Responder(HttpStatusCode.OK, """{ "versao": 6, "proximaAcao": "Aguardar" }""");
        await _sessao.CarregarAsync();

        await _jogador.JogarTurnoAsync();
        await _jogador.JogarTurnoAsync();

        var primeira = JsonDocument.Parse(_handler.Requisicoes[1].Corpo!).RootElement;
        var segunda = JsonDocument.Parse(_handler.Requisicoes[2].Corpo!).RootElement;
        Assert.NotEqual(primeira.GetProperty("valorB").GetInt32(), segunda.GetProperty("valorB").GetInt32());
    }

    [Fact]
    public async Task Sem_encaixe_compra()
    {
        _handler.Responder(HttpStatusCode.OK, """
                    { "versao": 5, "proximaAcao": "Comprar", "mao": [ { "valorA": 3, "valorB": 3 } ], "mesa": [ { "valorA": 6, "valorB": 1 } ] }
                    """)
                .Responder(HttpStatusCode.OK, """{ "versao": 6, "proximaAcao": "Aguardar" }""");
        await _sessao.CarregarAsync();

        await _jogador.JogarTurnoAsync();

        Assert.Equal("/api/partidas/p1/comprar", _handler.Requisicoes[1].Caminho);
    }
}
