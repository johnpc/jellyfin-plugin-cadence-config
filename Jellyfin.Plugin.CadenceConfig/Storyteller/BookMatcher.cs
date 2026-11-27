using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        /// <summary>
        /// Picks the Storyteller book for the first of several Jellyfin titles that matches (the album tag, then the
        /// item name: tags like "01 Spin" or "Dungeon Crawler Carl, Book 2 - Carl's Doomsday Scenario" miss, the name hits).
        /// </summary>
        /// <param name="titles">Candidate Jellyfin titles, most specific first; null/blank entries are skipped.</param>
        /// <param name="author">The Jellyfin author (album artist), may be null.</param>
        /// <param name="books">The Storyteller library.</param>
        /// <returns>The match, or null.</returns>
        public static StorytellerBook? Match(IEnumerable<string?> titles, string? author, IEnumerable<StorytellerBook> books)
        {
            var library = books as IReadOnlyCollection<StorytellerBook> ?? books.ToList();
            var wanted = titles.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToList();

            // Exact titles across EVERY candidate before any subtitle-tolerant match: the album tag
            // "Dungeon Crawler Carl, Book 2 - Carl's Doomsday Scenario" starts with book 1's title, so a
            // tolerant first pass would hand book 2's listener book 1's alignment.
            return First(wanted, author, library, exact: true) ?? First(wanted, author, library, exact: false);
        }

        /// <summary>Picks the Storyteller book for a Jellyfin title/author, or null.</summary>
        /// <param name="title">The Jellyfin book title.</param>
        /// <param name="author">The Jellyfin author (album artist), may be null.</param>
        /// <param name="books">The Storyteller library.</param>
        /// <returns>The match, or null.</returns>
        public static StorytellerBook? Match(string? title, string? author, IEnumerable<StorytellerBook> books)
        {
            return Match(new[] { title }, author, books);
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

            // Storyteller passes epub titles through verbatim, HTML entities included ("Escape 9&#8211;5").
            var text = Brackets().Replace(WebUtility.HtmlDecode(title).ToLowerInvariant(), " ");
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

        private static StorytellerBook? First(IReadOnlyList<string?> titles, string? author, IReadOnlyCollection<StorytellerBook> books, bool exact)
        {
            return titles.Select(t => MatchOne(t, author, books, exact)).FirstOrDefault(m => m != null);
        }

        private static StorytellerBook? MatchOne(string? title, string? author, IEnumerable<StorytellerBook> books, bool exact)
        {
            var wanted = Normalize(title);
            if (wanted.Length == 0)
            {
                return null;
            }

            // Most specific wins: "The Fourth Turning Is Here: What the Seasons..." tolerates both "The Fourth Turning"
            // (an older book) and "The Fourth Turning Is Here"; the longer shared title is the right one.
            var authorTokens = AuthorTokens(author);
            StorytellerBook? best = null;
            var bestScore = -1;
            foreach (var book in books)
            {
                var score = Score(wanted, book, exact);
                if (score > bestScore && AuthorsAgree(authorTokens, book))
                {
                    best = book;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int Score(string wanted, StorytellerBook book, bool exact)
        {
            return BookTitles.Of(book)
                .Select(Normalize)
                .Where(t => TitlesMatch(wanted, t, exact))
                .Select(t => Math.Min(t.Length, wanted.Length))
                .DefaultIfEmpty(-1)
                .Max();
        }

        private static bool AuthorsAgree(HashSet<string> authorTokens, StorytellerBook book)
        {
            var theirs = book.Authors.SelectMany(a => AuthorTokens(a.Name)).ToHashSet();
            return authorTokens.Count == 0 || theirs.Count == 0 || authorTokens.Overlaps(theirs);
        }

        private static bool TitlesMatch(string a, string b, bool exact)
        {
            if (a == b)
            {
                return true;
            }

            if (exact)
            {
                return false;
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
