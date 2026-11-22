using System;
using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Names for Storyteller accounts the plugin creates: <c>jf-&lt;slug&gt;</c> from the Jellyfin
    /// username, with <c>-2</c>, <c>-3</c>… when Storyteller already has that name. Pure.
    /// </summary>
    public static class ProvisionNames
    {
        /// <summary>The prefix every provisioned account carries, so they are recognisable in Storyteller.</summary>
        public const string Prefix = "jf-";

        /// <summary>Longest username we generate (prefix and suffix included).</summary>
        public const int MaxLength = 24;

        /// <summary>How many name variants to try before giving up.</summary>
        public const int MaxAttempts = 5;

        /// <summary>The email Storyteller keys the invite by; nothing is ever sent to it.</summary>
        public const string EmailDomain = "cadence.invalid";

        /// <summary>The username for a Jellyfin user, for the given attempt (1 = plain, 2 = "-2", …).</summary>
        /// <param name="jellyfinUsername">The Jellyfin username.</param>
        /// <param name="attempt">1-based attempt number.</param>
        /// <returns>The Storyteller username.</returns>
        public static string For(string jellyfinUsername, int attempt = 1)
        {
            var slug = Slug(jellyfinUsername);
            if (slug.Length == 0)
            {
                slug = "user";
            }

            var suffix = attempt <= 1 ? string.Empty : "-" + attempt.ToString(CultureInfo.InvariantCulture);
            var room = MaxLength - Prefix.Length - suffix.Length;
            if (slug.Length > room)
            {
                slug = slug[..room].TrimEnd('-');
            }

            return Prefix + slug + suffix;
        }

        /// <summary>The placeholder email for a generated username.</summary>
        /// <param name="username">The Storyteller username.</param>
        /// <returns>The email.</returns>
        public static string EmailFor(string username) => $"{username}@{EmailDomain}";

        /// <summary>Lowercase <c>[a-z0-9-]</c>, runs of anything else collapsed to one dash, no leading/trailing dash.</summary>
        /// <param name="value">Any text.</param>
        /// <returns>The slug (possibly empty).</returns>
        public static string Slug(string? value)
        {
            var sb = new StringBuilder();
            var dash = false;
            foreach (var ch in (value ?? string.Empty).ToLowerInvariant())
            {
                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    sb.Append(ch);
                    dash = false;
                }
                else if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }

            return sb.ToString().TrimEnd('-');
        }
    }
}
