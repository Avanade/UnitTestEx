using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;

namespace UnitTestEx.IsolatedFunction
{
    /// <summary>
    /// Test middleware: always logs; behaviour is otherwise opt-in via the <c>X-Middleware</c> request header (<c>deny</c>, <c>handle</c> or <c>set-item</c>).
    /// </summary>
    public sealed class TestMiddleware : IFunctionsWorkerMiddleware
    {
        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            var logger = context.GetLogger<TestMiddleware>();
            logger.LogInformation("Middleware before {FunctionName}.", context.FunctionDefinition.Name);

            var req = await context.GetHttpRequestDataAsync();
            var mode = req is not null && req.Headers.TryGetValues("X-Middleware", out var values) ? values.FirstOrDefault() : null;

            if (mode == "deny")
            {
                await SetResponseAsync(context, req!, HttpStatusCode.Unauthorized, "denied by middleware");
                return;
            }

            if (mode == "set-item" && context.GetHttpContext() is { } httpContext)
                httpContext.Items["MiddlewareValue"] = "set by middleware";

            try
            {
                await next(context);
            }
            catch (Exception ex) when (mode == "handle")
            {
                logger.LogWarning("Middleware handled {Message}.", ex.InnerException?.Message ?? ex.Message);
                await SetResponseAsync(context, req!, HttpStatusCode.InternalServerError, "handled by middleware");
                return;
            }

            logger.LogInformation("Middleware after {FunctionName}.", context.FunctionDefinition.Name);
        }

        /// <summary>
        /// Writes a middleware response using the active HTTP hosting model and stores it as the invocation result.
        /// </summary>
        /// <param name="context">The current function context.</param>
        /// <param name="req">The HTTP request associated with the invocation.</param>
        /// <param name="statusCode">The response status code.</param>
        /// <param name="body">The response body.</param>
        private static async Task SetResponseAsync(FunctionContext context, HttpRequestData req, HttpStatusCode statusCode, string body)
        {
            var httpContext = context.GetHttpContext();
            if (httpContext is not null && context.FunctionDefinition.Parameters.Any(p => p.Type == typeof(HttpRequest)))
            {
                httpContext.Response.StatusCode = (int)statusCode;
                await httpContext.Response.WriteAsync(body);
                return;
            }

            var res = req.CreateResponse(statusCode);
            await res.WriteStringAsync(body);
            context.GetInvocationResult().Value = res;
        }
    }

    public class MiddlewareFunctions
    {
        [Function("MiddlewareRequestAsync")]
        public async Task<Microsoft.AspNetCore.Mvc.IActionResult> RequestAsync([HttpTrigger(AuthorizationLevel.Function, "get", Route = "middleware/async")] HttpRequest req)
        {
            await Task.CompletedTask;
            return new Microsoft.AspNetCore.Mvc.OkObjectResult(req.HttpContext.Items["MiddlewareValue"]);
        }

        [Function("MiddlewareRequestSync")]
        public Microsoft.AspNetCore.Mvc.IActionResult RequestSync([HttpTrigger(AuthorizationLevel.Function, "get", Route = "middleware/sync")] HttpRequest req)
            => new Microsoft.AspNetCore.Mvc.OkObjectResult(req.HttpContext.Items["MiddlewareValue"]);

        [Function("MiddlewareRequestVoid")]
        public void RequestVoid([HttpTrigger(AuthorizationLevel.Function, "get", Route = "middleware/void")] HttpRequest req)
            => req.Headers["X-Function-Value"] = (string?)req.HttpContext.Items["MiddlewareValue"];

        [Function("ThrowsAction")]
        public Microsoft.AspNetCore.Mvc.IActionResult ThrowsAction([HttpTrigger(AuthorizationLevel.Function, "get", Route = "throws/action")] HttpRequest req) => throw new InvalidOperationException("action boom");

        [Function("ThrowsData")]
        public HttpResponseData ThrowsData([HttpTrigger(AuthorizationLevel.Function, "get", Route = "throws/data")] HttpRequestData req) => throw new InvalidOperationException("data boom");
    }
}
