using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.OKPB;

/// <summary>
/// Damage goes to the slotted power cell instead of the DamageableComponent
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PowerDamAbsorptionComponent : Component
{

    /// Fraction of damage converted to power cell charge drain
    [DataField]
    public float Absorption = 0.6f;
}
