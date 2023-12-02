// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Net.Http;
using System.Net.Mime;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Provdes the base <see cref="HttpResponseMessage"/> test assert helper capabilities.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="HttpResponseMessageAssertorBase{TSelf}"/> class.
    /// </remarks>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
    public abstract class HttpResponseMessageAssertorBase(TesterBase owner, HttpResponseMessage response)
    {
        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Owner { get; } = owner ?? throw new ArgumentNullException(nameof(owner));

        /// <summary>
        /// Gets the <see cref="HttpResponseMessage"/>.
        /// </summary>
        public HttpResponseMessage Response { get; } = response;

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor => Owner.Implementor;

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        public IJsonSerializer JsonSerializer => Owner.JsonSerializer;

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

            var value = JsonSerializer.Deserialize<T>(Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()!);

            foreach (var ext in TestSetUp.Extensions)
                ext.UpdateValueFromHttpResponseMessage(Owner, Response, ref value);

            return value;
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