using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides per tester arguments that can be used to control the tester behavior.
    /// </summary>
    public class TesterArgs
    {
        /// <summary>
        /// Indicates whether to bypass the execution of the configured run actions.
        /// </summary>
        /// <remarks>The run actions are: <see cref="TesterBase.PreRunActions"/>, <see cref="TesterBase.PostRunBeforeExpectationsActions"/> and <see cref="TesterBase.PostRunAfterExpectationsActions"/>.</remarks>
        public bool BypassRunActions { get; set; }
    }
}