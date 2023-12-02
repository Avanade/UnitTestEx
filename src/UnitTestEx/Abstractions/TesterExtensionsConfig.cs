// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Expectations;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the <see cref="TestSetUp.Extensions"/> configuration used by the <see cref="TesterBase"/>.
    /// </summary>
    public abstract class TesterExtensionsConfig
    {
        /// <summary>
        /// Occurs when the <see cref="TesterBase{TSelf}.UseSetUp(TestSetUp)"/> is invoked (just prior to <see cref="TesterBase{TSelf}.ResetHost(bool)"/> invocation).
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        public virtual void OnUseSetUp(TesterBase owner) { }

        /// <summary>
        /// Provides the opportunity to further configure the underlying test host <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        public virtual void ConfigureServices(TesterBase owner, IServiceCollection services) { }

        /// <summary>
        /// Updates the <paramref name="value"/> from the <see cref="HttpResponseMessage"/> (where applicable).
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="value">The deserialized value (override where applicable).</param>
        /// <remarks>The value will have already been deserialized.</remarks>
        public virtual void UpdateValueFromHttpResponseMessage<TValue>(TesterBase owner, HttpResponseMessage response, ref TValue? value) { }

        /// <summary>
        /// Updates the <paramref name="value"/> from the <see cref="IActionResult"/> (where applicable).
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="actionResult">The <see cref="IActionResult"/>.</param>
        /// <param name="value">The deserialized value (override where applicable).</param>
        /// <remarks>The value will have already been deserialized/converted.</remarks>
        public virtual void UpdateValueFromActionResult<TValue>(TesterBase owner, IActionResult actionResult, ref TValue? value) { }

        /// <summary>
        /// Provides the opportunity to extend the <see cref="ExpectationsBase.AssertAsync(AssertArgs)"/> functionality.
        /// </summary>
        /// <typeparam name="TTester">The tester <see cref="Type"/> (see <see cref="ExpectationsArranger{TTester}.Tester"/>).</typeparam>
        /// <param name="expectation">The <see cref="ExpectationsBase"/> being asserted.</param>
        /// <param name="args">The <see cref="AssertArgs"/>.</param>
        public virtual Task ExpectationAssertAsync<TTester>(ExpectationsBase<TTester> expectation, AssertArgs args) => Task.CompletedTask;
    }
}