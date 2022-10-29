// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides <see cref="CoreEx.Events.EventData"/> expectations and matching.
    /// </summary>
    public sealed class EventExpectations
    {
        private readonly Dictionary<string, List<(string? Source, string? Subject, string? Action, EventData? Event, string[] MembersToIgnore)>> _expectedEvents = new();
        private bool _expectNoEvents;
        private bool _expectEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventExpectations"/> class.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        public EventExpectations(TesterBase tester)
        {
            Tester = tester ?? throw new ArgumentNullException(nameof(tester));
            _expectNoEvents = tester.SetUp.ExpectNoEvents;
        }

        /// <summary>
        /// Gets the <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Tester { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor => Tester.Implementor;

        /// <summary>
        /// Expects that no events have been published.
        /// </summary>
        /// <remarks>This is ignored when an expected event has been configured.</remarks>
        public void ExpectNoEvents() => _expectNoEvents = true;

        /// <summary>
        /// Expects that at least one event has been published.
        /// </summary>
        public void ExpectEvents()
        {
            if (!Tester.IsExpectedEventPublisherEnabled)
                throw new NotSupportedException($"The {nameof(TestSetUp)}.{nameof(TestSetUp.ExpectedEventsEnabled)} or TesterBase.UseExpectedEvents must be used before this functionality can be executed; note that enabling will automatically replace the {nameof(IEventPublisher)} to use the {nameof(ExpectedEventPublisher)}.");

            if (_expectedEvents.Count > 0)
                throw new InvalidOperationException($"{nameof(ExpectEvents)} can not be used where one or more explicit events has already been defined.");

            _expectEvents = true;
        }

        /// <summary>
        /// Adds the event into the dictionary.
        /// </summary>
        private void Add(string? destination, (string? Source, string? Subject, string? Action, EventData? Event, string[] MembersToIgnore) @event)
        {
            if (!Tester.IsExpectedEventPublisherEnabled)
                throw new NotSupportedException($"The {nameof(TestSetUp)}.{nameof(TestSetUp.ExpectedEventsEnabled)} or TesterBase.UseExpectedEvents must be used before this functionality can be executed; note that enabling will automatically replace the {nameof(IEventPublisher)} to use the {nameof(ExpectedEventPublisher)}.");

            if (_expectEvents)
                throw new InvalidOperationException($"An explicit {nameof(Expect)} can not be used where {nameof(ExpectEvents)} has already been defined.");

            var key = destination ?? ExpectedEventPublisher.NullKeyName;
            if (_expectedEvents.TryGetValue(key, out var events))
                events.Add(@event);
            else
                _expectedEvents.Add(key, new() { @event });
        }

        /// <summary>
        /// Expects that the corresponding event has been published (in order specified). The expected event <paramref name="subject"/> and <paramref name="action"/> can use wildcards. All other <see cref="EventData"/> properties are not matched/verified.
        /// </summary>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="subject">The expected subject (may contain wildcards).</param>
        /// <param name="action">The expected action (may contain wildcards).</param>
        public void Expect(string? destination, string subject, string? action = "*") => Add(destination, ("*", subject ?? throw new ArgumentNullException(nameof(subject)), action, null, Array.Empty<string>()));

        /// <summary>
        /// Expects that the corresponding event has been published (in order specified). The expected event <paramref name="source"/>, <paramref name="subject"/> and <paramref name="action"/> can use wildcards. All other <see cref="EventData"/> 
        /// properties are not matched/verified.
        /// </summary>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="source">The expected source formatted as a <see cref="Uri"/> (may contain wildcards).</param>
        /// <param name="subject">The expected subject (may contain wildcards).</param>
        /// <param name="action">The expected action (may contain wildcards).</param>
        public void Expect(string? destination, string source, string subject, string? action = "*") => Add(destination, (source ?? throw new ArgumentNullException(nameof(source)), subject ?? throw new ArgumentNullException(nameof(subject)), action, null, Array.Empty<string>()));

        /// <summary>
        /// Expects that the corresponding <paramref name="event"/> has been published (in order specified). All properties for expected event will be compared again the actual.
        /// </summary>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="event">The expected <paramref name="event"/>. Wildcards are supported for <see cref="EventDataBase.Subject"/> and <see cref="EventDataBase.Action"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison. Defaults to <see cref="TestSetUp.ExpectedEventsMembersToIgnore"/>.</param>
        /// <remarks>Wildcards are supported for <see cref="EventDataBase.Subject"/>, <see cref="EventDataBase.Action"/> and <see cref="EventDataBase.Type"/>.</remarks>
        public void Expect(string? destination, EventData @event, params string[] membersToIgnore) => Add(destination, (null, @event?.Subject, @event?.Action, @event ?? throw new ArgumentNullException(nameof(@event)), membersToIgnore));

        /// <summary>
        /// Expects that the corresponding <paramref name="event"/> has been published (in order specified). All properties for expected event will be compared again the actual.
        /// </summary>
        /// <param name="destination">The named destination (e.g. queue or topic).</param>
        /// <param name="source">The expected source formatted as a <see cref="Uri"/> (may contain wildcards).</param>
        /// <param name="event">The expected <paramref name="event"/>. Wildcards are supported for <see cref="EventDataBase.Subject"/> and <see cref="EventDataBase.Action"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison. Defaults to <see cref="TestSetUp.ExpectedEventsMembersToIgnore"/>.</param>
        /// <remarks>Wildcards are supported for <see cref="EventDataBase.Subject"/>, <see cref="EventDataBase.Action"/> and <see cref="EventDataBase.Type"/>.</remarks>
        public void Expect(string? destination, string source, EventData @event, params string[] membersToIgnore) => Add(destination, (source, @event?.Subject, @event?.Action, @event ?? throw new ArgumentNullException(nameof(@event)), membersToIgnore));

        /// <summary>
        /// Verifies that the <see cref="ExpectedEventPublisher"/> events match the <see cref="EventExpectations"/> (in order specified).
        /// </summary>
        public void Assert()
        {
            if (Tester.SharedState.ExpectedEventPublisher == null)
            {
                if (_expectNoEvents)
                    return;
                else
                    throw new InvalidOperationException($"The {nameof(TesterBase.SharedState)}.{nameof(ExpectedEventPublisher)} must not be null; there is an internal issue.");
            }

            if (!Tester.SharedState.ExpectedEventPublisher.IsEmpty)
                Implementor.AssertFail("Expected Event Publish/Send mismatch; there are one or more published events that have not been sent.");

            var names = Tester.SharedState.ExpectedEventPublisher.SentEvents.Keys.ToArray();
            if (_expectNoEvents && !_expectEvents && _expectedEvents.Count == 0 && names.Length > 0)
                Implementor.AssertFail($"Expected no Event(s); one or more were published.");

            if (names.Length == 0 && (_expectEvents || _expectedEvents.Count != 0))
                Implementor.AssertFail($"Expected Event(s); none were published.");

            if (_expectEvents)
                return;

            if (names.Length != _expectedEvents.Count)
                Implementor.AssertFail($"Expected {_expectedEvents.Count} event destination(s); there were {names.Length}.");

            if (names.Length == 1 && _expectedEvents.Count == 1 && _expectedEvents.ContainsKey(ExpectedEventPublisher.NullKeyName))
            {
                var key = _expectedEvents.Keys.First();
                AssertDestination(key, _expectedEvents[key], GetEvents(names[0]).ToList());
                return;
            }

            foreach (var name in names)
            {
                if (_expectedEvents.TryGetValue(name, out var exp))
                    AssertDestination(name, exp, GetEvents(name).ToList());
                else
                    Implementor.AssertFail($"Published event(s) to destination '{name}'; these were not expected.");
            }

            var missing = string.Join(", ", _expectedEvents.Keys.Where(key => !names.Contains(key)).Select(x => $"'{x}'"));
            if (!string.IsNullOrEmpty(missing))
                Implementor.AssertFail($"Expected event(s) to be published to destination(s): {missing}; none were found.");
        }

        /// <summary>
        /// Gets the event from the event storage.
        /// </summary>
        private List<EventData> GetEvents(string? name) => Tester.SharedState.ExpectedEventPublisher!.SentEvents.TryGetValue(name ?? ExpectedEventPublisher.NullKeyName, out var queue) ? queue.ToList() : new();

        /// <summary>
        /// Asserts the events for the destination.
        /// </summary>
        private void AssertDestination(string? destination, List<(string? Source, string? Subject, string? Action, EventData? Event, string[] MembersToIgnore)> expectedEvents, List<EventData> actualEvents)
        {
            if (actualEvents == null)
                throw new ArgumentNullException(nameof(actualEvents));

            if (actualEvents.Count != expectedEvents.Count)
                Implementor.AssertFail($"Destination {destination}: Expected {_expectedEvents.Count} event(s); there were {actualEvents.Count} actual.");

            for (int i = 0; i < actualEvents.Count; i++)
            {
                var act = actualEvents[i]!;
                var exp = expectedEvents[i].Event;
                var wcexp = exp ?? new EventData { Subject = expectedEvents[i].Subject, Action = expectedEvents[i].Action };

                // Assert source, subject, action and type using wildcards where specified.
                if (expectedEvents[i].Source != null && !WildcardMatch(expectedEvents[i].Source!, act.Source?.ToString(), '/'))
                    Implementor.AssertFail($"Destination {destination}: Expected Event[{i}].{nameof(EventDataBase.Source)} '{expectedEvents[i].Source}' does not match actual '{act.Source}'.");

                if (wcexp.Subject != null && !WildcardMatch(wcexp.Subject!, act.Subject?.ToString(), Tester.SetUp.ExpectedEventsEventDataFormatter.SubjectSeparatorCharacter))
                    Implementor.AssertFail($"Destination {destination}: Expected Event[{i}].{nameof(EventDataBase.Subject)} '{wcexp.Subject}' does not match actual '{act.Subject}'.");

                if (wcexp.Action != null && !WildcardMatch(wcexp.Action!, act.Action?.ToString(), char.MinValue))
                    Implementor.AssertFail($"Destination {destination}: Expected Event[{i}].{nameof(EventDataBase.Action)} '{wcexp.Action}' does not match actual '{act.Action}'.");

                if (wcexp.Type != null && !WildcardMatch(wcexp.Type!, act.Type?.ToString(), Tester.SetUp.ExpectedEventsEventDataFormatter.TypeSeparatorCharacter))
                    Implementor.AssertFail($"Destination {destination}: Expected Event[{i}].{nameof(EventDataBase.Type)} '{wcexp.Type}' does not match actual '{act.Type}'.");

                // Where there is *no* expected eventdata then skip comparison.
                if (exp == null)
                    continue;

                // Compare the events.
                var list = new List<string>(Tester.SetUp.ExpectedEventsMembersToIgnore);
                list.AddRange(new string[] { nameof(EventDataBase.Source), nameof(EventDataBase.Subject), nameof(EventDataBase.Action), nameof(EventDataBase.Type) });
                list.AddRange(expectedEvents[i].MembersToIgnore);

                var cr = ObjectComparer.Compare(exp, act, list.ToArray());
                if (!cr.AreEqual)
                    Implementor.AssertFail($"Destination {destination}: Expected event is not equal to actual: {cr.DifferencesString}");
            }
        }

        /// <summary>
        /// Performs a wildcard match on each part of the strings using the <paramref name="separatorCharacter"/> to split.
        /// </summary>
        /// <param name="expected">The expected including wildcards.</param>
        /// <param name="actual">The actual to compare against the expected.</param>
        /// <param name="separatorCharacter">The seperator character.</param>
        /// <returns><c>true</c> where there is a wildcard match; otherwise, <c>false</c>.</returns>
        public bool WildcardMatch(string expected, string? actual, char separatorCharacter)
        {
            if ((expected ?? throw new ArgumentNullException(nameof(expected))) == "*")
                return true;

            var eparts = expected.Split(separatorCharacter);
            if (actual == null)
                return false;

            var aparts = actual.Split(separatorCharacter);

            // Compare each part for an exact match or wildcard.
            for (int i = 0; i < eparts.Length; i++)
            {
                if (i >= aparts.Length)
                    return false;

                if (new string[] { aparts[i] }.WhereWildcard(x => x, eparts[i], ignoreCase: false, wildcard: Tester.SetUp.ExpectedEventsWildcard).FirstOrDefault() == null)
                    return false;
            }

            if (aparts.Length == eparts.Length)
                return true;

            // Where longer make sure last part is a multi wildcard.
            return aparts.Length > eparts.Length && eparts[^1] == new string(new char[] { Tester.SetUp.ExpectedEventsWildcard.MultiWildcard });
        }
    }
}