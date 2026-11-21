using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Matches a Jellyfin audiobook (title + author) to a Storyteller book. Pure. Titles are normalized
    /// (case, punctuation, "(Unabridged)"-style suffixes, a subtitle after ':' when only one side has
    /// one); the authors must share at least one name token so "Dune" by Herbert never matches a
    /// different "Dune".
    /// </summary>
    public static partial class BookMatcher
    {
        private static readonly string[] Noise = { "unabridged", "abridged", "a novel", "audiobook", "the audiobook" };

        /// <summary>Picks the Storyteller book for a Jellyfin title/author, or null.</summary>
        /// <param name="title">The Jellyfin book title.</param>
        /// <param name="author">The Jellyfin author (album artist), may be null.</param>
        /// <param name="books">The Storyteller library.</param>
        /// <returns>The match, or null.</returns>
        public static StorytellerBook? Match(string? title, string? author, IEnumerable<StorytellerBook> books)
        {
            var wanted = Normalize(title);
            if (wanted.Length == 0)
            {
                return null;
            }

            var authorTokens = AuthorTokens(author);
            StorytellerBook? titleOnly = null;
            foreach (var book in books)
            {
                if (!TitlesMatch(wanted, Normalize(book.Title)))
                {
                    continue;
                }

                var theirs = book.Authors.SelectMany(a => AuthorTokens(a.Name)).ToHashSet();
                if (authorTokens.Count == 0 || theirs.Count == 0 || authorTokens.Overlaps(theirs))
                {
                    return book;
                }

                titleOnly ??= book;
            }

            return authorTokens.Count == 0 ? titleOnly : null;
        }

        /// <summary>Lowercase, bracketed/noise suffixes removed, punctuation → space, spaces collapsed.</summary>
        /// <param name="title">A title.</param>
        /// <returns>The normalized form.</returns>
        public static string Normalize(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var text = Brackets().Replace(title.ToLowerInvariant(), " ");
            foreach (var noise in Noise)
            {
                text = text.Replace(noise, " ", StringComparison.Ordinal);
            }

            text = Punctuation().Replace(text, " ");
            return Spaces().Replace(text, " ").Trim();
        }

        /// <summary>Significant name tokens of an author ("Towles, Amor" → {towles, amor}; drops initials).</summary>
        /// <param name="author">The author string.</param>
        /// <returns>The tokens.</returns>
        public static HashSet<string> AuthorTokens(string? author)
        {
            return Normalize(author)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1)
                .ToHashSet();
        }

        private static bool TitlesMatch(string a, string b)
        {
            if (a == b)
            {
                return true;
            }

            // Subtitle tolerance: "the fourth turning an american prophecy" vs "the fourth turning".
            var shorter = a.Length < b.Length ? a : b;
            var longer = ReferenceEquals(shorter, a) ? b : a;
            return shorter.Length >= 4 && longer.StartsWith(shorter + " ", StringComparison.Ordinal);
        }

        [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
        private static partial Regex Brackets();

        [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
        private static partial Regex Punctuation();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Spaces();
    }
}
