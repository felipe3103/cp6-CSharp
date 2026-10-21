using DominoQuina.Core.Dominio;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Dominio;

public class RegrasDominoTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    [InlineData(20, 4)]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(12, 0)]
    public void Pontua_soma_dividida_por_5_quando_multipla_de_5(int soma, int pontos) =>
        Assert.Equal(pontos, RegrasDomino.PontosPorSoma(soma));

    [Fact]
    public void Pontos_da_jogada_usam_as_duas_pontas_externas()
    {
        // Mesa [6|1]: pontas 6 e 1. Jogar [1|4] na direita deixa 6 + 4 = 10 → 2 pontos.
        Assert.Equal(2, RegrasDomino.PontosDaJogada(6, 1, new Pedra(1, 4), Lado.Direita));

        // Jogar [6|3] na esquerda deixa 3 + 1 = 4 → 0 pontos.
        Assert.Equal(0, RegrasDomino.PontosDaJogada(6, 1, new Pedra(6, 3), Lado.Esquerda));
    }

    [Fact]
    public void Abertura_pontua_pela_soma_da_pedra() =>
        Assert.Equal(2, RegrasDomino.PontosDaJogada(null, null, new Pedra(5, 5), Lado.Esquerda));

    [Fact]
    public void Jogadas_validas_so_com_pedras_que_encaixam()
    {
        var estado = Estados.Criar(mao: "12 34 66 15", mesa: "61");

        var jogadas = RegrasDomino.JogadasValidas(estado);

        Assert.Contains(jogadas, j => j.Pedra == new Pedra(1, 2) && j.Lado == Lado.Direita);
        Assert.Contains(jogadas, j => j.Pedra == new Pedra(6, 6) && j.Lado == Lado.Esquerda);
        Assert.Contains(jogadas, j => j.Pedra == new Pedra(1, 5) && j.Lado == Lado.Direita);
        Assert.DoesNotContain(jogadas, j => j.Pedra == new Pedra(3, 4));
    }

    [Fact]
    public void Na_abertura_so_a_pedra_obrigatoria_e_permitida()
    {
        var estado = Estados.Criar(mao: "12 66 34", acao: ProximaAcao.JogarAbertura, abertura: new Pedra(6, 6));

        var jogadas = RegrasDomino.JogadasValidas(estado);

        var unica = Assert.Single(jogadas);
        Assert.Equal(new Pedra(6, 6), unica.Pedra);
    }

    [Fact]
    public void Com_encaixe_nao_pode_comprar_nem_passar()
    {
        var permitidas = RegrasDomino.Permitidas(Estados.Criar(mao: "12", mesa: "61"));

        Assert.True(permitidas.PodeJogar);
        Assert.False(permitidas.PodeComprar);
        Assert.False(permitidas.PodePassar);
    }

    [Fact]
    public void Sem_encaixe_e_com_cava_so_pode_comprar()
    {
        var permitidas = RegrasDomino.Permitidas(Estados.Criar(mao: "23 44", mesa: "61", cava: 5));

        Assert.False(permitidas.PodeJogar);
        Assert.True(permitidas.PodeComprar);
        Assert.False(permitidas.PodePassar);
    }

    [Fact]
    public void Passar_so_sem_cava_e_sem_encaixe()
    {
        var permitidas = RegrasDomino.Permitidas(Estados.Criar(mao: "23 44", mesa: "61", cava: 0));

        Assert.False(permitidas.PodeComprar);
        Assert.True(permitidas.PodePassar);
    }

    [Fact]
    public void A_proxima_acao_da_API_prevalece()
    {
        var permitidas = RegrasDomino.Permitidas(Estados.Criar(mao: "23", mesa: "61", acao: ProximaAcao.Passar, cava: null));

        Assert.True(permitidas.PodePassar);
        Assert.False(permitidas.PodeComprar);
    }

    [Fact]
    public void Fora_da_vez_nada_e_permitido()
    {
        var permitidas = RegrasDomino.Permitidas(Estados.Criar(mao: "12", mesa: "61", acao: ProximaAcao.Aguardar));

        Assert.Same(AcoesPermitidas.Nenhuma, permitidas);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(10.5, 1)]
    [InlineData(30, 1)]
    [InlineData(45, 2)]
    [InlineData(59.9, 2)]
    [InlineData(60, 3)]
    [InlineData(120, 3)]
    public void Desconto_por_demora_segue_a_tabela(double segundos, int desconto) =>
        Assert.Equal(desconto, RegrasDomino.DescontoPorDemora(TimeSpan.FromSeconds(segundos)));
}
