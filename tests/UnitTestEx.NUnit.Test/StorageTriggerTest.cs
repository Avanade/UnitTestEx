using NUnit.Framework;
using System;
using UnitTestEx.Azure.Functions;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test;

[TestFixture]
public class StorageTriggerTest
{
    [Test]
    public void BlobTrigger_Bytes()
    {
        using var test = FunctionTester.Create<StorageTriggerFunctions>();
        const string content = """{"firstName":"Jane","lastName":"Doe"}""";

        test.Trigger<StorageTriggerFunctions>()
            .Run(f => StorageTriggerFunctions.ProcessBlob(content))
            .AssertValue("""{"firstName":"Jane","lastName":"Doe"}""");
    }

    [Test]
    public void QueueTrigger_Person()
    {
        using var test = FunctionTester.Create<StorageTriggerFunctions>();

        test.Trigger<StorageTriggerFunctions>()
            .Run(f => StorageTriggerFunctions.ProcessQueuePerson(new Person("Jane", "Doe")))
            .AssertValue("Jane Doe");
    }

    [Test]
    public void QueueTrigger_String()
    {
        using var test = FunctionTester.Create<StorageTriggerFunctions>();

        test.Trigger<StorageTriggerFunctions>()
            .Run(f => StorageTriggerFunctions.ProcessQueueString("hello from storage queue"))
            .AssertValue("hello from storage queue");
    }
}
