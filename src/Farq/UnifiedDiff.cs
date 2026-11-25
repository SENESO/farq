using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Farq;

/// <summary>
/// Unified diff formatting and application. Formatting groups changes into
/// hunks with a configurable number of context lines. Application is exact:
/// context and removed lines must match at the positions the header declares,
/// there is no fuzz factor.
/// </summary>
public static class UnifiedDiff
{
    /// <summary>
    /// Formats an edit script as a unified diff with <paramref name="context"/>
    /// lines of unchanged text around each change.
    /// </summary>
    public static string Format(IReadOnlyList<DiffLine> lines, string oldName = "a/file", string newName = "b/file", int context = 3)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (context < 0)
            throw new ArgumentOutOfRangeException(nameof(context), context, "Context cannot be negative.");

        var sb = new StringBuilder();
        sb.Append("--- ").Append(oldName).Append('\n');
        sb.Append("+++ ").Append(newName).Append('\n');

        var i = 0;
        while (i < lines.Count)
        {
            // Find the next change.
            while (i < lines.Count && lines[i].Kind == DiffLineKind.Equal)
                i++;
            if (i >= lines.Count)
                break;

            // Hunk starts at most `context` lines before the change.
            var start = Math.Max(0, i - context);
            var end = i;
            // Extend while gaps between changes fit in 2*context lines.
            while (end < lines.Count)
            {
                var j = end;
                while (j < lines.Count && lines[j].Kind != DiffLineKind.Equal)
                    j++;
                var gap = 0;
                while (j + gap < lines.Count && lines[j + gap].Kind == DiffLineKind.Equal && gap <= 2 * context)
                    gap++;
                if (j + gap >= lines.Count || gap > 2 * context)
                {
                    end = Math.Min(lines.Count, j + (gap > 2 * context ? context : gap));
                    break;
                }
                end = j + gap;
            }

            var hunk = lines.Skip(start).Take(end - start).ToArray();
            var oldStart = hunk.First(l => l.OldNumber is not null).OldNumber!.Value;
            var newStart = hunk.First(l => l.NewNumber is not null).NewNumber!.Value;
            var oldLen = hunk.Count(l => l.Kind != DiffLineKind.Added);
            var newLen = hunk.Count(l => l.Kind != DiffLineKind.Removed);

            sb.Append("@@ -").Append(oldStart).Append(',').Append(oldLen)
              .Append(" +").Append(newStart).Append(',').Append(newLen).Append(" @@\n");
            foreach (var line in hunk)
            {
                sb.Append(line.Kind switch
                {
                    DiffLineKind.Equal => ' ',
                    DiffLineKind.Removed => '-',
                    _ => '+',
                }).Append(line.Text).Append('\n');
            }

            i = end;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies a unified diff to <paramref name="oldLines"/>. Exact: context
    /// and removed lines must match at the declared positions.
    /// </summary>
    /// <exception cref="PatchApplicationException">The patch does not match the lines.</exception>
    public static IReadOnlyList<string> Apply(IReadOnlyList<string> oldLines, string patch)
    {
        ArgumentNullException.ThrowIfNull(oldLines);
        ArgumentNullException.ThrowIfNull(patch);

        var result = new List<string>(oldLines);
        var lines = patch.Split('\n');
        var i = 0;
        var offset = 0; // how much earlier hunks have shifted positions

        while (i < lines.Length)
        {
            var line = lines[i];
            if (!line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            var (oldStart, oldLen, _, _) = ParseHunkHeader(line);
            i++;

            var at = oldStart - 1 + offset;
            if (at < 0 || at > result.Count)
                throw new PatchApplicationException($"Hunk '{line}' starts outside the file.");

            var hunkAdded = 0;
            var hunkRemoved = 0;
            while (i < lines.Length && lines[i].Length > 0 && lines[i][0] is ' ' or '-' or '+')
            {
                var text = lines[i][1..];
                switch (lines[i][0])
                {
                    case ' ':
                    case '-':
                        if (at >= result.Count || result[at] != text)
                            throw new PatchApplicationException(
                                $"Line {at + 1} does not match the patch: expected '{text}'.");
                        if (lines[i][0] == '-')
                        {
                            result.RemoveAt(at);
                            hunkRemoved++;
                        }
                        else
                        {
                            at++;
                        }
                        break;
                    case '+':
                        result.Insert(at, text);
                        at++;
                        hunkAdded++;
                        break;
                }
                i++;
            }
            offset += hunkAdded - hunkRemoved;
        }

        return result;
    }

    /// <summary>Applies a unified diff to text split on newlines.</summary>
    public static string Apply(string oldText, string patch) =>
        string.Join('\n', Apply(Diff.SplitLines(oldText), patch));

    private static (int OldStart, int OldLen, int NewStart, int NewLen) ParseHunkHeader(string line)
    {
        // @@ -oldStart,oldLen +newStart,newLen @@
        var at = line.IndexOf(" @@", 3, StringComparison.Ordinal);
        if (at < 0 || !line.StartsWith("@@ -", StringComparison.Ordinal))
            throw new PatchApplicationException($"Malformed hunk header: '{line}'.");

        var span = line.AsSpan(4, at - 4);
        var plus = span.IndexOf('+');
        if (plus < 0)
            throw new PatchApplicationException($"Malformed hunk header: '{line}'.");

        return (ParsePair(span[..plus].ToString(), line).Item1,
                ParsePair(span[..plus].ToString(), line).Item2,
                ParsePair(span[(plus + 1)..].ToString(), line).Item1,
                ParsePair(span[(plus + 1)..].ToString(), line).Item2);
    }

    private static (int, int) ParsePair(string text, string header)
    {
        text = text.Trim();
        var comma = text.IndexOf(',');
        if (comma < 0
            || !int.TryParse(text.AsSpan(..comma), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(text.AsSpan(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var len))
            throw new PatchApplicationException($"Malformed hunk header: '{header}'.");
        return (start, len);
    }
}
