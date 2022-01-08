// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// An <see cref="IAuthorizationHandler"/> to <b>bypass</b> authorization checking; i.e. act as anonymous.
    /// </summary>
    public class BypassAuthorizationHandler : IAuthorizationHandler
    {
        /// <summary>
        /// Handle by succeeding all checks.
        /// </summary>
        /// <param name="context">The <see cref="AuthorizationHandlerContext"/>.</param>
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (IAuthorizationRequirement requirement in context.PendingRequirements.ToList())
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}