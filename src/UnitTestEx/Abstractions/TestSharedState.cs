// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnitTestEx.Expectations;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a means to share state between the <see cref="TesterBase"/> and the corresponding execution.
    /// </summary>
    public sealed class TestSharedState
    {
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<string, List<string?>> _logOutput = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSharedState"/> class.
        /// </summary>
        internal TestSharedState() { }

        /// <summary>
        /// Gets the <see cref="HttpContextAccessor"/>.
        /// </summary>
        public IHttpContextAccessor? HttpContextAccessor { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ExpectedEventPublisher"/>.
        /// </summary>
        public ExpectedEventPublisher? ExpectedEventPublisher { get; set; }

        /// <summary>
        /// Adds the <see cref="ILogger"/> log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        public void AddLoggerMessage(string? message)
        {
            var id = GetRequestId();

            lock (_lock)
            {
                var logs = _logOutput.GetOrAdd(id, _ => new());
                logs.Add(message);
            }
        }

        /// <summary>
        /// Gets the correlation identifier.
        /// </summary>
        private string GetRequestId()
        {
            if (HttpContextAccessor == null || HttpContextAccessor.HttpContext == null)
                return string.Empty;

            if (HttpContextAccessor.HttpContext.Items.TryGetValue(AspNetCore.HttpTesterBase.RequestIdName, out var id))
                return (string)id!;

            string sid = HttpContextAccessor.HttpContext.Request.Headers.TryGetValue(AspNetCore.HttpTesterBase.RequestIdName, out var vals) ? vals.First() : string.Empty;
            HttpContextAccessor.HttpContext.Items.TryAdd(AspNetCore.HttpTesterBase.RequestIdName, sid);
            return sid;
        }

        /// <summary>
        /// Gets the <see cref="ILogger"/> messages (including those not attributed to any specific <paramref name="requestId"/>).
        /// </summary>
        /// <param name="requestId">The unit testing request identifier.</param>
        /// <remarks>Also clears the messages.</remarks>
        public IEnumerable<string?> GetLoggerMessages(string? requestId = null)
        {
            lock (_lock)
            {
                var logs = new List<string?>();
                if (_logOutput.TryRemove(string.Empty, out var l1) && l1 != null)
                    logs.AddRange(l1);

                if (!string.IsNullOrEmpty(requestId) && _logOutput.TryRemove(requestId, out var l2) && l2 != null)
                    logs.AddRange(l2);

                return logs;
            }
        }

        /// <summary>
        /// Resets the <see cref="TestSharedState"/>.
        /// </summary>
        public void ResetEventStorage()
        {
            ExpectedEventPublisher = null;
            _logOutput.Clear();
        }
    }
}