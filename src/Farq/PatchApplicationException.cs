using System;

namespace Farq;

/// <summary>A unified diff could not be applied to the given lines.</summary>
public sealed class PatchApplicationException : Exception
{
    /// <summary>Creates the exception with the reason.</summary>
    public PatchApplicationException(string reason) : base(reason)
    {
    }
}
