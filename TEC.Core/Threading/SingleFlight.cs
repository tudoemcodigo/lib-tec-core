namespace TEC.Core.Threading;

/// <summary>
/// Garante <b>uma única execução por chave</b> de uma operação assíncrona: chamadas simultâneas com a mesma chave aguardam
/// a execução em andamento em vez de dispará-la de novo. Evita <i>cache stampede</i> (muitas requisições consultando a
/// mesma fonte lenta ao mesmo tempo quando o cache expira).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Nada é guardado depois que a execução termina: combine com um cache (grave o resultado dentro da operação).</item>
/// <item>Exceções da operação chegam a todos que a aguardavam; a próxima chamada executa de novo.</item>
/// <item>Cancelamento: cada chamador pode desistir da espera com o próprio token. A operação compartilhada só é cancelada
/// quando TODOS os que a aguardam desistiram; quem continua esperando nunca recebe o cancelamento de outro chamador.</item>
/// <item>Thread-safe; feito para muitos acessos simultâneos (uma trava curta por chamada, nunca durante a operação).</item>
/// </list>
/// </remarks>
/// <typeparam name="TKey">Tipo da chave.</typeparam>
/// <typeparam name="TValue">Tipo do resultado.</typeparam>
public sealed class SingleFlight<TKey, TValue>
    where TKey : notnull
{
    private readonly Lock _sync = new();
    private readonly Dictionary<TKey, Flight> _flights;

    /// <summary>Cria a coordenação com o comparador de chaves informado (padrão: o do tipo).</summary>
    /// <param name="comparer">Comparador das chaves (ex.: <see cref="StringComparer.Ordinal"/>).</param>
    public SingleFlight(IEqualityComparer<TKey>? comparer = null) => _flights = new Dictionary<TKey, Flight>(comparer);

    /// <summary>Quantidade de execuções em andamento (diagnóstico e testes).</summary>
    public int InFlightCount
    {
        get
        {
            lock (_sync)
                return _flights.Count;
        }
    }

    /// <summary>
    /// Executa <paramref name="operation"/> para a chave ou, se já houver uma execução em andamento para ela, aguarda essa.
    /// </summary>
    /// <param name="key">Chave da operação.</param>
    /// <param name="operation">Operação; recebe a chave e o token compartilhado (cancelado só quando todos desistem).</param>
    /// <param name="cancellationToken">Desiste da espera (não cancela a operação enquanto outros aguardam).</param>
    /// <returns>Resultado da execução compartilhada.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> foi cancelado.</exception>
    public async Task<TValue> RunAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(operation);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Flight flight;
            bool owner = false;
            lock (_sync)
            {
                // Execução abandonada (todos desistiram) ainda não removida: começa outra no lugar dela
                if (!_flights.TryGetValue(key, out flight!) || flight.Cancellation.IsCancellationRequested)
                {
                    flight = new Flight();
                    _flights[key] = flight;
                    owner = true;
                }

                flight.Waiters++;
            }

            if (owner)
                _ = ExecuteAsync(key, flight, operation);

            try
            {
                return await flight.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && flight.Cancellation.IsCancellationRequested)
            {
                // A execução compartilhada foi cancelada porque os demais desistiram entre a entrada e a espera: tenta de novo
            }
            finally
            {
                Leave(flight);
            }
        }
    }

    private async Task ExecuteAsync(TKey key, Flight flight, Func<TKey, CancellationToken, Task<TValue>> operation)
    {
        try
        {
            // A operação nunca roda dentro da trava nem no contexto síncrono de quem chamou
            await Task.Yield();
            flight.Completion.TrySetResult(await operation(key, flight.Cancellation.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (flight.Cancellation.IsCancellationRequested)
        {
            flight.Completion.TrySetCanceled(flight.Cancellation.Token);
        }
        catch (Exception exception)
        {
            flight.Completion.TrySetException(exception);
        }
        finally
        {
            lock (_sync)
            {
                // Remove só a própria execução (outra pode ter ocupado a chave depois de um abandono)
                if (_flights.TryGetValue(key, out var current) && ReferenceEquals(current, flight))
                    _flights.Remove(key);
            }

            flight.Cancellation.Dispose();
        }
    }

    private void Leave(Flight flight)
    {
        lock (_sync)
        {
            flight.Waiters--;
            if (flight.Waiters == 0 && !flight.Completion.Task.IsCompleted)
            {
                try
                {
                    flight.Cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // A execução terminou e liberou o token entre a conferência e o cancelamento: nada a cancelar
                }
            }
        }
    }

    private sealed class Flight
    {
        public TaskCompletionSource<TValue> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource Cancellation { get; } = new();

        // Protegido por _sync
        public int Waiters { get; set; }
    }
}
