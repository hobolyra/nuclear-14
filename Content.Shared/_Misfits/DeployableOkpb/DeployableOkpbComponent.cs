using Content.Shared.DoAfter;
using Content.Shared.Physics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.DeployableOkpb;

/// <summary>
/// Anchored deployable barrier that swaps between half-wall (retracted, vaultable) and full-tile (deployed) collision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeployableOkpbComponent : Component
{
    /// <summary>
    /// Fixture id whose layer/mask are swapped on deploy.
    /// </summary>
    [DataField]
    public string FixtureId = "fix1";

    [DataField]
    public CollisionGroup RetractedLayer = CollisionGroup.HalfWallLayer | CollisionGroup.BulletImpassable;

    [DataField]
    public CollisionGroup RetractedMask = CollisionGroup.HalfWallLayer;

    [DataField]
    public CollisionGroup DeployedLayer = CollisionGroup.FullTileLayer | CollisionGroup.BulletImpassable;

    [DataField]
    public CollisionGroup DeployedMask = CollisionGroup.FullTileMask;

    /// <summary>
    /// Seconds to remove an inserted power cell via DoAfter.
    /// </summary>
    [DataField]
    public float CellRemovalDelay = 2f;

    /// <summary>
    /// Set briefly before an authorized cell eject so removal attempts are not cancelled.
    /// </summary>
    [ViewVariables]
    public bool AllowNextCellEject;
}

[Serializable, NetSerializable]
public enum DeployableOkpbVisuals : byte
{
    Deployed,
}

/// <summary>
/// DoAfter event for removing the Deployable-OKPB power cell.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveDeployableOkpbCellEvent : SimpleDoAfterEvent;
