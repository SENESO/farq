namespace Farq;

/// <summary>
/// One line of a line-based edit script, with its 1-based line number in the
/// version it belongs to (both numbers for <see cref="DiffLineKind.Equal"/>).
/// </summary>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldNumber, int? NewNumber);
