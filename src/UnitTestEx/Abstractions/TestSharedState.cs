// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a means to share state between the <see cref="TesterBase"/> and the corresponding execution.
    /// </summary>
    public sealed class TestSharedState
    {
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<string, List<(DateTime, string?)>> _logOutput = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSharedState"/> class.
        /// </summary>
        internal TestSharedState() { }

        /// <summary>
        /// Gets the <see cref="HttpContextAccessor"/>.
        /// </summary>
        public IHttpContextAccessor? HttpContextAccessor { get; set; }

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

                // Parse in the message date where possible to ensure correct sequencing; assumes date/time is first 25 characters.
                DateTime now = DateTime.Now;
                if (message is not null && message.Length >= 25 && DateTime.TryParse(message[0..25], out now)) { }

                // Append asterisks to the message to indicate that it is not attributed to a specific request.
                logs.Add((now, $"{message}{(string.IsNullOrEmpty(id) ? "**" : "")}"));
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

            string sid = HttpContextAccessor.HttpContext.Request.Headers.TryGetValue(AspNetCore.HttpTesterBase.RequestIdName, out var vals) ? vals.First() ?? string.Empty : string.Empty;
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
                var logs = new List<(DateTime, string?)>();
                if (_logOutput.TryRemove(string.Empty, out var l1) && l1 != null)
                    logs.AddRange(l1);

                if (!string.IsNullOrEmpty(requestId) && _logOutput.TryRemove(requestId, out var l2) && l2 != null)
                    logs.AddRange(l2);

                return logs.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
            }
        }

        /// <summary>
        /// Gets the state extension data that can be used for additional state information (where applicable).
        /// </summary>
        public ConcurrentDictionary<string, object?> StateData { get; } = new ConcurrentDictionary<string, object?>();

        /// <summary>
        /// Resets the <see cref="TestSharedState"/>.
        /// </summary>
        /// <remarks>Clears existing <see cref="GetLoggerMessages">logger messages</see> and <see cref="StateData"/>.</remarks>
        public void Reset()
        {
            _logOutput.Clear();
            StateData.Clear();
        }
    }
}