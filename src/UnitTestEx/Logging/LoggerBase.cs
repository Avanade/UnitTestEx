// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace UnitTestEx.Logging
{
    /// <summary>
    /// Represents the base logger functionality
    /// </summary>
    /// <param name="name">The name of the logger.</param>
    /// <param name="scopeProvider">The <see cref="IExternalScopeProvider"/>.</param>
    public abstract class LoggerBase(string name, IExternalScopeProvider? scopeProvider = null) : ILogger
    {
        private readonly IExternalScopeProvider _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();

        /// <summary>
        /// Gets the name of the logger.
        /// </summary>
        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

        /// <inheritdoc />
#if NET6_0_OR_GREATER
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);
#else
        public IDisposable BeginScope<TState>(TState state) => _scopeProvider.Push(state);
#endif

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            ArgumentNullException.ThrowIfNull(formatter);

            var sb = new StringBuilder();
            sb.Append($"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffff", DateTimeFormatInfo.InvariantInfo)} {GetLogLevel(logLevel)}: {formatter(state, exception)} [{Name}]");

            _scopeProvider?.ForEachScope<object>((scope, _) => ScopeWriter(sb, scope), null!);

            if (exception != null)
            {
                sb.AppendLine();
                sb.Append(exception);
            }

            WriteMessage(ReformatMessage(sb.ToString()));
        }

        /// <summary>
        /// Write out the scope content.
        /// </summary>
        private static void ScopeWriter(StringBuilder sb, object? scope)
        {
            if (scope == null)
                return;

            if (scope is IEnumerable<KeyValuePair<string, object>> dict && dict.Any())
            {
                if (dict.Count() == 1 && dict.First().Key == "{OriginalFormat}")
                    return;

                bool first = true;
                sb.Append(" >");
                foreach (var kv in dict)
                {
                    if (kv.Key != "{OriginalFormat}")
                    {
                        if (first)
                            first = false;
                        else
                            sb.Append(',');

                        sb.Append($" {kv.Key ?? "<null>"}=\"{kv.Value ?? "<null>"}\"");
                    }
                }
            }
            else
                sb.Append($" > {scope}");
        }

        /// <summary>
        /// Reformats the message (pretty-printer).
        /// </summary>
        private static string ReformatMessage(string message)
        {
            var sb = new StringBuilder();
            var sr = new StringReader(message);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (sb.Length == 0)
                    sb.Append($"{line}");
                else
                {
                    sb.AppendLine();
                    sb.Append($"{new string(' ', 32)}{line}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the log message.
        /// </summary>
        /// <param name="message">The message.</param>
        protected abstract void WriteMessage(string message);

        /// <summary>
        /// Gets the shortened log level.
        /// </summary>
        public static string GetLogLevel(LogLevel level) => level switch
        {
            LogLevel.Critical => "crit",
            LogLevel.Error => "fail",
            LogLevel.Warning => "warn",
            LogLevel.Information => "info",
            LogLevel.Debug => "dbug",
            LogLevel.Trace => "trce",
            _ => "???"
        };
    }
}