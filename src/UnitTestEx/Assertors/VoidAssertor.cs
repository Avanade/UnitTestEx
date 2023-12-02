// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the test assert helper where there is no return value; i.e. <see cref="void"/>.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public class VoidAssertor(TesterBase owner, Exception? exception) : AssertorBase<VoidAssertor>(owner, exception) { }
}