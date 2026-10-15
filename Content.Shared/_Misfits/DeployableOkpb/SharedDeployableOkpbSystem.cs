using Content.Shared.Climbing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Toggleable;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Misfits.DeployableOkpb;

/// <summary>
/// Handles deploy/retract collision, climbability, and activate-attempt gates for Deployable-OKPB.
/// </summary>
public abstract partial class SharedDeployableOkpbSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployableOkpbComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DeployableOkpbComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<DeployableOkpbComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<DeployableOkpbComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<DeployableOkpbComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<DeployableOkpbComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
    }

    private void OnMapInit(Entity<DeployableOkpbComponent> ent, ref MapInitEvent args)
    {
        ApplyDeployedState(ent, _toggle.IsActivated(ent.Owner));
        UpdatePullPhysics(ent);
    }

    private void OnActivateAttempt(Entity<DeployableOkpbComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!CanDeployOkpb((ent.Owner, ent.Comp), args.User))
            args.Cancelled = true;
    }

    private void OnToggled(Entity<DeployableOkpbComponent> ent, ref ItemToggledEvent args)
    {
        TrySetDeployed((ent.Owner, ent.Comp), args.Activated);
    }

    private void OnAnchorChanged(Entity<DeployableOkpbComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdatePullPhysics(ent);

        if (args.Anchored)
            return;

        if (_toggle.IsActivated(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);
    }

    /// <summary>
    /// Unanchored OKPBs are Dynamic so they can be pulled; anchored ones stay Static.
    /// </summary>
    private void UpdatePullPhysics(EntityUid uid)
    {
        if (!TryComp(uid, out PhysicsComponent? physics))
            return;

        var anchored = Transform(uid).Anchored;
        _physics.SetBodyType(uid, anchored ? BodyType.Static : BodyType.Dynamic, body: physics);
    }

    private void OnPowerCellChanged(Entity<DeployableOkpbComponent> ent, ref PowerCellChangedEvent args)
    {
        if (!args.Ejected)
            return;

        if (_toggle.IsActivated(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);
    }

    private void OnPowerCellEmpty(Entity<DeployableOkpbComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (_toggle.IsActivated(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);
    }

    /// <summary>
    /// Public API: attempt to set deployed state. Turning on requires <see cref="CanDeployOkpb"/>.
    /// </summary>
    public bool TrySetDeployed(Entity<DeployableOkpbComponent?> ent, bool deployed)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (deployed && !CanDeployOkpb((ent.Owner, ent.Comp), user: null, quiet: true))
            return false;

        ApplyDeployedState((ent.Owner, ent.Comp), deployed);
        return true;
    }

    /// <summary>
    /// Pure check: anchored and a charged power cell is present.
    /// </summary>
    public bool CanDeployOkpb(Entity<DeployableOkpbComponent?> ent, EntityUid? user = null, bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!Transform(ent).Anchored)
        {
            if (!quiet && user != null)
                Popup.PopupEntity(Loc.GetString("deployable-okpb-not-anchored"), ent, user.Value);
            return false;
        }

        if (!HasDeployCharge(ent, user, quiet))
            return false;

        return true;
    }

    /// <summary>
    /// Overridden on the server to require a slotted cell with remaining charge.
    /// </summary>
    protected virtual bool HasDeployCharge(Entity<DeployableOkpbComponent?> ent, EntityUid? user, bool quiet)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (TryGetCellSlotId(ent.Owner, out var slotId) && _slots.GetItemOrNull(ent.Owner, slotId) != null)
            return true;

        if (!quiet && user != null)
            Popup.PopupEntity(Loc.GetString("deployable-okpb-no-cell"), ent, user.Value);

        return false;
    }

    protected bool TryGetCellSlotId(EntityUid uid, out string slotId)
    {
        if (TryComp<PowerCellSlotComponent>(uid, out var slot))
        {
            slotId = slot.CellSlotId;
            return true;
        }

        slotId = string.Empty;
        return false;
    }

    private void ApplyDeployedState(Entity<DeployableOkpbComponent> ent, bool deployed)
    {
        var uid = ent.Owner;
        var comp = ent.Comp;

        var fixture = _fixtures.GetFixtureOrNull(uid, comp.FixtureId);
        if (fixture != null)
        {
            if (deployed)
            {
                _physics.SetCollisionLayer(uid, comp.FixtureId, fixture, (int)comp.DeployedLayer);
                _physics.SetCollisionMask(uid, comp.FixtureId, fixture, (int)comp.DeployedMask);
                _physics.SetHard(uid, fixture, true);
            }
            else
            {
                _physics.SetCollisionLayer(uid, comp.FixtureId, fixture, (int)comp.RetractedLayer);
                _physics.SetCollisionMask(uid, comp.FixtureId, fixture, (int)comp.RetractedMask);
                _physics.SetHard(uid, fixture, true);
            }
        }

        if (deployed)
            RemCompDeferred<ClimbableComponent>(uid);
        else
            EnsureComp<ClimbableComponent>(uid);

        _appearance.SetData(uid, DeployableOkpbVisuals.Deployed, deployed);
        _appearance.SetData(uid, ToggleVisuals.Toggled, deployed);

        // Lock cell while deployed so it cannot be swapped mid-barrier.
        if (TryGetCellSlotId(uid, out var slotId) && _slots.TryGetSlot(uid, slotId, out var slot))
            _slots.SetLock(uid, slot, deployed);
    }
}
