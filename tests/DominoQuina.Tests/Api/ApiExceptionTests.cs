using System.Net;
using DominoQuina.Core.Api;

namespace DominoQuina.Tests.Api;

public class ApiExceptionTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "novo token")]
    [InlineData(HttpStatusCode.Forbidden, "permissão")]
    [InlineData(HttpStatusCode.Conflict, "conflito de versão")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "não avançou")]
    [InlineData(HttpStatusCode.TooManyRequests, "Aguarde")]
    public void Mensagem_segue_a_tabela_de_rejeicoes(HttpStatusCode status, string trecho) =>
        Assert.Contains(trecho, new ApiException(status, null, null).MensagemParaUsuario);

    [Fact]
    public void Limite_429_usa_Retry_After()
    {
        var resposta = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        resposta.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));

        var erro = ApiException.Criar(resposta, "");

        Assert.Equal(TimeSpan.FromSeconds(42), erro.AguardarPor);
        Assert.Contains("42 s", erro.MensagemParaUsuario);
    }

    [Fact]
    public void Le_ProblemDetails_de_validacao()
    {
        var resposta = new HttpResponseMessage(HttpStatusCode.BadRequest);

        var erro = ApiException.Criar(resposta, """{ "title": "Erro de validação", "errors": { "email": ["obrigatório"] } }""");

        Assert.Contains("email: obrigatório", erro.Detalhe);
    }

    [Fact]
    public void Corpo_que_nao_e_JSON_vira_detalhe()
    {
        var erro = ApiException.Criar(new HttpResponseMessage(HttpStatusCode.BadGateway), "<html>erro</html>");

        Assert.Equal("<html>erro</html>", erro.Detalhe);
    }
}
