// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Entities;
using CoreEx.Http;
using CoreEx.Wildcards;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitTestEx.Abstractions;
using UnitTestEx.AspNetCore;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides the ability to set up-front expectations versus post execution asserts.
    /// </summary>
    /// <typeparam name="TResponse">The response value <see cref="Type"/>.</typeparam>
    /// <typeparam name="TOwner">The owning <see cref="HttpTesterBase"/>.</typeparam>
    public class HttpTestExpectations<TResponse, TOwner> : HttpTestExpectations where TOwner : HttpTesterBase
    {
        private Func<TOwner, TResponse>? _expectedValueFunc;
        private bool _expectedNullValue;
        private bool _expectedPrimaryKey;
        private bool _expectedETag;
        private string? _expectedPreviousETag;
        private ChangeLog? _expectedChangeLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpTestExpectations{TResponse, TOwner}"/> class.
        /// </summary>
        /// <param name="tester">The parent/owning <see cref="HttpTesterBase{TSelf}"/>.</param>
        internal HttpTestExpectations(HttpTesterBase tester) : base(tester) { }

        /// <summary>
        /// Gets the members to ignore names list.
        /// </summary>
        public List<string> MembersToIgnore { get; } = new();

        /// <summary>
        /// Expect a <c>null</c> response value.
        /// </summary>
        public void SetExpectNullValue() => _expectedNullValue = true;

        /// <summary>
        /// Expects the <see cref="IPrimaryKey"/> to be implemented and have non-null <see cref="CompositeKey.Args"/>.
        /// </summary>
        public void SetExpectPrimaryKey()
        {
            VerifyImplements<IPrimaryKey>();
            _expectedPrimaryKey = true;
        }

        /// <summary>
        /// Expects the <see cref="IETag"/> to be implemaned and have a non-null value and different to <paramref name="previousETag"/> where specified.
        /// </summary>
        /// <param name="previousETag">The previous ETag value.</param>
        public void SetExpectETag(string? previousETag = null)
        {
            VerifyImplements<IETag>();
            _expectedETag = true;
            _expectedPreviousETag = previousETag;
            MembersToIgnore.Add(nameof(IETag.ETag));
        }

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.CreatedBy"/> and <see cref="ChangeLog.CreatedDate"/> matching the specified values.
        /// </summary>
        /// <param name="createdby">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="createdDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        public void SetExpectedChangeLogCreated(string? createdby = null, DateTime? createdDateGreaterThan = null)
        {
            VerifyImplements<IChangeLog>();
            _expectedChangeLog ??= new ChangeLog();
            _expectedChangeLog.CreatedBy = createdby ?? Tester.Owner.Username;
            _expectedChangeLog.CreatedDate = Cleaner.Clean(createdDateGreaterThan ?? DateTime.UtcNow.Subtract(new TimeSpan(0, 0, 1)));
            MembersToIgnore.Add(nameof(IChangeLog.ChangeLog));
        }

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.UpdatedBy"/> and <see cref="ChangeLog.UpdatedDate"/> matching the specified values.
        /// </summary>
        /// <param name="updatedBy">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="updatedDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        public void SetExpectedChangeLogUpdated(string? updatedBy = null, DateTime? updatedDateGreaterThan = null)
        {
            VerifyImplements<IChangeLog>();
            _expectedChangeLog ??= new ChangeLog();
            _expectedChangeLog.UpdatedBy = updatedBy ?? Tester.Owner.Username;
            _expectedChangeLog.UpdatedDate = Cleaner.Clean(updatedDateGreaterThan ?? DateTime.UtcNow.Subtract(new TimeSpan(0, 0, 1)));
            MembersToIgnore.Add(nameof(IChangeLog.ChangeLog));
        }

        /// <summary>
        /// Verifies implements <see cref="Type"/>.
        /// </summary>
        private static void VerifyImplements<T>()
        {
            if (typeof(TResponse).GetInterface(typeof(T).Name) != null)
                throw new InvalidOperationException($"{nameof(TResponse)} must implement the interface {typeof(T).Name}.");
        }

        /// <summary>
        /// Expect a response comparing the result of the specified <paramref name="expectedValueFunc"/> (and optionally any additional <paramref name="membersToIgnore"/> from the comparison).
        /// </summary>
        /// <param name="expectedValueFunc">The function to generate the response value to compare.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        public void SetExpectedValue(Func<TOwner, TResponse>? expectedValueFunc, params string[] membersToIgnore)
        {
            _expectedValueFunc = expectedValueFunc;
            MembersToIgnore.AddRange(membersToIgnore);
        }

        /// <inheritdoc/>
        public override void Assert(HttpResult noValueResult)
        {
            base.Assert(noValueResult);
            var result = Convert(noValueResult);

            if (_expectedNullValue)
                Tester.Implementor.AssertAreEqual(default!, result.Value, "ExpectNullValue");

            if (result == null && (_expectedPrimaryKey || _expectedETag || _expectedChangeLog != null || _expectedValueFunc != null))
                Tester.Implementor.AssertFail($"Expected non-null response; no response returned.");

            if (_expectedPrimaryKey)
            {
                if ((result!.Value is not IPrimaryKey pk) || pk.PrimaryKey.Args == null || pk.PrimaryKey.Args.Length == 0 || pk.PrimaryKey.Args.Any(x => x == null))
                    Tester.Implementor.AssertFail($"Expected {nameof(IPrimaryKey)}.{nameof(IPrimaryKey.PrimaryKey)}.{nameof(CompositeKey.Args)} array to have no null values.");
            }

            if (_expectedETag)
            {
                var et = result!.Value as IETag;
                if (et == null || et!.ETag == null)
                    Tester.Implementor.AssertFail($"Expected {nameof(IETag)}.{nameof(IETag.ETag)} to have a non-null value.");

                if (!string.IsNullOrEmpty(_expectedPreviousETag) && _expectedPreviousETag == et!.ETag)
                    Tester.Implementor.AssertFail("Expected IETag.ETag value is the same as previous.");
            }

            if (_expectedChangeLog != null)
            {
                var cl = result!.Value as IChangeLog;
                if (cl == null || cl.ChangeLog == null)
                    Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)} to have a non-null value.");

                if (_expectedChangeLog.CreatedBy != null || cl!.ChangeLog!.CreatedBy != null)
                {
                    var wcr = Wildcard.BothAll.Parse(_expectedChangeLog.CreatedBy).ThrowOnError();
                    if (cl?.ChangeLog?.CreatedBy == null || !wcr.CreateRegex().IsMatch(cl!.ChangeLog!.CreatedBy))
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)}.{nameof(ChangeLog.CreatedBy)} '{_expectedChangeLog.CreatedBy}'; actual '{cl?.ChangeLog?.CreatedBy}'.");
                }

                if (!cl!.ChangeLog!.CreatedDate.HasValue)
                    Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.CreatedDate)} to have a non-null value.");
                else if (cl.ChangeLog.CreatedDate.Value < _expectedChangeLog!.CreatedDate!.Value)
                    Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.CreatedDate)} actual '{cl.ChangeLog.CreatedDate.Value}' to be greater than expected '{_expectedChangeLog!.CreatedDate!.Value}'.");

                if (_expectedChangeLog.UpdatedBy != null || cl!.ChangeLog!.UpdatedBy != null)
                {
                    var wcr = Wildcard.BothAll.Parse(_expectedChangeLog.UpdatedBy).ThrowOnError();
                    if (cl?.ChangeLog?.UpdatedBy == null || !wcr.CreateRegex().IsMatch(cl!.ChangeLog!.UpdatedBy))
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.UpdatedBy)} '{_expectedChangeLog.UpdatedBy}'; actual '{cl?.ChangeLog?.UpdatedBy}'.");
                }

                if (!cl!.ChangeLog!.UpdatedDate.HasValue)
                    Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.UpdatedDate)} to have a non-null value.");
                else if (cl.ChangeLog.UpdatedDate.Value < _expectedChangeLog!.UpdatedDate!.Value)
                    Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.UpdatedDate)} actual '{cl.ChangeLog.UpdatedDate.Value}' to be greater than expected '{_expectedChangeLog!.UpdatedDate!.Value}'.");
            }

            if (_expectedValueFunc != null)
            {
                var expectedValue = _expectedValueFunc((TOwner)Tester) ?? throw new InvalidOperationException("ExpectValue function must not return null.");
                var cr = ObjectComparer.Compare(expectedValue, result!.Value, MembersToIgnore.ToArray());
                if (!cr.AreEqual)
                    Tester.Implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }
        }

        /// <summary>
        /// Convert <see cref="HttpResult"/> into <see cref="HttpResult{T}"/>.
        /// </summary>
        private HttpResult<TResponse> Convert(HttpResult result)
        {
            var type = typeof(TResponse);
            var cr = type.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollectionResult<,>)).SingleOrDefault();
            if (cr == null)
                return HttpResult.CreateAsync<TResponse>(result.Response, Tester.JsonSerializer).GetAwaiter().GetResult();

            var types = new Type[] { type, cr.GetGenericArguments()[0], cr.GetGenericArguments()[1] };
            var mi = typeof(HttpResult).GetMethod(nameof(HttpResult.CreateAsync), 3, types);
            return (HttpResult<TResponse>)mi!.MakeGenericMethod(types).Invoke(null, new object?[] { result.Response })!;
        }
    }
}