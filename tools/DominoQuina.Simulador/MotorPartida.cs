namespace DominoQuina.Simulador;

/// <summary>Rejeição de regra ou de acesso, convertida em resposta HTTP pelo simulador.</summary>
public sealed class ErroApi(int status, string codigo, string mensagem) : Exception(mensagem)
{
    /// <summary>Status HTTP.</summary>
    public int Status { get; } = status;

    /// <summary>Código de erro (ex.: <c>JOGADOR_JA_ATIVO</c>).</summary>
    public string Codigo { get; } = codigo;
}

/// <summary>Pedra do simulador. Implementação própria, independente do cliente.</summary>
public readonly record struct Peca(int A, int B)
{
    /// <summary>Soma das metades.</summary>
    public int Soma => A + B;

    /// <summary>Dupla.</summary>
    public bool EhDupla => A == B;

    /// <summary>Alguma metade vale <paramref name="valor"/>.</summary>
    public bool Contem(int valor) => A == valor || B == valor;

    /// <summary>Metades trocadas.</summary>
    public Peca Invertida() => new(B, A);

    /// <summary>Mesma pedra em qualquer orientação.</summary>
    public bool Mesma(Peca outra) => (A == outra.A && B == outra.B) || (A == outra.B && B == outra.A);
}

/// <summary>Participante de uma partida simulada.</summary>
public sealed class Participante(string jogadorId, string nome)
{
    /// <summary>Perfil.</summary>
    public string JogadorId { get; } = jogadorId;

    /// <summary>Nome do perfil.</summary>
    public string Nome { get; } = nome;

    /// <summary>Mão (privada).</summary>
    public List<Peca> Mao { get; } = [];

    /// <summary>Pontos na partida.</summary>
    public int Pontos { get; set; }
}

/// <summary>
/// Motor de uma partida de dominó ponta de quina para dois jogadores, seguindo as regras do checkpoint.
/// </summary>
/// <remarks>Não é thread-safe: o chamador deve usar <see cref="Trava"/>.</remarks>
public sealed class MotorPartida
{
    private readonly Random _aleatorio;
    private readonly HashSet<Guid> _chavesUsadas = [];
    private int _vez;
    private Peca? _pedraAbertura;

    /// <summary>Cria a mesa.</summary>
    public MotorPartida(int pontuacaoAlvo, Random? aleatorio = null)
    {
        if (pontuacaoAlvo <= 0) throw new ErroApi(400, "PONTUACAO_ALVO_INVALIDA", "A pontuação alvo deve ser positiva.");
        PontuacaoAlvo = pontuacaoAlvo;
        _aleatorio = aleatorio ?? Random.Shared;
    }

    /// <summary>Trava da partida.</summary>
    public object Trava { get; } = new();

    /// <summary>Identificador.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>Pontuação alvo.</summary>
    public int PontuacaoAlvo { get; }

    /// <summary>AguardandoJogadores, EmAndamento ou Encerrada.</summary>
    public string Situacao { get; private set; } = "AguardandoJogadores";

    /// <summary>Versão do estado, incrementada a cada mudança.</summary>
    public long Versao { get; private set; } = 1;

    /// <summary>Rodada atual.</summary>
    public int Rodada { get; private set; }

    /// <summary>Turno atual.</summary>
    public int Turno { get; private set; }

    /// <summary>Participantes, na ordem de ingresso.</summary>
    public List<Participante> Participantes { get; } = [];

    /// <summary>Mesa orientada da esquerda para a direita.</summary>
    public List<Peca> Mesa { get; } = [];

    /// <summary>Cava (pedras para compra).</summary>
    public List<Peca> Cava { get; } = [];

    /// <summary>Vencedor da partida.</summary>
    public string? VencedorId { get; private set; }

    /// <summary>Participante com a vez.</summary>
    public Participante? DaVez => Situacao == "EmAndamento" ? Participantes[_vez] : null;

    /// <summary>Ingressa um perfil. Repetir o ingresso do mesmo perfil não tem efeito.</summary>
    public void Entrar(string jogadorId, string nome)
    {
        if (Participantes.Any(p => p.JogadorId == jogadorId)) return;
        if (Situacao != "AguardandoJogadores") throw new ErroApi(409, "PARTIDA_JA_INICIADA", "A partida já começou.");
        if (Participantes.Count >= 2) throw new ErroApi(409, "MESA_CHEIA", "A mesa não tem vaga.");

        Participantes.Add(new Participante(jogadorId, nome));
        Versao++;
    }

    /// <summary>Inicia a partida com duas participações.</summary>
    public void Iniciar(string jogadorId)
    {
        ExigirParticipante(jogadorId);
        if (Situacao != "AguardandoJogadores") throw new ErroApi(409, "PARTIDA_JA_INICIADA", "A partida já começou.");
        if (Participantes.Count < 2) throw new ErroApi(422, "JOGADORES_INSUFICIENTES", "São necessários dois jogadores.");

        Situacao = "EmAndamento";
        NovaRodada();
        Versao++;
    }

    /// <summary>Joga uma pedra na ponta indicada (0 = esquerda, 1 = direita).</summary>
    public void Jogar(string jogadorId, long versaoEsperada, Guid chave, Peca peca, int lado)
    {
        if (!_chavesUsadas.Add(chave)) return; // mesma intenção: não duplica
        try
        {
            var jogador = ValidarVez(jogadorId, versaoEsperada);
            var indice = jogador.Mao.FindIndex(p => p.Mesma(peca));
            if (indice < 0) throw new ErroApi(422, "PEDRA_NAO_ESTA_NA_MAO", $"A pedra [{peca.A}|{peca.B}] não está na sua mão.");
            var pedra = jogador.Mao[indice];

            if (Mesa.Count == 0)
            {
                if (_pedraAbertura is { } obrigatoria && !obrigatoria.Mesma(pedra))
                    throw new ErroApi(422, "ABERTURA_OBRIGATORIA", $"A abertura deve ser com [{obrigatoria.A}|{obrigatoria.B}].");
                Mesa.Add(pedra);
            }
            else if (lado == 0)
            {
                var ponta = Mesa[0].A;
                if (!pedra.Contem(ponta)) throw new ErroApi(422, "ENCAIXE_INVALIDO", $"A pedra não encaixa na ponta esquerda ({ponta}).");
                Mesa.Insert(0, pedra.B == ponta ? pedra : pedra.Invertida());
            }
            else if (lado == 1)
            {
                var ponta = Mesa[^1].B;
                if (!pedra.Contem(ponta)) throw new ErroApi(422, "ENCAIXE_INVALIDO", $"A pedra não encaixa na ponta direita ({ponta}).");
                Mesa.Add(pedra.A == ponta ? pedra : pedra.Invertida());
            }
            else
            {
                throw new ErroApi(422, "LADO_INVALIDO", "Lado deve ser 0 (esquerda) ou 1 (direita).");
            }

            jogador.Mao.RemoveAt(indice);
            _pedraAbertura = null;

            var soma = Mesa.Count == 1 ? Mesa[0].Soma : Mesa[0].A + Mesa[^1].B;
            if (soma > 0 && soma % 5 == 0) jogador.Pontos += soma / 5;

            if (!VerificarFimDaPartida())
            {
                if (jogador.Mao.Count == 0) FimDaRodada(jogador);
                else PassarVez();
            }

            Versao++;
        }
        catch
        {
            _chavesUsadas.Remove(chave);
            throw;
        }
    }

    /// <summary>Compra uma pedra. Se ela não encaixar, a vez passa.</summary>
    public void Comprar(string jogadorId, long versaoEsperada, Guid chave)
    {
        if (!_chavesUsadas.Add(chave)) return;
        try
        {
            var jogador = ValidarVez(jogadorId, versaoEsperada);
            if (Mesa.Count == 0) throw new ErroApi(422, "COMPRA_INVALIDA", "Abra a rodada com a pedra obrigatória.");
            if (PodeJogar(jogador)) throw new ErroApi(422, "COMPRA_INVALIDA", "Há pedra na mão que encaixa.");
            if (Cava.Count == 0) throw new ErroApi(422, "CAVA_VAZIA", "A cava está vazia: passe a vez.");

            var comprada = Cava[^1];
            Cava.RemoveAt(Cava.Count - 1);
            jogador.Mao.Add(comprada);

            if (!Encaixa(comprada)) PassarVez();
            Versao++;
        }
        catch
        {
            _chavesUsadas.Remove(chave);
            throw;
        }
    }

    /// <summary>Passa a vez (só sem cava e sem pedra compatível).</summary>
    public void Passar(string jogadorId, long versaoEsperada, Guid chave)
    {
        if (!_chavesUsadas.Add(chave)) return;
        try
        {
            var jogador = ValidarVez(jogadorId, versaoEsperada);
            if (Mesa.Count == 0 || PodeJogar(jogador)) throw new ErroApi(422, "PASSAGEM_INVALIDA", "Há pedra na mão que encaixa.");
            if (Cava.Count > 0) throw new ErroApi(422, "PASSAGEM_INVALIDA", "A cava ainda tem pedras: compre.");

            PassarVez();
            Versao++;
        }
        catch
        {
            _chavesUsadas.Remove(chave);
            throw;
        }
    }

    /// <summary>Próxima ação do perfil.</summary>
    public string ProximaAcao(string jogadorId)
    {
        if (Situacao == "Encerrada") return "Encerrar";
        if (DaVez is not { } daVez || daVez.JogadorId != jogadorId) return "Aguardar";
        if (Mesa.Count == 0) return "JogarAbertura";
        if (PodeJogar(daVez)) return "Jogar";
        return Cava.Count > 0 ? "Comprar" : "Passar";
    }

    /// <summary>Visão do perfil: a própria mão e só a quantidade de pedras do adversário.</summary>
    public object Visao(string jogadorId)
    {
        var eu = ExigirParticipante(jogadorId);
        var adversario = Participantes.FirstOrDefault(p => p.JogadorId != jogadorId);
        var abro = DaVez?.JogadorId == jogadorId && Mesa.Count == 0;

        return new
        {
            partidaId = Id,
            versao = Versao,
            situacao = Situacao,
            rodada = Rodada,
            turno = Turno,
            pontuacaoAlvo = PontuacaoAlvo,
            proximaAcao = ProximaAcao(jogadorId),
            jogadorDaVezId = DaVez?.JogadorId,
            pedraAbertura = abro && _pedraAbertura is { } ab ? new { valorA = ab.A, valorB = ab.B } : null,
            mao = eu.Mao.Select(p => new { valorA = p.A, valorB = p.B }),
            mesa = Mesa.Select(p => new { valorA = p.A, valorB = p.B }),
            pontaEsquerda = Mesa.Count > 0 ? Mesa[0].A : (int?)null,
            pontaDireita = Mesa.Count > 0 ? Mesa[^1].B : (int?)null,
            quantidadePedrasAdversario = adversario?.Mao.Count ?? 0,
            pedrasNaCava = Cava.Count,
            placar = Participantes.Select(p => new { jogadorId = p.JogadorId, nome = p.Nome, pontos = p.Pontos }),
            vencedorId = VencedorId,
        };
    }

    private Participante ExigirParticipante(string jogadorId) =>
        Participantes.FirstOrDefault(p => p.JogadorId == jogadorId)
        ?? throw new ErroApi(403, "NAO_PARTICIPANTE", "O perfil não participa desta partida.");

    private Participante ValidarVez(string jogadorId, long versaoEsperada)
    {
        var jogador = ExigirParticipante(jogadorId);
        if (Situacao != "EmAndamento") throw new ErroApi(422, "PARTIDA_FORA_DE_ANDAMENTO", "A partida não está em andamento.");
        if (versaoEsperada != Versao) throw new ErroApi(409, "VERSAO_DESATUALIZADA", $"Versão esperada {versaoEsperada}, atual {Versao}.");
        if (DaVez!.JogadorId != jogadorId) throw new ErroApi(422, "TURNO_INVALIDO", "Não é a sua vez.");
        return jogador;
    }

    private bool Encaixa(Peca peca) => Mesa.Count == 0 || peca.Contem(Mesa[0].A) || peca.Contem(Mesa[^1].B);

    private bool PodeJogar(Participante participante) => participante.Mao.Any(Encaixa);

    private void PassarVez()
    {
        _vez = 1 - _vez;
        Turno++;

        // Travamento: sem cava e ninguém encaixa.
        if (Cava.Count == 0 && Participantes.All(p => !PodeJogar(p)))
            FimDaRodada(batedor: null);
    }

    /// <summary>
    /// Fecha a rodada. Quem bate (ou, no travamento, quem tem menos pontos na mão) ganha os pontos
    /// das pedras do adversário arredondados para o múltiplo de 5 mais próximo, divididos por 5.
    /// </summary>
    private void FimDaRodada(Participante? batedor)
    {
        var vencedor = batedor;
        if (vencedor is null)
        {
            var ordenados = Participantes.OrderBy(p => p.Mao.Sum(x => x.Soma)).ToList();
            if (ordenados[0].Mao.Sum(x => x.Soma) < ordenados[1].Mao.Sum(x => x.Soma)) vencedor = ordenados[0];
        }

        if (vencedor is not null)
        {
            var restantes = Participantes.Where(p => p != vencedor).Sum(p => p.Mao.Sum(x => x.Soma));
            vencedor.Pontos += (int)Math.Round(restantes / 5.0, MidpointRounding.AwayFromZero);
        }

        if (!VerificarFimDaPartida()) NovaRodada();
    }

    private bool VerificarFimDaPartida()
    {
        var lider = Participantes.OrderByDescending(p => p.Pontos).First();
        if (lider.Pontos < PontuacaoAlvo) return false;

        Situacao = "Encerrada";
        VencedorId = lider.JogadorId;
        return true;
    }

    private void NovaRodada()
    {
        Rodada++;
        Turno = 1;
        Mesa.Clear();
        Cava.Clear();

        var pedras = new List<Peca>();
        for (var a = 0; a <= 6; a++)
            for (var b = a; b <= 6; b++)
                pedras.Add(new Peca(a, b));
        var embaralhadas = pedras.OrderBy(_ => _aleatorio.Next()).ToList();

        for (var i = 0; i < Participantes.Count; i++)
        {
            Participantes[i].Mao.Clear();
            Participantes[i].Mao.AddRange(embaralhadas.Skip(i * 7).Take(7));
        }
        Cava.AddRange(embaralhadas.Skip(Participantes.Count * 7));

        // Abre quem tem a maior dupla; sem dupla, a maior pedra.
        var (dono, pedra) = Participantes
            .SelectMany((p, indice) => p.Mao.Select(x => (indice, x)))
            .OrderByDescending(t => t.x.EhDupla)
            .ThenByDescending(t => t.x.Soma)
            .ThenByDescending(t => Math.Max(t.x.A, t.x.B))
            .First();
        _vez = dono;
        _pedraAbertura = pedra;
    }
}
