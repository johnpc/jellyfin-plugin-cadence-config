using System;

namespace Jellyfin.Plugin.CadenceConfig.Api
{
    /// <summary>
    /// Decides whose data a per-user request may touch. The user comes from the VERIFIED sign-in,
    /// never from a query parameter alone: a <c>userId</c> the client sends is accepted only when it
    /// names the caller. (It used to be trusted as given, so any signed-in user could act on another
    /// user's account.)
    /// </summary>
    public static class CallerScope
    {
        /// <summary>
        /// Resolves the user a request acts for.
        /// </summary>
        /// <param name="caller">The authenticated caller's id (empty when the token carries no user, e.g. an API key).</param>
        /// <param name="requested">The <c>userId</c> the client sent, or empty when it sent none.</param>
        /// <returns>The user to act for, or <c>null</c> when the request must be refused.</returns>
        public static Guid? Resolve(Guid caller, Guid requested)
        {
            if (caller == Guid.Empty)
            {
                return null;
            }

            return requested == Guid.Empty || requested == caller ? caller : null;
        }
    }
}
