#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Functions.Worker;
using Moq;
using NUnit.Framework;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Assertors;
using UnitTestEx.Azure.Functions;
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

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public void Conversion_RestoresOriginalStreamWithoutAppendingPriorBytes(bool actionResult, bool fail)
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Post, "render");
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("prior body"));
        request.HttpContext.Response.Body = body;
        body.Position = 3;
        var result = new DeferredResult(fail);

        Func<HttpResponseMessageAssertor> convert = () => actionResult
            ? new ActionResultAssertor(test, result, null, null).ToHttpResponseMessageAssertor(request)
            : new HttpResultAssertor(test, result, null, null).ToHttpResponseMessageAssertor(request);

        if (fail)
            Assert.Throws<InvalidOperationException>(() => convert());
        else
        {
            var response = convert().AssertOK().AssertContent("deferred only");
            Assert.That(response.Response.Content.Headers.ContentLanguage, Does.Contain("en"));
        }

        Assert.That(request.HttpContext.Response.Body, Is.SameAs(body));
        Assert.That(body.CanRead, Is.True);
        Assert.That(body.CanWrite, Is.True);
        Assert.That(body.Position, Is.EqualTo(3));
        Assert.That(Encoding.UTF8.GetString(body.ToArray()), Is.EqualTo("prior body"));
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, false, true)]
    [TestCase(true, true, true)]
    public void Snapshot_PreservesDirectAndEagerResponses_WithoutExecutingResults(bool minimal, bool eager, bool bypassMiddleware)
    {
        using var test = FunctionTester.Create<HttpFunction>().UseWorkerMiddleware(!bypassMiddleware);
        var request = test.CreateHttpRequest(HttpMethod.Post, "eager");
        using var body = new MemoryStream();
        request.HttpContext.Response.Body = body;

        if (minimal)
        {
            var result = test.HttpTrigger<EagerFunctions>().Run(f => f.Minimal(request, eager));
            result.SnapshotHttpResponse(request.HttpContext.Response)
                .AssertCreated().AssertNamedHeader("X-Probe", "already-written").AssertValue(new { value = "\u00e9xample" });
            result.SnapshotHttpResponse(request.HttpContext.Response).AssertContent(EagerFunctions.Json);
        }
        else
        {
            var result = test.HttpTrigger<EagerFunctions>().Run(f => f.Action(request, eager));
            result.SnapshotHttpResponse(request.HttpContext.Response)
                .AssertCreated().AssertNamedHeader("X-Probe", "already-written").AssertValue(new { value = "\u00e9xample" });
            result.SnapshotHttpResponse(request.HttpContext.Response).AssertContent(EagerFunctions.Json);
        }

        Assert.That(body.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes(EagerFunctions.Json)));
        Assert.That(body.Position, Is.EqualTo(body.Length));
        Assert.That(body.CanWrite, Is.True);
        Assert.That(request.HttpContext.Response.Body, Is.SameAs(body));
    }

    [TestCase(200)]
    [TestCase(204)]
    public void Snapshot_PreservesBodylessResponsesAndStreamPosition(int status)
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "empty");
        request.HttpContext.Response.StatusCode = status;
        var result = new ActionResultAssertor(test, new EmptyResult(), null, null);
        result.SnapshotHttpResponse(request.HttpContext.Response)
            .Assert((System.Net.HttpStatusCode)status).AssertContent(string.Empty);
    }

    [Test]
    public void Snapshot_PreservesBinaryBytesContentHeadersLogsAndIndependence()
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "binary");
        byte[] bytes = [0, 255, 128, 1, 0];
        using var body = new MemoryStream((byte[])bytes.Clone());
        request.HttpContext.Response.Body = body;
        request.HttpContext.Response.ContentType = "application/octet-stream";
        request.HttpContext.Response.Headers.ContentLanguage = "en";
        body.Position = 2;
        var result = new ValueAssertor<IResult>(test, Results.Empty, ["invocation log"], null);
        var snapshot = result.SnapshotHttpResponse(request.HttpContext.Response);

        Assert.That(snapshot.Response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult(), Is.EqualTo(bytes));
        snapshot.AssertContentType("application/octet-stream");
        Assert.That(snapshot.Response.Content.Headers.ContentLanguage, Does.Contain("en"));
        Assert.That(snapshot.LogMessages, Does.Contain("invocation log"));
        Assert.That(body.Position, Is.EqualTo(2));
        body.Position = 0;
        body.WriteByte(42);
        body.Dispose();
        Assert.That(snapshot.Response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult(), Is.EqualTo(bytes));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void Snapshot_RejectsUnreadableOrNonSeekableStreams(bool readable, bool seekable)
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "unsupported");
        var body = new Mock<Stream>();
        body.SetupGet(value => value.CanRead).Returns(readable);
        body.SetupGet(value => value.CanSeek).Returns(seekable);
        request.HttpContext.Response.Body = body.Object;

        var exception = Assert.Throws<NotSupportedException>(() =>
            new HttpResultAssertor(test, Results.Empty, null, null).SnapshotHttpResponse(request.HttpContext.Response));

        Assert.That(exception!.Message, Does.Contain("before invoking"));
        Assert.That(request.HttpContext.Response.Body, Is.SameAs(body.Object));
    }

    [Test]
    public void Snapshot_RestoresPositionWhenReadingFails()
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "failing-stream");
        var body = new Mock<Stream>();
        body.SetupGet(value => value.CanRead).Returns(true);
        body.SetupGet(value => value.CanSeek).Returns(true);
        body.SetupProperty(value => value.Position, 7);
        body.Setup(value => value.CopyTo(It.IsAny<Stream>(), It.IsAny<int>())).Throws(new IOException("read failed"));
        request.HttpContext.Response.Body = body.Object;

        Assert.Throws<IOException>(() =>
            new ActionResultAssertor(test, new EmptyResult(), null, null).SnapshotHttpResponse(request.HttpContext.Response));
        Assert.That(body.Object.Position, Is.EqualTo(7));
        Assert.That(request.HttpContext.Response.Body, Is.SameAs(body.Object));
    }

    [Test]
    public void Snapshot_DoesNotConcealInvocationFailure()
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "failed");
        var result = new ActionResultAssertor(test, new EmptyResult(), null, new InvalidOperationException("invocation failed"));
        Assert.Throws<AssertionException>(() => result.SnapshotHttpResponse(request.HttpContext.Response));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Snapshot_DoesNotExecuteDeferredResultOrResponseCallbacks(bool actionResult)
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "snapshot");
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("already written"));
        request.HttpContext.Response.Body = body;
        var callbackInvoked = false;
        request.HttpContext.Response.OnStarting(() =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });
        var deferred = new RenderingResult();
        if (actionResult)
            new ActionResultAssertor(test, deferred, null, null).SnapshotHttpResponse(request.HttpContext.Response).AssertContent("already written");
        else
            new HttpResultAssertor(test, deferred, null, null).SnapshotHttpResponse(request.HttpContext.Response).AssertContent("already written");

        Assert.That(deferred.Context, Is.Null);
        Assert.That(callbackInvoked, Is.False);
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void DelegatedConversion_RestoresOriginalStream_ForValuesAndMultiOutput(bool actionResult, bool multiOutput)
    {
        using var test = FunctionTester.Create<HttpFunction>();
        var request = test.CreateHttpRequest(HttpMethod.Get, "delegated");
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("prior body"));
        request.HttpContext.Response.Body = body;
        object deferred = actionResult ? new ContentResult { Content = "deferred" } : Results.Text("deferred");

        var response = multiOutput
            ? new FunctionOutputAssertor<TestOutputs>(test, new TestOutputs { Http = deferred }, null, null).ToHttpResponseMessageAssertor(request)
            : new ValueAssertor<object>(test, deferred, null, null).ToHttpResponseMessageAssertor(request);

        response.AssertOK().AssertContent("deferred");
        Assert.That(request.HttpContext.Response.Body, Is.SameAs(body));
        Assert.That(body.CanRead, Is.True);
        Assert.That(Encoding.UTF8.GetString(body.ToArray()), Is.EqualTo("prior body"));
    }

    /// <summary>
    /// Supplies a deferred HTTP output to test the multi-output conversion path.
    /// </summary>
    private sealed class TestOutputs
    {
        [HttpResult]
        public object? Http { get; set; }
    }

    /// <summary>
    /// Provides functions that write responses before returning a no-op result.
    /// </summary>
    public sealed class EagerFunctions
    {
        public const string Json = "{\"value\":\"\u00e9xample\"}";

        /// <summary>
        /// Writes JSON directly or by eagerly executing an MVC result.
        /// </summary>
        [Function("EagerAction")]
        public async Task<IActionResult> Action([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "eager")] HttpRequest request, bool eager)
        {
            request.HttpContext.Response.Headers["X-Probe"] = "already-written";
            if (eager)
                await new ContentResult { StatusCode = 201, ContentType = "application/json; charset=utf-8", Content = Json }
                    .ExecuteResultAsync(new ActionContext(request.HttpContext, new RouteData(), new ActionDescriptor()));
            else
            {
                request.HttpContext.Response.StatusCode = 201;
                request.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await request.HttpContext.Response.WriteAsync(Json);
            }

            return new EmptyResult();
        }

        /// <summary>
        /// Writes JSON directly or by eagerly executing a Minimal API result.
        /// </summary>
        [Function("EagerMinimal")]
        public async Task<IResult> Minimal([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "eager")] HttpRequest request, bool eager)
        {
            request.HttpContext.Response.Headers["X-Probe"] = "already-written";
            if (eager)
                await Results.Content(Json, "application/json; charset=utf-8", statusCode: 201).ExecuteAsync(request.HttpContext);
            else
            {
                request.HttpContext.Response.StatusCode = 201;
                request.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await request.HttpContext.Response.WriteAsync(Json);
            }

            return Results.Empty;
        }
    }

    /// <summary>
    /// Provides a deferred MVC or Minimal API result that can fail after writing.
    /// </summary>
    private sealed class DeferredResult(bool fail) : IActionResult, IResult
    {
        /// <summary>
        /// Executes the deferred result using the action's HTTP context.
        /// </summary>
        public Task ExecuteResultAsync(ActionContext context) => ExecuteAsync(context.HttpContext);

        /// <summary>
        /// Writes deferred output or raises the requested execution failure.
        /// </summary>
        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.Headers.ContentLanguage = "en";
            await context.Response.WriteAsync("deferred only");
            if (fail)
                throw new InvalidOperationException("render failed");
        }
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
