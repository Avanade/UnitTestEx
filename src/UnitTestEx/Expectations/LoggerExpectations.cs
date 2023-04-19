// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides log expectations.
    /// </summary>
    public class LoggerExpectations
    {
        private readonly List<string> _expectTexts = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggerExpectations"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public LoggerExpectations(TestFrameworkImplementor implementor) => Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Expects that the <see cref="ILogger"/> will have logged a message that contains the specified <paramref name="texts"/>.
        /// </summary>
        /// <param name="texts">The text(s) that should appear in at least one log message.</param>
        public void SetExpectLogContains(params string[] texts) => texts.ForEach(t => _expectTexts.Add(string.IsNullOrEmpty(t) ? throw new ArgumentNullException(nameof(texts)) : t));

        /// <summary>
        /// Asserts the expectations. 
        /// </summary>
        /// <param name="logs">The logs.</param>
        public void Assert(IEnumerable<string?>? logs)
        {
            if ((logs is null || !logs.Any()) && _expectTexts.Count > 0)
                Implementor.AssertFail($"Expected one or more log texts that were not found.");

            foreach (var et in _expectTexts)
            {
                if (logs!.Any(l => !string.IsNullOrEmpty(l) && l.Contains(et, StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                Implementor.AssertFail($"Expected a log text to contain '{et}' that was not found.");
            }
        }
    }
}