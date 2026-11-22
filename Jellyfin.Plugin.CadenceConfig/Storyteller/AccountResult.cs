namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The outcome of an invite or account-creation call plus its payload (invite key or bearer token).</summary>
    /// <param name="Outcome">What happened.</param>
    /// <param name="Value">The invite key or the new account's token when <see cref="AccountOutcome.Ok"/>.</param>
    public sealed record AccountResult(AccountOutcome Outcome, string? Value);
}
