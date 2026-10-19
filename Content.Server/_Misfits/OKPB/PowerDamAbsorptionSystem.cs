using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared._Misfits.OKPB;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.PowerCell;

namespace Content.Server._Misfits.OKPB;

/// <summary>
/// Uses power cell charge to absorb damage instead of entity health
/// </summary>
public sealed partial class PowerDamAbsorptionSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerDamAbsorptionComponent, DamageModifyEvent>(OnDamageModify,
            after: [typeof(SharedArmorSystem)]);
    }

    private void OnDamageModify(Entity<PowerDamAbsorptionComponent> ent, ref DamageModifyEvent args)
    {
        TryAbsorbDamage((ent.Owner, ent.Comp), ref args);
    }


    /// Try to pull charge from cell to absorb damage
    public bool TryAbsorbDamage(Entity<PowerDamAbsorptionComponent?> ent, ref DamageModifyEvent args)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!CanAbsorb(ent, out var batteryUid, out var battery))
            return false;

        var total = args.Damage.GetTotal();
        if (total <= 0)
            return false;

        var absorption = ent.Comp.Absorption;
        if (absorption <= 0f)
            return false;

        var desiredDrain = (float)total * absorption;
        var used = Math.Min(desiredDrain, battery.CurrentCharge);
        _battery.UseCharge(batteryUid, used, battery);

        // account for insufficient charge
        var remainingFraction = 1f - (used / desiredDrain);
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

    /// Can damage be absorbed from the cell?
    public bool CanAbsorb(
        Entity<PowerDamAbsorptionComponent?> ent,
        out EntityUid batteryUid,
        out BatteryComponent battery)
    {
        batteryUid = default;
        battery = null!;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var cellEnt, out var cellBattery)
            || cellBattery.CurrentCharge <= 0)
            return false;

        batteryUid = cellEnt.Value;
        battery = cellBattery;
        return true;
    }
}
