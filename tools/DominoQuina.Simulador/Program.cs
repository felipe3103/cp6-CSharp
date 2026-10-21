using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DominoQuina.Simulador;

// Simulador local da API do Dominó Ponta de Quina, em memória, para desenvolvimento e testes.
// Segue o contrato descrito nos slides do checkpoint. Não substitui a API oficial.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Armazem>();
var app = builder.Build();

app.Use(async (contexto, proximo) =>
{
    try
    {
        await proximo(contexto);
    }
    catch (ErroApi erro)
    {
        contexto.Response.StatusCode = erro.Status;
        if (erro.Status == 429) contexto.Response.Headers.RetryAfter = "60";
        await contexto.Response.WriteAsJsonAsync(new { codigo = erro.Codigo, mensagem = erro.Message, status = erro.Status });
    }
});

app.MapGet("/", () => Results.Text("Simulador da API Dominó Ponta de Quina. Token de desenvolvimento: GET /dev/token?email=..."));

// ----- Identidade -----

app.MapPost("/api/identidade/token", (PedidoToken pedido, Armazem armazem, ILogger<Program> log) =>
{
    var token = armazem.SolicitarToken(pedido.Email);
    log.LogWarning("[simulador] Token de {Email}: {Token}", pedido.Email, token); // no lugar do e-mail
    return Results.Accepted();
});

app.MapGet("/dev/token", (string email, Armazem armazem) => Results.Ok(new { token = armazem.TokenDe(email) }));

// ----- Jogadores -----

app.MapGet("/api/jogadores", (HttpContext http, Armazem armazem) =>
{
    var email = armazem.Autenticar(http);
    return Results.Ok(armazem.Jogadores.Values.Where(j => j.Email == email).Select(j => new { id = j.Id, nome = j.Nome }));
});

app.MapPost("/api/jogadores", (PedidoJogador pedido, HttpContext http, Armazem armazem) =>
{
    var email = armazem.Autenticar(http);
    if (string.IsNullOrWhiteSpace(pedido.Nome)) throw new ErroApi(400, "NOME_OBRIGATORIO", "Informe o nome.");
    var jogador = new Jogador(Guid.NewGuid().ToString(), email, pedido.Nome.Trim());
    armazem.Jogadores[jogador.Id] = jogador;
    return Results.Created($"/api/jogadores/{jogador.Id}", new { id = jogador.Id, nome = jogador.Nome });
});

app.MapPost("/api/jogadores/{id}/ativar-cliente", (string id, PedidoAtivacao pedido, HttpContext http, Armazem armazem) =>
{
    var email = armazem.Autenticar(http);
    var jogador = armazem.JogadorDaConta(id, email);
    if (pedido.ChaveAtivacao is not { Length: 64 } chave || !chave.All(Uri.IsHexDigit))
        throw new ErroApi(400, "CHAVE_INVALIDA", "A chave deve ter 64 caracteres hexadecimais.");

    lock (jogador)
    {
        var hash = Armazem.Hash(chave);
        if (jogador.HashChave is not null && jogador.HashChave != hash)
            throw new ErroApi(409, "JOGADOR_JA_ATIVO", "O perfil está ativo em outro cliente.");
        jogador.HashChave = hash;
    }

    return Results.Ok(new { id = jogador.Id, nome = jogador.Nome, ativo = true });
});

app.MapDelete("/api/jogadores/{id}/desativar-cliente", (string id, HttpContext http, Armazem armazem) =>
{
    var jogador = armazem.ExigirPerfil(http, id);
    lock (jogador) jogador.HashChave = null;
    return Results.NoContent();
});

// ----- Partidas -----

app.MapGet("/api/partidas", (HttpContext http, Armazem armazem) =>
{
    armazem.Autenticar(http);
    return Results.Ok(armazem.Partidas.Values
        .Where(p => p.Situacao == "AguardandoJogadores")
        .Select(p => new { id = p.Id, pontuacaoAlvo = p.PontuacaoAlvo, situacao = p.Situacao, quantidadeJogadores = p.Participantes.Count }));
});

app.MapPost("/api/partidas", (PedidoPartida pedido, HttpContext http, Armazem armazem) =>
{
    var jogador = armazem.ExigirPerfil(http, pedido.JogadorId);
    var partida = new MotorPartida(pedido.PontuacaoAlvo);
    lock (partida.Trava) partida.Entrar(jogador.Id, jogador.Nome);
    armazem.Partidas[partida.Id] = partida;
    lock (partida.Trava) return Results.Created($"/api/partidas/{partida.Id}", partida.Visao(jogador.Id));
});

app.MapGet("/api/partidas/{id}", (string id, string jogadorId, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, jogadorId, (partida, jogador) => partida.Visao(jogador.Id)));

app.MapGet("/api/partidas/{id}/atualizacoes", (string id, string jogadorId, long versaoConhecida, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, jogadorId, (partida, jogador) => partida.Versao > versaoConhecida ? partida.Visao(jogador.Id) : null));

app.MapGet("/api/partidas/{id}/resultado", (string id, string jogadorId, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, jogadorId, (partida, _) =>
    {
        if (partida.Situacao != "Encerrada") throw new ErroApi(409, "PARTIDA_EM_ANDAMENTO", "A partida ainda não terminou.");
        return new
        {
            situacao = partida.Situacao,
            vencedorId = partida.VencedorId,
            vencedorNome = partida.Participantes.First(p => p.JogadorId == partida.VencedorId).Nome,
            placar = partida.Participantes.Select(p => new { jogadorId = p.JogadorId, nome = p.Nome, pontos = p.Pontos }),
        };
    }));

app.MapPost("/api/partidas/{id}/entrar", (string id, PedidoParticipacao pedido, HttpContext http, Armazem armazem) =>
{
    var jogador = armazem.ExigirPerfil(http, pedido.JogadorId);
    var partida = armazem.Partida(id);
    lock (partida.Trava)
    {
        partida.Entrar(jogador.Id, jogador.Nome);
        return Results.Ok(partida.Visao(jogador.Id));
    }
});

app.MapPost("/api/partidas/{id}/iniciar", (string id, PedidoParticipacao pedido, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, pedido.JogadorId, (partida, jogador) =>
    {
        partida.Iniciar(jogador.Id);
        return partida.Visao(jogador.Id);
    }));

app.MapPost("/api/partidas/{id}/jogar", (string id, PedidoJogada pedido, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, pedido.JogadorId, (partida, jogador) =>
    {
        partida.Jogar(jogador.Id, pedido.VersaoEsperada, pedido.ChaveIdempotencia, new Peca(pedido.ValorA, pedido.ValorB), pedido.Lado);
        return partida.Visao(jogador.Id);
    }));

app.MapPost("/api/partidas/{id}/comprar", (string id, PedidoComando pedido, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, pedido.JogadorId, (partida, jogador) =>
    {
        partida.Comprar(jogador.Id, pedido.VersaoEsperada, pedido.ChaveIdempotencia);
        return partida.Visao(jogador.Id);
    }));

app.MapPost("/api/partidas/{id}/passar", (string id, PedidoComando pedido, HttpContext http, Armazem armazem) =>
    armazem.NaPartida(http, id, pedido.JogadorId, (partida, jogador) =>
    {
        partida.Passar(jogador.Id, pedido.VersaoEsperada, pedido.ChaveIdempotencia);
        return partida.Visao(jogador.Id);
    }));

app.Run();

/// <summary>Ponto de entrada (público para os testes de integração).</summary>
public partial class Program;

namespace DominoQuina.Simulador
{
    /// <summary>Corpo do pedido de token.</summary>
    public sealed record PedidoToken(string Email);

    /// <summary>Corpo da criação de perfil.</summary>
    public sealed record PedidoJogador(string Nome);

    /// <summary>Corpo da ativação.</summary>
    public sealed record PedidoAtivacao(string? ChaveAtivacao);

    /// <summary>Corpo da criação de partida.</summary>
    public sealed record PedidoPartida(int PontuacaoAlvo, string JogadorId);

    /// <summary>Corpo de entrar/iniciar.</summary>
    public sealed record PedidoParticipacao(string JogadorId);

    /// <summary>Corpo de comprar/passar.</summary>
    public sealed record PedidoComando(string JogadorId, long VersaoEsperada, Guid ChaveIdempotencia);

    /// <summary>Corpo de jogar.</summary>
    public sealed record PedidoJogada(string JogadorId, long VersaoEsperada, Guid ChaveIdempotencia, int ValorA, int ValorB, int Lado);

    /// <summary>Perfil registrado no simulador.</summary>
    public sealed class Jogador(string id, string email, string nome)
    {
        /// <summary>Identificador.</summary>
        public string Id { get; } = id;

        /// <summary>Conta dona.</summary>
        public string Email { get; } = email;

        /// <summary>Nome.</summary>
        public string Nome { get; } = nome;

        /// <summary>Hash SHA-256 da chave do cliente ativo (a chave em si não é guardada).</summary>
        public string? HashChave { get; set; }
    }

    /// <summary>Dados em memória do simulador e verificações de acesso.</summary>
    public sealed class Armazem
    {
        private readonly ConcurrentDictionary<string, string> _tokenPorEmail = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _emailPorToken = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _pedidos = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Perfis por id.</summary>
        public ConcurrentDictionary<string, Jogador> Jogadores { get; } = new();

        /// <summary>Partidas por id.</summary>
        public ConcurrentDictionary<string, MotorPartida> Partidas { get; } = new();

        /// <summary>Cria a conta se preciso e devolve o token (limite: 5 pedidos por minuto).</summary>
        public string SolicitarToken(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ErroApi(400, "EMAIL_INVALIDO", "E-mail inválido.");

            var pedidos = _pedidos.GetOrAdd(email, _ => []);
            lock (pedidos)
            {
                pedidos.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-1));
                if (pedidos.Count >= 5) throw new ErroApi(429, "LIMITE_TOKEN", "Muitas solicitações. Aguarde 60 segundos.");
                pedidos.Add(DateTime.UtcNow);
            }

            return TokenDe(email);
        }

        /// <summary>Token da conta (criado na primeira vez).</summary>
        public string TokenDe(string email) => _tokenPorEmail.GetOrAdd(email, e =>
        {
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
            _emailPorToken[token] = e;
            return token;
        });

        /// <summary>Conta do Bearer, ou 401.</summary>
        public string Autenticar(HttpContext http)
        {
            var cabecalho = http.Request.Headers.Authorization.ToString();
            if (cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && _emailPorToken.TryGetValue(cabecalho[7..].Trim(), out var email))
                return email;
            throw new ErroApi(401, "TOKEN_INVALIDO", "Token ausente, inválido ou revogado.");
        }

        /// <summary>Perfil da conta, ou 404/403.</summary>
        public Jogador JogadorDaConta(string id, string email)
        {
            if (!Jogadores.TryGetValue(id, out var jogador)) throw new ErroApi(404, "JOGADOR_NAO_ENCONTRADO", "Perfil não encontrado.");
            if (jogador.Email != email) throw new ErroApi(403, "PERFIL_DE_OUTRA_CONTA", "O perfil não pertence a esta conta.");
            return jogador;
        }

        /// <summary>Perfil autenticado e ativado com a chave de <c>X-Jogador-Ativacao</c>.</summary>
        public Jogador ExigirPerfil(HttpContext http, string jogadorId)
        {
            var jogador = JogadorDaConta(jogadorId, Autenticar(http));
            var chave = http.Request.Headers["X-Jogador-Ativacao"].ToString();
            if (jogador.HashChave is null || string.IsNullOrEmpty(chave) || Hash(chave) != jogador.HashChave)
                throw new ErroApi(403, "ATIVACAO_INVALIDA", "Perfil não ativado neste cliente.");
            return jogador;
        }

        /// <summary>Partida, ou 404.</summary>
        public MotorPartida Partida(string id) =>
            Partidas.TryGetValue(id, out var partida) ? partida : throw new ErroApi(404, "PARTIDA_NAO_ENCONTRADA", "Partida não encontrada.");

        /// <summary>Executa <paramref name="acao"/> com a partida travada; <c>null</c> vira 204.</summary>
        public IResult NaPartida(HttpContext http, string partidaId, string jogadorId, Func<MotorPartida, Jogador, object?> acao)
        {
            var jogador = ExigirPerfil(http, jogadorId);
            var partida = Partida(partidaId);
            lock (partida.Trava)
            {
                var resultado = acao(partida, jogador);
                return resultado is null ? Results.NoContent() : Results.Ok(resultado);
            }
        }

        /// <summary>SHA-256 em hexadecimal.</summary>
        public static string Hash(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }
}
