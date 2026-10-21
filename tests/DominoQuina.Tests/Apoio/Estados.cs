using DominoQuina.Core.Dominio;

namespace DominoQuina.Tests.Apoio;

/// <summary>Fábrica de estados para os testes.</summary>
public static class Estados
{
    /// <summary>Converte "12 34" em [1|2] [3|4].</summary>
    public static List<Pedra> Pedras(string texto) =>
        texto.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => new Pedra(p[0] - '0', p[1] - '0'))
            .ToList();

    /// <summary>Estado na vez do perfil "eu".</summary>
    public static EstadoJogo Criar(
        string mao,
        string mesa = "",
        ProximaAcao acao = ProximaAcao.Jogar,
        int? cava = 7,
        Pedra? abertura = null,
        long versao = 5)
    {
        var pedrasMesa = Pedras(mesa);
        return new EstadoJogo
        {
            PartidaId = "p1",
            JogadorId = "eu",
            Versao = versao,
            Situacao = "EmAndamento",
            Rodada = 1,
            Turno = 3,
            PontuacaoAlvo = 50,
            ProximaAcao = acao,
            Mao = Pedras(mao),
            Mesa = pedrasMesa,
            PontaEsquerda = pedrasMesa.Count > 0 ? pedrasMesa[0].ValorA : null,
            PontaDireita = pedrasMesa.Count > 0 ? pedrasMesa[^1].ValorB : null,
            PedrasNaCava = cava,
            PedraAbertura = abertura,
        };
    }
}
