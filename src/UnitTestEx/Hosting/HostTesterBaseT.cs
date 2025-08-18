using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides the base host unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TService">The host/service <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="HostTesterBase{THost, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
    public class HostTesterBase<TService, TSelf>(TesterBase owner, IServiceProvider serviceProvider) : HostTesterBase<TService>(owner, serviceProvider) where TService : class where TSelf : HostTesterBase<TService, TSelf>
    {
    }
}