// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides the base <see cref="IHost"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="THost">The host <see cref="Type"/>.</typeparam>
    public class HostTesterBase<THost> where THost : class
    {
        /// <summary>
        /// Initializes a new <see cref="HostTesterBase{TFunction}"/> class.
        /// </summary>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected HostTesterBase(IServiceScope serviceScope, TestFrameworkImplementor implementor)
        {
            ServiceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
        }

        /// <summary>
        /// Gets the <see cref="IServiceScope"/>.
        /// </summary>
        protected IServiceScope ServiceScope { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Create (instantiate) the <typeparamref name="THost"/> using the <see cref="ServiceScope"/> to provide the constructor based dependency injection (DI) values.
        /// </summary>
        private THost CreateFunction() => ServiceScope.ServiceProvider.CreateInstance<THost>();

        /// <summary>
        /// Orchestrates the execution of a method as described by the <paramref name="expression"/> returning no result.
        /// </summary>
        /// <param name="expression">The method execution expression.</param>
        /// <param name="paramAttributeType">The optional parameter <see cref="Attribute"/> <see cref="Type"/> to find.</param>
        /// <param name="onBeforeRun">Action to verify the method parameters prior to method invocation.</param>
        /// <returns>The resulting exception if any and elapsed milliseconds.</returns>
        protected async Task<(Exception? Exception, long ElapsedMilliseconds)> RunAsync(Expression<Func<THost, Task>> expression, Type? paramAttributeType, Action<object?[], Attribute?, object?>? onBeforeRun)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (!(expression.Body is MethodCallExpression mce))
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

            var pis = mce.Method.GetParameters();
            var @params = new object?[pis.Length];
            Attribute? paramAttribute = null;
            object? paramValue = null;

            for (int i = 0; i < mce.Arguments.Count; i++)
            {
                var ue = Expression.Convert(mce.Arguments[i], typeof(object));
                var le = Expression.Lambda<Func<object>>(ue);
                @params[i] = le.Compile().Invoke();

                if (paramAttribute == null && paramAttributeType != null)
                {
                    paramAttribute = (Attribute?)pis[i].GetCustomAttributes(paramAttributeType, false).FirstOrDefault()!;
                    paramValue = @params[i];
                }
            }

            onBeforeRun?.Invoke(@params, paramAttribute, paramValue);

            var f = CreateFunction();
            var sw = Stopwatch.StartNew();

            try
            {
                await ((Task)mce.Method.Invoke(f, @params)!).ConfigureAwait(false);
                sw.Stop();
                return (null, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (ex, sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Orchestrates the execution of a method as described by the <paramref name="expression"/> returning a result of <see cref="Type"/> <typeparamref name="TResult"/>.
        /// </summary>
        /// <param name="expression">The method execution expression.</param>
        /// <param name="paramAttributeType">The optional parameter <see cref="Attribute"/> <see cref="Type"/> to find.</param>
        /// <param name="onBeforeRun">Action to verify the method parameters prior to method invocation.</param>
        /// <returns>The resulting value, resulting exception if any, and elapsed milliseconds.</returns>
        protected async Task<(TResult Result, Exception? Exception, long ElapsedMilliseconds)> RunAsync<TResult>(Expression<Func<THost, Task<TResult>>> expression, Type? paramAttributeType, Action<object?[], Attribute?, object?>? onBeforeRun)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (!(expression.Body is MethodCallExpression mce))
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

            var pis = mce.Method.GetParameters();
            var @params = new object?[pis.Length];
            Attribute? paramAttribute = null;
            object? paramValue = null;

            for (int i = 0; i < mce.Arguments.Count; i++)
            {
                var ue = Expression.Convert(mce.Arguments[i], typeof(object));
                var le = Expression.Lambda<Func<object>>(ue);
                @params[i] = le.Compile().Invoke();

                if (paramAttribute == null && paramAttributeType != null)
                {
                    paramAttribute = (Attribute?)pis[i].GetCustomAttributes(paramAttributeType, false).FirstOrDefault()!;
                    paramValue = @params[i];
                }
            }

            onBeforeRun?.Invoke(@params, paramAttribute, paramValue);

            var f = CreateFunction();
            var sw = Stopwatch.StartNew();

            try
            {
                var mr = await ((Task<TResult>)mce.Method.Invoke(f, @params)!).ConfigureAwait(false);
                sw.Stop();
                return (mr, null, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (default!, ex, sw.ElapsedMilliseconds);
            }
        }
    }
}