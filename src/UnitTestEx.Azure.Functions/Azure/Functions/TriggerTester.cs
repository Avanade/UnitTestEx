using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Hosting;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides a generic isolated-worker trigger tester for triggers without a dedicated tester.
    /// </summary>
    /// <typeparam name="TFunction">The function type.</typeparam>
    public sealed class TriggerTester<TFunction> : HostTesterBase<TFunction, TriggerTester<TFunction>>, IExpectations<TriggerTester<TFunction>> where TFunction : class
    {
        /// <summary>
        /// Initializes a new trigger tester.
        /// </summary>
        public TriggerTester(TesterBase owner, IServiceScope serviceScope) : base(owner, serviceScope.ServiceProvider)
        {
            ExpectationsArranger = new ExpectationsArranger<TriggerTester<TFunction>>(owner, this);
        }

        /// <summary>
        /// Gets the expectations arranger.
        /// </summary>
        public ExpectationsArranger<TriggerTester<TFunction>> ExpectationsArranger { get; }

        /// <summary>
        /// Runs a synchronous trigger function that returns no value.
        /// </summary>
        public VoidAssertor Run(Expression<Action<TFunction>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs a synchronous trigger function that returns no value.
        /// </summary>
        public async Task<VoidAssertor> RunAsync(Expression<Action<TFunction>> expression)
        {
            ValidateTrigger(expression);
            var (_, ex, elapsed) = await RunSyncAsync(expression, null, null).ConfigureAwait(false);
            return await CompleteVoidAsync(ex, elapsed).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs an asynchronous trigger function that returns no value.
        /// </summary>
        public VoidAssertor Run(Expression<Func<TFunction, Task>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs an asynchronous trigger function that returns no value.
        /// </summary>
        public async Task<VoidAssertor> RunAsync(Expression<Func<TFunction, Task>> expression)
        {
            ValidateTrigger(expression);
            var (ex, elapsed) = await RunAsync(expression, null, null).ConfigureAwait(false);
            return await CompleteVoidAsync(ex, elapsed).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs a synchronous trigger function returning a value.
        /// </summary>
        public ValueAssertor<TValue> Run<TValue>(Expression<Func<TFunction, TValue>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs a synchronous trigger function returning a value.
        /// </summary>
        public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Expression<Func<TFunction, TValue>> expression)
        {
            ValidateTrigger(expression);
            var (result, ex, elapsed) = await RunSyncAsync(expression, null, null).ConfigureAwait(false);
            return await CompleteValueAsync(result, ex, elapsed).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs an asynchronous trigger function returning a value.
        /// </summary>
        public ValueAssertor<TValue> Run<TValue>(Expression<Func<TFunction, Task<TValue>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs an asynchronous trigger function returning a value.
        /// </summary>
        public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Expression<Func<TFunction, Task<TValue>>> expression)
        {
            ValidateTrigger(expression);
            var (result, ex, elapsed) = await RunAsync(expression, null, null).ConfigureAwait(false);
            return await CompleteValueAsync(result, ex, elapsed).ConfigureAwait(false);
        }

        private async Task<VoidAssertor> CompleteVoidAsync(Exception? exception, double elapsed)
        {
            await Task.Delay(UnitTestEx.TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            var logs = Owner.SharedState.GetLoggerMessages();
            LogResult(null, exception, elapsed, logs);
            await ExpectationsArranger.AssertAsync(logs, exception).ConfigureAwait(false);
            return new VoidAssertor(Owner, logs, exception);
        }

        private async Task<ValueAssertor<TValue>> CompleteValueAsync<TValue>(TValue result, Exception? exception, double elapsed)
        {
            await Task.Delay(UnitTestEx.TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            var logs = Owner.SharedState.GetLoggerMessages();
            LogResult(result, exception, elapsed, logs);
            await ExpectationsArranger.AssertValueAsync(logs, result, exception).ConfigureAwait(false);
            return new ValueAssertor<TValue>(Owner, result, logs, exception);
        }

        private void ValidateTrigger(LambdaExpression expression)
        {
            var method = MethodCallExpressionValidate(expression).Method;
            if (!method.GetParameters().SelectMany(p => p.GetCustomAttributesData()).Any(a => IsWorkerTriggerAttribute(a.AttributeType)))
                throw new InvalidOperationException($"Function method '{method.Name}' does not have an isolated-worker trigger attribute.");
        }

        private static bool IsWorkerTriggerAttribute(Type type)
            => type.Name.EndsWith("TriggerAttribute", StringComparison.Ordinal)
                && type.Namespace?.StartsWith("Microsoft.Azure.Functions.Worker", StringComparison.Ordinal) == true;

        private void LogResult(object? result, Exception? exception, double elapsed, IEnumerable<string?> logs)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("FUNCTION TRIGGER TESTER...");
            Implementor.WriteLine($"Function: {typeof(TFunction).Name}");
            Implementor.WriteLine($"Elapsed (ms): {elapsed}");
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
            if (logs.Any())
            {
                foreach (var message in logs)
                    Implementor.WriteLine(message);
            }
            else
                Implementor.WriteLine("None.");

            if (exception is not null)
            {
                Implementor.WriteLine($"Exception: {exception.Message} [{exception.GetType().Name}]");
                Implementor.WriteLine(exception.ToString());
            }
            else if (result is not null)
            {
                Implementor.WriteLine($"Result: {result.GetType().Name}");
                Implementor.WriteLine(Owner.JsonSerializer.Serialize(result));
            }
        }
    }
}
