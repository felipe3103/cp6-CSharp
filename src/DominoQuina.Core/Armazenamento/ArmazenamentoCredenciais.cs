using System.Text.Json;

namespace DominoQuina.Core.Armazenamento;

/// <summary>
/// Dados que o cliente guarda entre execuções.
/// </summary>
public sealed class DadosCliente
{
    /// <summary>E-mail usado para pedir o token.</summary>
    public string? Email { get; set; }

    /// <summary>Token Bearer recebido por e-mail. Reutilizável até ser revogado.</summary>
    public string? Token { get; set; }

    /// <summary>Chave de ativação deste cliente para cada jogador-perfil.</summary>
    public Dictionary<string, string> ChavesAtivacao { get; set; } = new();

    /// <summary>Perfil ativado por último.</summary>
    public string? JogadorAtivoId { get; set; }

    /// <summary>Nome do perfil ativado por último.</summary>
    public string? JogadorAtivoNome { get; set; }

    /// <summary>Última partida aberta, para retomar depois de reabrir o app.</summary>
    public string? PartidaAtualId { get; set; }
}

/// <summary>
/// Guarda token e chaves de ativação num arquivo JSON na pasta do usuário.
/// </summary>
/// <remarks>
/// O arquivo fica em <c>~/.domino-quina/{cliente}.json</c>. No macOS e no Linux, a pasta recebe
/// permissão 700 e o arquivo 600, então só o dono lê. No Windows, vale a ACL do perfil do usuário.
/// Cada nome de cliente tem um arquivo próprio, com chaves próprias: a interface web e o bot
/// do console são clientes diferentes para a API.
/// </remarks>
public sealed class ArmazenamentoCredenciais
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };
    private readonly object _trava = new();

    /// <summary>Cria o armazenamento do cliente <paramref name="nomeCliente"/>.</summary>
    /// <param name="nomeCliente">Nome do arquivo (ex.: <c>web</c>, <c>bot</c>).</param>
    /// <param name="diretorio">Pasta alternativa; o padrão é <c>~/.domino-quina</c>.</param>
    public ArmazenamentoCredenciais(string nomeCliente, string? diretorio = null)
    {
        Diretorio = diretorio ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".domino-quina");
        Caminho = Path.Combine(Diretorio, $"{nomeCliente}.json");
    }

    /// <summary>Pasta dos arquivos.</summary>
    public string Diretorio { get; }

    /// <summary>Caminho completo do arquivo deste cliente.</summary>
    public string Caminho { get; }

    /// <summary>Lê os dados salvos (ou dados vazios se o arquivo não existir).</summary>
    public DadosCliente Ler()
    {
        lock (_trava)
        {
            if (!File.Exists(Caminho)) return new DadosCliente();
            var json = File.ReadAllText(Caminho);
            return JsonSerializer.Deserialize<DadosCliente>(json, Opcoes) ?? new DadosCliente();
        }
    }

    /// <summary>Lê, aplica <paramref name="alteracao"/> e grava de forma atômica.</summary>
    public DadosCliente Atualizar(Action<DadosCliente> alteracao)
    {
        lock (_trava)
        {
            var dados = Ler();
            alteracao(dados);
            Gravar(dados);
            return dados;
        }
    }

    /// <summary>
    /// Devolve a chave de ativação salva para o perfil ou gera e grava uma nova.
    /// </summary>
    /// <remarks>
    /// A chave é gravada <b>antes</b> de chamar a API: se a resposta da ativação se perder,
    /// a próxima tentativa usa a mesma chave e a API reconhece a ativação existente.
    /// </remarks>
    public string ObterOuCriarChave(string jogadorId, Func<string> gerar)
    {
        lock (_trava)
        {
            var dados = Ler();
            if (dados.ChavesAtivacao.TryGetValue(jogadorId, out var chave)) return chave;

            chave = gerar();
            dados.ChavesAtivacao[jogadorId] = chave;
            Gravar(dados);
            return chave;
        }
    }

    private void Gravar(DadosCliente dados)
    {
        Directory.CreateDirectory(Diretorio);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(Diretorio, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var temporario = Caminho + ".tmp";
        var opcoesArquivo = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            opcoesArquivo.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var arquivo = new FileStream(temporario, opcoesArquivo))
            JsonSerializer.Serialize(arquivo, dados, Opcoes);

        File.Move(temporario, Caminho, overwrite: true);
    }
}
