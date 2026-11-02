#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Assertors;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test.Other;

[TestFixture]
public sealed class HttpRenderingTest
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void Conversion_ExecutesResultWithSelectedContext_WithoutDrivingResponseCallbacks(bool actionResult, bool supplyRequest)
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Post, "render?format=csv");
        request.HttpContext.Response.Headers["X-Middleware"] = "present";
        var result = new RenderingResult();
        var supplied = supplyRequest ? request : null;

        var response = actionResult
            ? new ActionResultAssertor(test, result, null, null).ToHttpResponseMessageAssertor(supplied)
            : new HttpResultAssertor(test, result, null, null).ToHttpResponseMessageAssertor(supplied);

        response.AssertOK().AssertContent(supplyRequest ? "POST;csv" : ";");
        Assert.That(result.Context, Is.Not.Null);
        Assert.That(result.Context!.RequestServices, Is.SameAs(supplyRequest ? request.HttpContext.RequestServices : test.Services));
        if (supplyRequest)
        {
            Assert.That(result.Context, Is.SameAs(request.HttpContext));
            response.AssertNamedHeader("X-Middleware", "present");
        }
        else
            Assert.That(result.Context, Is.Not.SameAs(request.HttpContext));

        Assert.That(result.StartingCalled, Is.False);
        Assert.That(result.CompletedCalled, Is.False);
        response.AssertNoNamedHeader("X-OnStarting");
    }

    private sealed class RenderingResult : IActionResult, IResult
    {
        public HttpContext? Context { get; private set; }
        public bool StartingCalled { get; private set; }
        public bool CompletedCalled { get; private set; }

        /// <summary>
        /// Executes the MVC result against its action context's HTTP context.
        /// </summary>
        public Task ExecuteResultAsync(ActionContext context) => ExecuteAsync(context.HttpContext);

        /// <summary>
        /// Writes request details and registers callbacks to expose response lifecycle behavior.
        /// </summary>
        public async Task ExecuteAsync(HttpContext context)
        {
            Context = context;
            context.Response.OnStarting(() =>
            {
                StartingCalled = true;
                context.Response.Headers["X-OnStarting"] = "called";
                return Task.CompletedTask;
            });
            context.Response.OnCompleted(() =>
            {
                CompletedCalled = true;
                return Task.CompletedTask;
            });
            await context.Response.WriteAsync($"{context.Request.Method};{context.Request.Query["format"]}");
        }
    }
}
