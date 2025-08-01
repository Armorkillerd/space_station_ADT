using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Silicons.Laws;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server.Silicons.StationAi;

public sealed class StationAiSystem : SharedStationAiSystem
{
    [Dependency] private readonly IChatManager _chats = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SiliconLawSystem _law = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!; // ADT-Tweak

    private readonly HashSet<Entity<StationAiCoreComponent>> _ais = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandICChatRecipients);
    }

    private void OnExpandICChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = Transform(ev.Source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);

        // This function ensures that chat popups appear on camera views that have connected microphones.
        var query = EntityManager.EntityQueryEnumerator<StationAiCoreComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entStationAiCore, out var entXform))
        {
            var stationAiCore = new Entity<StationAiCoreComponent?>(ent, entStationAiCore);

            if (!TryGetHeld(stationAiCore, out var insertedAi) || !TryComp(insertedAi, out ActorComponent? actor))
                continue;

            if (stationAiCore.Comp?.RemoteEntity == null || stationAiCore.Comp.Remote)
                continue;

            var xform = Transform(stationAiCore.Comp.RemoteEntity.Value);

            var range = (xform.MapID != sourceXform.MapID)
                ? -1
                : (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();

            if (range < 0 || range > ev.VoiceRange)
                continue;

            ev.Recipients.TryAdd(actor.PlayerSession, new ICChatRecipientData(range, false));
        }
    }

    public override bool SetVisionEnabled(Entity<StationAiVisionComponent> entity, bool enabled, bool announce = false)
    {
        if (!base.SetVisionEnabled(entity, enabled, announce))
            return false;

        if (announce)
        {
            AnnounceSnip(entity.Owner);
        }

        return true;
    }

    public override bool SetWhitelistEnabled(Entity<StationAiWhitelistComponent> entity, bool enabled, bool announce = false)
    {
        if (!base.SetWhitelistEnabled(entity, enabled, announce))
            return false;

        if (announce)
        {
            AnnounceSnip(entity.Owner);
        }

        return true;
    }

    public override void AnnounceIntellicardUsage(EntityUid uid, SoundSpecifier? cue = null)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var msg = Loc.GetString("ai-consciousness-download-warning");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chats.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Red);

        if (cue != null && _mind.TryGetMind(uid, out var mindId, out _))
            _roles.MindPlaySound(mindId, cue);
    }

    private void AnnounceSnip(EntityUid entity)
    {
        var xform = Transform(entity);

        if (!TryComp(xform.GridUid, out MapGridComponent? grid))
            return;

        _ais.Clear();
        _lookup.GetChildEntities(xform.GridUid.Value, _ais);
        var filter = Filter.Empty();

        foreach (var ai in _ais)
        {
            // TODO: Filter API?
            if (TryComp(ai.Owner, out ActorComponent? actorComp))
            {
                filter.AddPlayer(actorComp.PlayerSession);
            }
        }

        // TEST
        // filter = Filter.Broadcast();

        // No easy way to do chat notif embeds atm.
        var tile = Maps.LocalToTile(xform.GridUid.Value, grid, xform.Coordinates);
        var msg = Loc.GetString("ai-wire-snipped", ("coords", tile));

        _chats.ChatMessageToMany(ChatChannel.Notifications, msg, msg, entity, false, true, filter.Recipients.Select(o => o.Channel));
        // Apparently there's no sound for this.
    }

    // ADT SAI Custom start
    public override void SetLoadoutExtraLawset(EntityUid brain, Dictionary<string, string> data)
    {
        base.SetLoadoutExtraLawset(brain, data);

        if (data.TryGetValue(ExtraLoadoutLawsetId, out var lawset))
            _law.SetLaws((ProtoId<SiliconLawsetPrototype>)lawset, brain);
    }

    public override void SetLoadoutOnTakeover(EntityUid core, EntityUid brain)
    {
        base.SetLoadoutOnTakeover(core, brain);
        if (!_mind.TryGetMind(brain, out _, out var mind) || !_player.TryGetSessionById(mind.UserId, out var session))
            return;
        var profile = _ticker.GetPlayerProfile(session);

        if (profile == null)
            return;

        if (!profile.Loadouts.TryGetValue("JobStationAi", out var loadout))
            return;

        SetLoadoutExtraLawset(brain, loadout.ExtraData);
        SetLoadoutExtraVisuals(core, loadout.ExtraData);
    }
    // ADT SAI Custom end
}
