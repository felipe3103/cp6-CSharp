using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Autonomo;
using DominoQuina.Core.Conta;
using DominoQuina.Core.Dominio;
using DominoQuina.Core.Jogo;

namespace DominoQuina.Bot;

/// <summary>
/// Comandos do bot de console: autenticação, perfis e partidas com o jogador autônomo.
/// </summary>
public sealed class AplicacaoBot
{
    private readonly Argumentos _args;
    private readonly DominoApiClient _api;
    private readonly ServicoConta _conta;

    /// <summary>Configura API e armazenamento a partir das opções <c>--api</c> e <c>--cliente</c>.</summary>
    public AplicacaoBot(Argumentos args)
    {
        _args = args;
        var url = args.Valor("api", Environment.GetEnvironmentVariable("DOMINO_API_URL") ?? RotasApi.UrlBasePadrao)!;
        _api = new DominoApiClient(new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(20) });

        // Cada nome de cliente tem chaves próprias: rode "--cliente bot2" para um segundo bot na mesma máquina.
        _conta = new ServicoConta(_api, new ArmazenamentoCredenciais(args.Valor("cliente", "bot")!));
    }

    /// <summary>Executa o comando e devolve o código de saída.</summary>
    public async Task<int> ExecutarAsync(CancellationToken ct)
    {
        try
        {
            switch (_args.Comando)
            {
                case "token": return await TokenAsync(ct);
                case "perfis": return await PerfisAsync(ct);
                case "ativar": return await AtivarAsync(ct);
                case "desativar": return await DesativarAsync(ct);
                case "partidas": return await PartidasAsync(ct);
                case "jogar": return await JogarAsync(ct);
                default:
                    MostrarAjuda();
                    return _args.Comando == "ajuda" ? 0 : 1;
            }
        }
        catch (ApiException ex)
        {
            Erro(ex.MensagemParaUsuario);
            return 2;
        }
        catch (Exception ex) when (ex is HttpRequestException or ArgumentException or InvalidOperationException)
        {
            Erro(ex.Message);
            return 2;
        }
    }

    private async Task<int> TokenAsync(CancellationToken ct)
    {
        if (_args.Valor("email") is { } email)
        {
            await _conta.SolicitarTokenAsync(email, ct);
            Console.WriteLine($"Pedido aceito (202). O token foi enviado para {email}.");
            Console.WriteLine("Depois rode: token --definir <TOKEN>");
            return 0;
        }

        if (_args.Valor("definir") is { } token)
        {
            await _conta.DefinirTokenAsync(token, ct);
            Console.WriteLine("Token validado e salvo.");
            return 0;
        }

        Erro("Use: token --email <e-mail FIAP>  ou  token --definir <TOKEN>");
        return 1;
    }

    private async Task<int> PerfisAsync(CancellationToken ct)
    {
        ExigirToken();

        if (_args.Valor("criar") is { } nome)
        {
            var criado = await _conta.CriarJogadorAsync(nome, ct);
            Console.WriteLine($"Perfil criado: {criado.NomeExibicao} ({criado.Identificador})");
        }

        var jogadores = await _conta.ListarJogadoresAsync(ct);
        Console.WriteLine(jogadores.Count == 0 ? "Nenhum perfil. Crie com: perfis --criar <nome>" : "Perfis da conta:");
        foreach (var jogador in jogadores)
        {
            var ativo = _conta.PerfilAtivo?.JogadorId == jogador.Identificador ? "  [ativo neste cliente]" : "";
            Console.WriteLine($"  {jogador.Identificador}  {jogador.NomeExibicao}{ativo}");
        }

        return 0;
    }

    private async Task<int> AtivarAsync(CancellationToken ct)
    {
        ExigirToken();
        var alvo = _args.Posicionais.FirstOrDefault() ?? throw new ArgumentException("Use: ativar <jogadorId ou nome>");

        var jogadores = await _conta.ListarJogadoresAsync(ct);
        var jogador = jogadores.FirstOrDefault(j => j.Identificador == alvo)
                      ?? jogadores.FirstOrDefault(j => string.Equals(j.Nome, alvo, StringComparison.OrdinalIgnoreCase));

        var perfil = await _conta.AtivarAsync(jogador?.Identificador ?? alvo, jogador?.NomeExibicao ?? alvo, ct);
        Console.WriteLine($"Perfil {perfil.Nome} ativo neste cliente.");
        return 0;
    }

    private async Task<int> DesativarAsync(CancellationToken ct)
    {
        ExigirToken();
        if (_conta.PerfilAtivo is null)
        {
            Console.WriteLine("Nenhum perfil ativo neste cliente.");
            return 0;
        }

        await _conta.DesativarAsync(ct);
        Console.WriteLine("Perfil liberado para outro cliente.");
        return 0;
    }

    private async Task<int> PartidasAsync(CancellationToken ct)
    {
        ExigirToken();
        var partidas = await _api.ListarPartidasAsync(_conta.PerfilAtivo, ct);
        Console.WriteLine(partidas.Count == 0 ? "Nenhuma partida disponível." : "Partidas:");
        foreach (var partida in partidas)
            Console.WriteLine($"  {partida.Identificador}  {partida.Ocupacao}/2  até {partida.PontuacaoAlvo}  {partida.Situacao}");
        return 0;
    }

    private async Task<int> JogarAsync(CancellationToken ct)
    {
        ExigirToken();
        var perfil = await _conta.GarantirAtivacaoAsync(ct)
                     ?? throw new InvalidOperationException("Nenhum perfil ativo. Rode: ativar <jogadorId>");

        string partidaId;
        var criei = false;

        if (_args.Valor("partida") is { } existente)
        {
            partidaId = existente;
            Console.WriteLine($"Retomando a partida {partidaId}.");
        }
        else if (_args.Valor("entrar") is { } alvo)
        {
            await _api.EntrarPartidaAsync(perfil, alvo, ct);
            partidaId = alvo;
            Console.WriteLine($"Entrou na partida {partidaId}.");
        }
        else if (_args.Tem("entrar-qualquer"))
        {
            partidaId = await EntrarEmQualquerAsync(perfil, ct);
        }
        else
        {
            var pontuacaoAlvo = _args.Inteiro("alvo", 50);
            partidaId = await _api.CriarPartidaAsync(perfil, pontuacaoAlvo, ct);
            criei = true;
            Console.WriteLine($"Partida criada: {partidaId} (até {pontuacaoAlvo}). Aguardando o segundo jogador…");
        }

        _conta.LembrarPartida(partidaId);
        var sessao = new SessaoPartida(_api, perfil, partidaId);
        sessao.EstadoAlterado += Imprimir;
        await sessao.CarregarAsync(ct);

        await AguardarInicioAsync(sessao, tentarIniciar: criei || _args.Tem("iniciar"), ct);

        var jogador = new JogadorAutonomo(sessao);
        jogador.Registro += mensagem => Console.WriteLine($"  » {mensagem}");
        var final = await jogador.JogarAteOFimAsync(ct);

        ImprimirResultado(final);
        try
        {
            var resultado = await _api.ObterResultadoAsync(perfil, partidaId, ct);
            Console.WriteLine($"Resultado oficial: {resultado.JsonBruto}");
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"Resultado oficial indisponível: {ex.MensagemParaUsuario}");
        }

        return 0;
    }

    private async Task<string> EntrarEmQualquerAsync(Perfil perfil, CancellationToken ct)
    {
        Console.WriteLine("Procurando mesa com vaga…");
        while (true)
        {
            var partidas = await _api.ListarPartidasAsync(perfil, ct);
            foreach (var partida in partidas.Where(p => p.Ocupacao < 2))
            {
                try
                {
                    await _api.EntrarPartidaAsync(perfil, partida.Identificador, ct);
                    Console.WriteLine($"Entrou na partida {partida.Identificador} (até {partida.PontuacaoAlvo}).");
                    return partida.Identificador;
                }
                catch (ApiException ex)
                {
                    // A vaga pode ter sido ocupada entre a listagem e o ingresso.
                    Console.WriteLine($"  Não entrou em {partida.Identificador}: {ex.MensagemParaUsuario}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
    }

    private static async Task AguardarInicioAsync(SessaoPartida sessao, bool tentarIniciar, CancellationToken ct)
    {
        var proximaTentativa = DateTime.MinValue;
        while (!sessao.Estado!.Iniciada)
        {
            var estado = sessao.Estado;
            var doisJogadores = estado.Placar.Count >= 2;

            // Sem lista de participantes no estado, tenta iniciar a cada 5 s; a API rejeita se faltar alguém.
            if (tentarIniciar && (doisJogadores || estado.Placar.Count == 0) && DateTime.UtcNow >= proximaTentativa)
            {
                var resultado = await sessao.IniciarAsync(ct);
                if (resultado.Aceita)
                {
                    Console.WriteLine("Partida iniciada.");
                    continue;
                }

                proximaTentativa = DateTime.UtcNow.AddSeconds(5);
            }

            if (!await sessao.AtualizarAsync(ct))
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    private static void Imprimir(EstadoJogo estado)
    {
        Console.WriteLine();
        Console.WriteLine($"── v{estado.Versao} · rodada {estado.Rodada} · turno {estado.Turno} · {estado.Situacao} · próxima: {ProximaAcaoTexto.Descrever(estado.ProximaAcao)}");
        Console.WriteLine($"   Mesa : {(estado.Mesa.Count == 0 ? "(vazia)" : string.Concat(estado.Mesa))}   pontas {estado.PontaEsquerda?.ToString() ?? "-"} / {estado.PontaDireita?.ToString() ?? "-"}");
        Console.WriteLine($"   Mão  : {string.Join(' ', estado.Mao)}   (oponente: {estado.QuantidadePedrasAdversario} pedras{(estado.PedrasNaCava is { } c ? $", cava: {c}" : "")})");
        if (estado.Placar.Count > 0)
            Console.WriteLine($"   Placar: {string.Join("  ×  ", estado.Placar.Select(p => $"{(p.EhVoce ? "Você" : p.Nome)} {p.Pontos}"))}   (alvo {estado.PontuacaoAlvo})");
    }

    private static void ImprimirResultado(EstadoJogo final)
    {
        Console.WriteLine();
        var venci = final.VencedorId is not null && final.VencedorId == final.JogadorId;
        Console.WriteLine(venci ? "🏆 Vitória!" : final.VencedorId is null ? "Partida encerrada." : $"Vitória de {final.Placar.FirstOrDefault(p => p.JogadorId == final.VencedorId)?.Nome ?? final.VencedorId}.");
    }

    private void ExigirToken()
    {
        if (!_conta.Autenticado)
            throw new InvalidOperationException("Sem token. Rode: token --email <e-mail FIAP>  e depois  token --definir <TOKEN>");
    }

    private static void Erro(string mensagem)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(mensagem);
        Console.ResetColor();
    }

    private static void MostrarAjuda() => Console.WriteLine("""
        Dominó Ponta de Quina — jogador autônomo

        Uso: dotnet run --project src/DominoQuina.Bot -- <comando> [opções]

          token --email <e-mail>        Pede o token (chega por e-mail)
          token --definir <TOKEN>       Valida e salva o token
          perfis [--criar <nome>]       Lista (e cria) jogadores-perfil
          ativar <jogadorId|nome>       Ativa o perfil neste cliente
          desativar                     Libera o perfil para outro cliente
          partidas                      Lista partidas
          jogar [--alvo 50]             Cria uma mesa, espera o adversário, inicia e joga
          jogar --entrar <partidaId>    Entra numa mesa e joga
          jogar --entrar-qualquer       Entra na primeira mesa com vaga e joga
          jogar --partida <partidaId>   Retoma uma partida em que já participa

        Opções gerais:
          --cliente <nome>   Arquivo de credenciais (~/.domino-quina/<nome>.json). Padrão: bot
          --api <url>        URL da API (ou variável DOMINO_API_URL)
        """);
}
