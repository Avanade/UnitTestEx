// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Entities;
using CoreEx.Wildcards;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitTestEx.Abstractions;
using UnitTestEx.AspNetCore;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides the ability to set up-front response <i>value</i> expectations versus post execution asserts.
    /// </summary>
    /// <typeparam name="TContext">The test value context.</typeparam>
    /// <typeparam name="TValue">The response value <see cref="Type"/>.</typeparam>
    public class ResponseValueExpectations<TContext, TValue> 
    {
        private readonly object _testContext;
        private Func<object, TValue>? _expectedValueFunc;
        private bool _expectedNullValue;
        private bool _expectedId;
        private object? _expectedIdValue;
        private bool _expectedPrimaryKey;
        private CompositeKey? _expectedPrimaryKeyValue;
        private bool _expectedETag;
        private string? _expectedPreviousETag;
        private ChangeLog? _expectedChangeLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseValueExpectations{TContext, TValue}"/> class.
        /// </summary>
        /// <param name="tester">The parent/owning <see cref="TesterBase"/>.</param>
        /// <param name="testContext">The text context (for example <see cref="HttpTesterBase"/>).</param>
        public ResponseValueExpectations(TesterBase tester, TContext testContext)
        {
            Tester = tester;
            _testContext = testContext ?? new object();
        }

        /// <summary>
        /// Gets the <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Tester { get; }

        /// <summary>
        /// Gets the members to ignore names list.
        /// </summary>
        public List<string> MembersToIgnore { get; } = new();

        /// <summary>
        /// Expect a <c>null</c> response value.
        /// </summary>
        public void SetExpectNullValue() => _expectedNullValue = true;

        /// <summary>
        /// Expects the <see cref="IIdentifier"/> to be implemented and have non-default <see cref="IIdentifier.Id"/>.
        /// </summary>
        public void SetExpectIdentifier(object? id = null)
        {
            VerifyImplements<IIdentifier>();
            _expectedId = true;
            _expectedIdValue = id;
            MembersToIgnore.Add(nameof(IIdentifier.Id));
        }

        /// <summary>
        /// Expects the <see cref="IPrimaryKey"/> to be implemented and have non-null <see cref="CompositeKey.Args"/>.
        /// </summary>
        public void SetExpectPrimaryKey(CompositeKey? primaryKey = null)
        {
            VerifyImplements<IPrimaryKey>();
            _expectedPrimaryKey = true;
            _expectedPrimaryKeyValue = primaryKey;
            MembersToIgnore.Add(nameof(IPrimaryKey.PrimaryKey));
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
        /// <param name="createdDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than or equal to; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        public void SetExpectChangeLogCreated(string? createdby = null, DateTime? createdDateGreaterThan = null)
        {
            VerifyImplements<IChangeLog>();
            _expectedChangeLog ??= new ChangeLog();
            _expectedChangeLog.CreatedBy = createdby ?? Tester.Username;
            _expectedChangeLog.CreatedDate = Cleaner.Clean(createdDateGreaterThan ?? DateTime.UtcNow.Subtract(new TimeSpan(0, 0, 1)));
            MembersToIgnore.Add(nameof(IChangeLog.ChangeLog));
        }

        /// <summary>
        /// Expects the <see cref="IChangeLog"/> to be implemented for the response with generated values for the underlying <see cref="ChangeLog.UpdatedBy"/> and <see cref="ChangeLog.UpdatedDate"/> matching the specified values.
        /// </summary>
        /// <param name="updatedBy">The specific <see cref="ChangeLog.CreatedBy"/> value where specified (can include wildcards); otherwise, indicates to check for user running the test (see <see cref="Abstractions.TesterBase.Username"/>).</param>
        /// <param name="updatedDateGreaterThan">The <see cref="DateTime"/> in which the <see cref="ChangeLog.CreatedDate"/> should be greater than or equal to; where <c>null</c> it will default to <see cref="DateTime.Now"/>.</param>
        public void SetExpectChangeLogUpdated(string? updatedBy = null, DateTime? updatedDateGreaterThan = null)
        {
            VerifyImplements<IChangeLog>();
            _expectedChangeLog ??= new ChangeLog();
            _expectedChangeLog.UpdatedBy = updatedBy ?? Tester.Username;
            _expectedChangeLog.UpdatedDate = Cleaner.Clean(updatedDateGreaterThan ?? DateTime.UtcNow.Subtract(new TimeSpan(0, 0, 1)));
            MembersToIgnore.Add(nameof(IChangeLog.ChangeLog));
        }

        /// <summary>
        /// Verifies implements <see cref="Type"/>.
        /// </summary>
        private static void VerifyImplements<T>()
        {
            if (typeof(TValue).GetInterface(typeof(T).FullName ?? typeof(T).Name) == null)
                throw new InvalidOperationException($"{nameof(TValue)} must implement the interface {typeof(T).Name}.");
        }

        /// <summary>
        /// Expect a response comparing the result of the specified <paramref name="expectedValueFunc"/> (and optionally any additional <paramref name="membersToIgnore"/> from the comparison).
        /// </summary>
        /// <param name="expectedValueFunc">The function to generate the response value to compare.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        public void SetExpectValue(Func<object, TValue> expectedValueFunc, params string[] membersToIgnore)
        {
            _expectedValueFunc = expectedValueFunc;
            MembersToIgnore.AddRange(membersToIgnore);
        }

        /// <inheritdoc/>
        public void Assert(TValue? value)
        {
            if (_expectedNullValue && value is not null)
                Tester.Implementor.AssertFail("Expected a null value; actual non-null.");

            if (value == null && (_expectedId || _expectedPrimaryKey || _expectedETag || _expectedChangeLog != null || _expectedValueFunc != null))
                Tester.Implementor.AssertFail($"Expected non-null response; no response returned.");

            if (_expectedId)
            {
                var id = ((IIdentifier)value!).Id;
                if (id == null)
                    Tester.Implementor.AssertFail($"Expected {nameof(IIdentifier)}.{nameof(IIdentifier.Id)} to have a non-null value.");

                if (_expectedIdValue == null)
                {
                    if (System.Collections.Comparer.Default.Compare(id, id!.GetType().IsClass ? null! : Activator.CreateInstance(id!.GetType())) == 0)
                        Tester.Implementor.AssertFail($"Expected {nameof(IIdentifier)}.{nameof(IIdentifier.Id)} to have a non-default value.");
                }
                else
                    Tester.Implementor.AssertAreEqual(_expectedIdValue, id, $"Expected {nameof(IIdentifier)}.{nameof(IIdentifier.Id)} value of '{_expectedIdValue}'; actual '{id}'.");
            }

            if (_expectedPrimaryKey)
            {
                if (value is not IPrimaryKey pk || pk.PrimaryKey.Args == null || pk.PrimaryKey.IsInitial)
                    Tester.Implementor.AssertFail($"Expected {nameof(IPrimaryKey)}.{nameof(IPrimaryKey.PrimaryKey)}.{nameof(CompositeKey.Args)} to have one or more non-default values.");

                if (_expectedPrimaryKeyValue.HasValue && value is IPrimaryKey pk2)
                    Tester.Implementor.AssertAreEqual(_expectedPrimaryKeyValue.Value, pk2.PrimaryKey, $"Expected {nameof(IPrimaryKey)}.{nameof(IPrimaryKey.PrimaryKey)} value of '{_expectedPrimaryKeyValue.Value}'; actual '{pk2.PrimaryKey}'.");
            }

            if (_expectedETag)
            {
                var et = value as IETag;
                if (et == null || et!.ETag == null)
                    Tester.Implementor.AssertFail($"Expected {nameof(IETag)}.{nameof(IETag.ETag)} to have a non-null value.");

                if (_expectedPreviousETag != null)
                    Tester.Implementor.AssertAreEqual(_expectedPreviousETag, et!.ETag, $"Expected {nameof(IETag)}.{nameof(IETag.ETag)} value of '{_expectedPreviousETag}'; actual '{et!.ETag}'.");
            }

            if (_expectedChangeLog != null)
            {
                var cl = value as IChangeLog;
                if (cl == null || cl.ChangeLog == null)
                    Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)} to have a non-null value.");

                if (_expectedChangeLog.CreatedBy != null)
                {
                    if (cl!.ChangeLog!.CreatedBy == null)
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)}.{nameof(ChangeLog.CreatedBy)} value of '{_expectedChangeLog.CreatedBy}'; actual was null.");
                    else
                    {
                        var wcr = Wildcard.BothAll.Parse(_expectedChangeLog.CreatedBy).ThrowOnError();
                        if (cl?.ChangeLog?.CreatedBy == null || !wcr.CreateRegex().IsMatch(cl!.ChangeLog!.CreatedBy))
                            Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)}.{nameof(ChangeLog.CreatedBy)} value of '{_expectedChangeLog.CreatedBy}'; actual '{cl?.ChangeLog?.CreatedBy}'.");
                    }
                }

                if (_expectedChangeLog.CreatedDate != null)
                {
                    if (!cl!.ChangeLog!.CreatedDate.HasValue)
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.CreatedDate)} to have a non-null value.");
                    else if (cl!.ChangeLog!.CreatedDate.Value < _expectedChangeLog.CreatedDate.Value)
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.CreatedDate)} value of '{_expectedChangeLog.CreatedDate.Value}'; actual '{cl.ChangeLog.CreatedDate.Value}' must be greater than or equal to expected.");
                }

                if (_expectedChangeLog.UpdatedBy != null)
                {
                    if (cl!.ChangeLog!.UpdatedBy == null)
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)}.{nameof(ChangeLog.UpdatedBy)} value of '{_expectedChangeLog.UpdatedBy}'; actual was null.");
                    else
                    {
                        var wcr = Wildcard.BothAll.Parse(_expectedChangeLog.UpdatedBy).ThrowOnError();
                        if (cl?.ChangeLog?.UpdatedBy == null || !wcr.CreateRegex().IsMatch(cl!.ChangeLog!.UpdatedBy))
                            Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(ChangeLog)}.{nameof(ChangeLog.UpdatedBy)} value of '{_expectedChangeLog.UpdatedBy}'; actual '{cl?.ChangeLog?.UpdatedBy}'.");
                    }
                }

                if (_expectedChangeLog.UpdatedDate != null)
                {
                    if (!cl!.ChangeLog!.UpdatedDate.HasValue)
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.UpdatedDate)} to have a non-null value.");
                    else if (cl!.ChangeLog!.UpdatedDate.Value < _expectedChangeLog.UpdatedDate.Value)
                        Tester.Implementor.AssertFail($"Expected {nameof(IChangeLog)}.{nameof(IChangeLog.ChangeLog)}.{nameof(ChangeLog.UpdatedDate)} value of '{_expectedChangeLog.UpdatedDate.Value}'; actual '{cl.ChangeLog.UpdatedDate.Value}' must be greater than or equal to expected.");
                }
            }

            if (_expectedValueFunc != null)
            {
                var expectedValue = _expectedValueFunc(_testContext) ?? throw new InvalidOperationException("ExpectValue function must not return null.");
                var cr = ObjectComparer.Compare(expectedValue, value, MembersToIgnore.ToArray());
                if (!cr.AreEqual)
                    Tester.Implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }
        }
    }
}