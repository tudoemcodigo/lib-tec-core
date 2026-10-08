using TEC.Core.Threading;

namespace TEC.Core.Tests.Threading;

public class SingleFlightTests
{
    [Test]
    public async Task Concurrent_calls_with_same_key_run_the_operation_once()
    {
        var flight = new SingleFlight<string, int>(StringComparer.Ordinal);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int executions = 0;

        async Task<int> Operation(string key, CancellationToken ct)
        {
            Interlocked.Increment(ref executions);
            await release.Task.WaitAsync(ct);
            return 42;
        }

        var calls = Enumerable.Range(0, 100).Select(_ => Task.Run(() => flight.RunAsync("k", Operation))).ToArray();
        await WaitUntil(() => Volatile.Read(ref executions) == 1);
        release.SetResult();

        int[] results = await Task.WhenAll(calls);
        await Assert.That(executions).IsEqualTo(1);
        await Assert.That(results.All(r => r == 42)).IsTrue();
        await Assert.That(flight.InFlightCount).IsEqualTo(0);
    }

    [Test]
    public async Task Different_keys_run_independently()
    {
        var flight = new SingleFlight<int, int>();
        int[] results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => flight.RunAsync(i, (k, _) => Task.FromResult(k * 2))));
        await Assert.That(results).IsEquivalentTo(Enumerable.Range(0, 10).Select(i => i * 2));
    }

    [Test]
    public async Task Exception_reaches_all_waiters_and_next_call_runs_again()
    {
        var flight = new SingleFlight<string, int>();
        int executions = 0;

        await Assert.That(() => flight.RunAsync("k", (_, _) =>
        {
            Interlocked.Increment(ref executions);
            return Task.FromException<int>(new InvalidOperationException("falhou"));
        })).Throws<InvalidOperationException>();

        int value = await flight.RunAsync("k", (_, _) =>
        {
            Interlocked.Increment(ref executions);
            return Task.FromResult(7);
        });

        await Assert.That(value).IsEqualTo(7);
        await Assert.That(executions).IsEqualTo(2);
    }

    [Test]
    public async Task One_caller_giving_up_does_not_cancel_the_others()
    {
        var flight = new SingleFlight<string, int>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken shared = default;

        async Task<int> Operation(string key, CancellationToken ct)
        {
            shared = ct;
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            return 1;
        }

        using var impatient = new CancellationTokenSource();
        var first = flight.RunAsync("k", Operation, impatient.Token);
        await started.Task;
        var second = flight.RunAsync("k", Operation);

        await impatient.CancelAsync();
        await Assert.That(() => first).Throws<OperationCanceledException>();
        await Assert.That(shared.IsCancellationRequested).IsFalse();

        release.SetResult();
        await Assert.That(await second).IsEqualTo(1);
    }

    [Test]
    public async Task Operation_is_cancelled_when_every_waiter_gives_up()
    {
        var flight = new SingleFlight<string, int>();
        var observedCancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Operation(string key, CancellationToken ct)
        {
            await using var registration = ct.Register(() => observedCancellation.TrySetResult());
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }

        using var cts = new CancellationTokenSource();
        var call = flight.RunAsync("k", Operation, cts.Token);
        await cts.CancelAsync();

        await Assert.That(() => call).Throws<OperationCanceledException>();
        await observedCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntil(() => flight.InFlightCount == 0);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condição não atingida em 5 s.");
            await Task.Delay(5);
        }
    }
}
