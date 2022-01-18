using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    public class LoggerTest
    {
        [TestMethod]
        public void Test()
        {
            var l = new MSTestLogger("LoggerTest");

            var scope = l.BeginScope(new Dictionary<string, object>() { { "CorrelationId", "abc" }, { "AltCode", 1234 } });
            l.LogInformation("A single line of {Text}.", "text");
            var scope2 = l.BeginScope(new Dictionary<string, object>() { { "Other", "bananas" } });
            l.LogWarning($"First line of text.{Environment.NewLine}Second line of text.{Environment.NewLine}Third line of text.");
            l.LogInformation("A single line of text.");
            scope2.Dispose();
            l.LogWarning($"First line of text.{Environment.NewLine}Second line of text.{Environment.NewLine}Third line of text.");
            scope.Dispose();
            l.LogInformation("A single line of text.");
        }
    }
}