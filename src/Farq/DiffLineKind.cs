namespace Farq;

/// <summary>How one line participates in an edit script.</summary>
public enum DiffLineKind
{
    /// <summary>Present in both versions.</summary>
    Equal,
    /// <summary>Removed from the old version.</summary>
    Removed,
    /// <summary>Added in the new version.</summary>
    Added,
}
