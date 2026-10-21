using DominoQuina.Core.Dominio;
using DominoQuina.Tests.Apoio;

namespace DominoQuina.Tests.Dominio;

public class OrientadorMesaTests
{
    [Fact]
    public void Inverte_pedras_para_as_metades_vizinhas_coincidirem()
    {
        var mesa = Estados.Pedras("16 66 36 53");

        var orientada = OrientadorMesa.Orientar(mesa);

        Assert.Equal(Estados.Pedras("16 66 63 35"), orientada);
    }

    [Fact]
    public void Primeira_pedra_e_virada_para_tocar_a_segunda()
    {
        var orientada = OrientadorMesa.Orientar(Estados.Pedras("16 12"));

        Assert.Equal(Estados.Pedras("61 12"), orientada);
    }

    [Fact]
    public void Pedra_unica_segue_a_ponta_esquerda_informada() =>
        Assert.Equal(Estados.Pedras("52"), OrientadorMesa.Orientar(Estados.Pedras("25"), pontaEsquerda: 5));

    [Fact]
    public void Mesa_vazia() => Assert.Empty(OrientadorMesa.Orientar([]));
}
