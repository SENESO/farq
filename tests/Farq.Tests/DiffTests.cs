using System;
using System.Linq;
using Xunit;

namespace Farq.Tests;

public class DiffTests
{
    [Fact]
    public void Identical_inputs_produce_no_changes()
    {
        var lines = Diff.Compute(new[] { "a", "b", "c" }, new[] { "a", "b", "c" });

        Assert.All(lines, l => Assert.Equal(DiffLineKind.Equal, l.Kind));
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void Empty_inputs_produce_no_lines()
    {
        Assert.Empty(Diff.Compute(Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact]
    public void Pure_insertions_at_the_start_middle_and_end()
    {
        var lines = Diff.Compute(new[] { "a", "b" }, new[] { "x", "a", "y", "b", "z" });

        Assert.Equal(
            new[]
            {
                (DiffLineKind.Added, "x"),
                (DiffLineKind.Equal, "a"),
                (DiffLineKind.Added, "y"),
                (DiffLineKind.Equal, "b"),
                (DiffLineKind.Added, "z"),
            },
            lines.Select(l => (l.Kind, l.Text)).ToArray());
    }

    [Fact]
    public void Pure_removals()
    {
        var lines = Diff.Compute(new[] { "a", "x", "b", "y" }, new[] { "a", "b" });

        Assert.Equal(
            new[]
            {
                (DiffLineKind.Equal, "a"),
                (DiffLineKind.Removed, "x"),
                (DiffLineKind.Equal, "b"),
                (DiffLineKind.Removed, "y"),
            },
            lines.Select(l => (l.Kind, l.Text)).ToArray());
    }

    [Fact]
    public void A_replacement_is_remove_then_add()
    {
        var lines = Diff.Compute(new[] { "a", "b", "c" }, new[] { "a", "x", "c" });

        Assert.Equal(
            new[]
            {
                (DiffLineKind.Equal, "a"),
                (DiffLineKind.Removed, "b"),
                (DiffLineKind.Added, "x"),
                (DiffLineKind.Equal, "c"),
            },
            lines.Select(l => (l.Kind, l.Text)).ToArray());
    }

    [Fact]
    public void The_edit_script_is_minimal()
    {
        var oldLines = Enumerable.Range(0, 40).Select(i => $"line {i}").ToArray();
        var newLines = oldLines.Where((_, i) => i != 17).Concat(new[] { "line 40" }).ToArray();

        var lines = Diff.Compute(oldLines, newLines);

        Assert.Equal(1, lines.Count(l => l.Kind == DiffLineKind.Removed));
        Assert.Equal(1, lines.Count(l => l.Kind == DiffLineKind.Added));
    }

    [Fact]
    public void Line_numbers_track_both_versions()
    {
        var lines = Diff.Compute(new[] { "a", "b", "c" }, new[] { "a", "x", "c" });

        Assert.Equal((1, 1), (lines[0].OldNumber, lines[0].NewNumber));
        Assert.Equal((2, (int?)null), (lines[1].OldNumber, lines[1].NewNumber));
        Assert.Equal(((int?)null, 2), (lines[2].OldNumber, lines[2].NewNumber));
        Assert.Equal((3, 3), (lines[3].OldNumber, lines[3].NewNumber));
    }
}
