// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a base class for one-off test set up.
    /// </summary>
    /// <remarks>The inheriting class must also reference via the <see cref="OneOffTestSetUpAttribute"/> to enable.</remarks>
    public abstract class OneOffTestSetUpBase
    {
        /// <summary>
        /// Executes the one-off test set up.
        /// </summary>
        public abstract void SetUp();
    }
}