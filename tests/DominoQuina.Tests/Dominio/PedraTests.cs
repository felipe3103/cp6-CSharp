using DominoQuina.Core.Dominio;

namespace DominoQuina.Tests.Dominio;

public class PedraTests
{
    [Fact]
    public void Dupla_e_soma()
    {
        Assert.True(new Pedra(6, 6).EhDupla);
        Assert.False(new Pedra(6, 1).EhDupla);
        Assert.Equal(7, new Pedra(6, 1).Soma);
    }

    [Fact]
    public void MesmaPedra_ignora_orientacao()
    {
        Assert.True(new Pedra(2, 5).MesmaPedra(new Pedra(5, 2)));
        Assert.False(new Pedra(2, 5).MesmaPedra(new Pedra(2, 4)));
    }

    [Theory]
    [InlineData(3, 5, 3, 5)]
    [InlineData(3, 5, 5, 3)]
    [InlineData(4, 4, 4, 4)]
    public void ValorExposto_e_a_outra_metade(int a, int b, int ponta, int esperado) =>
        Assert.Equal(esperado, new Pedra(a, b).ValorExpostoAoEncaixar(ponta));

    [Fact]
    public void ValorExposto_falha_se_nao_encaixa() =>
        Assert.Throws<InvalidOperationException>(() => new Pedra(1, 2).ValorExpostoAoEncaixar(6));

    [Fact]
    public void ToString_no_formato_da_mesa() => Assert.Equal("[6|1]", new Pedra(6, 1).ToString());
}
