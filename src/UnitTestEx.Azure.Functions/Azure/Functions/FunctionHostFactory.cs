// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from: https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/Microsoft.Extensions.HostFactoryResolver/src/HostFactoryResolver.cs

using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Executes an application's entry point (e.g. top-level statements) and captures the <see cref="IHost"/> at <c>Build()</c>, aborting before <c>Run()</c>; i.e. the worker is never started.
    /// </summary>
    internal sealed class FunctionHostFactory : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private static readonly AsyncLocal<FunctionHostFactory?> _current = new();
        private readonly TaskCompletionSource<IHost> _host = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<IHostBuilder>? _configure;
        private IDisposable? _hostingSubscription;
        private bool _abandoned;

        /// <summary>
        /// Creates a host factory with optional host-builder customization.
        /// </summary>
        /// <param name="configure">The callback used to configure the host builder, if provided.</param>
        private FunctionHostFactory(Action<IHostBuilder>? configure) => _configure = configure;

        /// <summary>
        /// Indicates whether the <paramref name="assembly"/> has an entry point that can be used to build the host.
        /// </summary>
        public static bool CanBuildHost(Assembly assembly) => assembly.EntryPoint is not null;

        /// <summary>
        /// Builds (but does not start) the <see cref="IHost"/> by invoking the <paramref name="assembly"/> entry point.
        /// </summary>
        public static IHost BuildHost(Assembly assembly, string[] args, Action<IHostBuilder>? configure = null, TimeSpan? timeout = null)
        {
            var entryPoint = assembly.EntryPoint ?? throw new InvalidOperationException($"Assembly '{assembly.GetName().Name}' has no entry point; unable to build the host.");
            var factory = new FunctionHostFactory(configure);
            using var subscription = DiagnosticListener.AllListeners.Subscribe(factory);
            var thread = new Thread(() =>
            {
                try
                {
                    _current.Value = factory;
                    entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0 ? null : [args]);
                    factory._host.TrySetException(new InvalidOperationException($"The entry point for assembly '{assembly.GetName().Name}' exited without building an {nameof(IHost)}."));
                }
                catch (TargetInvocationException ex) when (ex.InnerException is HostAbortedException && (factory._abandoned || factory._host.Task.IsCompletedSuccessfully))
                {
                    // Expected; the HostBuilt callback deliberately aborts before the application's Run call.
                }
                catch (TargetInvocationException ex)
                {
                    factory._host.TrySetException(ex.InnerException ?? ex);
                }
                catch (Exception ex)
                {
                    factory._host.TrySetException(ex);
                }
                finally
                {
                    _current.Value = null;
                    factory._hostingSubscription?.Dispose();
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
            var wait = timeout ?? TimeSpan.FromSeconds(30);
            if (!thread.Join(wait))
            {
                lock (factory._host)
                {
                    factory._abandoned = true;
                    if (factory._host.Task.IsCompletedSuccessfully)
                        factory._host.Task.GetAwaiter().GetResult().Dispose();
                }

                throw new TimeoutException($"The entry point for assembly '{assembly.GetName().Name}' did not build its host within {wait}.");
            }

            return factory._host.Task.GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
        {
            if (_current.Value == this && value.Name == "Microsoft.Extensions.Hosting")
                _hostingSubscription = value.Subscribe(this);
        }

        /// <inheritdoc/>
        void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> value)
        {
            if (_current.Value != this)
                return;

            if (value.Key == "HostBuilding")
                _configure?.Invoke((IHostBuilder)value.Value!);
            else if (value.Key == "HostBuilt")
            {
                var host = (IHost)value.Value!;
                lock (_host)
                {
                    if (_abandoned)
                        host.Dispose();
                    else
                        _host.SetResult(host);
                }

                throw new HostAbortedException();
            }
        }

        /// <inheritdoc/>
        void IObserver<DiagnosticListener>.OnCompleted() { }

        /// <inheritdoc/>
        void IObserver<KeyValuePair<string, object?>>.OnCompleted() => _hostingSubscription?.Dispose();

        /// <inheritdoc/>
        void IObserver<DiagnosticListener>.OnError(Exception error) { }

        /// <inheritdoc/>
        void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) => _host.TrySetException(error);
    }
}
