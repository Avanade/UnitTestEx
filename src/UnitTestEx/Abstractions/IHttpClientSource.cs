// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System.Collections.Generic;
using System.Net.Http;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Enables the creation of an <see cref="HttpClient"/> that targets a given test host/resource, independent of the underlying hosting model.
    /// </summary>
    /// <remarks>This is the seam that allows the <see cref="AspNetCore.HttpTesterBase"/>-based testers (see <see cref="AspNetCore.HttpTester"/>, <see cref="AspNetCore.HttpTester{TValue}"/> and
    /// <see cref="AspNetCore.ControllerTester{TController}"/>) to be reused across both a single in-process host (e.g. <see cref="AspNetCore.ApiTesterBase{TEntryPoint, TSelf}"/>, backed by a
    /// <c>TestServer</c>) and a multi-host distributed application (e.g. a companion Aspire tester, backed by <c>DistributedApplication.CreateHttpClient</c>).</remarks>
    public interface IHttpClientSource
    {
        /// <summary>
        /// Creates a new <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="name">The optional name used to select the target resource/endpoint; ignored by single-host implementations that only ever have the one target.</param>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        HttpClient CreateHttpClient(string? name = null);

        /// <summary>
        /// Gets the log messages captured for the specified request, where supported by the underlying host.
        /// </summary>
        /// <param name="requestId">The unique request identifier (see <see cref="AspNetCore.HttpTesterBase.RequestId"/>).</param>
        /// <returns>The log messages; where <c>null</c> the caller falls back to its default, in-process logger-based correlation (see <see cref="TestSharedState.GetLoggerMessages(string?)"/>), which
        /// requires no changes to any existing, single in-process host implementation of this interface.</returns>
        IEnumerable<string?>? GetRequestLogMessages(string requestId) => null;
    }
}
