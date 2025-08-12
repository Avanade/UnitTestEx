// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides log expectations.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="tester">The initiating tester.</param>
    public class LoggerExpectations<TTester>(TesterBase owner, TTester tester) : ExpectationsBase<TTester>(owner, tester)
    {
        private readonly List<string> _expectTexts = [];

        /// <inheritdoc/>
        public override string Title => "Logger expectations";

        /// <summary>
        /// Expects that the <see cref="ILogger"/> will have logged a message that contains the specified <paramref name="texts"/>.
        /// </summary>
        /// <param name="texts">The text(s) that should appear in at least one log message line.</param>
        public void SetExpectLogContains(params string[] texts) => _expectTexts.AddRange(texts.Where(t => !string.IsNullOrEmpty(t)));

        /// <inheritdoc/>
        protected override Task OnAssertAsync(AssertArgs args)
        {
            if ((args.Logs is null || !args.Logs.Any()) && _expectTexts.Count > 0)
                args.Tester.Implementor.AssertFail($"Expected one or more log texts that were not found.");

            foreach (var et in _expectTexts)
            {
                if (args.Logs!.Any(l => !string.IsNullOrEmpty(l) && l.Contains(et, StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                args.Tester.Implementor.AssertFail($"Expected a log text to contain '{et}' that was not found.");
            }

            return Task.CompletedTask;
        }
    }
}