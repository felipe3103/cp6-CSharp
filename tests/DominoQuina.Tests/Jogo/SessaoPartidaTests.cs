using System.Net;
using System.Text.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Jogo;

public class SessaoPartidaTests
{
    private static readonly Perfil Perfil = new("eu", "Ana", new string('b', 64));
    private readonly HandlerFalso _handler = new();
    private readonly SessaoPartida _sessao;

    public SessaoPartidaTests()
    {
        var api = new DominoApiClient(_handler.CriarCliente()) { Token = "tok" };
        _sessao = new SessaoPartida(api, Perfil, "p1", esperaEntreTentativas: TimeSpan.Zero);
    }

    private static string Estado(long versao, string proximaAcao = "Jogar") => $$"""
        { "partidaId": "p1", "versao": {{versao}}, "proximaAcao": "{{proximaAcao}}",
          "mao": [ { "valorA": 1, "valorB": 2 } ], "mesa": [ { "valorA": 6, "valorB": 1 } ] }
        """;

    [Fact]
    public async Task Aplica_somente_versoes_mais_novas()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5))
                .Responder(HttpStatusCode.OK, Estado(4))   // atrasada
                .Responder(HttpStatusCode.OK, Estado(6));
        var alteracoes = new List<long>();
        _sessao.EstadoAlterado += e => alteracoes.Add(e.Versao);

        await _sessao.CarregarAsync();
        var mudouComVelha = await _sessao.AtualizarAsync();
        var mudouComNova = await _sessao.AtualizarAsync();

        Assert.False(mudouComVelha);
        Assert.True(mudouComNova);
        Assert.Equal([5L, 6L], alteracoes);
    }

    [Fact]
    public async Task Atualizacao_204_mantem_o_estado()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5)).Responder(HttpStatusCode.NoContent);
        await _sessao.CarregarAsync();

        Assert.False(await _sessao.AtualizarAsync());
        Assert.Equal(5, _sessao.Estado!.Versao);
        Assert.Contains("versaoConhecida=5", _handler.Requisicoes[1].Caminho);
    }

    [Fact]
    public async Task Jogada_aceita_aplica_o_estado_da_resposta()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5)).Responder(HttpStatusCode.OK, Estado(6, "Aguardar"));
        await _sessao.CarregarAsync();

        var resultado = await _sessao.JogarAsync(new Pedra(1, 2), Lado.Direita);

        Assert.True(resultado.Aceita);
        Assert.Equal(6, _sessao.Estado!.Versao);
        Assert.Equal(5, JsonDocument.Parse(_handler.Requisicoes[1].Corpo!).RootElement.GetProperty("versaoEsperada").GetInt64());
    }

    [Fact]
    public async Task Rejeicao_422_nao_finge_que_a_jogada_deu_certo()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5))
                .Responder(HttpStatusCode.UnprocessableEntity, """{ "codigo": "ENCAIXE_INVALIDO" }""");
        await _sessao.CarregarAsync();
        var antes = _sessao.Estado;

        var resultado = await _sessao.JogarAsync(new Pedra(1, 2), Lado.Esquerda);

        Assert.False(resultado.Aceita);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resultado.Erro!.StatusCode);
        Assert.Same(antes, _sessao.Estado);
        Assert.Contains("não avançou", resultado.Mensagem);
    }

    [Fact]
    public async Task Conflito_de_versao_recarrega_o_estado_oficial()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5))
                .Responder(HttpStatusCode.Conflict, """{ "codigo": "VERSAO_DESATUALIZADA" }""")
                .Responder(HttpStatusCode.OK, Estado(8, "Aguardar"));
        await _sessao.CarregarAsync();

        var resultado = await _sessao.ComprarAsync();

        Assert.False(resultado.Aceita);
        Assert.Equal(8, _sessao.Estado!.Versao);
        Assert.Equal(HttpMethod.Get, _handler.Requisicoes[2].Metodo);
    }

    [Fact]
    public async Task Falha_de_rede_repete_com_a_mesma_chave_de_idempotencia()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5))
                .FalharRede()
                .Responder(HttpStatusCode.OK, Estado(6, "Aguardar"));
        await _sessao.CarregarAsync();

        var resultado = await _sessao.JogarAsync(new Pedra(1, 2), Lado.Direita);

        Assert.True(resultado.Aceita);
        var chaves = _handler.Requisicoes.Skip(1)
            .Select(r => JsonDocument.Parse(r.Corpo!).RootElement.GetProperty("chaveIdempotencia").GetString())
            .ToList();
        Assert.Equal(2, chaves.Count);
        Assert.Equal(chaves[0], chaves[1]);
    }

    [Fact]
    public async Task Cada_intencao_nova_usa_uma_chave_nova()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5))
                .Responder(HttpStatusCode.UnprocessableEntity, "{}")
                .Responder(HttpStatusCode.UnprocessableEntity, "{}");
        await _sessao.CarregarAsync();

        await _sessao.JogarAsync(new Pedra(1, 2), Lado.Direita);
        await _sessao.JogarAsync(new Pedra(1, 2), Lado.Direita);

        var chaves = _handler.Requisicoes.Skip(1)
            .Select(r => JsonDocument.Parse(r.Corpo!).RootElement.GetProperty("chaveIdempotencia").GetString())
            .ToList();
        Assert.NotEqual(chaves[0], chaves[1]);
    }

    [Fact]
    public async Task Rede_fora_do_ar_propaga_o_erro_sem_alterar_o_estado()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5)).FalharRede().FalharRede().FalharRede();
        await _sessao.CarregarAsync();

        await Assert.ThrowsAsync<HttpRequestException>(() => _sessao.PassarAsync());
        Assert.Equal(5, _sessao.Estado!.Versao);
    }

    [Fact]
    public async Task Resposta_sem_estado_faz_nova_consulta()
    {
        _handler.Responder(HttpStatusCode.OK, Estado(5))
                .Responder(HttpStatusCode.NoContent)
                .Responder(HttpStatusCode.OK, Estado(6, "Aguardar"));
        await _sessao.CarregarAsync();

        var resultado = await _sessao.PassarAsync();

        Assert.True(resultado.Aceita);
        Assert.Equal(6, _sessao.Estado!.Versao);
    }
}
