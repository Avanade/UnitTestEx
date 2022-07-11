// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides the base host unit-testing capabilities.
    /// </summary>
    /// <typeparam name="THost">The host <see cref="Type"/>.</typeparam>
    public class HostTesterBase<THost> where THost : class
    {
        /// <summary>
        /// Initializes a new <see cref="HostTesterBase{TFunction}"/> class.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        protected HostTesterBase(TesterBase tester, IServiceScope serviceScope)
        {
            Tester = tester ?? throw new ArgumentNullException(nameof(tester));
            ServiceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
        }

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        protected TesterBase Tester { get; }

        /// <summary>
        /// Gets the <see cref="IServiceScope"/>.
        /// </summary>
        protected IServiceScope ServiceScope { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected TestFrameworkImplementor Implementor => Tester.Implementor;

        /// <summary>
        /// Gets or sets the <see cref="IJsonSerializer"/>.
        /// </summary>
        protected IJsonSerializer JsonSerializer => Tester.JsonSerializer;

        /// <summary>
        /// Create (instantiate) the <typeparamref name="THost"/> using the <see cref="ServiceScope"/> to provide the constructor based dependency injection (DI) values.
        /// </summary>
        private THost CreateHost() => ServiceScope.ServiceProvider.CreateInstance<THost>();

        /// <summary>
        /// Orchestrates the execution of a method as described by the <paramref name="expression"/> returning no result.
        /// </summary>
        /// <param name="expression">The method execution expression.</param>
        /// <param name="paramAttributeType">The optional parameter <see cref="Attribute"/> <see cref="Type"/> to find.</param>
        /// <param name="onBeforeRun">Action to verify the method parameters prior to method invocation.</param>
        /// <returns>The resulting exception if any and elapsed milliseconds.</returns>
        protected async Task<(Exception? Exception, double ElapsedMilliseconds)> RunAsync(Expression<Func<THost, Task>> expression, Type? paramAttributeType, Action<object?[], Attribute?, object?>? onBeforeRun)
        {
            var mce = MethodCallExpressionValidate(expression);
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

            var h = CreateHost();
            var sw = Stopwatch.StartNew();

            try
            {
                await ((Task)mce.Method.Invoke(h, @params)!).ConfigureAwait(false);
                sw.Stop();
                return (null, sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (ex, sw.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Orchestrates the execution of a method as described by the <paramref name="expression"/> returning a result of <see cref="Type"/> <typeparamref name="TValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The method execution expression.</param>
        /// <param name="paramAttributeType">The optional parameter <see cref="Attribute"/> <see cref="Type"/> to find.</param>
        /// <param name="onBeforeRun">Action to verify the method parameters prior to method invocation.</param>
        /// <returns>The resulting value, resulting exception if any, and elapsed milliseconds.</returns>
        protected async Task<(TValue Result, Exception? Exception, double ElapsedMilliseconds)> RunAsync<TValue>(Expression<Func<THost, Task<TValue>>> expression, Type? paramAttributeType, Action<object?[], Attribute?, object?>? onBeforeRun)
        {
            var mce = MethodCallExpressionValidate(expression);
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

            var h = CreateHost();
            var sw = Stopwatch.StartNew();

            try
            {
                var mr = await expression.Compile().Invoke(h).ConfigureAwait(false);
                sw.Stop();
                return (mr, null, sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (default!, ex, sw.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Validates that the <paramref name="expression"/> is a valid <see cref="MethodCallExpression"/>.
        /// </summary>
        /// <param name="expression">The <see cref="Expression"/>.</param>
        internal static MethodCallExpression MethodCallExpressionValidate([NotNull] Expression expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression is not LambdaExpression lex)
                throw new ArgumentException($"Expression must be of Type '{nameof(LambdaExpression)}'.", nameof(expression));

            if (lex.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (lex.Body is not MethodCallExpression mce)
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

            if (mce.Object == null || mce.Object.NodeType != ExpressionType.Parameter)
                throw new InvalidOperationException($"UnitTestEx methods that enable an expression must not include method-chaining '{mce}'; i.e. must be an invocation of a single method with zero or more arguments only." 
                    + Environment.NewLine + $"This is because UnitTestEx is reflecting the underlying type and arguments to validate, log, and in some instances refactor the execution using this information. If this is not the desired " 
                    + "behavior then consider using one of the other methods that does not use expressions to execute the test.");

            return mce;
        }
    }
}