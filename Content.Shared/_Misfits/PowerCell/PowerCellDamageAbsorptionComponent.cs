using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.PowerCell;

/// <summary>
/// Incoming damage drains charge from the entity's PowerCellSlot instead of applying to its DamageableComponent.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerCellDamageAbsorptionComponent : Component
{
    /// <summary>
    /// Fraction of incoming damage total converted to power cell charge drain (1 damage → Absorption charge).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Absorption = 0.6f;
}
