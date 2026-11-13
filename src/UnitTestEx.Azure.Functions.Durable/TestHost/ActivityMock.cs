using System.Collections.Concurrent;
using Microsoft.DurableTask;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>Creates activity mocks with an explicit matching strategy.</summary>
public static class ActivityMock
{
    /// <summary>Creates a mock that assigns responses by matching each activity input.</summary>
    /// <typeparam name="TInput">The activity input type.</typeparam>
    /// <typeparam name="TOutput">The activity output type.</typeparam>
    /// <param name="responses">The input-matched responses.</param>
    /// <returns>An input-matched mock.</returns>
    public static ActivityMock<TInput, TOutput> ByMatch<TInput, TOutput>(params ActivityResponse<TInput, TOutput>[] responses) => new(true, responses);

    /// <summary>Creates a mock that assigns responses by handler invocation order.</summary>
    /// <typeparam name="TInput">The activity input type.</typeparam>
    /// <typeparam name="TOutput">The activity output type.</typeparam>
    /// <param name="responses">The ordered responses.</param>
    /// <returns>An invocation-ordered mock.</returns>
    public static ActivityMock<TInput, TOutput> InOrder<TInput, TOutput>(params ActivityResponse<TInput, TOutput>[] responses) => new(false, responses);
}

/// <summary>Routes activity invocations to one uniquely matching response plan.</summary>
/// <remarks>
/// Each invocation, including a retry, consumes one response. Ordered mocks use handler arrival order,
/// not guaranteed orchestration scheduling order. Dispose the mock to cancel pending responses.
/// Completion signals describe handler completion, not engine processing of its result.
/// </remarks>
/// <typeparam name="TInput">The activity input type.</typeparam>
/// <typeparam name="TOutput">The activity output type.</typeparam>
public sealed class ActivityMock<TInput, TOutput> : IDisposable
{
#if NET8_0
    private readonly object _lock = new();
#else
    private readonly Lock _lock = new();
#endif
    private readonly ActivityResponse<TInput, TOutput>[] _responses;
    private readonly ConcurrentQueue<TInput> _completionOrder = new();
    private readonly TaskCompletionSource _allInvocations = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _matchByInput;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    /// <summary>Initializes an activity mock with an explicit matching mode.</summary>
    /// <param name="matchByInput">Whether responses are matched by input rather than invocation order.</param>
    /// <param name="responses">The responses available to incoming invocations.</param>
    internal ActivityMock(bool matchByInput, params ActivityResponse<TInput, TOutput>[] responses)
    {
        ArgumentNullException.ThrowIfNull(responses);
        if (responses.Length == 0 || responses.Any(response => response is null))
            throw new ArgumentException("At least one non-null response is required.", nameof(responses));

        if (responses.Any(response => response.HasInputMatcher != matchByInput))
            throw new ArgumentException("Input-matched and invocation-ordered responses cannot be mixed.", nameof(responses));

        if (responses.Distinct().Count() != responses.Length)
            throw new ArgumentException("A response plan cannot appear more than once.", nameof(responses));

        _responses = [.. responses];
        lock (ActivityResponse<TInput, TOutput>.ConfigurationSync)
        {
            if (_responses.Any(response => response.IsAttached))
                throw new InvalidOperationException("A response plan cannot be reused by another handler.");

            foreach (var response in _responses)
                response.Attach();
        }

        _matchByInput = matchByInput;
    }

    /// <summary>Gets a response plan by its zero-based declaration position.</summary>
    /// <remarks>The index identifies the configured plan, not the order in which matching handlers arrive.</remarks>
    /// <param name="index">The zero-based response plan index.</param>
    /// <returns>The configured response plan.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the configured response plans.</exception>
    public ActivityResponse<TInput, TOutput> this[int index]
    {
        get
        {
            if (index < 0 || index >= _responses.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The response index is outside the configured response plans.");

            return _responses[index];
        }
    }

    /// <summary>Waits until all configured response plans have matched an invocation, defaulting to five seconds.</summary>
    /// <param name="timeout">The maximum time to wait; defaults to five seconds and otherwise must be positive.</param>
    /// <returns>A task that completes when every response plan has been matched.</returns>
    public Task WaitForAllInvocationsAsync(TimeSpan? timeout = null)
    {
        TimeSpan waitTimeout = timeout ?? TimeSpan.FromSeconds(5);
        if (waitTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), waitTimeout, "The invocation timeout must be positive.");

        return _allInvocations.Task.WaitAsync(waitTimeout);
    }

    /// <summary>Verifies that every response plan has been assigned to an activity invocation.</summary>
    /// <exception cref="InvalidOperationException">One or more response plans were not invoked.</exception>
    public void Verify()
    {
        lock (_lock)
        {
            int[] unused = [.. _responses.Select((response, index) => (response, index)).Where(entry => !entry.response.IsConsumed).Select(entry => entry.index + 1)];
            if (unused.Length != 0)
                throw new InvalidOperationException($"Scripted activity responses not invoked (1-based positions): {string.Join(", ", unused)}.");
        }
    }

    /// <summary>Gets the activity inputs in the order their handlers returned.</summary>
    public IReadOnlyCollection<TInput> CompletionOrder => [.. _completionOrder];

    /// <summary>Runs as the registered activity handler.</summary>
    /// <param name="context">The activity context.</param>
    /// <param name="input">The activity input.</param>
    /// <returns>The output configured by the matching response plan.</returns>
    public async Task<TOutput> InvokeAsync(TaskActivityContext context, TInput input)
    {
        ActivityResponse<TInput, TOutput> response = ReserveResponse(input);

        try
        {
            TOutput output = await response.ExecuteAsync(input).ConfigureAwait(false);
            _completionOrder.Enqueue(input);
            response.MarkCompleted();
            return output;
        }
        catch (Exception error)
        {
            response.MarkFailed(error);
            throw;
        }
    }

    /// <summary>Reserves the only unused response plan matching the activity input.</summary>
    /// <param name="input">The activity input.</param>
    /// <returns>The reserved response plan.</returns>
    /// <exception cref="InvalidOperationException">No response or multiple responses match the input.</exception>
    private ActivityResponse<TInput, TOutput> ReserveResponse(TInput input)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ActivityResponse<TInput, TOutput>[] matching = _matchByInput
                ? [.. _responses.Where(response => !response.IsConsumed && response.Matches(input))]
                : [.. _responses.Where(response => !response.IsConsumed).Take(1)];

            if (matching.Length != 1)
            {
                var error = new InvalidOperationException(matching.Length == 0
                    ? _matchByInput
                        ? $"No scripted activity response matched input '{input}'."
                        : "No unused in-order scripted activity response remains."
                    : $"Multiple scripted activity responses matched input '{input}'.");

                _allInvocations.TrySetException(error);
                throw error;
            }

            ActivityResponse<TInput, TOutput> response = matching[0];
            response.Consume(_shutdown.Token);
            if (_responses.All(candidate => candidate.IsConsumed))
                _allInvocations.TrySetResult();

            return response;
        }
    }

    /// <summary>Cancels pending responses without invoking their output factories.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _shutdown.Cancel();
            _allInvocations.TrySetCanceled();
            foreach (var response in _responses)
                response.Cancel();

            _shutdown.Dispose();
        }
    }
}
