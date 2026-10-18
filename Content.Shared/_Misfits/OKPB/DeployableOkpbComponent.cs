using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.OKPB;

/// <summary>
/// Deployable reflective barrier - dies if cell runs out or removed
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DeployableOkpbComponent : Component
{

    /// ID of the fixture that gets swapped on deploy
    [DataField]
    public string FixtureId = "fix1";

    /// fixture, captured so retracting restores to the YAML settings
    [DataField, AutoNetworkedField]
    public Vector2[]? RetractedBounds;


    /// Lets the remove-cell verb pass the container block. Everything else stays cancelled.
    [ViewVariables]
    public bool AllowNextCellEject;
}


/// DoAfter for turning the barrier off by hand.
[Serializable, NetSerializable]
public sealed partial class DeactivateOkpbEvent : SimpleDoAfterEvent;
