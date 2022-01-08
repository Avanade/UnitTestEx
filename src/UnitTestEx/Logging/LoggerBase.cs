// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Globalization;

namespace UnitTestEx.Logging
{
    /// <summary>
    /// Represents the base logger functionality
    /// </summary>
    public abstract class LoggerBase : ILogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoggerBase"/> class.
        /// </summary>
        /// <param name="name">The name of the logger.</param>
        public LoggerBase(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

        /// <summary>
        /// Gets the name of the logger.
        /// </summary>
        public string Name { get; }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => NullScope.Default;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            var message = $"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffff", DateTimeFormatInfo.InvariantInfo)} {GetLogLevel(logLevel)}: {formatter(state, exception)} [{Name}]";
            if (exception != null)
                message += Environment.NewLine + exception;

            if (string.IsNullOrEmpty(message))
                return;

            WriteMessage(message);
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
            LogLevel.Critical => "cri",
            LogLevel.Error => "err ",
            LogLevel.Warning => "wrn",
            LogLevel.Information => "inf",
            LogLevel.Debug => "dbg",
            LogLevel.Trace => "trc",
            _ => "???"
        };
    }
}