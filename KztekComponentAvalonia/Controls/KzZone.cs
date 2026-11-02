using Avalonia;
using System;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Represents a single annotated zone on the KzZoneEditor canvas.
/// </summary>
public sealed class KzZone
{
    public Guid   Id     { get; }
    public Rect   Bounds { get; private set; }
    public string Label  { get; set; }
    public string Color  { get; set; }

    public KzZone(Rect bounds, string label = "", string color = "#F05922")
    {
        Id     = Guid.NewGuid();
        Bounds = bounds;
        Label  = label;
        Color  = color;
    }

    private KzZone(Guid id, Rect bounds, string label, string color)
    {
        Id     = id;
        Bounds = bounds;
        Label  = label;
        Color  = color;
    }

    /// <summary>Copy preserving the same Id — used by undo/redo snapshot.</summary>
    public KzZone Clone() => new(Id, Bounds, Label, Color);

    /// <summary>Copy with a new Id — used when duplicating a zone as a new independent zone.</summary>
    public KzZone DeepCopyWithNewId() => new(Bounds, Label, Color);

    /// <summary>Called only by KzZoneEditor to sync canvas coordinates back to the zone model.</summary>
    internal void UpdateBounds(Rect bounds) => Bounds = bounds;
}
