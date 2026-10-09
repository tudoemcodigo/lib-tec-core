using TEC.Core.Common.Results;

namespace TEC.Core.Tests.Common;

public class ResultCompositionTests
{
    private static readonly Error Falha = Error.BusinessRule("FALHA", "Falhou.");

    [Test]
    public async Task Bind_runs_next_only_on_success()
    {
        Result<int> sucesso = 2;
        Result<int> falha = Falha;
        var chamadas = 0;

        var dobrado = sucesso.Bind(v => { chamadas++; return Result.Success(v * 2); });
        var propagado = falha.Bind(v => { chamadas++; return Result.Success(v * 2); });

        await Assert.That(dobrado.Value).IsEqualTo(4);
        await Assert.That(propagado.Error).IsEqualTo(Falha);
        await Assert.That(chamadas).IsEqualTo(1);
    }

    [Test]
    public async Task Bind_to_result_without_value_keeps_all_errors()
    {
        var outro = Error.Validation("CAMPO", "Inválido.", "campo");
        var falha = Result.Failure<int>(Falha, outro);

        var resultado = falha.Bind(_ => Result.Success());

        await Assert.That(resultado.IsFailure).IsTrue();
        await Assert.That(resultado.Errors.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Bind_on_result_without_value_chains_and_short_circuits()
    {
        var sucesso = Result.Success().Bind(() => Result.Success("ok"));
        var falha = Result.Failure(Falha).Bind(() => Result.Success("ok"));

        await Assert.That(sucesso.Value).IsEqualTo("ok");
        await Assert.That(falha.Error).IsEqualTo(Falha);
        await Assert.That(Result.Success().Bind(() => Falha).Error).IsEqualTo(Falha);
    }

    [Test]
    public async Task Ensure_fails_when_predicate_is_false()
    {
        Result<int> valor = 5;
        var erro = Error.Validation("MAIOR", "Deve ser maior que 10.", "valor");

        await Assert.That(valor.Ensure(v => v > 10, erro).Error).IsEqualTo(erro);
        await Assert.That(valor.Ensure(v => v > 1, erro).Value).IsEqualTo(5);
        await Assert.That(Result.Failure<int>(Falha).Ensure(_ => false, erro).Error).IsEqualTo(Falha);
    }

    [Test]
    public async Task Async_chain_composes_and_stops_at_first_failure()
    {
        static Task<Result<int>> Carregar(int v) => Task.FromResult(Result.Success(v));
        var chamadas = 0;

        var resultado = await Carregar(3)
            .BindAsync(v => Task.FromResult(Result.Success(v + 1)))
            .BindAsync(v => v > 3 ? Result.Failure<int>(Falha) : Result.Success(v))
            .MapAsync(v => { chamadas++; return v.ToString(System.Globalization.CultureInfo.InvariantCulture); });

        await Assert.That(resultado.Error).IsEqualTo(Falha);
        await Assert.That(chamadas).IsEqualTo(0);
    }

    [Test]
    public async Task BindAsync_on_result_without_value_skips_on_failure()
    {
        var chamadas = 0;
        var resultado = await Result.Failure(Falha).BindAsync(() => { chamadas++; return Task.FromResult(Result.Success()); });
        var semValor = await Result.Success(1).BindAsync(_ => Task.FromResult(Result.Success()));

        await Assert.That(resultado.Error).IsEqualTo(Falha);
        await Assert.That(chamadas).IsEqualTo(0);
        await Assert.That(semValor.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Bind_rejects_null_delegates()
    {
        await Assert.That(() => Result.Success(1).Bind((Func<int, Result<int>>)null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => Result.Success(1).Ensure(null!, Falha)).ThrowsExactly<ArgumentNullException>();
    }
}
