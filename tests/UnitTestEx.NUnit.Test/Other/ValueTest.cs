﻿using CoreEx.Entities;
using CoreEx.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net.Http;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ValueTest
    {
        [Test]
        public void CollectionResultValue()
        {
            var res = new HttpResponseMessage { Content = new StringContent("[{\"name\":\"jane\"},{\"name\":\"john\"}]") };
            var pcr = HttpResponseExpectations.GetValueFromHttpResponseMessage<PersonCollectionResult>(res, JsonSerializer.Default);
            Assert.IsNotNull(pcr);
            Assert.IsNotNull(pcr.Items);
            Assert.AreEqual(2, pcr.Items.Count);
            Assert.AreEqual("jane", pcr.Items[0].Name);
            Assert.AreEqual("john", pcr.Items[1].Name);
            Assert.IsNull(pcr.Paging);
        }

        public class Person
        {
            public string Name { get; set; }
        }

        public class PersonCollection : List<Person> { }

        public class PersonCollectionResult : CollectionResult<PersonCollection, Person> { }
    }
}