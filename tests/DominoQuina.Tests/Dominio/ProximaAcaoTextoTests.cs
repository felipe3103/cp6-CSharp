using DominoQuina.Core.Dominio;

namespace DominoQuina.Tests.Dominio;

public class ProximaAcaoTextoTests
{
    [Theory]
    [InlineData("Aguardar", ProximaAcao.Aguardar)]
    [InlineData("AGUARDANDO", ProximaAcao.Aguardar)]
    [InlineData("JogarAbertura", ProximaAcao.JogarAbertura)]
    [InlineData("JOGAR_ABERTURA", ProximaAcao.JogarAbertura)]
    [InlineData("jogar-abertura", ProximaAcao.JogarAbertura)]
    [InlineData("Jogar", ProximaAcao.Jogar)]
    [InlineData("comprar", ProximaAcao.Comprar)]
    [InlineData("Passar", ProximaAcao.Passar)]
    [InlineData("Encerrar", ProximaAcao.Encerrar)]
    [InlineData("Encerrada", ProximaAcao.Encerrar)]
    [InlineData("2", ProximaAcao.Jogar)]
    [InlineData("", ProximaAcao.Desconhecida)]
    [InlineData(null, ProximaAcao.Desconhecida)]
    [InlineData("xyz", ProximaAcao.Desconhecida)]
    public void Interpreta_variacoes_de_texto(string? texto, ProximaAcao esperada) =>
        Assert.Equal(esperada, ProximaAcaoTexto.Interpretar(texto));
}
