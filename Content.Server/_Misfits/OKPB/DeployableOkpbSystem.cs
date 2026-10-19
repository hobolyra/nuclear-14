using Content.Server.PowerCell;
using Content.Shared._Misfits.OKPB;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.OKPB;

/// <summary>
/// Deployable OKPB - cell checks, and removal actions
/// </summary>
public sealed partial class DeployableOkpbSystem : SharedDeployableOkpbSystem
{

    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployableOkpbComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DeployableOkpbComponent, ItemToggleDeactivateAttemptEvent>(OnDeactivateAttempt);
        SubscribeLocalEvent<DeployableOkpbComponent, DeactivateOkpbEvent>(OnDeactivateDoAfter);
        SubscribeLocalEvent<DeployableOkpbComponent, ContainerIsRemovingAttemptEvent>(OnCellRemovingAttempt);
        SubscribeLocalEvent<DeployableOkpbComponent, InteractUsingEvent>(OnInteractUsing);
    }

    /// Time it takes to directly turn off
    private const float DeactivateDelay = 2f;

    private void OnDeactivateAttempt(Entity<DeployableOkpbComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
    {
        if (args.User is not { } user)
            return;

        if (TryStartDeactivate((ent.Owner, ent.Comp), user))
            args.Cancelled = true;
    }

    private void OnDeactivateDoAfter(Entity<DeployableOkpbComponent> ent, ref DeactivateOkpbEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (TryTurnOff((ent.Owner, ent.Comp), args.User))
            args.Handled = true;
    }

    public bool TryStartDeactivate(Entity<DeployableOkpbComponent?> ent, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (ent.Comp.AllowImmediateDeactivate)
            return false;

        if (!CanTurnOff(ent))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, DeactivateDelay,
            new DeactivateOkpbEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    public bool TryTurnOff(Entity<DeployableOkpbComponent?> ent, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanTurnOff(ent, quiet: true))
            return false;

        ent.Comp.AllowImmediateDeactivate = true;
        var deactivated = _toggle.TryDeactivate(ent.Owner, user, predicted: false);
        ent.Comp.AllowImmediateDeactivate = false;
        return deactivated;
    }

    public bool CanTurnOff(Entity<DeployableOkpbComponent?> ent, bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        return _toggle.IsActivated(ent.Owner);
    }

    private void OnInteractUsing(Entity<DeployableOkpbComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryBlockCellSwap((ent.Owner, ent.Comp), args.Used, args.User))
            args.Handled = true;
    }


    /// Cells can't be hotswapped
    public bool TryBlockCellSwap(Entity<DeployableOkpbComponent?> ent, EntityUid used, EntityUid user)
    {
        if (!CanBlockCellSwap(ent, used))
            return false;

        Popup.PopupEntity(Loc.GetString("deployable-okpb-swap-blocked"), ent, user);
        return true;
    }

    public bool CanBlockCellSwap(Entity<DeployableOkpbComponent?> ent, EntityUid used)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!HasComp<PowerCellComponent>(used))
            return false;

        if (!TryGetCellSlotId(ent.Owner, out var slotId) || _slots.GetItemOrNull(ent.Owner, slotId) == null)
            return false;

        return true;
    }

    /// Power cell checks
    protected override bool HasDeployCharge(Entity<DeployableOkpbComponent?> ent, EntityUid? user, bool quiet)
    {
        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            if (!quiet && user != null)
                Popup.PopupEntity(Loc.GetString("deployable-okpb-no-cell"), ent, user.Value);
            return false;
        }

        if (battery.CurrentCharge <= 0)
        {
            if (!quiet && user != null)
                Popup.PopupEntity(Loc.GetString("deployable-okpb-no-charge"), ent, user.Value);
            return false;
        }

        return true;
    }

    private void OnGetVerbs(Entity<DeployableOkpbComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!CanRemoveCell((ent.Owner, ent.Comp), args.User, quiet: true))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("deployable-okpb-remove-cell-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
            Act = () => TryRemoveCell((ent.Owner, ent.Comp), user),
            Priority = 1,
        });
    }


    /// Can has remove cell??????
    public bool CanRemoveCell(Entity<DeployableOkpbComponent?> ent, EntityUid user, bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (_toggle.IsActivated(ent.Owner))
        {
            if (!quiet)
                Popup.PopupEntity(Loc.GetString("deployable-okpb-remove-cell-deployed"), ent, user);
            return false;
        }

        if (!TryGetCellSlotId(ent.Owner, out var slotId) || _slots.GetItemOrNull(ent.Owner, slotId) == null)
        {
            if (!quiet)
                Popup.PopupEntity(Loc.GetString("deployable-okpb-no-cell"), ent, user);
            return false;
        }

        return true;
    }

    /// Puts the cell into hands. Deployed can't have cells removed
    public bool TryRemoveCell(Entity<DeployableOkpbComponent?> ent, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanRemoveCell(ent, user, quiet: true))
            return false;

        if (!TryGetCellSlotId(ent.Owner, out var slotId) || !_slots.TryGetSlot(ent.Owner, slotId, out var slot))
            return false;

        ent.Comp.AllowNextCellEject = true;
        var ejected = _slots.TryEjectToHands(ent.Owner, slot, user);
        ent.Comp.AllowNextCellEject = false;

        return ejected && _slots.GetItemOrNull(ent.Owner, slotId) == null;
    }



    private void OnCellRemovingAttempt(Entity<DeployableOkpbComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (!TryGetCellSlotId(ent.Owner, out var slotId) || args.Container.ID != slotId)
            return;

        if (ent.Comp.AllowNextCellEject)
            return;

        args.Cancel();
    }
}
