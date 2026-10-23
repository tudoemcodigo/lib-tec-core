using System.Text.Json;
using TEC.Core.Common.Results;
using TEC.Core.Common.Serialization;
using TEC.Core.Responses;
using TEC.Core.Responses.Pagination;
using TUnit.Assertions.Enums;

namespace TEC.Core.Tests.Responses;

public class ApiResponseTests
{
    private sealed record CustomerDto(int Id, string Name);

    [Test]
    public async Task Ok_SerializesInCamelCaseWithExpectedOrder()
    {
        var response = ApiResponse<CustomerDto>.Ok(new CustomerDto(1, "João"), "Consulta realizada") with { TraceId = "abc" };

        var json = response.ToJson();
        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        await Assert.That(names).IsEquivalentTo(
            new[] { "success", "statusCode", "message", "data", "errors", "timestamp", "traceId" }, CollectionOrdering.Matching);
        await Assert.That(json).Contains("\"name\":\"João\"");
    }

    [Test]
    public async Task FromResult_Success()
    {
        var response = ApiResponse<int>.FromResult(Result.Success(42));

        await Assert.That(response.Success).IsTrue();
        await Assert.That(response.StatusCode).IsEqualTo(200);
        await Assert.That(response.Data).IsEqualTo(42);
    }

    [Test]
    [Arguments(ErrorType.Validation, 400)]
    [Arguments(ErrorType.Unauthorized, 401)]
    [Arguments(ErrorType.Forbidden, 403)]
    [Arguments(ErrorType.NotFound, 404)]
    [Arguments(ErrorType.Conflict, 409)]
    [Arguments(ErrorType.Failure, 500)]
    public async Task FromResult_MapsErrorTypeToStatusCode(ErrorType type, int expectedStatus)
    {
        var result = Result.Failure<CustomerDto>(new Error("ERRO", "Mensagem", type));

        var response = ApiResponse<CustomerDto>.FromResult(result);

        await Assert.That(response.Success).IsFalse();
        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);

        // Erros internos (500) não expõem detalhes ao cliente
        if (type == ErrorType.Failure)
        {
            await Assert.That(response.Errors).IsEmpty();
        }
        else
        {
            await Assert.That(response.Errors).HasSingleItem();
            await Assert.That(response.Errors[0].Code).IsEqualTo("ERRO");
        }
    }

    [Test]
    public async Task ValidationError_WithMultipleErrors()
    {
        var result = Result.Failure(
            Error.Validation("CPF_INVALIDO", "CPF inválido", "cpf"),
            Error.Validation("EMAIL_INVALIDO", "E-mail inválido", "email"));

        var response = ApiResponse.FromResult(result);

        await Assert.That(response.StatusCode).IsEqualTo(400);
        await Assert.That(response.Message).IsEqualTo("Um ou mais erros de validação ocorreram.");
        await Assert.That(response.Errors.Select(e => e.Field)).IsEquivalentTo(new string?[] { "cpf", "email" }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task PagedResponse_IncludesPagination()
    {
        var paged = Enumerable.Range(1, 42).ToPagedResult(page: 2, pageSize: 10);

        var response = PagedResponse<int>.Create(paged);
        using var doc = JsonDocument.Parse(response.ToJson());
        var pagination = doc.RootElement.GetProperty("pagination");

        await Assert.That(response.Data!).IsEquivalentTo(Enumerable.Range(11, 10), CollectionOrdering.Matching);
        await Assert.That(pagination.GetProperty("totalPages").GetInt32()).IsEqualTo(5);
        await Assert.That(pagination.GetProperty("hasNextPage").GetBoolean()).IsTrue();
        await Assert.That(pagination.GetProperty("hasPreviousPage").GetBoolean()).IsTrue();
    }

    // Regressão: ToPagedResult sobre IQueryable materializava a consulta inteira
    [Test]
    public async Task ToPagedResult_Queryable_CountsAndFetchesOnlyThePage()
    {
        var source = new CountingEnumerable(Enumerable.Range(1, 1000));
        var query = source.AsQueryable();

        var page = query.ToPagedResult(page: 3, pageSize: 10);

        await Assert.That(page.Items).IsEquivalentTo(Enumerable.Range(21, 10), CollectionOrdering.Matching);
        await Assert.That(page.TotalItems).IsEqualTo(1000L);
        // Count percorre a sequência uma vez; a página para no 30º item (sem lista intermediária de 1000 itens)
        await Assert.That(source.Enumerations).IsEqualTo(2);

        var beyond = query.ToPagedResult(page: 200, pageSize: 10);
        await Assert.That(beyond.Items).IsEmpty();
        await Assert.That(beyond.TotalItems).IsEqualTo(1000L);
        await Assert.That(source.Enumerations).IsEqualTo(3); // página além do total: só o COUNT
    }

    // Regressão: (totalItems + pageSize - 1) estourava perto de long.MaxValue
    [Test]
    public async Task PaginationInfo_HugeTotal_DoesNotOverflow()
    {
        var info = PaginationInfo.Create(1, 10, long.MaxValue);
        await Assert.That(info.TotalPages).IsEqualTo(int.MaxValue);
        await Assert.That(info.HasNextPage).IsTrue();

        await Assert.That(PaginationInfo.Create(1, 1000, long.MaxValue - 1).TotalPages).IsEqualTo(int.MaxValue);
        await Assert.That(PaginationInfo.Create(1, 10, 41).TotalPages).IsEqualTo(5);
        await Assert.That(PaginationInfo.Create(1, 10, 40).TotalPages).IsEqualTo(4);
        await Assert.That(PaginationInfo.Create(1, 10, 0).TotalPages).IsEqualTo(0);
    }

    // Regressão: a validação do construtor era burlável com "with"
    [Test]
    public async Task ApiErrorAndPagedResult_With_AreValidated()
    {
        var error = new ApiError("CODIGO", "Mensagem");
        await Assert.That(() => error with { Code = "" }).ThrowsExactly<ArgumentException>();
        await Assert.That(() => error with { Message = null! }).ThrowsExactly<ArgumentNullException>();

        var result = new PagedResult<int>([1], 1, 10, 1);
        await Assert.That(() => result with { Page = 0 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => result with { PageSize = 0 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => result with { TotalItems = -1 }).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => result with { Items = null! }).ThrowsExactly<ArgumentNullException>();
        await Assert.That((result with { Page = 2 }).Page).IsEqualTo(2);
    }

    private sealed class CountingEnumerable(IEnumerable<int> inner) : IEnumerable<int>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<int> GetEnumerator()
        {
            Enumerations++;
            return inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    public async Task Json_RoundTrip()
    {
        var json = ApiResponse<CustomerDto>.NotFound("Cliente não encontrado").ToJson();
        var back = json.FromJson<ApiResponse<CustomerDto>>();

        await Assert.That(back).IsNotNull();
        await Assert.That(back!.StatusCode).IsEqualTo(404);
        await Assert.That(back.Message).IsEqualTo("Cliente não encontrado");
    }
}
