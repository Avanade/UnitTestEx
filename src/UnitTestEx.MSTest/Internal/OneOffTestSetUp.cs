// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Abstractions;

[assembly: OneOffTestSetUp(typeof(UnitTestEx.MSTest.Internal.OneOffTestSetUp))]

namespace UnitTestEx.MSTest.Internal
{
    /// <summary>
    /// Performs one-off test set up.
    /// </summary>
    public class OneOffTestSetUp : OneOffTestSetUpBase
    {
        /// <inheritdoc/>
        public override void SetUp() => TestFrameworkImplementor.SetGlobalCreateFactory(() => new MSTestImplementor());
    }
}