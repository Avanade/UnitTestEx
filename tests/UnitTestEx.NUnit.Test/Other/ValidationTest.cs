﻿using CoreEx.Validation;
using NUnit.Framework;
using UnitTestEx.Api.Models;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ValidationTest
    {
        [Test]
        public void Validate_Success()
        {
            using var test = ValidationTester.Create();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .Run<IValidator<Person>, Person>(new Person { Id = 1, FirstName = "Bob" })
                .AssertSuccess();
        }

        [Test]
        public void Validate_Success2()
        {
            using var test = ValidationTester.Create();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .ExpectSuccess()
                .Run<IValidator<Person>, Person>(new Person { Id = 1, FirstName = "Bob" });
        }

        [Test]
        public void Validate_Error()
        {
            using var test = ValidationTester.Create();

            test.ReplaceScoped<IValidator<Person>, PersonValidator>()
                .Run<IValidator<Person>, Person>(new Person())
                .AssertErrors(
                    "Identifier is required.",
                    "First Name is required.");
        }

        [Test]
        public void Validate_Error2()
        {
            using var test = ValidationTester.Create();

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