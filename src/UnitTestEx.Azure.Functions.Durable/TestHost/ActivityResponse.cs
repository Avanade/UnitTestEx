namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>Creates scripted activity responses.</summary>
public static class ActivityResponse
{
    /// <summary>Creates a response selected by its input matcher.</summary>
    /// <typeparam name="TInput">The activity input type.</typeparam>
    /// <typeparam name="TOutput">The activity output type.</typeparam>
    /// <param name="matches">The input matcher.</param>
    /// <param name="createOutput">The output factory.</param>
    /// <returns>An input-matched response plan.</returns>
    public static ActivityResponse<TInput, TOutput> Match<TInput, TOutput>(Func<TInput, bool> matches, Func<TInput, TOutput> createOutput) => new(matches ?? throw new ArgumentNullException(nameof(matches)), createOutput);

    /// <summary>Creates a response selected by its position in the invocation sequence.</summary>
    /// <typeparam name="TInput">The activity input type.</typeparam>
    /// <typeparam name="TOutput">The activity output type.</typeparam>
    /// <param name="createOutput">The output factory.</param>
    /// <returns>An invocation-ordered response plan.</returns>
    public static ActivityResponse<TInput, TOutput> Next<TInput, TOutput>(Func<TInput, TOutput> createOutput) => new(null, createOutput);
}

/// <summary>Defines one input-matched or invocation-ordered activity response.</summary>
/// <remarks>
/// Responses return immediately unless configured with <see cref="After"/> or <see cref="Pending"/>.
/// Delays use real wall-clock time. A response belongs to one handler and is frozen when attached.
/// </remarks>
/// <typeparam name="TInput">The activity input type.</typeparam>
/// <typeparam name="TOutput">The activity output type.</typeparam>
public sealed class ActivityResponse<TInput, TOutput>
{
#if NET8_0
    /// <summary>Synchronizes response configuration and handler ownership.</summary>
    internal static readonly object ConfigurationSync = new();
#else
    /// <summary>Synchronizes response configuration and handler ownership.</summary>
    internal static readonly Lock ConfigurationSync = new();
#endif
    private readonly Func<TInput, bool>? _matches;
    private readonly Func<TInput, TOutput> _createOutput;
    private TimeSpan? _delay;
    private bool _waitForRelease;
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _completionConfigured;
    private CancellationToken _cancellationToken;

    /// <summary>Gets whether this plan belongs to a handler.</summary>
    internal bool IsAttached { get; private set; }

    /// <summary>Freezes configuration and assigns this plan to a handler.</summary>
    internal void Attach() => IsAttached = true;

    /// <summary>Initializes one response plan.</summary>
    /// <param name="matches">The optional input matcher.</param>
    /// <param name="createOutput">The output factory.</param>
    internal ActivityResponse(Func<TInput, bool>? matches, Func<TInput, TOutput> createOutput)
    {
        _matches = matches;
        _createOutput = createOutput ?? throw new ArgumentNullException(nameof(createOutput));
    }

    /// <summary>Gets whether this response plan matches by input.</summary>
    internal bool HasInputMatcher => _matches is not null;

    /// <summary>Gets whether this response plan has been assigned to an invocation.</summary>
    internal bool IsConsumed { get; private set; }

    /// <summary>Tests whether the response plan accepts an input.</summary>
    /// <param name="input">The activity input.</param>
    /// <returns>True when the response plan matches.</returns>
    internal bool Matches(TInput input) => _matches?.Invoke(input) ?? false;

    /// <summary>Marks this response plan as assigned to an invocation.</summary>
    /// <param name="cancellationToken">The handler lifetime cancellation token.</param>
    internal void Consume(CancellationToken cancellationToken)
    {
        IsConsumed = true;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Configures an automatic response after simulated work.</summary>
    /// <param name="delay">The simulated work duration.</param>
    /// <returns>This response plan.</returns>
    public ActivityResponse<TInput, TOutput> After(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "The response delay cannot be negative.");

        lock (ConfigurationSync)
        {
            if (IsAttached)
                throw new InvalidOperationException("A response cannot be modified after it is attached to a handler.");

            if (_completionConfigured)
                throw new InvalidOperationException("A response can have only one completion strategy.");

            _delay = delay;
            _completionConfigured = true;
        }
        return this;
    }

    /// <summary>Configures a response that remains pending until explicitly released.</summary>
    /// <returns>This response plan.</returns>
    public ActivityResponse<TInput, TOutput> Pending()
    {
        lock (ConfigurationSync)
        {
            if (IsAttached)
                throw new InvalidOperationException("A response cannot be modified after it is attached to a handler.");

            if (_completionConfigured)
                throw new InvalidOperationException("A response can have only one completion strategy.");

            _waitForRelease = true;
            _completionConfigured = true;
        }
        return this;
    }

    /// <summary>Waits for configured work or release, then creates the response output.</summary>
    /// <param name="input">The activity input.</param>
    /// <returns>The activity output.</returns>
    internal async Task<TOutput> ExecuteAsync(TInput input)
    {
        if (_waitForRelease)
            await _release.Task.WaitAsync(_cancellationToken).ConfigureAwait(false);
        else if (_delay is TimeSpan delay)
            await Task.Delay(delay, _cancellationToken).ConfigureAwait(false);

        _cancellationToken.ThrowIfCancellationRequested();
        return _createOutput(input);
    }

    /// <summary>Releases a pending response and waits for the activity handler to finish.</summary>
    /// <param name="timeout">The maximum time to wait for the handler to finish.</param>
    /// <returns>A task that completes when the activity handler returns.</returns>
    /// <exception cref="InvalidOperationException">This response was configured with a delay instead of a release gate.</exception>
    public async Task ReleaseAsync(TimeSpan timeout)
    {
        if (!_waitForRelease)
            throw new InvalidOperationException("Only a pending activity response can be released.");

        _release.TrySetResult();
        await _completed.Task.WaitAsync(timeout).ConfigureAwait(false);
    }

    /// <summary>Waits for the activity handler to finish this response.</summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>A task that completes when the activity handler returns.</returns>
    public Task WaitForCompletionAsync(TimeSpan timeout) => _completed.Task.WaitAsync(timeout);

    /// <summary>Signals that the activity handler has returned.</summary>
    internal void MarkCompleted() => _completed.TrySetResult();

    /// <summary>Propagates the handler failure to response waiters.</summary>
    /// <param name="error">The original handler error.</param>
    internal void MarkFailed(Exception error)
    {
        if (error is OperationCanceledException)
            _completed.TrySetCanceled();
        else
            _completed.TrySetException(error);
    }

    /// <summary>Cancels unfinished response waiters.</summary>
    internal void Cancel() => _completed.TrySetCanceled();
}
