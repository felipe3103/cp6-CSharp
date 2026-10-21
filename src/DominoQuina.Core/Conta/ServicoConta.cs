using System.Net;
using DominoQuina.Core.Api;
using DominoQuina.Core.Armazenamento;
using DominoQuina.Core.Seguranca;

namespace DominoQuina.Core.Conta;

/// <summary>
/// Fluxo de acesso: token por e-mail, escolha do jogador-perfil e ativação neste cliente.
/// </summary>
public sealed class ServicoConta
{
    private readonly DominoApiClient _api;
    private readonly ArmazenamentoCredenciais _armazenamento;
    private bool _ativacaoConfirmada;

    /// <summary>Cria o serviço e restaura o token e o perfil salvos.</summary>
    public ServicoConta(DominoApiClient api, ArmazenamentoCredenciais armazenamento)
    {
        _api = api;
        _armazenamento = armazenamento;

        var dados = armazenamento.Ler();
        _api.Token = dados.Token;
        Email = dados.Email;

        if (dados.JogadorAtivoId is { } id && dados.ChavesAtivacao.TryGetValue(id, out var chave))
            PerfilAtivo = new Perfil(id, dados.JogadorAtivoNome ?? id, chave);
    }

    /// <summary>E-mail usado no último pedido de token.</summary>
    public string? Email { get; private set; }

    /// <summary>Indica se há um token salvo.</summary>
    public bool Autenticado => !string.IsNullOrWhiteSpace(_api.Token);

    /// <summary>Perfil ativado neste cliente, se houver.</summary>
    public Perfil? PerfilAtivo { get; private set; }

    /// <summary>Última partida aberta neste cliente.</summary>
    public string? PartidaAtualId => _armazenamento.Ler().PartidaAtualId;

    /// <summary>Disparado quando token ou perfil mudam.</summary>
    public event Action? Alterado;

    /// <summary>Pede o token por e-mail. A API responde 202 e envia o token ao endereço informado.</summary>
    /// <exception cref="ArgumentException">E-mail em formato inválido.</exception>
    public async Task SolicitarTokenAsync(string email, CancellationToken ct = default)
    {
        email = email.Trim();
        if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            throw new ArgumentException("Informe um e-mail válido (use o e-mail FIAP).", nameof(email));

        await _api.SolicitarTokenAsync(email, ct);
        Email = email;
        _armazenamento.Atualizar(d => d.Email = email);
    }

    /// <summary>
    /// Guarda o token recebido por e-mail depois de validá-lo numa rota protegida.
    /// </summary>
    /// <exception cref="ApiException">401: o token não foi aceito.</exception>
    public async Task DefinirTokenAsync(string token, CancellationToken ct = default)
    {
        token = token.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = token[7..].Trim();

        var anterior = _api.Token;
        _api.Token = token;
        try
        {
            await _api.ListarJogadoresAsync(ct);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _api.Token = anterior;
            throw;
        }
        catch (ApiException)
        {
            // Outros erros (ex.: rota de jogadores diferente) não invalidam o token.
        }

        _ativacaoConfirmada = false;
        _armazenamento.Atualizar(d => d.Token = token);
        Alterado?.Invoke();
    }

    /// <summary>Esquece o token salvo neste computador. As chaves de ativação continuam guardadas.</summary>
    public void Sair()
    {
        _api.Token = null;
        PerfilAtivo = null;
        _ativacaoConfirmada = false;
        _armazenamento.Atualizar(d =>
        {
            d.Token = null;
            d.JogadorAtivoId = null;
            d.JogadorAtivoNome = null;
        });
        Alterado?.Invoke();
    }

    /// <summary>Lista os perfis da conta.</summary>
    public Task<IReadOnlyList<JogadorDto>> ListarJogadoresAsync(CancellationToken ct = default) => _api.ListarJogadoresAsync(ct);

    /// <summary>Cria um perfil na conta.</summary>
    public Task<JogadorDto> CriarJogadorAsync(string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Informe o nome do perfil.", nameof(nome));
        return _api.CriarJogadorAsync(nome.Trim(), ct);
    }

    /// <summary>
    /// Ativa o perfil neste cliente, gerando (ou reaproveitando) a chave aleatória deste cliente.
    /// </summary>
    /// <exception cref="ApiException">409 <c>JOGADOR_JA_ATIVO</c>: o perfil está ativo em outro cliente.</exception>
    public async Task<Perfil> AtivarAsync(string jogadorId, string nome, CancellationToken ct = default)
    {
        var chave = _armazenamento.ObterOuCriarChave(jogadorId, GeradorChaveAtivacao.Gerar);
        await _api.AtivarClienteAsync(jogadorId, chave, ct);

        PerfilAtivo = new Perfil(jogadorId, nome, chave);
        _ativacaoConfirmada = true;
        _armazenamento.Atualizar(d =>
        {
            d.JogadorAtivoId = jogadorId;
            d.JogadorAtivoNome = nome;
        });
        Alterado?.Invoke();
        return PerfilAtivo;
    }

    /// <summary>
    /// Repete a ativação do perfil salvo com a mesma chave, uma vez por execução do app.
    /// A API reconhece a ativação existente.
    /// </summary>
    public async Task<Perfil?> GarantirAtivacaoAsync(CancellationToken ct = default)
    {
        if (PerfilAtivo is null || _ativacaoConfirmada) return PerfilAtivo;
        return await AtivarAsync(PerfilAtivo.JogadorId, PerfilAtivo.Nome, ct);
    }

    /// <summary>
    /// Libera o perfil para outro cliente e descarta a chave, que não serve mais.
    /// </summary>
    public async Task DesativarAsync(CancellationToken ct = default)
    {
        if (PerfilAtivo is not { } perfil) return;

        await _api.DesativarClienteAsync(perfil, ct);

        PerfilAtivo = null;
        _ativacaoConfirmada = false;
        _armazenamento.Atualizar(d =>
        {
            d.ChavesAtivacao.Remove(perfil.JogadorId);
            d.JogadorAtivoId = null;
            d.JogadorAtivoNome = null;
            d.PartidaAtualId = null;
        });
        Alterado?.Invoke();
    }

    /// <summary>Lembra a partida aberta para poder retomá-la depois.</summary>
    public void LembrarPartida(string? partidaId) => _armazenamento.Atualizar(d => d.PartidaAtualId = partidaId);
}
