// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides the base <b>Expectations</b> implementation.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/></param>
    /// <param name="tester">The initiating tester.</param>
    public abstract class ExpectationsBase<TTester>(TesterBase owner, TTester tester) : ExpectationsBase(owner)
    {
        /// <summary>
        /// Gets the initiating tester.
        /// </summary>
        public TTester Tester { get; } = tester ?? throw new ArgumentNullException(nameof(tester));

        /// <inheritdoc/>
        protected internal override async Task OnExtensionsAssert(AssertArgs args)
        {
            foreach (var ext in TestSetUp.Extensions)
                await ext.ExpectationAssertAsync<TTester>(this, args).ConfigureAwait(false);
        }
    }
}