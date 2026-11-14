using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>Verifies assembly boundaries, scoped activity ownership and early discovery errors.</summary>
[TestFixture]
public sealed class SelfDiscoveryTest
{
    /// <summary>Runs the complete production batch, including instance/context, void and Task activity bodies.</summary>
    [Test]
    public async Task ExecutesRealBatchAndDisposesApplicationScopes()
    {
        var journals = new ConcurrentBag<RecordingJournal>();
        using var functions = FunctionTester.Create<WorkflowActivities>();
        functions.ConfigureServices(services => services.AddScoped<IWorkflowJournal>(_ =>
        {
            var journal = new RecordingJournal();
            journals.Add(journal);
            return journal;
        }));
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery()
            .Orchestrator<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (context, _) => BatchOrchestration.ProcessBatch(context))
            .SubOrchestrator<BatchResult, BatchResult>(nameof(BatchOrchestration.FinalizeBatch), (context, _) => new BatchOrchestration().FinalizeBatch(context))
            .StartAsync(new BatchRequest(" batch-real ", [new("A", 10m), new("B", 20m)]));

        run.ExpectSuccess().ExpectLogContains("Processing item A.").ExpectLogContains("Processing item B.");
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result =>
            result.AssertCompleted().AssertOutput(new BatchResult("BATCH-REAL", 60m, [new("A", 20m), new("B", 40m)])));
        Assert.That(journals, Has.Count.EqualTo(4));
        Assert.That(journals.All(journal => journal.Disposed), Is.True);
        Assert.That(journals.SelectMany(journal => journal.References), Is.EqualTo(new[] { "BATCH-REAL" }));
        Assert.That(journals.SelectMany(journal => journal.Results).Single().Total, Is.EqualTo(60m));
    }

    /// <summary>Runs the same complete batch with its instance child orchestration discovered instead of manually registered.</summary>
    [Test]
    public async Task ExecutesRealBatchWithDiscoveredSubOrchestrator()
    {
        var journals = new ConcurrentBag<RecordingJournal>();
        using var functions = FunctionTester.Create<WorkflowActivities>();
        functions.ConfigureServices(services => services.AddScoped<IWorkflowJournal>(_ =>
        {
            var journal = new RecordingJournal();
            journals.Add(journal);
            return journal;
        }));
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery()
            .Orchestrator<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (context, _) => BatchOrchestration.ProcessBatch(context))
            .StartAsync(new BatchRequest(" batch-real ", [new("A", 10m), new("B", 20m)]));

        run.ExpectSuccess().ExpectLogContains("Processing item A.").ExpectLogContains("Processing item B.");
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result =>
            result.AssertCompleted().AssertOutput(new BatchResult("BATCH-REAL", 60m, [new("A", 20m), new("B", 40m)])));
        Assert.That(journals, Has.Count.EqualTo(4));
        Assert.That(journals.All(journal => journal.Disposed), Is.True);
        Assert.That(journals.SelectMany(journal => journal.References), Is.EqualTo(new[] { "BATCH-REAL" }));
        Assert.That(journals.SelectMany(journal => journal.Results).Single().Total, Is.EqualTo(60m));
    }

    /// <summary>Confirms a discovered instance and its scoped dependencies stay alive across child replay and are disposed with the run.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task DiscoveredChildRetainsInstanceAcrossReplay(bool registered)
    {
        var scopes = new ConcurrentBag<OrchestrationScope>();
        var instances = new ConcurrentBag<ScopedOrchestration>();
        using var functions = FunctionTester.Create<WorkflowActivities>();
        functions.ConfigureServices(services =>
        {
            services.AddScoped(_ =>
            {
                var scope = new OrchestrationScope(instances);
                scopes.Add(scope);
                return scope;
            });
            if (registered)
                services.AddScoped<ScopedOrchestration>();
        });
        var run = await functions.DurableTask()
            .WithSelfDiscovery<SelfDiscoveryTest>()
            .WithSelfDiscovery()
            .Orchestrator<string>("ParentProbe", async context =>
            {
                var children = await Task.WhenAll(context.CallSubOrchestratorAsync<string>("ScopedDiscoveryChild", " child "),
                    context.CallSubOrchestratorAsync<string>("ScopedDiscoveryChild", " other "));
                return string.Join(":", children);
            })
            .StartAsync();
        try
        {
            await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput("CHILD:OTHER"));
            Assert.That(instances, Has.Count.EqualTo(2));
            Assert.That(instances.All(instance => instance.ReplayCount > 0 && !instance.Disposed), Is.True);
            Assert.That(scopes, Has.Count.EqualTo(2));
            Assert.That(scopes.All(scope => !scope.Disposed), Is.True);
        }
        finally
        {
            await run.DisposeAsync();
        }

        Assert.That(instances.All(instance => instance.Disposed), Is.True);
        Assert.That(scopes.All(scope => scope.Disposed), Is.True);
    }

    /// <summary>Ensures an explicit root registration overrides a discovered function with the same name.</summary>
    [Test]
    public async Task ExplicitRootOverridesDiscovery()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery()
            .Orchestrator<string, string>(nameof(FailureOrchestration.RecoverWorkflow), (_, _) => Task.FromResult("explicit root"))
            .StartAsync("ignored");
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5), result => result.AssertCompleted().AssertOutput("explicit root"));
    }

    /// <summary>Uses the configured SDK worker converter when a discovered child reads typed input from its context.</summary>
    [Test]
    public async Task DiscoveredChildUsesConfiguredWorkerConverter()
    {
        var converter = new JsonDataConverter(new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery<SelfDiscoveryTest>()
            .ConfigureHostServices(services =>
            {
                services.Configure<DurableTaskWorkerOptions>(options => options.DataConverter = converter);
                services.Configure<DurableTaskClientOptions>(options => options.DataConverter = converter);
            })
            .Orchestrator<DiscoveryPayload, string>("ConverterParent", (context, input) => context.CallSubOrchestratorAsync<string>("ConverterChild", input))
            .StartAsync(new DiscoveryPayload { MyValue = "input value" });
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput("input value"));
    }

    /// <summary>Preserves timer diagnostics when an orchestration reached through discovery schedules a timer.</summary>
    [Test]
    public async Task DiscoveredChildReportsWallClockTimer()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery()
            .Orchestrator<ApprovalRequest, ApprovalDecision>("ApprovalParent", (context, input) =>
                context.CallSubOrchestratorAsync<ApprovalDecision>(nameof(ApprovalOrchestration.AwaitApproval), input, new SubOrchestrationOptions { InstanceId = "discovered-approval" }))
            .StartAsync(new ApprovalRequest(TimeSpan.FromHours(24)));
        run.ExpectSuccess().ExpectLogContains("uses the host's real wall clock");
        await run.WaitForStartAsync();
        await run.Host.Client.RaiseEventAsync("discovered-approval", "Approved", new ApprovalDecision(true));
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput(new ApprovalDecision(true)));
    }

    /// <summary>Retains continuation semantics and preservation warnings in a discovered child.</summary>
    [Test]
    public async Task DiscoveredChildContinuesAndReportsPreservationWarning()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery<SelfDiscoveryTest>()
            .Orchestrator<int>("ContinuationParent", context => context.CallSubOrchestratorAsync<int>("DiscoveryContinuation", 0))
            .StartAsync();
        run.ExpectSuccess().ExpectLogContains("buffered-event preservation is unsupported");
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput(2));
    }

    /// <summary>Allows a discovered child orchestration to complete without returning a value.</summary>
    [Test]
    public async Task DiscoveredChildCanReturnTaskWithoutOutput()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery<SelfDiscoveryTest>()
            .Orchestrator<string>("NoResultParent", async context =>
            {
                await context.CallSubOrchestratorAsync("DiscoveryNoResult");
                return "completed";
            })
            .StartAsync();
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput("completed"));
    }

    /// <summary>Preserves a real activity failure and disposes its scoped dependencies before reporting completion.</summary>
    [Test]
    public async Task FailedInstanceActivityDisposesApplicationScope()
    {
        var journal = new RecordingJournal();
        using var functions = FunctionTester.Create<WorkflowActivities>();
        functions.ConfigureServices(services => services.AddScoped<IWorkflowJournal>(_ => journal));
#pragma warning disable DURABLE2002 // The referenced ProcessItem returns Task<ItemResult>; the worker analyzer incorrectly identifies its result as WorkItem here.
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery()
            .Orchestrator<WorkItem, ItemResult>("InvalidItem", (context, input) => context.CallActivityAsync<ItemResult>(nameof(WorkflowActivities.ProcessItem), input))
            .StartAsync(new WorkItem("INVALID", -1m));
#pragma warning restore DURABLE2002

        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(5), result => result.AssertFailed().AssertFailureCausedBy<ArgumentOutOfRangeException>());
        Assert.That(journal.Disposed, Is.True);
    }

    /// <summary>Composes an additional marker assembly and retains discovery through no-input builder configuration and child registration.</summary>
    /// <param name="registered">Whether activity instances are registered in DI or constructed by the adapter.</param>
    [TestCase(false)]
    [TestCase(true)]
    public async Task AdditionalAssemblyRunsValueTasksWithFreshOwnedInstances(bool registered)
    {
        var probes = new ConcurrentBag<ScopedProbe>();
        var activities = new ConcurrentBag<ProbeActivities>();
        using var functions = FunctionTester.Create<WorkflowActivities>();
        functions.ConfigureServices(services =>
        {
            services.AddScoped(_ =>
            {
                var probe = new ScopedProbe(activities);
                probes.Add(probe);
                return probe;
            });
            if (registered)
                services.AddScoped<ProbeActivities>();
        });
        await using var run = await functions.DurableTask()
            .WithSelfDiscovery<SelfDiscoveryTest>()
            .WithSelfDiscovery()
            .ConfigureHostServices(services => services.AddSingleton(new object()))
            .Orchestrator<string>("DiscoveryProbe", async context =>
            {
#pragma warning disable DURABLE2002 // The worker analyzer does not unwrap ValueTask; these calls exercise the SDK-only discovery adapter.
                string first = await context.CallActivityAsync<string>("DiscoveryValueTask", " first ");
                string second = await context.CallActivityAsync<string>("DiscoveryValueTask", " second ");
                await context.CallActivityAsync("DiscoveryVoidValueTask", "unused");
#pragma warning restore DURABLE2002
                return await context.CallSubOrchestratorAsync<string>("NormalizeChild", $"{first}:{second}");
            })
            .ConfigureHostServices(services => services.AddSingleton(new object()))
            .SubOrchestrator<string, string>("NormalizeChild", (context, input) => context.CallActivityAsync<string>(nameof(ActivityTransforms.NormalizeReference), input))
            .StartAsync();

        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertCompleted().AssertOutput("FIRST:SECOND"));
        Assert.That(probes, Has.Count.EqualTo(2));
        Assert.That(probes.All(probe => probe.Disposed), Is.True);
        Assert.That(activities, Has.Count.EqualTo(2));
        Assert.That(activities.All(activity => activity.Disposed), Is.True);
    }

    /// <summary>Fails on unsupported parameter signatures while enabling discovery, before any SDK host starts.</summary>
    [Test]
    public void UnsupportedSignatureFailsDuringDiscovery()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        Type marker = CreateActivityAssembly("InvalidDiscovery", [("UnsupportedActivity", true)]);
        var error = Assert.Throws<TargetInvocationException>(() => DiscoverMarker(functions.DurableTask(), marker));
        Assert.That(error!.InnerException, Is.TypeOf<NotSupportedException>());
        Assert.That(error.InnerException!.Message, Does.Contain("UnsupportedActivity").And.Contain("FunctionContext/CancellationToken"));
    }

    /// <summary>Reports colliding Function names instead of choosing an arbitrary discovered method.</summary>
    /// <param name="separateAssemblies">Whether the conflicting methods come from different marker assemblies.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void DuplicateNamesFailDuringDiscovery(bool separateAssemblies)
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        Type marker = CreateActivityAssembly("DuplicateDiscovery", separateAssemblies ? [("DuplicateActivity", false)] : [("DuplicateActivity", false), ("DuplicateActivity", false)]);
        object builder = functions.DurableTask();
        if (separateAssemblies)
        {
            builder = DiscoverMarker(builder, marker)!;
            marker = CreateActivityAssembly("SecondDiscovery", [("DuplicateActivity", false)]);
        }
        var error = Assert.Throws<TargetInvocationException>(() => DiscoverMarker(builder, marker));
        Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(error.InnerException!.Message, Does.Contain("DuplicateActivity").And.Contain("Method0"));
    }

    /// <summary>Rejects unsupported orchestration signatures before creating the SDK host.</summary>
    [Test]
    public void UnsupportedOrchestrationSignatureFailsDuringDiscovery()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        Type marker = CreateOrchestrationAssembly("InvalidOrchestration", true);
        var error = Assert.Throws<TargetInvocationException>(() => DiscoverMarker(functions.DurableTask(), marker));
        Assert.That(error!.InnerException, Is.TypeOf<NotSupportedException>());
        Assert.That(error.InnerException!.Message, Does.Contain("DiscoveredChild").And.Contain("TaskOrchestrationContext"));
    }

    /// <summary>Rejects duplicate discovered child names across selected marker assemblies.</summary>
    [Test]
    public void DuplicateOrchestrationNamesFailDuringDiscovery()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        object builder = DiscoverMarker(functions.DurableTask(), CreateOrchestrationAssembly("FirstOrchestration", false))!;
        var error = Assert.Throws<TargetInvocationException>(() => DiscoverMarker(builder, CreateOrchestrationAssembly("SecondOrchestration", false)));
        Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(error.InnerException!.Message, Does.Contain("DiscoveredChild").And.Contain("orchestration"));
    }

    /// <summary>Invokes discovery using a generated assembly marker so invalid fixtures do not contaminate other discovery tests.</summary>
    private static object? DiscoverMarker(object builder, Type marker)
        => builder.GetType().GetMethods().Single(method => method.Name == "WithSelfDiscovery" && method.IsGenericMethodDefinition).MakeGenericMethod(marker).Invoke(builder, null);

    /// <summary>Builds isolated worker-activity fixtures for invalid signature and duplicate-name cases.</summary>
    private static Type CreateActivityAssembly(string name, (string Name, bool Invalid)[] activities)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"{name}-{Guid.NewGuid():N}"), AssemblyBuilderAccess.RunAndCollect);
        var type = assembly.DefineDynamicModule(name).DefineType("DiscoveryFixture", TypeAttributes.Public);
        for (int index = 0; index < activities.Length; index++)
        {
            var method = type.DefineMethod($"Method{index}", MethodAttributes.Public | MethodAttributes.Static, typeof(string),
                activities[index].Invalid ? [typeof(string), typeof(int)] : [typeof(string)]);
            method.SetCustomAttribute(new CustomAttributeBuilder(typeof(FunctionAttribute).GetConstructor([typeof(string)])!, [activities[index].Name]));
            method.DefineParameter(1, ParameterAttributes.None, "input").SetCustomAttribute(new CustomAttributeBuilder(typeof(ActivityTriggerAttribute).GetConstructor(Type.EmptyTypes)!, []));
            method.GetILGenerator().Emit(OpCodes.Ldarg_0);
            method.GetILGenerator().Emit(OpCodes.Ret);
        }

        return type.CreateType()!;
    }

    /// <summary>Builds isolated orchestration fixtures for discovery validation.</summary>
    private static Type CreateOrchestrationAssembly(string name, bool invalid)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"{name}-{Guid.NewGuid():N}"), AssemblyBuilderAccess.RunAndCollect);
        var type = assembly.DefineDynamicModule(name).DefineType("OrchestrationFixture", TypeAttributes.Public);
        var method = type.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Static, typeof(Task<string>),
            invalid ? [typeof(TaskOrchestrationContext), typeof(string)] : [typeof(TaskOrchestrationContext)]);
        method.SetCustomAttribute(new CustomAttributeBuilder(typeof(FunctionAttribute).GetConstructor([typeof(string)])!, ["DiscoveredChild"]));
        method.DefineParameter(1, ParameterAttributes.None, "context").SetCustomAttribute(new CustomAttributeBuilder(typeof(OrchestrationTriggerAttribute).GetConstructor(Type.EmptyTypes)!, []));
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldstr, "value");
        il.Emit(OpCodes.Call, typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(string)));
        il.Emit(OpCodes.Ret);
        return type.CreateType()!;
    }

    /// <summary>Records journal side effects and scoped disposal for real production activities.</summary>
    private sealed class RecordingJournal : IWorkflowJournal, IDisposable
    {
        /// <summary>Gets recorded references.</summary>
        public List<string> References { get; } = [];

        /// <summary>Gets published batches.</summary>
        public List<BatchResult> Results { get; } = [];

        /// <summary>Gets whether this scope-owned journal was disposed.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Record(string reference) => References.Add(reference);

        /// <inheritdoc/>
        public Task PublishAsync(BatchResult result) { Results.Add(result); return Task.CompletedTask; }

        /// <inheritdoc/>
        public void Dispose() => Disposed = true;
    }

    /// <summary>Provides a scoped dependency to distinguish concurrent or successive activity instances.</summary>
    private sealed class ScopedProbe(ConcurrentBag<ProbeActivities> activities) : IDisposable
    {
        /// <summary>Gets the instances created with this dependency.</summary>
        public ConcurrentBag<ProbeActivities> Activities { get; } = activities;

        /// <summary>Gets whether the dependency was disposed.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => Disposed = true;
    }

    /// <summary>Exercises additional-assembly instance discovery, supplied context/tokens and async instance disposal.</summary>
    private sealed class ProbeActivities(ScopedProbe probe) : IAsyncDisposable
    {
        /// <summary>Gets whether the adapter-owned activity instance was disposed.</summary>
        public bool Disposed { get; private set; }

        /// <summary>Reads a supplied FunctionContext and token while its scoped dependency is alive.</summary>
        [Function("DiscoveryValueTask")]
        public ValueTask<string> Transform([ActivityTrigger] string input, FunctionContext context, CancellationToken cancellationToken)
        {
            Assert.That(context.InstanceServices.GetRequiredService<ScopedProbe>(), Is.SameAs(probe));
            Assert.That(context.CancellationToken, Is.EqualTo(cancellationToken));
            Assert.That(probe.Disposed, Is.False);
            probe.Activities.Add(this);
            return ValueTask.FromResult(input.Trim());
        }

        /// <summary>Checks a non-generic ValueTask return from a discovered static method.</summary>
        [Function("DiscoveryVoidValueTask")]
        public static ValueTask Complete([ActivityTrigger] string input) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    /// <summary>Tracks the scoped lifetime used by a discovered instance orchestrator.</summary>
    private sealed class OrchestrationScope(ConcurrentBag<ScopedOrchestration> instances) : IDisposable
    {
        /// <summary>Gets the instance observations.</summary>
        public ConcurrentBag<ScopedOrchestration> Instances { get; } = instances;

        /// <summary>Gets whether the scope has been disposed.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => Disposed = true;
    }

    /// <summary>Runs a discovered instance child with a durable activity and a scoped dependency.</summary>
    private sealed class ScopedOrchestration : IAsyncDisposable
    {
        private readonly OrchestrationScope _scope;

        /// <summary>Initializes an instance and records it for lifetime assertions.</summary>
        public ScopedOrchestration(OrchestrationScope scope)
        {
            _scope = scope;
            scope.Instances.Add(this);
        }

        /// <summary>Gets the number of replay invocations.</summary>
        public int ReplayCount { get; private set; }

        /// <summary>Gets whether the instance was disposed.</summary>
        public bool Disposed { get; private set; }

        /// <summary>Calls a real discovered activity while preserving the scope across replays.</summary>
        [Function("ScopedDiscoveryChild")]
        public async Task<string> Run([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            Assert.That(_scope.Disposed, Is.False);
            Assert.That(Disposed, Is.False);
            if (context.IsReplaying)
                ReplayCount++;
            return await context.CallActivityAsync<string>(nameof(ActivityTransforms.NormalizeReference), context.GetInput<string>());
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    /// <summary>Supplies a field whose custom serialization options must be honored by child discovery.</summary>
    public sealed class DiscoveryPayload
    {
        /// <summary>Gets or sets the test field.</summary>
        public string MyValue = string.Empty;
    }

    /// <summary>Contains a child orchestration using its typed context input.</summary>
    private static class ConverterOrchestration
    {
        /// <summary>Returns the value deserialized through the configured worker converter.</summary>
        [Function("ConverterChild")]
        public static Task<string> Run([OrchestrationTrigger] TaskOrchestrationContext context)
            => Task.FromResult(context.GetInput<DiscoveryPayload>()?.MyValue ?? "missing");
    }

    /// <summary>Exercises continuation and diagnostic forwarding in a discovered child.</summary>
    private static class ContinuationProbe
    {
        /// <summary>Continues until the supplied generation reaches two.</summary>
        [Function("DiscoveryContinuation")]
        public static Task<int> Run([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            int current = context.GetInput<int>();
            if (current < 2)
                context.ContinueAsNew(current + 1, preserveUnprocessedEvents: true);
            return Task.FromResult(current);
        }

        /// <summary>Completes without an orchestration result.</summary>
        [Function("DiscoveryNoResult")]
        public static Task Complete([OrchestrationTrigger] TaskOrchestrationContext context) => Task.CompletedTask;
    }
}
