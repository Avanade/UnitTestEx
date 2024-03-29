﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Hosting;
using UnitTestEx.Json;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides Azure Function <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> or <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    public class HttpTriggerTester<TFunction> : HostTesterBase<TFunction>, IExpectations<HttpTriggerTester<TFunction>> where TFunction : class
    {
        /// <summary>
        /// Initializes a new <see cref="HttpTriggerTester{TFunction}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        public HttpTriggerTester(TesterBase owner, IServiceScope serviceScope) : base(owner, serviceScope) => ExpectationsArranger = new ExpectationsArranger<HttpTriggerTester<TFunction>>(owner, this);

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<HttpTriggerTester<TFunction>> ExpectationsArranger { get; }

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> or <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) function using an <see cref="HttpRequestMessage"/> within the <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="ActionResultAssertor"/>.</returns>
        public ActionResultAssertor Run(Expression<Func<TFunction, Task<IActionResult>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> or <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) function using an <see cref="HttpRequestMessage"/> within the <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="ActionResultAssertor"/>.</returns>
        public async Task<ActionResultAssertor> RunAsync(Expression<Func<TFunction, Task<IActionResult>>> expression)
        {
            (IActionResult result, Exception? ex, double ms) = await RunAsync(expression, [typeof(Microsoft.Azure.WebJobs.HttpTriggerAttribute), typeof(Microsoft.Azure.Functions.Worker.HttpTriggerAttribute)], (p, a, v) =>
            {
                var requestVal = v;
                var httpRequest = v as HttpRequest;
                LogRequest(httpRequest, requestVal);

                var httpTriggerAttribute = a as Microsoft.Azure.WebJobs.HttpTriggerAttribute;
                if (httpRequest != null && httpTriggerAttribute is not null && !httpTriggerAttribute.Methods.Contains(httpRequest.Method, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"The function {nameof(Microsoft.Azure.WebJobs.HttpTriggerAttribute)} supports {nameof(Microsoft.Azure.WebJobs.HttpTriggerAttribute.Methods)} of {string.Join(" or ", httpTriggerAttribute.Methods.Select(x => $"'{x.ToUpperInvariant()}'"))}; however, invoked using '{httpRequest.Method.ToUpperInvariant()}' which is not valid.");

                var httpTriggerAttribute2 = a as Microsoft.Azure.Functions.Worker.HttpTriggerAttribute;
                if (httpRequest != null && httpTriggerAttribute2 is not null && httpTriggerAttribute2.Methods is not null && !httpTriggerAttribute2.Methods!.Contains(httpRequest.Method, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"The function {nameof(Microsoft.Azure.Functions.Worker.HttpTriggerAttribute)} supports {nameof(Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods)} of {string.Join(" or ", httpTriggerAttribute2.Methods.Select(x => $"'{x.ToUpperInvariant()}'"))}; however, invoked using '{httpRequest.Method.ToUpperInvariant()}' which is not valid.");
            }).ConfigureAwait(false);

            await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            var logs = Owner.SharedState.GetLoggerMessages();
            LogResponse(result, ex, ms, logs);

            await ExpectationsArranger.AssertAsync(logs, ex).ConfigureAwait(false);

            return new ActionResultAssertor(Owner, result, ex);
        }

        /// <summary>
        /// Log the request to the output.
        /// </summary>
        private void LogRequest(HttpRequest? req, object? reqVal)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("FUNCTION HTTP-TRIGGER TESTER...");

            if (req != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine("REQUEST >");
                Implementor.WriteLine($"Request: {req.Method} {req.GetEncodedUrl()}");
                Implementor.WriteLine($"Headers: {(req.Headers == null || !req.Headers.Any() ? "none" : "")}");
                if (req.Headers != null && req.Headers.Any())
                {
                    foreach (var hdr in req.Headers)
                    {
                        var sb = new StringBuilder();
                        foreach (var v in hdr.Value)
                        {
                            if (sb.Length > 0)
                                sb.Append(", ");

                            sb.Append(v);
                        }

                        Implementor.WriteLine($"  {hdr.Key}: {sb}");
                    }

                    Implementor.WriteLine("");
                }

                object? jo = null;
                if (req.Body != null)
                {
                    if (req.Body.CanRead)
                    {
                        req.Body.Position = 0;
                        using var sr = new StreamReader(req.Body, leaveOpen: true);
                        var body = sr.ReadToEnd();

                        // Parse out the content.
                        if (body.Length > 0)
                        {
                            try
                            {
                                jo = JsonSerializer.Deserialize(body);
                            }
                            catch (Exception) { /* This is being swallowed by design. */ }
                        }

                        // Reset the request body position back to start.
                        req.Body.Position = 0;

                        // Continue logging.
                        Implementor.WriteLine($"Content: [{req.ContentType ?? "None"}]");
                        if (jo != null || !string.IsNullOrEmpty(body))
                            Implementor.WriteLine(jo == null ? body : JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
                    }
                    else
                        Implementor.WriteLine($"Content: [{req.ContentType ?? "None"}] => Response.Body is not in a read state.");
                }
            }

            if (req == null && reqVal != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine("REQUEST >");
                Implementor.WriteLine($"Type: {reqVal.GetType()}");
                if (reqVal is string str)
                    Implementor.WriteLine($"Content: {str}");
                else if (reqVal is IFormattable ifm)
                    Implementor.WriteLine($"Content: {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                else
                    Implementor.WriteLine(JsonSerializer.Serialize(reqVal, JsonWriteFormat.Indented));
            }
        }

        /// <summary>
        /// Log the response to the output.
        /// </summary>
        private void LogResponse(IActionResult res, Exception? ex, double ms, IEnumerable<string?>? logs)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
            if (logs is not null && logs.Any())
            {
                foreach (var msg in logs)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");

            Implementor.WriteLine("");
            Implementor.WriteLine($"RESPONSE >");
            Implementor.WriteLine($"Elapsed (ms): {ms}");
            if (ex != null)
            {
                Implementor.WriteLine($"Exception: {ex.Message} [{ex.GetType().Name}]");
                Implementor.WriteLine(ex.ToString());
            }
            else
            {
                Implementor.WriteLine($"IActionResult: {res.GetType().Name}");
                if (res is IStatusCodeActionResult scar && scar.StatusCode != null)
                    Implementor.WriteLine($"HttpStatusCode: {(HttpStatusCode)scar.StatusCode} ({scar.StatusCode})");

                if (res is ObjectResult or)
                {
                    var ct = or.ContentTypes == null || or.ContentTypes.Count == 0 ? "None" : string.Join(",", or.ContentTypes);

                    if (or.Value is string str)
                        Implementor.WriteLine($"Content: [{ct}] {str}");
                    else if (or.Value is IFormattable ifm)
                        Implementor.WriteLine($"Content: [{ct}] {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                    else
                    {
                        Implementor.WriteLine($"Content: [{ct}] {(or.Value == null ? "<none>" : $"Type: {or.Value.GetType()}")}");
                        Implementor.WriteLine(JsonSerializer.Serialize(or.Value, JsonWriteFormat.Indented));
                    }
                }
                else if (res is JsonResult jr)
                {
                    Implementor.WriteLine($"Content: [{jr.ContentType ?? "None"}]");
                    if (jr.Value != null)
                        Implementor.WriteLine(JsonSerializer.Serialize(jr.Value, JsonWriteFormat.Indented));
                }
                else if (res is ContentResult cr)
                {
                    Implementor.WriteLine($"Content: [{cr.ContentType ?? "None"}]");
                    if (cr.Content == null)
                        Implementor.WriteLine("<null>");
                    else
                    {
                        try
                        {
                            var jo = JsonSerializer.Deserialize(cr.Content);
                            Implementor.WriteLine(JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
                        }
                        catch
                        {
                            Implementor.WriteLine(cr.Content);
                        }
                    }
                }
            }

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }
    }
}