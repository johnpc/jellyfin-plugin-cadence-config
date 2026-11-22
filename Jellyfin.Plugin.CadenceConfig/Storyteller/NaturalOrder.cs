using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Orders strings with embedded numbers numerically ("part-2" before "part-10"), ordinal otherwise —
    /// how Storyteller sorts its audio chunks when it concatenates them.
    /// </summary>
    public sealed class NaturalOrder : IComparer<string>
    {
        /// <summary>The shared instance.</summary>
        public static readonly NaturalOrder Instance = new();

        /// <inheritdoc />
        public int Compare(string? x, string? y)
        {
            x ??= string.Empty;
            y ??= string.Empty;
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var si = i;
                    var sj = j;
                    while (i < x.Length && char.IsDigit(x[i]))
                    {
                        i++;
                    }

                    while (j < y.Length && char.IsDigit(y[j]))
                    {
                        j++;
                    }

                    var a = x[si..i].TrimStart('0');
                    var b = y[sj..j].TrimStart('0');
                    var byLength = a.Length.CompareTo(b.Length);
                    var cmp = byLength != 0 ? byLength : string.CompareOrdinal(a, b);
                    if (cmp != 0)
                    {
                        return cmp;
                    }
                }
                else
                {
                    var cmp = x[i].CompareTo(y[j]);
                    if (cmp != 0)
                    {
                        return cmp;
                    }

                    i++;
                    j++;
                }
            }

            return (x.Length - i).CompareTo(y.Length - j);
        }
    }
}
