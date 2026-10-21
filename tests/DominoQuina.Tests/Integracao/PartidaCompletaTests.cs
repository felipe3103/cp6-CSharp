using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DominoQuina.Core.Api;
using DominoQuina.Core.Autonomo;
using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;
using DominoQuina.Core.Seguranca;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DominoQuina.Tests.Integracao;

/// <summary>
/// Testes de ponta a ponta contra o simulador local da API (em memória).
/// </summary>
public sealed class PartidaCompletaTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<(DominoApiClient Api, Perfil Perfil)> NovoJogadorAsync(string nome)
    {
        var http = fabrica.CreateClient();
        var api = new DominoApiClient(http);
        var email = $"{nome.ToLowerInvariant()}-{Guid.NewGuid():N}@fiap.com.br";

        await api.SolicitarTokenAsync(email);
        var resposta = await http.GetFromJsonAsync<JsonElement>($"/dev/token?email={Uri.EscapeDataString(email)}");
        api.Token = resposta.GetProperty("token").GetString();

        var jogador = await api.CriarJogadorAsync(nome);
        var chave = GeradorChaveAtivacao.Gerar();
        await api.AtivarClienteAsync(jogador.Identificador, chave);
        return (api, new Perfil(jogador.Identificador, nome, chave));
    }

    private sealed record Mesa(SessaoPartida Ana, DominoApiClient ApiAna, SessaoPartida Bia, DominoApiClient ApiBia)
    {
        public bool AnaAbre => Ana.Estado!.MinhaVez;
    }

    private async Task<Mesa> MesaIniciadaAsync(int alvo = 50)
    {
        var (apiAna, ana) = await NovoJogadorAsync("Ana");
        var (apiBia, bia) = await NovoJogadorAsync("Bia");

        var partidaId = await apiAna.CriarPartidaAsync(ana, alvo);
        await apiBia.EntrarPartidaAsync(bia, partidaId);

        var sessaoAna = new SessaoPartida(apiAna, ana, partidaId);
        var sessaoBia = new SessaoPartida(apiBia, bia, partidaId);
        await sessaoAna.CarregarAsync();
        Assert.True((await sessaoAna.IniciarAsync()).Aceita);
        await sessaoBia.CarregarAsync();
        return new Mesa(sessaoAna, apiAna, sessaoBia, apiBia);
    }

    [Fact]
    public async Task Dois_jogadores_autonomos_jogam_ate_a_pontuacao_alvo_sem_movimento_invalido()
    {
        var (ana, apiAna, bia, _) = await MesaIniciadaAsync(alvo: 30);
        var rejeicoes = new List<string>();
        var botAna = new JogadorAutonomo(ana, intervaloConsulta: TimeSpan.FromMilliseconds(5));
        var botBia = new JogadorAutonomo(bia, intervaloConsulta: TimeSpan.FromMilliseconds(5));
        botAna.Registro += m => { if (m.Contains("rejeitada")) lock (rejeicoes) rejeicoes.Add(m); };
        botBia.Registro += m => { if (m.Contains("rejeitada")) lock (rejeicoes) rejeicoes.Add(m); };
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var finais = await Task.WhenAll(botAna.JogarAteOFimAsync(limite.Token), botBia.JogarAteOFimAsync(limite.Token));

        Assert.All(finais, f => Assert.True(f.Encerrada));
        Assert.Equal(finais[0].VencedorId, finais[1].VencedorId);
        var vencedor = finais[0].Placar.Single(p => p.JogadorId == finais[0].VencedorId);
        Assert.True(vencedor.Pontos >= 30);
        Assert.Empty(rejeicoes);

        var resultado = await apiAna.ObterResultadoAsync(ana.Perfil, ana.PartidaId);
        Assert.Equal(finais[0].VencedorId, resultado.VencedorId);
    }

    [Fact]
    public async Task Inicio_distribui_sete_pedras_e_esconde_a_mao_do_adversario()
    {
        var (ana, _, bia, _) = await MesaIniciadaAsync();

        foreach (var estado in new[] { ana.Estado!, bia.Estado! })
        {
            Assert.Equal(7, estado.Mao.Count);
            Assert.Equal(7, estado.QuantidadePedrasAdversario);
            Assert.Empty(estado.Mesa);
        }

        // Só um dos dois abre, e só ele recebe a pedra obrigatória.
        var abre = ana.Estado!.MinhaVez ? ana.Estado : bia.Estado!;
        var espera = ana.Estado!.MinhaVez ? bia.Estado! : ana.Estado;
        Assert.Equal(ProximaAcao.JogarAbertura, abre.ProximaAcao);
        Assert.NotNull(abre.PedraAbertura);
        Assert.Contains(abre.Mao, p => p.MesmaPedra(abre.PedraAbertura!.Value));
        Assert.Equal(ProximaAcao.Aguardar, espera.ProximaAcao);
        Assert.Null(espera.PedraAbertura);
        Assert.Empty(espera.Mao.Intersect(abre.Mao));
    }

    [Fact]
    public async Task Jogada_fora_da_vez_e_rejeitada_e_a_partida_nao_avanca()
    {
        var (ana, _, bia, _) = await MesaIniciadaAsync();
        var foraDaVez = ana.Estado!.MinhaVez ? bia : ana;
        var versao = foraDaVez.Estado!.Versao;

        var resultado = await foraDaVez.JogarAsync(foraDaVez.Estado.Mao[0], Lado.Esquerda);

        Assert.False(resultado.Aceita);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resultado.Erro!.StatusCode);
        Assert.Equal(versao, foraDaVez.Estado.Versao);
    }

    [Fact]
    public async Task Abrir_com_pedra_diferente_da_obrigatoria_e_rejeitado()
    {
        var (ana, _, bia, _) = await MesaIniciadaAsync();
        var abre = ana.Estado!.MinhaVez ? ana : bia;
        var errada = abre.Estado!.Mao.First(p => !p.MesmaPedra(abre.Estado.PedraAbertura!.Value));

        var resultado = await abre.JogarAsync(errada, Lado.Esquerda);

        Assert.False(resultado.Aceita);
        Assert.Equal("ABERTURA_OBRIGATORIA", resultado.Erro!.Codigo);
    }

    [Fact]
    public async Task Repetir_a_mesma_intencao_nao_duplica_a_jogada()
    {
        var mesa = await MesaIniciadaAsync();
        var (abre, api) = mesa.AnaAbre ? (mesa.Ana, mesa.ApiAna) : (mesa.Bia, mesa.ApiBia);
        var versao = abre.Estado!.Versao;
        var pedra = abre.Estado.PedraAbertura!.Value;
        var chave = Guid.NewGuid();

        var primeira = await api.JogarAsync(abre.Perfil, abre.PartidaId, versao, chave, pedra, Lado.Esquerda);
        var repetida = await api.JogarAsync(abre.Perfil, abre.PartidaId, versao, chave, pedra, Lado.Esquerda);

        Assert.Equal(primeira!.Versao, repetida!.Versao);
        Assert.Single(repetida.Mesa);
        Assert.Equal(6, repetida.Mao.Count);
    }

    [Fact]
    public async Task Perfil_ativo_em_outro_cliente_recebe_409_ate_ser_liberado()
    {
        var (api, perfil) = await NovoJogadorAsync("Caio");

        var erro = await Assert.ThrowsAsync<ApiException>(() => api.AtivarClienteAsync(perfil.JogadorId, GeradorChaveAtivacao.Gerar()));
        Assert.True(erro.JogadorJaAtivo);

        await api.AtivarClienteAsync(perfil.JogadorId, perfil.ChaveAtivacao); // mesma chave: retomada
        await api.DesativarClienteAsync(perfil);
        await api.AtivarClienteAsync(perfil.JogadorId, GeradorChaveAtivacao.Gerar()); // outro cliente, depois de liberar
    }

    [Fact]
    public async Task Atualizacoes_respondem_204_quando_nada_mudou()
    {
        var mesa = await MesaIniciadaAsync();

        Assert.False(await mesa.Ana.AtualizarAsync());
    }

}
