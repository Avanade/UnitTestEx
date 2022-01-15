// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides the base Azure Function unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    public class TriggerTesterBase<TFunction> where TFunction : class
    {
        /// <summary>
        /// Initializes a new <see cref="GenericTriggerTester{TFunction}"/> class.
        /// </summary>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected TriggerTesterBase(IServiceScope serviceScope, TestFrameworkImplementor implementor)
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
        /// Create (instantiate) the <typeparamref name="TFunction"/> using the <see cref="ServiceScope"/> to provide the constructor based dependency injection (DI) values.
        /// </summary>
        private TFunction CreateFunction()
        {
            // Try instantiating using service provider and use if successful.
            var val = ServiceScope.ServiceProvider.GetService<TFunction>();
            if (val != null)
                return val;

            // Simulate the creation of a request scope.
            var type = typeof(TFunction);
            var ctor = type.GetConstructors().FirstOrDefault();
            if (ctor == null)
                return (TFunction)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Unable to instantiate Function Type '{type.Name}'"));

            // Simulate dependency injection for each parameter.
            var pis = ctor.GetParameters();
            var args = new object[pis.Length];
            for (int i = 0; i < pis.Length; i++)
            {
                args[i] = ServiceScope.ServiceProvider.GetRequiredService(pis[i].ParameterType);
            }

            return (TFunction)(Activator.CreateInstance(type, args) ?? throw new InvalidOperationException($"Unable to instantiate Function Type '{type.Name}'"));
        }

        /// <summary>
        /// Orchestrates the execution of the function method as described by the <paramref name="expression"/> returning no result.
        /// </summary>
        /// <param name="expression">The funtion execution expression.</param>
        /// <param name="paramAttributeType">The optional parameter <see cref="Attribute"/> <see cref="Type"/> to find.</param>
        /// <param name="onBeforeRun">Action to verify the method parameters prior to method invocation.</param>
        /// <returns>The resulting exception if any and elapsed milliseconds.</returns>
        protected (Exception? Exception, long ElapsedMilliseconds) RunFunction(Expression<Func<TFunction, Task>> expression, Type? paramAttributeType, Action<object?[], Attribute?, object?>? onBeforeRun)
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
            var mr = mce.Method.Invoke(f, @params)!;

            if (!(mr is Task tr))
                throw new InvalidOperationException($"The function method must return a result of Type {nameof(Task)}.");

            try
            {
                tr.GetAwaiter().GetResult();
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
        /// Orchestrates the execution of the function method as described by the <paramref name="expression"/> returning a result of <see cref="Type"/> <typeparamref name="TResult"/>.
        /// </summary>
        /// <param name="expression">The funtion execution expression.</param>
        /// <param name="paramAttributeType">The optional parameter <see cref="Attribute"/> <see cref="Type"/> to find.</param>
        /// <param name="onBeforeRun">Action to verify the method parameters prior to method invocation.</param>
        /// <returns>The resulting value, resulting exception if any, and elapsed milliseconds.</returns>
        protected (TResult Result, Exception? Exception, long ElapsedMilliseconds) RunFunction<TResult>(Expression<Func<TFunction, Task<TResult>>> expression, Type? paramAttributeType, Action<object?[], Attribute?, object?>? onBeforeRun)
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
            var mr = mce.Method.Invoke(f, @params)!;

            if (!(mr is Task<TResult> tr))
                throw new InvalidOperationException($"The function method must return a result of Type {nameof(Task<TResult>)}.");

            try
            {
                var r = tr.GetAwaiter().GetResult();
                sw.Stop();
                return (r, null, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (default!, ex, sw.ElapsedMilliseconds);
            }
        }
    }
}