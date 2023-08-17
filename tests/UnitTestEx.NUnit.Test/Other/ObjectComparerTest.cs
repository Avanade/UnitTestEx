﻿using NUnit.Framework;
using System;
using UnitTestEx.NUnit.Test.Model;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    [Obsolete($"This is being replaced by the {nameof(UnitTestEx.Abstractions.JsonElementComparer)} and usage of paths to ignore (versus members) to be more explicit.")]
    public class ObjectComparerTest
    {
        [Test]
        public void MatchyMatchy()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "Brown" };
            var p2 = new Person { FirstName = "Wendy", LastName = "Brown" };
            ObjectComparer.Assert(p1, p2);
        }

        [Test]
        public void MembersToIgnore_FullName()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "XXX" };
            var p2 = new Person { FirstName = "Wendy", LastName = "YYY" };
            ObjectComparer.Assert(p1, p2, "Person.LastName");
        }

        [Test]
        public void MembersToIgnore_PropertyName()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "XXX" };
            var p2 = new Person { FirstName = "Wendy", LastName = "YYY" };
            ObjectComparer.Assert(p1, p2, "LastName");
        }
    }
}