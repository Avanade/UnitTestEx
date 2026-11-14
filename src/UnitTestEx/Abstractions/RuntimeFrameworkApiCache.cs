// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Caches framework reflection results and binds reusable delegates for the selected test framework.
    /// </summary>
    internal sealed class RuntimeFrameworkApiCache
    {
        private static readonly ConcurrentDictionary<(TestFrameworkBackend, Assembly), Lazy<RuntimeFrameworkApiCache>> _instances = new();
        private readonly FrameworkAdapter _adapter;
        private readonly ConditionalWeakTable<Type, Lazy<Delegate>> _equalityAsserts = new();
        private readonly ConcurrentDictionary<string, Lazy<ExceptionFactories>> _exceptionFactories = new(StringComparer.Ordinal);
        private readonly Lazy<MethodInfo> _xunitEqualMethod;
        private readonly Lazy<NUnitMethods> _nunitMethods;
        private readonly Lazy<Action<string>> _nunitFail;
        private readonly Lazy<MethodInfo> _mstestEqualMethod;
        private readonly Lazy<Func<TextWriter?>?> _nunitOutputWriter;
        private readonly Lazy<Action<string, object?[]>?> _mstestOutputWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeFrameworkApiCache"/> class.
        /// </summary>
        /// <param name="adapter">The selected test framework adapter.</param>
        private RuntimeFrameworkApiCache(FrameworkAdapter adapter)
        {
            _adapter = adapter;
            _xunitEqualMethod = new Lazy<MethodInfo>(FindXunitEqualMethod, LazyThreadSafetyMode.ExecutionAndPublication);
            _nunitMethods = new Lazy<NUnitMethods>(FindNUnitMethods, LazyThreadSafetyMode.ExecutionAndPublication);
            _nunitFail = new Lazy<Action<string>>(BindNUnitFail, LazyThreadSafetyMode.ExecutionAndPublication);
            _mstestEqualMethod = new Lazy<MethodInfo>(FindMSTestEqualMethod, LazyThreadSafetyMode.ExecutionAndPublication);
            _nunitOutputWriter = new Lazy<Func<TextWriter?>?>(CreateNUnitOutputWriter, LazyThreadSafetyMode.ExecutionAndPublication);
            _mstestOutputWriter = new Lazy<Action<string, object?[]>?>(CreateMSTestOutputWriter, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Gets the process-wide cache for the selected framework assembly.
        /// </summary>
        /// <param name="adapter">The selected test framework adapter.</param>
        /// <returns>The framework API cache.</returns>
        public static RuntimeFrameworkApiCache Get(FrameworkAdapter adapter)
        {
            var assembly = adapter.Assembly ?? throw new InvalidOperationException("A framework assembly is required to cache its runtime API.");
            var key = (adapter.Backend, assembly);
            return _instances.GetOrAdd(key, _ => new Lazy<RuntimeFrameworkApiCache>(() => new RuntimeFrameworkApiCache(adapter), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        /// <summary>
        /// Gets the cached equality assertion delegate for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The equality assertion delegate.</returns>
        public Action<T?, T?, string?> GetEqualityAssert<T>()
        {
            var lazy = _equalityAsserts.GetValue(typeof(T), _ => new Lazy<Delegate>(() => CreateEqualityAssert<T>(), LazyThreadSafetyMode.ExecutionAndPublication));
            return (Action<T?, T?, string?>)lazy.Value;
        }

        /// <summary>Invokes NUnit's native failure API, preserving multiple-assert scopes.</summary>
        public void NUnitFail(string message) => _nunitFail.Value(message);

        /// <summary>
        /// Writes through the framework's default output facility when one is available.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns><see langword="true"/> if the message was written.</returns>
        public bool TryWriteFrameworkOutput(string? message)
        {
            if (_adapter.Backend == TestFrameworkBackend.NUnit && _nunitOutputWriter.Value?.Invoke() is { } writer)
            {
                writer.WriteLine(message);
                return true;
            }

            if ((_adapter.Backend is TestFrameworkBackend.MSTest3 or TestFrameworkBackend.MSTest4) && _mstestOutputWriter.Value is { } logger)
            {
                logger("{0}", [message]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a framework assertion exception using cached constructor delegates.
        /// </summary>
        /// <param name="exceptionTypeName">The exception type name.</param>
        /// <param name="message">The failure message.</param>
        /// <param name="innerException">The optional inner exception.</param>
        /// <returns>The framework assertion exception.</returns>
        public Exception CreateFrameworkException(string exceptionTypeName, string message, Exception? innerException = null)
        {
            var constructors = _exceptionFactories.GetOrAdd(exceptionTypeName, name => new Lazy<ExceptionFactories>(() => FindExceptionFactories(name), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            if (innerException is not null && constructors.WithInner is not null)
                return constructors.WithInner(message, innerException);

            return constructors.WithMessage?.Invoke(message) ?? throw new InvalidOperationException($"The test framework exception '{exceptionTypeName}' could not be created.");
        }

        /// <summary>
        /// Creates the equality assertion delegate for a closed generic value type.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The equality assertion delegate.</returns>
        private Delegate CreateEqualityAssert<T>()
        {
            return _adapter.Backend switch
            {
                TestFrameworkBackend.Xunit2 or TestFrameworkBackend.Xunit3 => CreateXunitEqualAssert<T>(),
                TestFrameworkBackend.NUnit => CreateNUnitEqualAssert<T>(),
                TestFrameworkBackend.MSTest3 or TestFrameworkBackend.MSTest4 => CreateMSTestEqualAssert<T>(),
                _ => throw new InvalidOperationException($"Cannot create an equality assertion for '{_adapter.Backend}'.")
            };
        }

        /// <summary>
        /// Binds xUnit's generic equality assertion to a reusable delegate.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The equality assertion delegate.</returns>
        private Action<T?, T?, string?> CreateXunitEqualAssert<T>()
        {
            var method = _xunitEqualMethod.Value.MakeGenericMethod(typeof(T));
            var assert = (Action<T, T>)method.CreateDelegate(typeof(Action<T, T>));
            return (expected, actual, _) => assert(expected!, actual!);
        }

        /// <summary>
        /// Binds NUnit's equality constraint and assertion methods to reusable delegates.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The equality assertion delegate.</returns>
        private Action<T?, T?, string?> CreateNUnitEqualAssert<T>()
        {
            var methods = _nunitMethods.Value;
            var assert = BindNUnitAssert<T>(methods.AssertThat);
            return (expected, actual, message) =>
            {
                var constraint = methods.EqualTo(expected);
                var nunitMessage = message is null
                    ? methods.NullMessage
                    : methods.ConvertMessage?.Invoke(message) ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly!.GetName().Name}' cannot convert an assertion message to '{methods.MessageType.FullName}'.");

                assert(actual!, constraint, nunitMessage);
            };
        }

        /// <summary>
        /// Binds MSTest's generic equality assertion to a reusable delegate.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The equality assertion delegate.</returns>
        private Action<T?, T?, string?> CreateMSTestEqualAssert<T>()
        {
            var method = _mstestEqualMethod.Value.MakeGenericMethod(typeof(T));
            if (_adapter.Backend == TestFrameworkBackend.MSTest4)
            {
                var assert = (MSTest4Equal<T>)method.CreateDelegate(typeof(MSTest4Equal<T>));
                return (expected, actual, message) => assert(expected!, actual!, message, null, null);
            }

            var assertV3 = (MSTest3Equal<T>)method.CreateDelegate(typeof(MSTest3Equal<T>));
            return (expected, actual, message) => assertV3(expected!, actual!, message, [expected, actual]);
        }

        /// <summary>
        /// Finds xUnit's generic equality assertion method.
        /// </summary>
        /// <returns>The open generic equality assertion method.</returns>
        private MethodInfo FindXunitEqualMethod()
        {
            var assertType = _adapter.Assembly?.GetType("Xunit.Assert") ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not contain 'Xunit.Assert'.");
            return assertType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Equal" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1 && m.GetParameters() is [{ ParameterType: var first }, { ParameterType: var second }] && first == m.GetGenericArguments()[0] && second == m.GetGenericArguments()[0])
                ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not expose the expected Xunit.Assert.Equal method.");
        }

        /// <summary>
        /// Finds NUnit's equality constraint, assertion method, and message conversion.
        /// </summary>
        /// <returns>The cached NUnit methods.</returns>
        private NUnitMethods FindNUnitMethods()
        {
            var assembly = _adapter.Assembly!;
            var isType = assembly.GetType("NUnit.Framework.Is") ?? throw new InvalidOperationException($"The selected test framework assembly '{assembly.GetName().Name}' does not contain 'NUnit.Framework.Is'.");
            var equalTo = isType.GetMethod("EqualTo", BindingFlags.Public | BindingFlags.Static, [typeof(object)]) ?? throw new InvalidOperationException($"The selected test framework assembly '{assembly.GetName().Name}' does not expose NUnit's Is.EqualTo method.");
            var assertType = assembly.GetType("NUnit.Framework.Assert") ?? throw new InvalidOperationException($"The selected test framework assembly '{assembly.GetName().Name}' does not contain 'NUnit.Framework.Assert'.");
            var assertThat = assertType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "That" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1 && m.GetParameters() is [{ ParameterType: var actualType }, { ParameterType: var constraintType }, ..] && actualType == m.GetGenericArguments()[0] && constraintType.FullName == "NUnit.Framework.Constraints.IResolveConstraint")
                ?? throw new InvalidOperationException($"The selected test framework assembly '{assembly.GetName().Name}' does not expose NUnit's Assert.That method.");

            var parameters = assertThat.GetParameters();
            if (parameters.Length < 3)
                throw new InvalidOperationException($"The selected test framework assembly '{assembly.GetName().Name}' does not expose an NUnit Assert.That overload with a message parameter.");

            var messageConverter = parameters[2].ParameterType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
            return new NUnitMethods(CreateUnaryObjectDelegate(equalTo), assertThat, parameters[2].ParameterType, messageConverter is null ? null : CreateUnaryObjectDelegate(messageConverter), Activator.CreateInstance(parameters[2].ParameterType));
        }

        /// <summary>Binds NUnit's native failure method once for the framework assembly.</summary>
        private Action<string> BindNUnitFail()
        {
            var assertType = _adapter.Assembly?.GetType("NUnit.Framework.Assert") ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not contain 'NUnit.Framework.Assert'.");
            var method = assertType.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static, [typeof(string)])
                ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not expose NUnit's Assert.Fail method.");

            return (Action<string>)method.CreateDelegate(typeof(Action<string>));
        }

        /// <summary>
        /// Finds the selected MSTest version's generic equality assertion method.
        /// </summary>
        /// <returns>The open generic equality assertion method.</returns>
        private MethodInfo FindMSTestEqualMethod()
        {
            var assertType = _adapter.Assembly?.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.Assert") ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not contain MSTest's Assert type.");
            var methods = assertType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "AreEqual" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1).ToArray();
            return (_adapter.Backend == TestFrameworkBackend.MSTest4
                ? methods.FirstOrDefault(m => m.GetParameters() is [{ ParameterType: var first }, { ParameterType: var second }, { ParameterType: var messageType }, { ParameterType: var expectedExpressionType }, { ParameterType: var actualExpressionType }] && first == m.GetGenericArguments()[0] && second == m.GetGenericArguments()[0] && messageType == typeof(string) && expectedExpressionType == typeof(string) && actualExpressionType == typeof(string))
                : methods.FirstOrDefault(m => m.GetParameters() is [{ ParameterType: var first }, { ParameterType: var second }, { ParameterType: var messageType }, { ParameterType: var argsType }] && first == m.GetGenericArguments()[0] && second == m.GetGenericArguments()[0] && messageType == typeof(string) && argsType == typeof(object[]))) ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not expose MSTest's Assert.AreEqual method.");
        }

        /// <summary>
        /// Binds NUnit's generic assertion method behind an object-typed delegate.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="method">The open generic assertion method.</param>
        /// <returns>The assertion delegate.</returns>
        private static Action<T, object?, object?> BindNUnitAssert<T>(MethodInfo method)
        {
            var closedMethod = method.MakeGenericMethod(typeof(T));
            var parameters = closedMethod.GetParameters();
            var delegateType = Expression.GetDelegateType(parameters.Select(p => p.ParameterType).Append(closedMethod.ReturnType).ToArray());
            var nativeAssert = closedMethod.CreateDelegate(delegateType);
            var actual = Expression.Parameter(typeof(T), "actual");
            var constraint = Expression.Parameter(typeof(object), "constraint");
            var message = Expression.Parameter(typeof(object), "message");
            var arguments = new Expression[parameters.Length];
            arguments[0] = actual;
            arguments[1] = Expression.Convert(constraint, parameters[1].ParameterType);
            arguments[2] = Expression.Convert(message, parameters[2].ParameterType);
            for (var i = 3; i < parameters.Length; i++)
                arguments[i] = parameters[i].ParameterType.IsValueType ? Expression.Default(parameters[i].ParameterType) : Expression.Constant(null, parameters[i].ParameterType);

            var call = Expression.Invoke(Expression.Constant(nativeAssert, delegateType), arguments);
            return Expression.Lambda<Action<T, object?, object?>>(call, actual, constraint, message).Compile();
        }

        /// <summary>
        /// Binds a unary framework method to an object-typed delegate.
        /// </summary>
        /// <param name="method">The framework method.</param>
        /// <returns>The bound delegate.</returns>
        private static Func<object?, object?> CreateUnaryObjectDelegate(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var delegateType = Expression.GetDelegateType(parameters.Select(p => p.ParameterType).Append(method.ReturnType).ToArray());
            var nativeMethod = method.CreateDelegate(delegateType);
            var value = Expression.Parameter(typeof(object), "value");
            var call = Expression.Invoke(Expression.Constant(nativeMethod, delegateType), Expression.Convert(value, parameters[0].ParameterType));
            return Expression.Lambda<Func<object?, object?>>(Expression.Convert(call, typeof(object)), value).Compile();
        }

        /// <summary>
        /// Creates the NUnit output-writer getter delegate when available.
        /// </summary>
        /// <returns>The output-writer getter.</returns>
        private Func<TextWriter?>? CreateNUnitOutputWriter()
        {
            var contextType = _adapter.Assembly?.GetType("NUnit.Framework.TestContext");
            var getter = contextType?.GetProperty("Out", BindingFlags.Public | BindingFlags.Static)?.GetMethod;
            return getter is not null && typeof(TextWriter).IsAssignableFrom(getter.ReturnType)
                ? (Func<TextWriter?>)getter.CreateDelegate(typeof(Func<TextWriter?>))
                : null;
        }

        /// <summary>
        /// Creates the MSTest output-writer delegate when available.
        /// </summary>
        /// <returns>The output-writer delegate.</returns>
        private Action<string, object?[]>? CreateMSTestOutputWriter()
        {
            var loggerType = _adapter.Assembly?.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.Logging.Logger");
            var method = loggerType?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "LogMessage" && m.GetParameters() is [{ ParameterType: var firstType }, { ParameterType: var secondType }] && firstType == typeof(string) && secondType == typeof(object[]));
            return method is null ? null : (Action<string, object?[]>)method.CreateDelegate(typeof(Action<string, object?[]>));
        }

        /// <summary>
        /// Finds the supplied framework exception's cached constructor delegates.
        /// </summary>
        /// <param name="exceptionTypeName">The exception type name.</param>
        /// <returns>The constructor delegates.</returns>
        private ExceptionFactories FindExceptionFactories(string exceptionTypeName)
        {
            var type = _adapter.Assembly?.GetType(exceptionTypeName) ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not contain '{exceptionTypeName}'.");
            var messageConstructor = type.GetConstructor([typeof(string)]);
            var innerConstructor = type.GetConstructor([typeof(string), typeof(Exception)]);
            return new ExceptionFactories(messageConstructor is null ? null : BindMessageExceptionConstructor(messageConstructor), innerConstructor is null ? null : BindInnerExceptionConstructor(innerConstructor));
        }

        /// <summary>
        /// Binds a message-only framework exception constructor to a reusable delegate.
        /// </summary>
        /// <param name="constructor">The message-only exception constructor.</param>
        /// <returns>The constructor delegate.</returns>
        private static Func<string, Exception> BindMessageExceptionConstructor(ConstructorInfo constructor)
        {
            var message = Expression.Parameter(typeof(string), "message");
            return Expression.Lambda<Func<string, Exception>>(Expression.Convert(Expression.New(constructor, message), typeof(Exception)), message).Compile();
        }

        /// <summary>
        /// Binds a framework exception constructor with an inner exception to a reusable delegate.
        /// </summary>
        /// <param name="constructor">The message-and-inner-exception constructor.</param>
        /// <returns>The constructor delegate.</returns>
        private static Func<string, Exception, Exception> BindInnerExceptionConstructor(ConstructorInfo constructor)
        {
            var message = Expression.Parameter(typeof(string), "message");
            var innerException = Expression.Parameter(typeof(Exception), "innerException");
            return Expression.Lambda<Func<string, Exception, Exception>>(Expression.Convert(Expression.New(constructor, message, innerException), typeof(Exception)), message, innerException).Compile();
        }

        /// <summary>
        /// Stores NUnit's bound equality and assertion methods.
        /// </summary>
        /// <param name="EqualTo">The bound equality-constraint factory.</param>
        /// <param name="AssertThat">The open generic assertion method.</param>
        /// <param name="MessageType">The framework message type.</param>
        /// <param name="ConvertMessage">The optional bound message converter.</param>
        /// <param name="NullMessage">The default framework message value.</param>
        private sealed record NUnitMethods(Func<object?, object?> EqualTo, MethodInfo AssertThat, Type MessageType, Func<object?, object?>? ConvertMessage, object? NullMessage);

        /// <summary>
        /// Stores cached framework exception constructor delegates.
        /// </summary>
        /// <param name="WithMessage">The message-only constructor delegate.</param>
        /// <param name="WithInner">The message-and-inner-exception constructor delegate.</param>
        private sealed record ExceptionFactories(Func<string, Exception>? WithMessage, Func<string, Exception, Exception>? WithInner);

        /// <summary>
        /// Binds MSTest v3's generic equality assertion signature.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">The assertion message.</param>
        /// <param name="args">The formatted assertion values.</param>
        private delegate void MSTest3Equal<T>(T expected, T actual, string? message, object?[] args);

        /// <summary>
        /// Binds MSTest v4's generic equality assertion signature.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">The assertion message.</param>
        /// <param name="expectedExpression">The optional expected expression.</param>
        /// <param name="actualExpression">The optional actual expression.</param>
        private delegate void MSTest4Equal<T>(T expected, T actual, string? message, string? expectedExpression, string? actualExpression);
    }
}
