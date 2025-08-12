// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides the base <b>Expectations</b> implementation.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/></param>
    public abstract class ExpectationsBase(TesterBase owner)
    {
        private List<Func<AssertArgs, Task<bool>>>? _extendedActions;

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Owner { get; } = owner ?? throw new ArgumentNullException(nameof(owner));

        /// <summary>
        /// Gets or sets the title used in the assertion output.
        /// </summary>
        public abstract string Title { get; }

        /// <summary>
        /// Gets or sets the order in which the expectation is asserted.
        /// </summary>
        public virtual int Order { get; } = 100;

        /// <summary>
        /// Indicates whether to skip the base <see cref="OnAssertAsync(AssertArgs)"/> invocation executing only the <see cref="AddExtension">extensions</see>.
        /// </summary>
        public bool SkipOnAssert { get; set; }

        /// <summary>
        /// Adds an extension to the assertion to be executed after the base assertion.
        /// </summary>
        /// <param name="extension">The extension.</param>
        /// <remarks>To stop any further extensions being executed, or the <see cref="OnLastAssertAsync(AssertArgs)"/>, the extension should signal handled with a response of <c>true</c>.</remarks>
        public void AddExtension(Func<AssertArgs, Task<bool>> extension)
        {
            _extendedActions ??= [];
            _extendedActions.Add(extension ?? throw new ArgumentNullException(nameof(extension)));
        }

        /// <summary>
        /// Performs the base <see cref="OnAssertAsync(AssertArgs)">assertion</see> and then any <see cref="AddExtension">extensions</see>.
        /// </summary>
        /// <param name="args">The <see cref="AssertArgs"/>.</param>
        public async Task AssertAsync(AssertArgs args)
        {
            ArgumentNullException.ThrowIfNull(args, nameof(args));

            if (!SkipOnAssert)
                await OnAssertAsync(args).ConfigureAwait(false);

            if (_extendedActions is not null)
            {
                foreach (var ea in _extendedActions)
                {
                    if (!await ea(args).ConfigureAwait(false))
                        return;
                }
            }

            await OnExtensionsAssert(args).ConfigureAwait(false);
            await OnLastAssertAsync(args).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the assertion.
        /// </summary>
        /// <param name="args">The <see cref="AssertArgs"/>.</param>
        protected abstract Task OnAssertAsync(AssertArgs args);

        /// <summary>
        /// Performs any extension assertions.
        /// </summary>
        /// <param name="args">The <see cref="AssertArgs"/>.</param>
        protected internal abstract Task OnExtensionsAssert(AssertArgs args);

        /// <summary>
        /// Performs any final assertion after all <see cref="AddExtension">extensions</see> have executed.
        /// </summary>
        /// <param name="args">The <see cref="AssertArgs"/>.</param>
        protected virtual Task OnLastAssertAsync(AssertArgs args) => Task.CompletedTask;

        /// <summary>
        /// Resets the expectation back to its orginating assert state to allow for a re-execution.
        /// </summary>
        public virtual void Reset() { }
    }
}