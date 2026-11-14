using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>Adapts a discovered context-only Functions orchestration to the SDK host.</summary>
internal sealed class DiscoveredDurableTaskOrchestrator : ITaskOrchestrator
{
    private readonly Func<object?, TaskOrchestrationContext, Task<object?>> _invoke;
    private readonly Func<TaskOrchestrationContext, object?>? _instance;
    private readonly Func<DataConverter>? _converter;

    /// <summary>Validates an orchestration method and compiles its invocation.</summary>
    /// <param name="method">The discovered orchestration method.</param>
    internal DiscoveredDurableTaskOrchestrator(MethodInfo method)
    {
        Method = method;
        var parameters = method.GetParameters();
        Type result = method.ReturnType;
        if (method.ContainsGenericParameters || method.DeclaringType is null || (!method.IsStatic && method.DeclaringType.IsAbstract)
            || parameters.Length != 1 || parameters[0].ParameterType != typeof(TaskOrchestrationContext) || !IsOrchestrationInput(parameters[0])
            || (result != typeof(Task) && (!result.IsGenericType || result.GetGenericTypeDefinition() != typeof(Task<>))))
            throw new NotSupportedException($"Discovered orchestration '{method.GetCustomAttribute<FunctionAttribute>()!.Name}' ({method.DeclaringType?.FullName}.{method.Name}) must accept only an OrchestrationTrigger TaskOrchestrationContext and return Task or Task<T>.");

        OutputType = result == typeof(Task) ? typeof(object) : result.GetGenericArguments()[0];
        var instance = Expression.Parameter(typeof(object), "instance");
        var context = Expression.Parameter(typeof(TaskOrchestrationContext), "context");
        var call = Expression.Call(method.IsStatic ? null : Expression.Convert(instance, method.DeclaringType), method, context);
        MethodInfo box = typeof(DiscoveredDurableTaskOrchestrator).GetMethod(result == typeof(Task) ? nameof(BoxNoResult) : nameof(BoxResult), BindingFlags.NonPublic | BindingFlags.Static)!;
        if (result != typeof(Task))
            box = box.MakeGenericMethod(OutputType);
        _invoke = Expression.Lambda<Func<object?, TaskOrchestrationContext, Task<object?>>>(Expression.Call(box, call), instance, context).Compile();
    }

    /// <summary>Creates a host registration using a run-owned instance and the SDK worker converter.</summary>
    private DiscoveredDurableTaskOrchestrator(DiscoveredDurableTaskOrchestrator original, Func<TaskOrchestrationContext, object?> instance, Func<DataConverter> converter)
    {
        Method = original.Method;
        OutputType = original.OutputType;
        _invoke = original._invoke;
        _instance = instance;
        _converter = converter;
    }

    /// <summary>Gets the discovered method.</summary>
    internal MethodInfo Method { get; }

    /// <inheritdoc/>
    public Type InputType => typeof(object);

    /// <inheritdoc/>
    public Type OutputType { get; }

    /// <summary>Identifies the worker Durable orchestration trigger without a binding extension dependency.</summary>
    internal static bool IsOrchestrationInput(ParameterInfo parameter)
        => parameter.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Microsoft.Azure.Functions.Worker.OrchestrationTriggerAttribute");

    /// <summary>Creates a registered adapter using its run-owned instance and worker converter.</summary>
    internal DiscoveredDurableTaskOrchestrator Bind(Func<TaskOrchestrationContext, object?> instance, Func<DataConverter> converter) => new(this, instance, converter);

    /// <inheritdoc/>
    public Task<object?> RunAsync(TaskOrchestrationContext context, object? input)
        => _invoke((_instance ?? throw new InvalidOperationException("The discovered orchestration has not been bound."))(context), new DurableTaskRegistryExtensions.WallClockTimerDiagnosticContext(context, (_converter ?? throw new InvalidOperationException("The discovered orchestration has not been bound."))()));

    /// <summary>Boxes the completed orchestration result on its deterministic execution context.</summary>
    private static async Task<object?> BoxResult<T>(Task<T> result) => await result;

    /// <summary>Awaits an orchestration without output on its deterministic execution context.</summary>
    private static async Task<object?> BoxNoResult(Task result) { await result; return null; }
}
