// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.WebJobs.ServiceBus;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Represents the <see cref="ServiceBusMessageActions"/> status.
    /// </summary>
    public enum ServiceBusMessageActionStatus
    {
        /// <summary>
        /// Indicates that no action occured.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the <b>AbandonMessageAsync</b> was invoked.
        /// </summary>
        Abandon,

        /// <summary>
        /// Indicates that the <b>CompleteMessageAsync</b> was invoked.
        /// </summary>
        Complete,

        /// <summary>
        /// Indicates that the <b>DeadLetterMessageAsync</b> was invoked.
        /// </summary>
        DeadLetter,

        /// <summary>
        /// Indicates that the <b>DeferMessageAsync</b> was invoked.
        /// </summary>
        Defer,

        /// <summary>
        /// Indicates that the <b>RenewMessageLockAsync</b> was invoked.
        /// </summary>
        Renew
    }
}