using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Conta;
using DominoQuina.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Cliente local de uma pessoa só: API, conta e credenciais são compartilhadas pelo app inteiro.
var urlApi = builder.Configuration["DominoApi:UrlBase"] ?? RotasApi.UrlBasePadrao;
builder.Services.AddSingleton(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
{
    BaseAddress = new Uri(urlApi),
    Timeout = TimeSpan.FromSeconds(20),
});
builder.Services.AddSingleton<DominoApiClient>();
builder.Services.AddSingleton(new ArmazenamentoCredenciais(builder.Configuration["DominoApi:NomeCliente"] ?? "web"));
builder.Services.AddSingleton<ServicoConta>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
