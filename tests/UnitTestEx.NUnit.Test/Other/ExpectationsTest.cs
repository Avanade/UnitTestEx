using CoreEx;
using CoreEx.Entities;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using UnitTestEx.Api.Models;
using UnitTestEx.Expectations;
using NU = NUnit;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ExpectationsTest
    {
        [Test]
        public void ExceptionSuccess_ExpectException()
        {
            var e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectException();

            var ex = Assert.Throws<AssertionException>(() => e.Assert(null));
            Assert.AreEqual("Expected an exception; however, the execution was successful.", ex.Message);

            e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectException<ValidationException>();

            ex = Assert.Throws<AssertionException>(() => e.Assert(null));
            Assert.AreEqual("Expected an exception; however, the execution was successful.", ex.Message);

            e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectException();
            e.Assert(new ValidationException("Error"));
            e.Assert(new DivideByZeroException("Error"));

            e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectException("Error");
            e.Assert(new ValidationException("Error"));
            e.Assert(new DivideByZeroException("Error"));

            ex = Assert.Throws<AssertionException>(() => e.Assert(new ValidationException("Bananas")));
            Assert.IsTrue(ex.Message.Contains("Expected Exception message 'Error' not equal to actual 'Bananas'."), ex.Message);

            e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectException<ValidationException>();
            e.Assert(new ValidationException("Error"));

            ex = Assert.Throws<AssertionException>(() => e.Assert(new DivideByZeroException("Error")));
            Assert.AreEqual("Expected Exception type 'ValidationException' not equal to actual 'DivideByZeroException'.", ex.Message);

            e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectException<ValidationException>("Error");
            e.Assert(new ValidationException("Error"));

            ex = Assert.Throws<AssertionException>(() => e.Assert(new ValidationException("Bananas")));
            Assert.IsTrue(ex.Message.Contains("Expected Exception message 'Error' not equal to actual 'Bananas'."));
        }

        [Test]
        public void ExceptionSuccess_ExpectErrorType()
        {
            var e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectErrorType("ValidationError");

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new DivideByZeroException()));
            Assert.IsTrue(ex.Message.Contains("Expected an ErrorType of ValidationError; however, the exception 'DivideByZeroException' does not implement IExtendedException."), ex.Message);

            e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectErrorType("ValidationError");
            e.Assert(new ValidationException());

            ex = Assert.Throws<AssertionException>(() => e.Assert(new NotFoundException()));
            Assert.IsTrue(ex.Message.Contains("Expected ErrorType 'ValidationError' not equal to actual 'NotFoundError'."), ex.Message);
        }

        [Test]
        public void ExceptionSuccess_ExpectErrors()
        {
            var e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectErrors("Abc", "Def");

            var mic = new CoreEx.Entities.MessageItemCollection();
            mic.AddError("Abc");
            mic.AddError("Def");

            e.Assert(new ValidationException(mic));

            var mic2 = new CoreEx.Entities.MessageItemCollection();
            mic2.AddError("Def");
            mic2.AddError("Xyz");

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new ValidationException(mic2)));
            Assert.IsTrue(ex.Message.Contains($"Expected messages not matched:{Environment.NewLine}  Error: Abc {Environment.NewLine} Actual messages not matched:{Environment.NewLine}  Error: Xyz"), ex.Message);
        }

        [Test]
        public void ExceptionSuccess_ExpectSuccess()
        {
            var e = new ExceptionSuccessExpectations(GenericTester.Create().Implementor);
            e.SetExpectSuccess();
            e.Assert(null);

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new DivideByZeroException("Oops")));
            Assert.AreEqual("Expected success; however, a 'DivideByZeroException' was thrown: Oops", ex.Message);
        }

        [Test]
        public void ResponseValue_ExpectIdentifier_Guid()
        {
            var e = new ResponseValueExpectations<object, Entity<Guid>>(GenericTester.Create(), new object());
            e.SetExpectIdentifier();
            e.Assert(new Entity<Guid> { Id = 1.ToGuid() });

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<Guid>()));
            Assert.AreEqual("Expected IIdentifier.Id to have a non-default value.", ex.Message);

            e.SetExpectIdentifier(1.ToGuid());
            e.Assert(new Entity<Guid> { Id = 1.ToGuid() });

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<Guid> { Id = 2.ToGuid() }));
            Assert.IsTrue(ex.Message.Contains($"Expected IIdentifier.Id value of '{1.ToGuid()}'; actual '{2.ToGuid()}'."), ex.Message);
        }

        [Test]
        public void ResponseValue_ExpectIdentifier_String()
        {
            var e = new ResponseValueExpectations<object, Entity<string>>(GenericTester.Create(), new object());
            e.SetExpectIdentifier();
            e.Assert(new Entity<string> { Id = "A" });

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string>()));
            Assert.AreEqual("Expected IIdentifier.Id to have a non-null value.", ex.Message);

            e.SetExpectIdentifier("A");
            e.Assert(new Entity<string> { Id = "A" });

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { Id = "B" }));
            Assert.IsTrue(ex.Message.Contains("Expected IIdentifier.Id value of 'A'; actual 'B'."), ex.Message);
        }

        [Test]
        public void ResponseValue_ExpectPrimaryKey()
        {
            var e = new ResponseValueExpectations<object, Entity2<string>>(GenericTester.Create(), new object());
            e.SetExpectPrimaryKey();
            e.Assert(new Entity2<string> { Id = "A" });

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity2<string>()));
            Assert.AreEqual("Expected IPrimaryKey.PrimaryKey.Args to have one or more non-default values.", ex.Message);

            e.SetExpectPrimaryKey(new CompositeKey("A"));
            e.Assert(new Entity2<string> { Id = "A" });

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity2<string> { Id = "B" }));
            Assert.IsTrue(ex.Message.Contains("Expected IPrimaryKey.PrimaryKey value of 'A'; actual 'B'."), ex.Message);
        }

        [Test]
        public void ResponseValue_ExpectETag()
        {
            var e = new ResponseValueExpectations<object, Entity<string>>(GenericTester.Create(), new object());
            e.SetExpectETag();
            e.Assert(new Entity<string> { ETag = "A" });

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string>()));
            Assert.AreEqual("Expected IETag.ETag to have a non-null value.", ex.Message);

            e.SetExpectETag("A");
            e.Assert(new Entity<string> { ETag = "B" });

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ETag = "A" }));
            Assert.IsTrue(ex.Message.Contains("Expected IETag.ETag value of 'A' to be different to actual."), ex.Message);
        }

        [Test]
        public void ResponseValue_ExpectExpectedChangeLogCreated()
        {
            var e = new ResponseValueExpectations<object, Entity<string>>(GenericTester.Create(), new object());
            e.SetExpectChangeLogCreated();
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { CreatedBy = "Anonymous", CreatedDate = DateTime.UtcNow } });

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string>()));
            Assert.AreEqual("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit) to have a non-null value.", ex.Message);

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ChangeLog = new ChangeLog() }));
            Assert.AreEqual("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit).CreatedBy value of 'Anonymous'; actual was null.", ex.Message);

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ChangeLog = new ChangeLog { CreatedBy = "Anonymous" } }));
            Assert.AreEqual("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit).CreatedDate to have a non-null value.", ex.Message);

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ChangeLog = new ChangeLog { CreatedBy = "Anonymous", CreatedDate = DateTime.UtcNow.AddMinutes(-1) } }));
            Assert.IsTrue(ex.Message.Contains("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit).CreatedDate value of '") && ex.Message.Contains("' must be greater than or equal to expected."), ex.Message);

            e.SetExpectChangeLogCreated("Banana");
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { CreatedBy = "Banana", CreatedDate = DateTime.UtcNow } });

            e.SetExpectChangeLogCreated("b*");
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { CreatedBy = "Banana", CreatedDate = DateTime.UtcNow } });

            e.SetExpectChangeLogCreated("*");
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { CreatedBy = "Banana", CreatedDate = DateTime.UtcNow } });
        }

        [Test]
        public void ResponseValue_ExpectExpectedChangeLogUpdated()
        {
            var e = new ResponseValueExpectations<object, Entity<string>>(GenericTester.Create(), new object());
            e.SetExpectChangeLogUpdated();
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { UpdatedBy = "Anonymous", UpdatedDate = DateTime.UtcNow } });

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string>()));
            Assert.AreEqual("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit) to have a non-null value.", ex.Message);

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ChangeLog = new ChangeLog() }));
            Assert.AreEqual("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit).UpdatedBy value of 'Anonymous'; actual was null.", ex.Message);

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ChangeLog = new ChangeLog { UpdatedBy = "Anonymous" } }));
            Assert.AreEqual("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit).UpdatedDate to have a non-null value.", ex.Message);

            ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<string> { ChangeLog = new ChangeLog { UpdatedBy = "Anonymous", UpdatedDate = DateTime.UtcNow.AddMinutes(-1) } }));
            Assert.IsTrue(ex.Message.Contains("Expected Change Log (IChangeLogAuditLog.ChangeLogAudit).UpdatedDate value of '") && ex.Message.Contains("' must be greater than or equal to expected."), ex.Message);

            e.SetExpectChangeLogUpdated("Banana");
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { UpdatedBy = "Banana", UpdatedDate = DateTime.UtcNow } });

            e.SetExpectChangeLogUpdated("b*");
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { UpdatedBy = "Banana", UpdatedDate = DateTime.UtcNow } });

            e.SetExpectChangeLogUpdated("*");
            e.Assert(new Entity<string> { ChangeLog = new ChangeLog { UpdatedBy = "Banana", UpdatedDate = DateTime.UtcNow } });
        }

        [Test]
        public void ResponseValue_ExpectValue()
        {
            var val = new Entity<Guid> { Id = 1.ToGuid(), Name = "Bob", ETag = "Xyz", ChangeLog = new ChangeLog { CreatedBy = "Anonymous", CreatedDate = DateTime.UtcNow, UpdatedBy = "Anonymous", UpdatedDate = DateTime.UtcNow.AddDays(1) } };
            var e = new ResponseValueExpectations<object, Entity<Guid>>(GenericTester.Create(), new object());
            e.SetExpectValue(_ => new Entity<Guid> { Name = "Bob" });

            Assert.Throws<AssertionException>(() => e.Assert(val));

            // Explicitly exclude properties.
            e = new ResponseValueExpectations<object, Entity<Guid>>(GenericTester.Create(), new object());
            e.SetExpectValue(_ => new Entity<Guid> { Name = "Bob" }, "Id", "ETag", "ChangeLog");
            e.Assert(val);

            // By expecting the properties they should be automatically excluded from the value comparison.
            e = new ResponseValueExpectations<object, Entity<Guid>>(GenericTester.Create(), new object());
            e.SetExpectValue(_ => new Entity<Guid> { Name = "Bob" });
            e.SetExpectIdentifier();
            e.SetExpectETag();
            e.SetExpectChangeLogCreated();
            e.Assert(val);
        }

        [Test]
        public void ResponseValue_ExpectNull()
        {
            var e = new ResponseValueExpectations<object, Entity<Guid>>(GenericTester.Create(), new object());
            e.SetExpectNullValue();
            e.Assert(null);

            var ex = Assert.Throws<AssertionException>(() => e.Assert(new Entity<Guid>()));
            Assert.AreEqual("Expected a null value; actual non-null.", ex.Message);
        }

        [Test]
        public void HttpResponse_ExpectStatusCode()
        {
            var e = new HttpResponseExpectations(GenericTester.Create());
            e.SetExpectStatusCode(System.Net.HttpStatusCode.OK);

            e.Assert(CoreEx.Http.HttpResult.CreateAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)).GetAwaiter().GetResult());

            var ex = Assert.Throws<AssertionException>(() => e.Assert(CoreEx.Http.HttpResult.CreateAsync(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)).GetAwaiter().GetResult()));
            Assert.AreEqual("Expected StatusCode value of 'OK (200)'; actual was 'NotFound (404)'.", ex.Message);
        }

        [Test]
        public void Event_ExpectNoEvents()
        {
            var t = GenericTester.Create().UseExpectedEvents();
            var ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();

            ep.Publish(new CoreEx.Events.EventData { Subject = "Abc" });
            ep.SendAsync().GetAwaiter().GetResult();

            var e = new EventExpectations(t);
            e.ExpectNoEvents();

            var ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Expected no Event(s); one or more were published.", ex.Message);
        }

        [Test]
        public void Event_Expect_Event()
        {
            var t = GenericTester.Create().UseExpectedEvents();
            var ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();

            ep.Publish(new CoreEx.Events.EventData { Subject = "data.abc", Action = "created" });
            ep.SendAsync().GetAwaiter().GetResult();

            var e = new EventExpectations(t);
            e.Expect(null, "*");
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, "data.abc");
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, "data.def");
            var ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Destination <default>: Expected Event[0].Subject 'data.def' does not match actual 'data.abc'.", ex.Message);

            e = new EventExpectations(t);
            e.Expect(null, "data.abc", "created");
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, "data.abc", "updated");
            ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Destination <default>: Expected Event[0].Action 'updated' does not match actual 'created'.", ex.Message);

            e = new EventExpectations(t);
            e.Expect(null, "data.a*", "c*");
            e.Assert();
        }

        [Test]
        public void Event_Expect_EventSource()
        {
            var t = GenericTester.Create().UseExpectedEvents();
            var ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();

            ep.Publish(new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created" });
            ep.SendAsync().GetAwaiter().GetResult();

            var e = new EventExpectations(t);
            e.Expect(null, "*", "*", "*");
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, "mydata/xyz", "data.abc", "*");
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, "mydata/uvw", "*", "*");
            var ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Destination <default>: Expected Event[0].Source 'mydata/uvw' does not match actual 'mydata/xyz'.", ex.Message);

            e = new EventExpectations(t);
            e.Expect(null, "mydata/xyz", "data.abc", "created");
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, "mydata/x*", "*", "c*");
            e.Assert();
        }

        [Test]
        public void Event_Expect_EventData()
        {
            var t = GenericTester.Create().UseExpectedEvents();
            var ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();

            ep.Publish(new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx" });
            ep.SendAsync().GetAwaiter().GetResult();

            var e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx" });
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "yy" });
            var ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.IsTrue(ex.Message.Contains("Expected.TenantId != Actual.TenantId"), ex.Message);

            e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "yy" }, "TenantId");
            e.Assert();
        }

        [Test]
        public void Event_Expect_EventData_Value()
        {
            var t = GenericTester.Create().UseExpectedEvents();
            var ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();

            ep.Publish(new CoreEx.Events.EventData<Person> { Id = Guid.NewGuid().ToString(), Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx", Value = new Person { Id = 1, FirstName = "Mary", LastName = "Jane" } });
            ep.SendAsync().GetAwaiter().GetResult();

            var e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData<Person> { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx", Value = new Person { Id = 1, FirstName = "Mary", LastName = "Jane" } });
            e.Assert();

            e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData<Person> { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx", Value = new Person { Id = 1, FirstName = "Mary", LastName = "Contrary" } });
            Assert.Throws<AssertionException>(() => e.Assert());

            e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData<Person> { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx", Value = new Person { Id = 1, FirstName = "Mary", LastName = "Contrary" } }, "LastName");
            e.Assert();
        }

        [Test]
        public void Event_Expect_Count()
        {
            var t = GenericTester.Create().UseExpectedEvents();
            var ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();

            var e = new EventExpectations(t);
            e.Expect(null, new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created", TenantId = "xx" });

            var ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Expected Event(s); none were published.", ex.Message);

            ep.PublishNamed("d1", new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created" });
            ep.PublishNamed("d2", new CoreEx.Events.EventData { Source = new Uri("mydata/uvw", UriKind.Relative), Subject = "data.def", Action = "created" });
            ep.SendAsync().GetAwaiter().GetResult();

            ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Expected 1 event destination(s); there were 2.", ex.Message);

            t = GenericTester.Create().UseExpectedEvents();
            ep = t.Services.GetRequiredService<CoreEx.Events.IEventPublisher>();
            ep.PublishNamed("d1", new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created" });
            ep.SendAsync().GetAwaiter().GetResult();

            e = new EventExpectations(t);
            e.Expect("d2", new CoreEx.Events.EventData { Source = new Uri("mydata/xyz", UriKind.Relative), Subject = "data.abc", Action = "created" });
            ex = Assert.Throws<AssertionException>(() => e.Assert());
            Assert.AreEqual("Published event(s) to destination 'd1'; these were not expected.", ex.Message);
        }

        public class Entity<TId> : IIdentifier<TId>, IChangeLog, IETag where TId : IComparable<TId>, IEquatable<TId>
        {
            public TId Id { get; set; }

            public string Name { get; set; }

            public ChangeLog ChangeLog { get; set; }

            public string ETag { get; set; }
        }

        public class Entity2<TId> : IPrimaryKey, IChangeLog, IETag where TId : IComparable<TId>, IEquatable<TId>
        {
            public TId Id { get; set; }

            public string Name { get; set; }

            public CompositeKey PrimaryKey => new CompositeKey(Id);

            public ChangeLog ChangeLog { get; set; }

            public string ETag { get; set; }
        }
    }
}