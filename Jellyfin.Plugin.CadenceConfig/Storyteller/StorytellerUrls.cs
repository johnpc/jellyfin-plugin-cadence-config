using System;
using System.Linq;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Pure URL builders for the Storyteller v2 API (the routes that exist on the 2.14.x server).
    /// Hrefs inside a publication are encoded segment by segment, exactly like Storyteller's own web
    /// reader does, so a chapter path with spaces or unicode round-trips.
    /// </summary>
    public static class StorytellerUrls
    {
        /// <summary>Normalizes the configured base URL (no trailing slash), or null when unusable.</summary>
        /// <param name="baseUrl">The configured Storyteller URL.</param>
        /// <returns>The base, or null.</returns>
        public static string? Base(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            var trimmed = baseUrl.Trim().TrimEnd('/');
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? trimmed
                : null;
        }

        /// <summary>The token (login) endpoint.</summary>
        /// <param name="baseUrl">The normalized base.</param>
        /// <returns>The URL.</returns>
        public static string Token(string baseUrl) => $"{baseUrl}/api/v2/token";

        /// <summary>The book list endpoint.</summary>
        /// <param name="baseUrl">The normalized base.</param>
        /// <returns>The URL.</returns>
        public static string Books(string baseUrl) => $"{baseUrl}/api/v2/books";

        /// <summary>The invites endpoint (admin: create an invite for a new user).</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <returns>The URL.</returns>
        public static string Invites(string baseUrl) => $"{baseUrl}/api/v2/invites";

        /// <summary>The users endpoint (accept an invite: creates the account and returns its token).</summary>
        /// <param name="baseUrl">The normalized base URL.</param>
        /// <returns>The URL.</returns>
        public static string Users(string baseUrl) => $"{baseUrl}/api/v2/users";

        /// <summary>The per-user position endpoint for a book.</summary>
        /// <param name="baseUrl">The normalized base.</param>
        /// <param name="bookUuid">The book.</param>
        /// <returns>The URL.</returns>
        public static string Positions(string baseUrl, string bookUuid) => $"{baseUrl}/api/v2/books/{bookUuid}/positions";

        /// <summary>A resource inside the readaloud publication (manifest, chapter, image, audio chunk).</summary>
        /// <param name="baseUrl">The normalized base.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="path">The publication-relative path (slash-separated, unencoded).</param>
        /// <returns>The URL.</returns>
        public static string Read(string baseUrl, string bookUuid, string path)
        {
            return $"{baseUrl}/api/v2/books/{bookUuid}/read/{EncodePath(path)}";
        }

        /// <summary>The guided-navigation document for a chapter.</summary>
        /// <param name="baseUrl">The normalized base.</param>
        /// <param name="bookUuid">The book.</param>
        /// <param name="chapterHref">The chapter href from the manifest's reading order.</param>
        /// <returns>The URL.</returns>
        public static string GuidedNavigation(string baseUrl, string bookUuid, string chapterHref)
        {
            return $"{baseUrl}/api/v2/books/{bookUuid}/read/~readium/guided-navigation.json?ref={EncodePath(chapterHref)}";
        }

        /// <summary>Encodes each path segment, leaving the slashes.</summary>
        /// <param name="path">A slash-separated path.</param>
        /// <returns>The encoded path.</returns>
        public static string EncodePath(string path)
        {
            return string.Join("/", path.Trim('/').Split('/').Select(Uri.EscapeDataString));
        }

        /// <summary>
        /// Resolves a reference found inside a chapter (or its guided-navigation doc) against that
        /// chapter's directory, yielding a publication-root path. Already-rooted refs pass through.
        /// </summary>
        /// <param name="chapterHref">The chapter the reference appeared in.</param>
        /// <param name="reference">The reference (any fragment is dropped).</param>
        /// <returns>The root-relative path.</returns>
        public static string ResolveAgainst(string chapterHref, string reference)
        {
            var hash = reference.IndexOf('#', StringComparison.Ordinal);
            var bare = hash >= 0 ? reference[..hash] : reference;
            if (bare.Length == 0)
            {
                return chapterHref;
            }

            if (!bare.StartsWith("./", StringComparison.Ordinal) && !bare.StartsWith("../", StringComparison.Ordinal))
            {
                return bare.TrimStart('/');
            }

            var dir = chapterHref.Contains('/', StringComparison.Ordinal)
                ? chapterHref[..chapterHref.LastIndexOf('/')].Split('/').ToList()
                : new System.Collections.Generic.List<string>();
            foreach (var segment in bare.Split('/'))
            {
                if (segment == "..")
                {
                    if (dir.Count > 0)
                    {
                        dir.RemoveAt(dir.Count - 1);
                    }
                }
                else if (segment != "." && segment.Length > 0)
                {
                    dir.Add(segment);
                }
            }

            return string.Join("/", dir);
        }
    }
}
