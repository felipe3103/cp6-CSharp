using DominoQuina.Core.Autonomo;
using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Autonomo;

public class EstrategiaGulosaTests
{
    private readonly EstrategiaGulosa _estrategia = new();

    [Fact]
    public void Prefere_a_jogada_que_pontua()
    {
        // Mesa [6|1]. [1|4] na direita → 6 + 4 = 10 → 2 pontos. [1|2] → 6 + 2 = 8 → 0.
        var estado = Estados.Criar(mao: "12 14", mesa: "61");

        var primeira = _estrategia.Candidatas(estado)[0];

        Assert.Equal(Decisao.Jogar(new Pedra(1, 4), Lado.Direita), primeira);
    }

    [Fact]
    public void Abre_com_a_pedra_obrigatoria()
    {
        var estado = Estados.Criar(mao: "12 55 34", acao: ProximaAcao.JogarAbertura, abertura: new Pedra(5, 5));

        var candidatas = _estrategia.Candidatas(estado);

        Assert.Equal([Decisao.Jogar(new Pedra(5, 5), Lado.Esquerda)], candidatas);
    }

    [Fact]
    public void Com_jogada_valida_nunca_sugere_comprar_ou_passar()
    {
        var candidatas = _estrategia.Candidatas(Estados.Criar(mao: "12 34", mesa: "61"));

        Assert.All(candidatas, d => Assert.Equal(TipoDecisao.Jogar, d.Tipo));
    }

    [Fact]
    public void Sem_encaixe_compra()
    {
        var candidatas = _estrategia.Candidatas(Estados.Criar(mao: "23 44", mesa: "61", cava: 3));

        Assert.Equal([Decisao.Comprar], candidatas);
    }

    [Fact]
    public void Sem_encaixe_e_sem_cava_passa()
    {
        var candidatas = _estrategia.Candidatas(Estados.Criar(mao: "23 44", mesa: "61", cava: 0));

        Assert.Equal([Decisao.Passar], candidatas);
    }

    [Fact]
    public void Fora_da_vez_nao_age() =>
        Assert.Empty(_estrategia.Candidatas(Estados.Criar(mao: "12", mesa: "61", acao: ProximaAcao.Aguardar)));

    [Fact]
    public void Empate_em_pontos_prefere_nao_entregar_pontos_e_depois_a_maior_pedra()
    {
        // Mesa [3|3]: nenhuma jogada pontua agora.
        // [3|4] deixa 4+3; resposta máxima: 3 + 2 = 5 ou 4 + 6 = 10 → 2 pontos para o adversário.
        // [3|0] deixa 0+3; resposta máxima: 0 + 2 = 2, 0 + 5 = 5 → 1 ponto.
        var candidatas = _estrategia.Candidatas(Estados.Criar(mao: "34 30", mesa: "33"));

        Assert.Equal(new Pedra(3, 0), candidatas[0].Pedra);
    }
}
