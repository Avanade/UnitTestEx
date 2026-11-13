using Moq;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Tracks registered activity-handler invocations for an individual test run.
/// </summary>
internal sealed class DurableTaskActivityVerifier
{
    private readonly List<ActivityInvocation> _activities = [];

    /// <summary>
    /// Registers an activity invocation expectation.
    /// </summary>
    /// <param name="name">The activity name.</param>
    /// <param name="expectedTimes">The expected handler invocation count.</param>
    /// <returns>A callback that records each handler invocation.</returns>
    internal Action Register(string name, Times expectedTimes)
    {
        var activity = new ActivityInvocation(name, expectedTimes);
        _activities.Add(activity);
        return activity.Record;
    }

    /// <summary>
    /// Verifies all registered activity invocation expectations.
    /// </summary>
    internal void Verify()
    {
        foreach (ActivityInvocation activity in _activities)
            activity.Verify();
    }

    /// <summary>
    /// Tracks and verifies invocation counts for one activity registration.
    /// </summary>
    /// <param name="name">The activity name.</param>
    /// <param name="expectedTimes">The expected handler invocation count.</param>
    private sealed class ActivityInvocation(string name, Times expectedTimes)
    {
        private int _count;

        /// <summary>
        /// Records one handler invocation.
        /// </summary>
        public void Record() => Interlocked.Increment(ref _count);

        /// <summary>
        /// Verifies that the observed count satisfies the configured expectation.
        /// </summary>
        /// <exception cref="InvalidOperationException">The observed count does not satisfy the expectation.</exception>
        public void Verify()
        {
            int count = Volatile.Read(ref _count);
            expectedTimes.Deconstruct(out int minimum, out int maximum);
            if (count < minimum || count > maximum)
                throw new InvalidOperationException($"Activity '{name}' was invoked {count} time(s); expected {expectedTimes}.");
        }
    }
}
