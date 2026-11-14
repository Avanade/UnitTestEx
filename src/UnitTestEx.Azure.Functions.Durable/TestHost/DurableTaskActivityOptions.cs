using Moq;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Configures optional invocation verification for a registered activity handler.
/// </summary>
public sealed class DurableTaskActivityOptions
{
    private Times _expectedTimes = Moq.Times.AtLeastOnce();

    /// <summary>
    /// Sets the number of handler invocations expected when <see cref="DurableTaskTestRun{TOutput}.Verify"/> is called.
    /// </summary>
    /// <param name="times">The expected number of handler invocations.</param>
    /// <returns>This options instance for fluent configuration.</returns>
    public DurableTaskActivityOptions Times(Times times)
    {
        _expectedTimes = times;
        return this;
    }

    /// <summary>
    /// Gets the expected number of handler invocations.
    /// </summary>
    internal Times ExpectedTimes => _expectedTimes;
}
