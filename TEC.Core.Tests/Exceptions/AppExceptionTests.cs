using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Exceptions;
using TEC.Core.Responses;
using TUnit.Assertions.Enums;

namespace TEC.Core.Tests.Exceptions;

public class AppExceptionTests
{
    public static IEnumerable<Func<(AppException Exception, int Status, ErrorType Type)>> Cases()
    {
        yield return () => (new BusinessException("SALDO_INSUFICIENTE", "Saldo insuficiente."), 422, ErrorType.BusinessRule);
        yield return () => (NotFoundException.For("Cliente"), 404, ErrorType.NotFound);
        yield return () => (new ConflictException("E-mail já cadastrado."), 409, ErrorType.Conflict);
        yield return () => (new ConcurrencyException(), 409, ErrorType.Conflict);
        yield return () => (new ForbiddenException(), 403, ErrorType.Forbidden);
        yield return () => (new UnauthenticatedException(), 401, ErrorType.Unauthorized);
        yield return () => (new RequestValidationException("email", "E-mail é obrigatório."), 400, ErrorType.Validation);
        yield return () => (new RateLimitExceededException(TimeSpan.FromSeconds(30)), 429, ErrorType.TooManyRequests);
        yield return () => (new IntegrationException("ReceitaFederal", "Timeout"), 502, ErrorType.ExternalService);
        yield return () => (new InvalidConfigurationException("Crypto:Key"), 500, ErrorType.Failure);
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task Exceptions_MapToExpectedStatusAndType(AppException exception, int status, ErrorType type)
    {
        await Assert.That(exception.StatusCode).IsEqualTo(status);
        await Assert.That(exception.ErrorType).IsEqualTo(type);

        var response = ApiResponse.FromException(exception, traceId: "abc");
        await Assert.That(response.StatusCode).IsEqualTo(status);
        await Assert.That(response.Success).IsFalse();
        await Assert.That(response.TraceId).IsEqualTo("abc");
    }

    [Test]
    public async Task BusinessException_ExposesMessageAndCode()
    {
        var response = ApiResponse.FromException(new BusinessException("SALDO_INSUFICIENTE", "Saldo insuficiente."));

        await Assert.That(response.Message).IsEqualTo("Saldo insuficiente.");
        await Assert.That(response.Errors).HasSingleItem();
        await Assert.That(response.Errors[0].Code).IsEqualTo("SALDO_INSUFICIENTE");
    }

    [Test]
    public async Task RequestValidationException_KeepsAllFieldErrors()
    {
        var exception = new RequestValidationException(
            Error.Validation("CPF_INVALIDO", "CPF inválido.", "cpf"),
            Error.Validation("EMAIL_OBRIGATORIO", "E-mail é obrigatório.", "email"));

        var response = ApiResponse<object>.FromException(exception);

        await Assert.That(response.StatusCode).IsEqualTo(400);
        await Assert.That(response.Errors.Select(e => e.Field)).IsEquivalentTo(new string?[] { "cpf", "email" }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task RequestValidationException_RejectsNonValidationErrors()
    {
        await Assert.That(() => new RequestValidationException(Error.NotFound("X", "x"))).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new RequestValidationException([])).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new RequestValidationException(" ", "mensagem")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task IntegrationAndConfiguration_NeverExposeDetails()
    {
        var integration = ApiResponse.FromException(
            new IntegrationException("ReceitaFederal", "Timeout em https://api.interna:8443/v1", new TimeoutException()));
        var configuration = ApiResponse.FromException(new InvalidConfigurationException("ConnectionStrings:Default"));

        await Assert.That(integration.ToJson()).DoesNotContain("api.interna");
        await Assert.That(integration.Errors).IsEmpty();
        await Assert.That(configuration.ToJson()).DoesNotContain("ConnectionStrings");
        await Assert.That(configuration.Errors).IsEmpty();
    }

    [Test]
    public async Task UnknownException_BecomesGenericInternalError()
    {
        var response = ApiResponse.FromException(new InvalidOperationException("Senha do banco: 123"));

        await Assert.That(response.StatusCode).IsEqualTo(500);
        await Assert.That(response.ToJson()).DoesNotContain("123");
    }

    [Test]
    public async Task NotFound_DoesNotLeakIdentifier()
    {
        var exception = NotFoundException.For("Cliente");
        await Assert.That(exception.Message).IsEqualTo("Cliente não encontrado(a).");
    }

    [Test]
    public async Task ToResult_ConvertsToFailure()
    {
        var result = new ConflictException("E-mail já cadastrado.").ToResult<int>();

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error!.Type).IsEqualTo(ErrorType.Conflict);
    }

    [Test]
    public async Task Exceptions_ValidateArguments()
    {
        await Assert.That(() => new BusinessException(" ")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new BusinessException("", "mensagem")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => NotFoundException.For("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new IntegrationException("", "x")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new InvalidConfigurationException("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => new RateLimitExceededException(TimeSpan.Zero)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new RateLimitExceededException(TimeSpan.FromDays(2))).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    // Regressão: new ConcurrencyException(null) era ambíguo entre (Exception?) e (string, Exception?)
    [Test]
    public async Task ConcurrencyException_Null_UsesDefaultMessage()
    {
        var exception = new ConcurrencyException(null);

        await Assert.That(exception.Message).IsEqualTo(ConcurrencyException.DefaultMessage);
        await Assert.That(exception.InnerException).IsNull();
        await Assert.That(new ConcurrencyException("Outra mensagem").Message).IsEqualTo("Outra mensagem");
    }

    // Regressão: new RequestValidationException() compilava (params vazio) e falhava só em tempo de execução
    [Test]
    public async Task RequestValidationException_RequiresAtLeastOneError()
    {
        var constructors = typeof(RequestValidationException).GetConstructors();
        var acceptsNoArguments = constructors.Any(c => c.GetParameters().All(p => p.IsOptional
            || p.IsDefined(typeof(ParamArrayAttribute), false)
            // Pelo nome: no net8.0 o compilador embute o próprio ParamCollectionAttribute no assembly
            || p.GetCustomAttributes(false).Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.ParamCollectionAttribute")));
        await Assert.That(acceptsNoArguments).IsFalse();

        var single = new RequestValidationException(Error.Validation("A", "Erro A", "a"));
        var many = new RequestValidationException(Error.Validation("A", "Erro A", "a"), Error.Validation("B", "Erro B", "b"));
        IEnumerable<Error> list = [Error.Validation("C", "Erro C", "c")];

        await Assert.That(single.Errors.Count).IsEqualTo(1);
        await Assert.That(many.Errors.Count).IsEqualTo(2);
        await Assert.That(new RequestValidationException(list).Errors.Count).IsEqualTo(1);
        await Assert.That(() => new RequestValidationException(Array.Empty<Error>())).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task InnerException_IsPreservedForLogs()
    {
        var inner = new TimeoutException();
        var exception = new IntegrationException("ViaCep", "Timeout", inner);

        await Assert.That(exception.InnerException).IsSameReferenceAs(inner);
        await Assert.That(exception.ServiceName).IsEqualTo("ViaCep");
    }
}
