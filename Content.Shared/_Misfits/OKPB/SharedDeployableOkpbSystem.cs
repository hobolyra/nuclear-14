using System.Numerics;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Toggleable;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Misfits.OKPB;

public abstract partial class SharedDeployableOkpbSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    private const CollisionGroup RetractedLayer = CollisionGroup.None;
    private const CollisionGroup RetractedMask = CollisionGroup.None;
    private const CollisionGroup DeployedLayer = CollisionGroup.FullTileLayer;
    private const CollisionGroup DeployedMask = CollisionGroup.FullTileMask;

    // Full-tile wall box. Retracted bounds stay whatever the prototype fixture already had
    // Was having issues keeping the rotated fixture while still making it full tile on deploy. Probably a messy fix.
    // Base originally wasn't walk through, wew
    private static readonly Vector2[] DeployedBounds =
    [
        new(-0.5f, -0.5f),
        new(0.5f, -0.5f),
        new(0.5f, 0.5f),
        new(-0.5f, 0.5f),
    ];

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
        if (args.Anchored)
            return;

        if (_toggle.IsActivated(ent.Owner))
            _toggle.TryDeactivate(ent.Owner);
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


    // Deployment checks choo choo
    public bool TrySetDeployed(Entity<DeployableOkpbComponent?> ent, bool deployed)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (deployed && !CanDeployOkpb((ent.Owner, ent.Comp), user: null, quiet: true))
            return false;

        ApplyDeployedState((ent.Owner, ent.Comp), deployed);
        return true;
    }

    
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


// Charged powercell checks
    protected virtual bool HasDeployCharge(Entity<DeployableOkpbComponent?> ent, EntityUid? user, bool quiet)
    {
        if (!TryComp<PowerCellSlotComponent>(ent.Owner, out var slot)
            || !_slots.TryGetSlot(ent.Owner, slot.CellSlotId, out var itemSlot)
            || itemSlot.Item is not { } cell
            || !HasComp<PowerCellComponent>(cell))
        {
            if (!quiet && user != null)
                Popup.PopupEntity(Loc.GetString("deployable-okpb-no-cell"), ent, user.Value);
            return false;
        }

        return true;
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
        if (fixture?.Shape is PolygonShape poly)
        {
            if (comp.RetractedBounds == null)
            {
                comp.RetractedBounds = poly.Vertices.ToArray();
                Dirty(uid, comp);
            }

            var bounds = deployed ? DeployedBounds : comp.RetractedBounds;
            _physics.SetVertices(uid, comp.FixtureId, fixture, poly, bounds);

            var layer = deployed ? DeployedLayer : RetractedLayer;
            var mask = deployed ? DeployedMask : RetractedMask;
            _physics.SetCollisionLayer(uid, comp.FixtureId, fixture, (int)layer);
            _physics.SetCollisionMask(uid, comp.FixtureId, fixture, (int)mask);
            _physics.SetHard(uid, fixture, deployed);
        }

        _appearance.SetData(uid, ToggleVisuals.Toggled, deployed);

        if (TryGetCellSlotId(uid, out var slotId) && _slots.TryGetSlot(uid, slotId, out var slot))
            _slots.SetLock(uid, slot, deployed);
    }
}
