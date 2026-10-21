using DominoQuina.Bot;

// Jogador autônomo de linha de comando. Use "ajuda" para ver os comandos.
using var cancelamento = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelamento.Cancel();
};

try
{
    return await new AplicacaoBot(Argumentos.Ler(args)).ExecutarAsync(cancelamento.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nInterrompido.");
    return 130;
}
