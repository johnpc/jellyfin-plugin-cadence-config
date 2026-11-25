using System;
using System.Buffers;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// The titles a Storyteller book can be recognized by. Pure. Storyteller's own title comes from the epub and is
    /// sometimes junk ("A Novel", "Stories" — the epub's subtitle promoted to title) or a different work's name (the
    /// epub "Do Androids Dream of Electric Sheep?" for the audiobook "Blade Runner"). The audiobook folder, named
    /// "&lt;Author&gt; - &lt;audiobook title&gt;" when the book was imported, carries the audiobook's own title.
    /// </summary>
    public static class BookTitles
    {
        private const string AuthorSeparator = " - ";

        private static readonly SearchValues<char> PathSeparators = SearchValues.Create("/\\");

        /// <summary>Gets the candidate titles of a book: its Storyteller title, then its audiobook folder's title.</summary>
        /// <param name="book">The book.</param>
        /// <returns>The non-empty candidates, most authoritative first.</returns>
        public static IReadOnlyList<string> Of(StorytellerBook book)
        {
            var titles = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(book.Title))
            {
                titles.Add(book.Title);
            }

            var folder = FolderTitle(book.Audiobook?.Filepath);
            if (folder.Length > 0)
            {
                titles.Add(folder);
            }

            return titles;
        }

        /// <summary>The title part of an audiobook folder path ("/library/Andy Weir - Project Hail Mary" → "Project Hail Mary").</summary>
        /// <param name="filepath">The audiobook directory, may be null.</param>
        /// <returns>The title, or empty.</returns>
        public static string FolderTitle(string? filepath)
        {
            if (string.IsNullOrWhiteSpace(filepath))
            {
                return string.Empty;
            }

            var name = filepath.TrimEnd('/', '\\');
            name = name[(name.AsSpan().LastIndexOfAny(PathSeparators) + 1)..];
            var cut = name.IndexOf(AuthorSeparator, StringComparison.Ordinal);
            return (cut < 0 ? name : name[(cut + AuthorSeparator.Length)..]).Trim();
        }
    }
}
