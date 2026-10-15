using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared._Misfits.PowerCell;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.PowerCell;

namespace Content.Server._Misfits.PowerCell;

/// <summary>
/// Drains slotted power cell charge in place of entity health when <see cref="PowerCellDamageAbsorptionComponent"/> is present.
/// </summary>
public sealed partial class PowerCellDamageAbsorptionSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerCellDamageAbsorptionComponent, DamageModifyEvent>(OnDamageModify,
            after: [typeof(SharedArmorSystem)]);
    }

    private void OnDamageModify(Entity<PowerCellDamageAbsorptionComponent> ent, ref DamageModifyEvent args)
    {
        TryAbsorbDamage((ent.Owner, ent.Comp), ref args);
    }

    /// <summary>
    /// Public API: attempt to absorb the damage into the slotted cell.
    /// </summary>
    public bool TryAbsorbDamage(Entity<PowerCellDamageAbsorptionComponent?> ent, ref DamageModifyEvent args)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!CanAbsorb(ent, out var batteryUid, out var battery))
            return false;

        var total = args.Damage.GetTotal();
        if (total <= 0)
            return false;

        var absorption = Math.Clamp(ent.Comp.Absorption, 0f, float.MaxValue);
        if (absorption <= 0f)
            return false;

        var desiredDrain = (float)total * absorption;
        var chargeBefore = battery.CurrentCharge;
        if (chargeBefore <= 0)
            return false;

        var used = Math.Min(desiredDrain, chargeBefore);
        _battery.UseCharge(batteryUid, used, battery);

        // Full absorption of the hit when enough charge was available for the desired drain.
        var remainingFraction = desiredDrain > 0f ? 1f - (used / desiredDrain) : 1f;
        if (remainingFraction <= 0f)
            args.Damage *= 0f;
        else if (remainingFraction < 1f)
            args.Damage *= remainingFraction;

        if (battery.CurrentCharge <= 0)
        {
            var ev = new PowerCellSlotEmptyEvent();
            RaiseLocalEvent(ent.Owner, ref ev);
        }

        return used > 0;
    }

    /// <summary>
    /// Pure check: can this entity absorb damage into a slotted cell with remaining charge?
    /// </summary>
    public bool CanAbsorb(
        Entity<PowerCellDamageAbsorptionComponent?> ent,
        out EntityUid batteryUid,
        out BatteryComponent battery)
    {
        batteryUid = default;
        battery = null!;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var cellEnt, out var cellBattery)
            || cellEnt == null
            || cellBattery == null
            || cellBattery.CurrentCharge <= 0)
            return false;

        batteryUid = cellEnt.Value;
        battery = cellBattery;
        return true;
    }
}
