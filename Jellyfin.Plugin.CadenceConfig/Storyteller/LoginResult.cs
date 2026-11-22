namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>The result of <see cref="StorytellerClient.LoginAsync"/>.</summary>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="Token">The bearer token when <see cref="LoginOutcome.Ok"/>.</param>
    public sealed record LoginResult(LoginOutcome Outcome, string? Token);
}
