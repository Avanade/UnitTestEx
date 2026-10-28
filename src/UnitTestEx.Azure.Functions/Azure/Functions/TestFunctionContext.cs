// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Worker;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides a unit-testing <see cref="FunctionContext"/> (isolated worker) implementation.
    /// </summary>
    /// <param name="instanceServices">The <see cref="FunctionContext.InstanceServices"/>.</param>
    /// <param name="functionId">The optional <see cref="FunctionContext.FunctionId"/>.</param>
    /// <remarks>The <see cref="TraceContext"/>, <see cref="BindingContext"/>, <see cref="RetryContext"/> and <see cref="FunctionDefinition"/> are not available (<c>null</c>) unless explicitly set.</remarks>
    public class TestFunctionContext(IServiceProvider instanceServices, string? functionId = null) : FunctionContext
    {
        /// <inheritdoc/>
        public override string InvocationId { get; } = Guid.NewGuid().ToString();

        /// <inheritdoc/>
        public override string FunctionId { get; } = functionId ?? "unittestex";

        /// <summary>
        /// Gets or sets the <see cref="Worker.TraceContext"/>.
        /// </summary>
        public TraceContext? TestTraceContext { get; set; }

        /// <inheritdoc/>
        public override TraceContext TraceContext => TestTraceContext!;

        /// <summary>
        /// Gets or sets the <see cref="Worker.BindingContext"/>.
        /// </summary>
        public BindingContext? TestBindingContext { get; set; }

        /// <inheritdoc/>
        public override BindingContext BindingContext => TestBindingContext!;

        /// <summary>
        /// Gets or sets the <see cref="Worker.RetryContext"/>.
        /// </summary>
        public RetryContext? TestRetryContext { get; set; }

        /// <inheritdoc/>
        public override RetryContext RetryContext => TestRetryContext!;

        /// <inheritdoc/>
        public override IServiceProvider InstanceServices { get; set; } = instanceServices ?? throw new ArgumentNullException(nameof(instanceServices));

        /// <summary>
        /// Gets or sets the <see cref="Worker.FunctionDefinition"/>.
        /// </summary>
        public FunctionDefinition? TestFunctionDefinition { get; set; }

        /// <inheritdoc/>
        public override FunctionDefinition FunctionDefinition => TestFunctionDefinition!;

        /// <inheritdoc/>
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

        /// <inheritdoc/>
        public override IInvocationFeatures Features { get; } = new TestInvocationFeatures();

        /// <summary>
        /// Gets or sets the <see cref="FunctionContext.CancellationToken"/>.
        /// </summary>
        public CancellationToken TestCancellationToken { get; set; }

        /// <inheritdoc/>
        public override CancellationToken CancellationToken => TestCancellationToken;

        /// <summary>
        /// Provides a simple <see cref="IInvocationFeatures"/> implementation.
        /// </summary>
        private sealed class TestInvocationFeatures : IInvocationFeatures
        {
            private readonly Dictionary<Type, object> _features = [];

            public T? Get<T>() => _features.TryGetValue(typeof(T), out var f) ? (T)f : default;

            public void Set<T>(T instance)
            {
                if (instance is null)
                    _features.Remove(typeof(T));
                else
                    _features[typeof(T)] = instance;
            }

            public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
