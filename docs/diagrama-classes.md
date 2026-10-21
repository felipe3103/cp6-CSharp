# Diagrama de classes

O diagrama usa [Mermaid](https://mermaid.js.org/) e é renderizado direto pelo GitHub.
O primeiro mostra o núcleo (`DominoQuina.Core`), que concentra as regras e a comunicação com a API.
O segundo mostra como a interface web e o bot de console usam o núcleo.

## Núcleo (`DominoQuina.Core`)

```mermaid
classDiagram
    direction LR

    namespace Dominio {
        class Pedra {
            <<record struct>>
            +int ValorA
            +int ValorB
            +bool EhDupla
            +int Soma
            +Contem(valor) bool
            +Invertida() Pedra
            +MesmaPedra(outra) bool
            +ValorExpostoAoEncaixar(ponta) int
        }
        class Lado {
            <<enum>>
            Esquerda = 0
            Direita = 1
        }
        class ProximaAcao {
            <<enum>>
            Aguardar
            JogarAbertura
            Jogar
            Comprar
            Passar
            Encerrar
            Desconhecida
        }
        class EstadoJogo {
            <<record>>
            +string PartidaId
            +string JogadorId
            +long Versao
            +string Situacao
            +int Rodada
            +int Turno
            +int PontuacaoAlvo
            +ProximaAcao ProximaAcao
            +Pedra? PedraAbertura
            +IReadOnlyList~Pedra~ Mao
            +IReadOnlyList~Pedra~ Mesa
            +int? PontaEsquerda
            +int? PontaDireita
            +int QuantidadePedrasAdversario
            +int? PedrasNaCava
            +IReadOnlyList~PlacarJogador~ Placar
            +string? VencedorId
            +bool Encerrada
            +bool MinhaVez
            +bool Iniciada
        }
        class PlacarJogador {
            <<record>>
            +string JogadorId
            +string Nome
            +int Pontos
            +bool EhVoce
        }
        class RegrasDomino {
            <<static>>
            +PontosPorSoma(soma) int
            +PontosDaJogada(esq, dir, pedra, lado) int
            +JogadasValidas(estado) IReadOnlyList~JogadaPossivel~
            +Permitidas(estado) AcoesPermitidas
            +DescontoPorDemora(tempo) int
        }
        class JogadaPossivel {
            <<record>>
            +Pedra Pedra
            +Lado Lado
            +int PontosEstimados
        }
        class AcoesPermitidas {
            <<record>>
            +IReadOnlyList~JogadaPossivel~ Jogadas
            +bool PodeComprar
            +bool PodePassar
            +bool PodeJogar
            +JogadasCom(pedra) IReadOnlyList~JogadaPossivel~
        }
        class OrientadorMesa {
            <<static>>
            +Orientar(mesa, pontaEsquerda) IReadOnlyList~Pedra~
        }
    }

    namespace Api {
        class DominoApiClient {
            -HttpClient _http
            +string? Token
            +SolicitarTokenAsync(email)
            +ListarJogadoresAsync() IReadOnlyList~JogadorDto~
            +CriarJogadorAsync(nome) JogadorDto
            +AtivarClienteAsync(jogadorId, chave)
            +DesativarClienteAsync(perfil)
            +ListarPartidasAsync(perfil) IReadOnlyList~PartidaResumoDto~
            +CriarPartidaAsync(perfil, alvo) string
            +EntrarPartidaAsync(perfil, partidaId)
            +IniciarPartidaAsync(perfil, partidaId) EstadoJogo?
            +ObterEstadoAsync(perfil, partidaId) EstadoJogo
            +ObterAtualizacoesAsync(perfil, partidaId, versao) EstadoJogo?
            +JogarAsync(perfil, partidaId, versao, chave, pedra, lado) EstadoJogo?
            +ComprarAsync(perfil, partidaId, versao, chave) EstadoJogo?
            +PassarAsync(perfil, partidaId, versao, chave) EstadoJogo?
            +ObterResultadoAsync(perfil, partidaId) ResultadoPartidaDto
        }
        class Perfil {
            <<record>>
            +string JogadorId
            +string Nome
            +string ChaveAtivacao
        }
        class ApiException {
            +HttpStatusCode StatusCode
            +string? Codigo
            +string? Detalhe
            +TimeSpan? AguardarPor
            +bool JogadorJaAtivo
            +bool ConflitoDeVersao
            +string MensagemParaUsuario
            +Criar(resposta, corpo)$ ApiException
        }
        class MapeadorEstado {
            <<static>>
            +TentarLer(json, jogadorId) EstadoJogo?
            +ParaDominio(dto, jogadorId) EstadoJogo
        }
        class RotasApi {
            <<static>>
        }
        class EstadoPartidaDto
        class JogadorDto
        class PartidaResumoDto
        class ResultadoPartidaDto
    }

    namespace Conta_e_Seguranca {
        class ServicoConta {
            +bool Autenticado
            +Perfil? PerfilAtivo
            +SolicitarTokenAsync(email)
            +DefinirTokenAsync(token)
            +AtivarAsync(jogadorId, nome) Perfil
            +GarantirAtivacaoAsync() Perfil?
            +DesativarAsync()
            +Sair()
        }
        class ArmazenamentoCredenciais {
            +string Caminho
            +Ler() DadosCliente
            +Atualizar(alteracao) DadosCliente
            +ObterOuCriarChave(jogadorId, gerar) string
        }
        class DadosCliente {
            +string? Token
            +Dictionary ChavesAtivacao
            +string? JogadorAtivoId
            +string? PartidaAtualId
        }
        class GeradorChaveAtivacao {
            <<static>>
            +Gerar()$ string
            +EhValida(chave)$ bool
        }
    }

    namespace Jogo {
        class SessaoPartida {
            -SemaphoreSlim _trava
            +Perfil Perfil
            +string PartidaId
            +EstadoJogo? Estado
            +event EstadoAlterado
            +CarregarAsync() EstadoJogo
            +AtualizarAsync() bool
            +IniciarAsync() ResultadoAcao
            +JogarAsync(pedra, lado) ResultadoAcao
            +ComprarAsync() ResultadoAcao
            +PassarAsync() ResultadoAcao
            +ExecutarAsync(decisao) ResultadoAcao
        }
        class Decisao {
            <<record>>
            +TipoDecisao Tipo
            +Pedra? Pedra
            +Lado Lado
        }
        class TipoDecisao {
            <<enum>>
            Jogar
            Comprar
            Passar
        }
        class ResultadoAcao {
            <<record>>
            +bool Aceita
            +EstadoJogo? Estado
            +ApiException? Erro
            +string Mensagem
        }
    }

    namespace Autonomo {
        class IEstrategia {
            <<interface>>
            +Candidatas(estado) IReadOnlyList~Decisao~
        }
        class EstrategiaGulosa
        class JogadorAutonomo {
            -HashSet~Decisao~ _rejeitadas
            +event Registro
            +JogarTurnoAsync() ResultadoAcao?
            +JogarAteOFimAsync() EstadoJogo
        }
    }

    EstadoJogo "1" *-- "*" Pedra : mão e mesa
    EstadoJogo "1" *-- "*" PlacarJogador
    EstadoJogo --> ProximaAcao
    JogadaPossivel --> Pedra
    JogadaPossivel --> Lado
    AcoesPermitidas "1" *-- "*" JogadaPossivel
    RegrasDomino ..> EstadoJogo : avalia
    RegrasDomino ..> AcoesPermitidas : cria

    DominoApiClient ..> RotasApi
    DominoApiClient ..> MapeadorEstado
    DominoApiClient ..> ApiException : lança
    DominoApiClient ..> Perfil : cabeçalho X-Jogador-Ativacao
    MapeadorEstado ..> EstadoPartidaDto
    MapeadorEstado ..> EstadoJogo : cria
    MapeadorEstado ..> OrientadorMesa

    ServicoConta --> DominoApiClient
    ServicoConta --> ArmazenamentoCredenciais
    ServicoConta ..> GeradorChaveAtivacao
    ServicoConta --> Perfil : PerfilAtivo
    ArmazenamentoCredenciais ..> DadosCliente

    SessaoPartida --> DominoApiClient
    SessaoPartida --> Perfil
    SessaoPartida --> EstadoJogo : último confirmado
    SessaoPartida ..> ResultadoAcao : devolve
    SessaoPartida ..> Decisao : executa
    ResultadoAcao --> ApiException
    Decisao --> TipoDecisao

    IEstrategia <|.. EstrategiaGulosa
    EstrategiaGulosa ..> RegrasDomino
    JogadorAutonomo --> SessaoPartida
    JogadorAutonomo --> IEstrategia
```

## Aplicações (`DominoQuina.Web` e `DominoQuina.Bot`)

```mermaid
classDiagram
    direction LR

    class Acesso {
        <<RazorPage>>
        rota /
        pedir e informar token
    }
    class Perfis {
        <<RazorPage>>
        rota /perfis
        criar, ativar, liberar perfil
    }
    class Partidas {
        <<RazorPage>>
        rota /partidas
        listar, criar, entrar
    }
    class Jogo {
        <<RazorPage>>
        rota /jogo/partidaId
        mesa, mão, placar, relógio
        modo autônomo
    }
    class PedraDomino {
        <<RazorComponent>>
    }
    class Relogio {
        <<RazorComponent>>
    }
    class AplicacaoBot {
        +ExecutarAsync() int
    }
    class Argumentos {
        +string Comando
        +Valor(nome) string?
    }

    Acesso --> ServicoConta
    Perfis --> ServicoConta
    Partidas --> ServicoConta
    Partidas --> DominoApiClient
    Jogo --> SessaoPartida
    Jogo --> JogadorAutonomo
    Jogo ..> RegrasDomino : jogadas permitidas
    Jogo *-- PedraDomino
    Jogo *-- Relogio
    AplicacaoBot --> ServicoConta
    AplicacaoBot --> SessaoPartida
    AplicacaoBot --> JogadorAutonomo
    AplicacaoBot ..> Argumentos
```

## Sequência de um turno

```mermaid
sequenceDiagram
    participant Tela as Tela / JogadorAutonomo
    participant Sessao as SessaoPartida
    participant API as API Dominó

    loop a cada 1 s
        Sessao->>API: GET /atualizacoes?versaoConhecida=v
        alt nada mudou
            API-->>Sessao: 204 No Content
        else estado novo
            API-->>Sessao: 200 + estado (v+1)
            Sessao-->>Tela: EstadoAlterado
        end
    end
    Tela->>Sessao: JogarAsync(pedra, lado)
    Sessao->>API: POST /jogar {versaoEsperada, chaveIdempotencia, valorA, valorB, lado}
    alt aceita
        API-->>Sessao: 200 + estado confirmado
        Sessao-->>Tela: ResultadoAcao.Aceita
    else 409 versão
        API-->>Sessao: 409
        Sessao->>API: GET /partidas/{id}
        Sessao-->>Tela: rejeitada (mesa atualizada)
    else 422 regra
        API-->>Sessao: 422
        Sessao-->>Tela: rejeitada (partida não avançou)
    else falha de rede
        Sessao->>API: repete com a MESMA chaveIdempotencia
    end
```
