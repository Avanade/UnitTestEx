// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using WireMock.Admin.Mappings;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Provides shared <see cref="ResponseModel"/> building logic reused by both <see cref="AspireHttpMockResponse"/> (single response) and
    /// <see cref="AspireHttpMockResponseSequenceItem"/> (one entry within a <see cref="AspireHttpMockResponseSequence"/>).
    /// </summary>
    internal static class ResponseModelHelper
    {
        /// <summary>
        /// Adds the specified response header.
        /// </summary>
        public static void Header(ResponseModel response, string name, string value)
        {
            response.Headers ??= new Dictionary<string, object>();
            response.Headers[name ?? throw new ArgumentNullException(nameof(name))] = value;
        }

        /// <summary>
        /// Adds the specified response headers.
        /// </summary>
        public static void Headers(ResponseModel response, IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach (var header in headers ?? throw new ArgumentNullException(nameof(headers)))
            {
                Header(response, header.Key, header.Value);
            }
        }

        /// <summary>
        /// Sets the status code with no body.
        /// </summary>
        public static void With(ResponseModel response, HttpStatusCode statusCode) => response.StatusCode = (int)statusCode;

        /// <summary>
        /// Sets the status code, body content and media type.
        /// </summary>
        public static void With(ResponseModel response, string content, HttpStatusCode statusCode, string mediaType)
        {
            response.StatusCode = (int)statusCode;
            response.Body = content ?? throw new ArgumentNullException(nameof(content));
            Header(response, "Content-Type", mediaType);
        }

        /// <summary>
        /// Sets the status code and JSON body content.
        /// </summary>
        public static void WithJson(ResponseModel response, string json, HttpStatusCode statusCode) => With(response, json, statusCode, MediaTypeNames.Application.Json);
    }
}
