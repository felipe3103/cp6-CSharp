using System.Net;
using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Conta;
using DominoQuina.Core.Seguranca;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Conta;

public sealed class ContaEArmazenamentoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "domino-testes-" + Guid.NewGuid().ToString("N"));
    private readonly HandlerFalso _handler = new();

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private ArmazenamentoCredenciais Armazenamento(string nome = "teste") => new(nome, _pasta);

    [Fact]
    public void Chave_de_ativacao_tem_64_hexadecimais_e_e_aleatoria()
    {
        var primeira = GeradorChaveAtivacao.Gerar();
        var segunda = GeradorChaveAtivacao.Gerar();

        Assert.True(GeradorChaveAtivacao.EhValida(primeira));
        Assert.Equal(64, primeira.Length);
        Assert.NotEqual(primeira, segunda);
        Assert.False(GeradorChaveAtivacao.EhValida("xyz"));
    }

    [Fact]
    public void Chave_e_reutilizada_para_o_mesmo_perfil()
    {
        var armazenamento = Armazenamento();

        var primeira = armazenamento.ObterOuCriarChave("jog-1", GeradorChaveAtivacao.Gerar);
        var segunda = Armazenamento().ObterOuCriarChave("jog-1", () => throw new InvalidOperationException("não deveria gerar"));

        Assert.Equal(primeira, segunda);
    }

    [Fact]
    public void Clientes_diferentes_tem_chaves_diferentes()
    {
        var web = Armazenamento("web").ObterOuCriarChave("jog-1", GeradorChaveAtivacao.Gerar);
        var bot = Armazenamento("bot").ObterOuCriarChave("jog-1", GeradorChaveAtivacao.Gerar);

        Assert.NotEqual(web, bot);
    }

    [Fact]
    public void Arquivo_de_credenciais_so_pode_ser_lido_pelo_dono()
    {
        if (OperatingSystem.IsWindows()) return;

        var armazenamento = Armazenamento();
        armazenamento.Atualizar(d => d.Token = "segredo");

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(armazenamento.Caminho));
    }

    [Fact]
    public async Task Ativacao_grava_a_chave_antes_de_chamar_a_API_e_reaproveita_depois_de_falha()
    {
        _handler.Responder(HttpStatusCode.ServiceUnavailable).Responder(HttpStatusCode.OK);
        var api = new DominoApiClient(_handler.CriarCliente()) { Token = "tok" };
        var conta = new ServicoConta(api, Armazenamento());

        await Assert.ThrowsAsync<ApiException>(() => conta.AtivarAsync("jog-1", "Ana"));
        var perfil = await conta.AtivarAsync("jog-1", "Ana");

        Assert.Equal(_handler.Requisicoes[0].Corpo, _handler.Requisicoes[1].Corpo);
        Assert.Equal("jog-1", perfil.JogadorId);
    }

    [Fact]
    public async Task Perfil_ativo_e_restaurado_ao_reabrir()
    {
        _handler.Responder(HttpStatusCode.OK);
        var api = new DominoApiClient(_handler.CriarCliente()) { Token = "tok" };
        var perfil = await new ServicoConta(api, Armazenamento()).AtivarAsync("jog-1", "Ana");

        var reaberto = new ServicoConta(new DominoApiClient(_handler.CriarCliente()), Armazenamento());

        Assert.Equal(perfil, reaberto.PerfilAtivo);
    }

    [Fact]
    public async Task Desativar_descarta_a_chave()
    {
        _handler.Responder(HttpStatusCode.OK).Responder(HttpStatusCode.NoContent);
        var api = new DominoApiClient(_handler.CriarCliente()) { Token = "tok" };
        var conta = new ServicoConta(api, Armazenamento());
        var antiga = (await conta.AtivarAsync("jog-1", "Ana")).ChaveAtivacao;

        await conta.DesativarAsync();

        Assert.Null(conta.PerfilAtivo);
        Assert.Equal(antiga, _handler.Requisicoes[1].Cabecalhos["X-Jogador-Ativacao"]);
        Assert.DoesNotContain("jog-1", Armazenamento().Ler().ChavesAtivacao.Keys);
    }

    [Fact]
    public async Task Token_invalido_nao_e_salvo()
    {
        _handler.Responder(HttpStatusCode.Unauthorized);
        var conta = new ServicoConta(new DominoApiClient(_handler.CriarCliente()), Armazenamento());

        await Assert.ThrowsAsync<ApiException>(() => conta.DefinirTokenAsync("ruim"));

        Assert.False(conta.Autenticado);
        Assert.Null(Armazenamento().Ler().Token);
    }

    [Fact]
    public async Task Token_aceita_prefixo_Bearer_colado_do_email()
    {
        _handler.Responder(HttpStatusCode.OK, "[]");
        var conta = new ServicoConta(new DominoApiClient(_handler.CriarCliente()), Armazenamento());

        await conta.DefinirTokenAsync("  Bearer abc123  ");

        Assert.Equal("abc123", Armazenamento().Ler().Token);
        Assert.Equal("Bearer abc123", _handler.Requisicoes[0].Cabecalhos["Authorization"]);
    }
}
