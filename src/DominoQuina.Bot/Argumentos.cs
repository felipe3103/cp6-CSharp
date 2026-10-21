namespace DominoQuina.Bot;

/// <summary>
/// Argumentos da linha de comando: um comando seguido de opções <c>--nome valor</c> ou <c>--flag</c>.
/// </summary>
public sealed class Argumentos
{
    private readonly Dictionary<string, string?> _opcoes = new(StringComparer.OrdinalIgnoreCase);

    private Argumentos(string comando, List<string> posicionais)
    {
        Comando = comando;
        Posicionais = posicionais;
    }

    /// <summary>Primeiro argumento (ex.: <c>jogar</c>).</summary>
    public string Comando { get; }

    /// <summary>Argumentos sem <c>--</c> depois do comando.</summary>
    public IReadOnlyList<string> Posicionais { get; }

    /// <summary>Interpreta <paramref name="args"/>.</summary>
    public static Argumentos Ler(string[] args)
    {
        var temComando = args.Length > 0 && !args[0].StartsWith("--");
        var comando = temComando ? args[0].ToLowerInvariant() : "ajuda";
        var posicionais = new List<string>();
        var resultado = new Argumentos(comando, posicionais);

        for (var i = temComando ? 1 : 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var nome = args[i][2..];
                var valor = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : null;
                resultado._opcoes[nome] = valor;
            }
            else
            {
                posicionais.Add(args[i]);
            }
        }

        return resultado;
    }

    /// <summary>Indica se a opção foi informada.</summary>
    public bool Tem(string nome) => _opcoes.ContainsKey(nome);

    /// <summary>Valor da opção, ou <paramref name="padrao"/>.</summary>
    public string? Valor(string nome, string? padrao = null) =>
        _opcoes.TryGetValue(nome, out var valor) && valor is not null ? valor : padrao;

    /// <summary>Valor inteiro da opção, ou <paramref name="padrao"/>.</summary>
    public int Inteiro(string nome, int padrao) =>
        int.TryParse(Valor(nome), out var numero) ? numero : padrao;
}
