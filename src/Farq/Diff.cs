using System;
using System.Collections.Generic;

namespace Farq;

/// <summary>
/// Line-based diffing with the Myers O(ND) algorithm: the edit script is the
/// shortest possible (up to ties), so equal content is maximally preserved.
/// </summary>
public static class Diff
{
    /// <summary>
    /// The shortest line-based edit script turning <paramref name="oldLines"/>
    /// into <paramref name="newLines"/>, in new-file order.
    /// </summary>
    public static IReadOnlyList<DiffLine> Compute(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        ArgumentNullException.ThrowIfNull(oldLines);
        ArgumentNullException.ThrowIfNull(newLines);

        var script = ShortestEdit(oldLines, newLines);

        // Assign line numbers in a forward pass.
        var result = new List<DiffLine>(script.Count);
        var oldNo = 1;
        var newNo = 1;
        foreach (var (kind, text) in script)
        {
            result.Add(kind switch
            {
                DiffLineKind.Equal => new DiffLine(kind, text, oldNo++, newNo++),
                DiffLineKind.Removed => new DiffLine(kind, text, oldNo++, null),
                _ => new DiffLine(kind, text, null, newNo++),
            });
        }
        return result;
    }

    /// <summary>Convenience overload splitting on newlines.</summary>
    public static IReadOnlyList<DiffLine> Compute(string oldText, string newText) =>
        Compute(SplitLines(oldText), SplitLines(newText));

    internal static string[] SplitLines(string text) =>
        text.Split('\n');

    private static List<(DiffLineKind Kind, string Text)> ShortestEdit(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var n = a.Count;
        var m = b.Count;
        var max = n + m;
        if (max == 0)
            return new List<(DiffLineKind, string)>();

        var v = new int[2 * max + 1];
        var trace = new List<int[]>();
        int Offset(int k) => k + max;

        for (var d = 0; d <= max; d++)
        {
            trace.Add((int[])v.Clone());
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[Offset(k - 1)] < v[Offset(k + 1)]))
                    x = v[Offset(k + 1)];
                else
                    x = v[Offset(k - 1)] + 1;

                var y = x - k;
                while (x < n && y < m && a[x] == b[y])
                {
                    x++;
                    y++;
                }
                v[Offset(k)] = x;

                if (x >= n && y >= m)
                    return Backtrack(trace, a, b);
            }
        }
        return Backtrack(trace, a, b);
    }

    private static List<(DiffLineKind, string)> Backtrack(List<int[]> trace, IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var max = a.Count + b.Count;
        int Offset(int k) => k + max;

        var x = a.Count;
        var y = b.Count;
        var script = new List<(DiffLineKind, string)>();

        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;
            var prevK = (k == -d || (k != d && v[Offset(k - 1)] < v[Offset(k + 1)])) ? k + 1 : k - 1;
            var prevX = v[Offset(prevK)];
            var prevY = prevX - prevK;

            while (x > prevX && y > prevY)
            {
                script.Add((DiffLineKind.Equal, a[x - 1]));
                x--;
                y--;
            }

            if (d > 0)
            {
                if (x == prevX)
                {
                    script.Add((DiffLineKind.Added, b[y - 1]));
                    y--;
                }
                else
                {
                    script.Add((DiffLineKind.Removed, a[x - 1]));
                    x--;
                }
            }
        }

        script.Reverse();
        return script;
    }
}
