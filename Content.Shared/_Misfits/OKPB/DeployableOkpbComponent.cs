using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.OKPB;

/// <summary>
/// Deployable reflective barrier - dies if cell runs out or removed
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeployableOkpbComponent : Component
{

    /// ID of the fixture that gets swapped on deploy
    [DataField]
    public string FixtureId = "fix1";


    /// How long it takes to remove the power cell
    [DataField]
    public float CellRemovalDelay = 2f;

    /// Needed to stop canceling doafters because fuck me
    [ViewVariables]
    public bool AllowNextCellEject;
}


/// DoAfter event for cell removal, where the hell do I put this? I don't want to create a new comp file
[Serializable, NetSerializable]
public sealed partial class RemoveDeployableOkpbCellEvent : SimpleDoAfterEvent;
