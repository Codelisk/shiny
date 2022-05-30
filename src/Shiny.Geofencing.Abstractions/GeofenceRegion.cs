﻿namespace Shiny.Locations;

public enum GeofenceState
{
    Unknown = 0,
    Entered = 1,
    Exited = 2
}


public record GeofenceRegion(
    string Identifier,
    Position Center,
    Distance Radius
) //: IStoreEntity
{
    /// <summary>
    /// Determines if this region is single use.
    /// </summary>
    public bool SingleUse { get; set; }

    /// <summary>
    /// Determines if the region should notify when entered.
    /// </summary>
    public bool NotifyOnEntry { get; set; } = true;

    /// <summary>
    /// Determines if the region should notify when exited.
    /// </summary>
    public bool NotifyOnExit { get; set; } = true;

    public override string ToString() => $"[Identifier: {this.Identifier}]";
}
