using System.Net;
using System.Text.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Dominio;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Api;

public class DominoApiClientTests
{
    private const string EstadoJson = """
        { "partidaId": "p1", "versao": 3, "situacao": "EmAndamento", "rodada": 1, "turno": 2,
          "proximaAcao": "Jogar", "mao": [ { "valorA": 1, "valorB": 2 } ],
          "mesa": [ { "valorA": 6, "valorB": 6 } ], "quantidadePedrasAdversario": 6 }
        """;

    private static readonly Perfil Perfil = new("jog-1", "Ana", new string('a', 64));
    private readonly HandlerFalso _handler = new();
    private readonly DominoApiClient _api;

    public DominoApiClientTests()
    {
        _api = new DominoApiClient(_handler.CriarCliente()) { Token = "tok" };
    }

    [Fact]
    public async Task Solicitar_token_envia_email_sem_autorizacao()
    {
        _handler.Responder(HttpStatusCode.Accepted);

        await _api.SolicitarTokenAsync("aluno@fiap.com.br");

        var requisicao = Assert.Single(_handler.Requisicoes);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal("/api/identidade/token", requisicao.Caminho);
        Assert.False(requisicao.Cabecalhos.ContainsKey("Authorization"));
        Assert.Equal("aluno@fiap.com.br", JsonDocument.Parse(requisicao.Corpo!).RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Ativar_envia_bearer_e_chave_no_corpo()
    {
        _handler.Responder(HttpStatusCode.OK);

        await _api.AtivarClienteAsync("jog-1", Perfil.ChaveAtivacao);

        var requisicao = Assert.Single(_handler.Requisicoes);
        Assert.Equal("/api/jogadores/jog-1/ativar-cliente", requisicao.Caminho);
        Assert.Equal("Bearer tok", requisicao.Cabecalhos["Authorization"]);
        Assert.Equal(Perfil.ChaveAtivacao, JsonDocument.Parse(requisicao.Corpo!).RootElement.GetProperty("chaveAtivacao").GetString());
    }

    [Fact]
    public async Task Desativar_envia_a_chave_atual_no_cabecalho()
    {
        _handler.Responder(HttpStatusCode.NoContent);

        await _api.DesativarClienteAsync(Perfil);

        var requisicao = Assert.Single(_handler.Requisicoes);
        Assert.Equal(HttpMethod.Delete, requisicao.Metodo);
        Assert.Equal(Perfil.ChaveAtivacao, requisicao.Cabecalhos["X-Jogador-Ativacao"]);
    }

    [Fact]
    public async Task Jogar_envia_o_comando_no_formato_do_contrato()
    {
        _handler.Responder(HttpStatusCode.OK, EstadoJson);
        var chave = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var estado = await _api.JogarAsync(Perfil, "p1", 2, chave, new Pedra(6, 1), Lado.Esquerda);

        var requisicao = Assert.Single(_handler.Requisicoes);
        Assert.Equal("/api/partidas/p1/jogar", requisicao.Caminho);
        Assert.Equal(Perfil.ChaveAtivacao, requisicao.Cabecalhos["X-Jogador-Ativacao"]);

        var corpo = JsonDocument.Parse(requisicao.Corpo!).RootElement;
        Assert.Equal("jog-1", corpo.GetProperty("jogadorId").GetString());
        Assert.Equal(2, corpo.GetProperty("versaoEsperada").GetInt64());
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", corpo.GetProperty("chaveIdempotencia").GetString());
        Assert.Equal(6, corpo.GetProperty("valorA").GetInt32());
        Assert.Equal(1, corpo.GetProperty("valorB").GetInt32());
        Assert.Equal(0, corpo.GetProperty("lado").GetInt32());

        Assert.NotNull(estado);
        Assert.Equal(3, estado.Versao);
    }

    [Fact]
    public async Task Lado_direito_e_enviado_como_1()
    {
        _handler.Responder(HttpStatusCode.OK, EstadoJson);

        await _api.JogarAsync(Perfil, "p1", 2, Guid.NewGuid(), new Pedra(6, 1), Lado.Direita);

        Assert.Equal(1, JsonDocument.Parse(_handler.Requisicoes[0].Corpo!).RootElement.GetProperty("lado").GetInt32());
    }

    [Fact]
    public async Task Atualizacoes_204_significa_nada_mudou()
    {
        _handler.Responder(HttpStatusCode.NoContent);

        var estado = await _api.ObterAtualizacoesAsync(Perfil, "p1", 7);

        Assert.Null(estado);
        Assert.Equal("/api/partidas/p1/atualizacoes?jogadorId=jog-1&versaoConhecida=7", _handler.Requisicoes[0].Caminho);
    }

    [Fact]
    public async Task Atualizacoes_200_traz_estado_novo()
    {
        _handler.Responder(HttpStatusCode.OK, EstadoJson);

        var estado = await _api.ObterAtualizacoesAsync(Perfil, "p1", 2);

        Assert.NotNull(estado);
        Assert.Equal([new Pedra(1, 2)], estado.Mao);
    }

    [Fact]
    public async Task Estado_embrulhado_em_objeto_estado_e_aceito()
    {
        _handler.Responder(HttpStatusCode.OK, $$"""{ "mensagem": "ok", "estado": {{EstadoJson}} }""");

        var estado = await _api.ObterEstadoAsync(Perfil, "p1");

        Assert.Equal(3, estado.Versao);
    }

    [Fact]
    public async Task Rejeicao_422_vira_ApiException_com_codigo_e_detalhe()
    {
        _handler.Responder(HttpStatusCode.UnprocessableEntity, """{ "codigo": "ENCAIXE_INVALIDO", "mensagem": "A pedra não encaixa." }""");

        var erro = await Assert.ThrowsAsync<ApiException>(() =>
            _api.JogarAsync(Perfil, "p1", 2, Guid.NewGuid(), new Pedra(3, 4), Lado.Esquerda));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, erro.StatusCode);
        Assert.Equal("ENCAIXE_INVALIDO", erro.Codigo);
        Assert.Contains("A partida não avançou", erro.MensagemParaUsuario);
        Assert.Contains("A pedra não encaixa.", erro.MensagemParaUsuario);
    }

    [Fact]
    public async Task Conflito_409_JOGADOR_JA_ATIVO_e_identificado()
    {
        _handler.Responder(HttpStatusCode.Conflict, """{ "title": "Conflito", "codigo": "JOGADOR_JA_ATIVO" }""");

        var erro = await Assert.ThrowsAsync<ApiException>(() => _api.AtivarClienteAsync("jog-1", Perfil.ChaveAtivacao));

        Assert.True(erro.JogadorJaAtivo);
        Assert.False(erro.ConflitoDeVersao);
    }

    [Fact]
    public async Task Listas_aceitam_array_na_raiz_ou_embrulhado()
    {
        _handler.Responder(HttpStatusCode.OK, """[ { "id": 10, "nome": "Ana" } ]""")
                .Responder(HttpStatusCode.OK, """{ "itens": [ { "jogadorId": "x", "nome": "Bia" } ] }""");

        var primeira = await _api.ListarJogadoresAsync();
        var segunda = await _api.ListarJogadoresAsync();

        Assert.Equal("10", primeira[0].Identificador); // id numérico vira texto
        Assert.Equal("x", segunda[0].Identificador);
    }

    [Fact]
    public async Task Criar_partida_le_o_id_do_corpo()
    {
        _handler.Responder(HttpStatusCode.Created, """{ "id": "nova-partida", "pontuacaoAlvo": 50 }""");

        var id = await _api.CriarPartidaAsync(Perfil, 50);

        Assert.Equal("nova-partida", id);
        var corpo = JsonDocument.Parse(_handler.Requisicoes[0].Corpo!).RootElement;
        Assert.Equal(50, corpo.GetProperty("pontuacaoAlvo").GetInt32());
        Assert.Equal("jog-1", corpo.GetProperty("jogadorId").GetString());
    }
}
