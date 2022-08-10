using CoreEx.Validation;
using UnitTestEx.Api.Models;
using UnitTestEx.Expectations;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test.Other
{
    public class ValidationTest : UnitTestBase
    {
        public ValidationTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Validate_Success()
        {
            using var test = CreateValidationTester();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .Run<IValidator<Person>, Person>(new Person { Id = 1, FirstName = "Bob" })
                .AssertSuccess();
        }

        [Fact]
        public void Validate_Success2()
        {
            using var test = CreateValidationTester();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .ExpectSuccess()
                .Run<IValidator<Person>, Person>(new Person { Id = 1, FirstName = "Bob" });
        }

        [Fact]
        public void Validate_Error()
        {
            using var test = CreateValidationTester();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .Run<IValidator<Person>, Person>(new Person())
                .AssertErrors(
                    "Identifier is required.",
                    "First Name is required.");
        }

        [Fact]
        public void Validate_Error2()
        {
            using var test = CreateValidationTester();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .ExpectErrors(
                    "Identifier is required.",
                    "First Name is required.")
                .Run<IValidator<Person>, Person>(new Person());
        }
    }

    public class PersonValidator : Validator<Person>
    {
        public PersonValidator()
        {
            Property(x => x.Id).Mandatory();
            Property(x => x.FirstName).Mandatory();
        }
    }
}