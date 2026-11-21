namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>How a login attempt ended — one explicit outcome, never a nullable token plus flags.</summary>
    public enum LoginOutcome
    {
        /// <summary>Credentials accepted; <see cref="LoginResult.Token"/> is set.</summary>
        Ok,

        /// <summary>Storyteller rejected the username/password (401).</summary>
        BadCredentials,

        /// <summary>Storyteller could not be reached or answered with an error.</summary>
        Unreachable,
    }
}
