// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using System;
using System.Net.Http;
using System.Net.Mime;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Provdes the base <see cref="HttpResponseMessage"/> test assert helper capabilities.
    /// </summary>
    public abstract class HttpResponseMessageAssertorBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageAssertorBase{TSelf}"/> class.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal HttpResponseMessageAssertorBase(HttpResponseMessage response, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer)
        {
            Response = response;
            Implementor = implementor;
            JsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Gets the <see cref="HttpResponseMessage"/>.
        /// </summary>
        public HttpResponseMessage Response { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        public IJsonSerializer JsonSerializer { get; }

        /// <summary>
        /// Gets the response content as the deserialized JSON value.
        /// </summary>
        /// <typeparam name="T">The content <see cref="Type"/>.</typeparam>
        /// <returns>The result value.</returns>
        /// <remarks>The content type must be <see cref="MediaTypeNames.Application.Json"/>.</remarks>
        public T? GetValue<T>()
        {
            Implementor.AssertAreEqual(MediaTypeNames.Application.Json, Response.Content?.Headers?.ContentType?.MediaType);
            if (Response.Content == null)
                return default!;

            return Expectations.HttpResponseExpectations.GetValueFromHttpResponseMessage<T>(Response, JsonSerializer);
        }

        /// <summary>
        /// Gets the response content as a <see cref="string"/>.
        /// </summary>
        /// <returns>The result content <see cref="string"/>.</returns>
        public string? GetContent()
        {
            if (Response.Content == null)
                return null;

            return Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
    }
}