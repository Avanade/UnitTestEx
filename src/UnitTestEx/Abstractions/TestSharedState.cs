// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a means to share state between the <see cref="TesterBase"/> and the corresponding execution.
    /// </summary>
    /// <remarks>The <see cref="GetHttpRequestId"/>-based functionality is primarily intended for use with <see cref="AspNetCore.HttpTesterBase"/> and related HTTP testing; however, it is available for any
    /// testing where sharing state between the tester and execution is required.
    /// <para>Be careful when using this class that data does not cross boundaries where it is scoped, or may be disposed, as this may result in unintended side-effect/consequences.</para></remarks>
    public sealed class TestSharedState
    {
#if NET9_0_OR_GREATER
        private readonly Lock _lock = new();
#else
        private readonly object _lock = new();
#endif
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
            var id = GetHttpRequestId();

            lock (_lock)
            {
                var logs = _logOutput.GetOrAdd(id, _ => []);

                // Parse in the message date where possible to ensure correct sequencing; assumes date/time is first 25 characters.
                DateTime now = DateTime.Now;
                if (message is not null && message.Length >= 25 && DateTime.TryParse(message[0..25], out now)) { }

                // Append asterisks to the message to indicate that it is not attributed to a specific request.
                logs.Add((now, $"{message}{(string.IsNullOrEmpty(id) ? "**" : "")}"));
            }
        }

        /// <summary>
        /// Gets the HTTP request correlation identifier.
        /// </summary>
        /// <remarks>This identifier is used to correlate log messages and other state information with a specific HTTP request.
        /// <para>This is only meaningful within the context of an executing host.</para></remarks>
        public string GetHttpRequestId()
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

                return [.. logs.OrderBy(x => x.Item1).Select(x => x.Item2)];
            }
        }

        /// <summary>
        /// Gets the state extension data that can be used for additional state information (where applicable).
        /// </summary>
        public ConcurrentDictionary<string, object?> StateData { get; } = new ConcurrentDictionary<string, object?>();

        /// <summary>
        /// Gets the state extension data for the specified <paramref name="requestId"/> that can be used for additional state information (where applicable).
        /// </summary>
        /// <param name="requestId">The unit testing request identifier.</param>
        /// <returns>The state extension data for the specified <paramref name="requestId"/>.</returns>
        /// <remarks>A <paramref name="requestId"/> that is <see cref="String.IsNullOrEmpty(string?)"/> will return the <see cref="StateData"/>; i.e. is assumed not to be request-based.</remarks>
        public ConcurrentDictionary<string, object?> RequestStateData(string? requestId)
            => string.IsNullOrEmpty(requestId)
                ? StateData
                : StateData.GetOrAdd(requestId, _ => new ConcurrentDictionary<string, object?>()) as ConcurrentDictionary<string, object?> ?? new ConcurrentDictionary<string, object?>();

        /// <summary>
        /// Removes the state data associated with the specified <paramref name="requestId"/>, if it exists.
        /// </summary>
        /// <param name="requestId">The unit testing request identifier.</param>
        public void RemoveRequestStateData(string? requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return;

            StateData.TryRemove(requestId, out _);
        }

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