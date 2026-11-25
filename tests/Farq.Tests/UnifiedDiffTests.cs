using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Farq.Tests;

public class UnifiedDiffTests
{
    [Fact]
    public void A_single_change_formats_as_one_hunk()
    {
        var lines = Diff.Compute(new[] { "a", "b", "c" }, new[] { "a", "x", "c" });

        var patch = UnifiedDiff.Format(lines, "a.txt", "b.txt");

        Assert.Equal(
            "--- a.txt\n" +
            "+++ b.txt\n" +
            "@@ -1,3 +1,3 @@\n" +
            " a\n" +
            "-b\n" +
            "+x\n" +
            " c\n",
            patch);
    }

    [Fact]
    public void No_changes_produce_headers_only()
    {
        var patch = UnifiedDiff.Format(Diff.Compute(new[] { "a" }, new[] { "a" }));

        Assert.Equal("--- a/file\n+++ b/file\n", patch);
    }

    [Fact]
    public void Distant_changes_split_into_separate_hunks()
    {
        var oldLines = Enumerable.Range(1, 20).Select(i => $"line {i}").ToArray();
        var newLines = oldLines.ToArray();
        newLines[2] = "changed 3";
        newLines[17] = "changed 18";

        var patch = UnifiedDiff.Format(Diff.Compute(oldLines, newLines), context: 2);

        Assert.Equal(2, patch.Split("\n").Count(l => l.StartsWith("@@ -", StringComparison.Ordinal)));
        Assert.Equal(newLines, UnifiedDiff.Apply(oldLines, patch).ToArray());
    }

    [Fact]
    public void Close_changes_merge_into_one_hunk()
    {
        var oldLines = Enumerable.Range(1, 20).Select(i => $"line {i}").ToArray();
        var newLines = oldLines.ToArray();
        newLines[7] = "changed 8";
        newLines[11] = "changed 12";

        var patch = UnifiedDiff.Format(Diff.Compute(oldLines, newLines), context: 3);

        Assert.Single(patch.Split("\n").Where(l => l.StartsWith("@@ -", StringComparison.Ordinal)));
        Assert.Equal(newLines, UnifiedDiff.Apply(oldLines, patch).ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(2026)]
    public void Format_then_apply_round_trips(int seed)
    {
        var random = new Random(seed);
        var oldLines = Enumerable.Range(0, 30).Select(_ => $"l{random.Next(10)}").ToArray();
        var newLines = Mutate(random, oldLines);

        var patch = UnifiedDiff.Format(Diff.Compute(oldLines, newLines), "old", "new");
        var applied = UnifiedDiff.Apply(oldLines, patch);

        Assert.Equal(newLines, applied.ToArray());
    }

    [Fact]
    public void Applying_to_mismatching_lines_throws()
    {
        var lines = Diff.Compute(new[] { "a", "b", "c" }, new[] { "a", "x", "c" });
        var patch = UnifiedDiff.Format(lines);

        Assert.Throws<PatchApplicationException>(() => UnifiedDiff.Apply(new[] { "a", "DIFFERENT", "c" }, patch));
    }

    [Fact]
    public void Malformed_hunk_headers_throw()
    {
        Assert.Throws<PatchApplicationException>(() => UnifiedDiff.Apply(new[] { "a" }, "--- a\n+++ b\n@@ nonsense @@\n a\n"));
    }

    [Fact]
    public void Text_overload_splits_and_joins_on_newlines()
    {
        var oldText = "one\ntwo\nthree";
        var newText = "one\nTWO\nthree";
        var patch = UnifiedDiff.Format(Diff.Compute(oldText, newText));

        Assert.Equal(newText, UnifiedDiff.Apply(oldText, patch));
    }

    private static string[] Mutate(Random random, IReadOnlyList<string> lines)
    {
        var result = new List<string>(lines);
        for (var i = 0; i < 8; i++)
        {
            switch (random.Next(3))
            {
                case 0 when result.Count > 1:
                    result.RemoveAt(random.Next(result.Count));
                    break;
                case 1:
                    result.Insert(random.Next(result.Count + 1), $"new{random.Next(100)}");
                    break;
                default:
                    result[random.Next(result.Count)] = $"edit{random.Next(100)}";
                    break;
            }
        }
        return result.ToArray();
    }
}
