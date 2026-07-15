using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CadenceConfig.Deezer
{
    /// <summary>
    /// A normalized (artist, title) key for matching a Deezer track to a Jellyfin library item.
    /// Normalization strips the noise that differs between catalogs — case, accents, "feat."
    /// credits, and remaster/version parentheticals — so "Hey Jude (Remastered 2015)" by
    /// "The Beatles" matches a plain "Hey Jude". Pure + value-equal so it is unit-testable and
    /// usable as a dictionary key.
    /// </summary>
    public readonly struct TrackKey : IEquatable<TrackKey>
    {
        private static readonly Regex FeatClause = new(
            @"\s*[\(\[]?\s*(feat|ft|featuring)\.?\s.*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex VersionParen = new(
            @"\s*[\(\[][^)\]]*(remaster|remastered|version|edit|mix|mono|stereo|deluxe|bonus|live|remix)[^)\]]*[\)\]]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex NonAlnum = new(
            @"[^a-z0-9]+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackKey"/> struct from a raw artist + title.
        /// </summary>
        /// <param name="artist">The artist name.</param>
        /// <param name="title">The track title.</param>
        public TrackKey(string? artist, string? title)
        {
            Artist = Normalize(artist);
            Title = Normalize(title);
        }

        /// <summary>Gets the normalized artist token.</summary>
        public string Artist { get; }

        /// <summary>Gets the normalized title token.</summary>
        public string Title { get; }

        /// <summary>Gets a value indicating whether both parts are non-empty (a usable match key).</summary>
        public bool IsValid => Artist.Length > 0 && Title.Length > 0;

        /// <summary>Value equality (==).</summary>
        /// <param name="left">Left key.</param>
        /// <param name="right">Right key.</param>
        /// <returns>True when equal.</returns>
        public static bool operator ==(TrackKey left, TrackKey right) => left.Equals(right);

        /// <summary>Value inequality (!=).</summary>
        /// <param name="left">Left key.</param>
        /// <param name="right">Right key.</param>
        /// <returns>True when not equal.</returns>
        public static bool operator !=(TrackKey left, TrackKey right) => !left.Equals(right);

        /// <summary>
        /// Lower-cases, removes accents, strips "feat." credits and version parentheticals, then
        /// collapses to a bare alphanumeric token so incidental punctuation/spacing can't block a
        /// match.
        /// </summary>
        /// <param name="value">The raw string.</param>
        /// <returns>The normalized token (empty when the input is null/blank).</returns>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lowered = value.Trim().ToLowerInvariant();
            lowered = FeatClause.Replace(lowered, string.Empty);
            lowered = VersionParen.Replace(lowered, string.Empty);
            lowered = StripAccents(lowered);
            return NonAlnum.Replace(lowered, string.Empty);
        }

        /// <summary>Equality by normalized (artist, title).</summary>
        /// <param name="other">The other key.</param>
        /// <returns>True when both normalized parts match.</returns>
        public bool Equals(TrackKey other) => Artist == other.Artist && Title == other.Title;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is TrackKey k && Equals(k);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Artist, Title);

        private static string StripAccents(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
