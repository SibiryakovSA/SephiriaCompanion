using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace SephiriaCompanion
{
    public sealed class CompanionMod : HorayModBase
    {
        static GameObject active;
        protected override void OnModLoaded()
        {
            if (active != null) UnityEngine.Object.DestroyImmediate(active);
            active = new GameObject("Sephiria Companion");
            UnityEngine.Object.DontDestroyOnLoad(active);
            active.AddComponent<CompanionRuntime>();
        }
        protected override void OnModUnloaded()
        {
            if (active != null) UnityEngine.Object.DestroyImmediate(active);
            active = null;
        }
    }

    public sealed partial class CompanionRuntime : MonoBehaviour
    {
        readonly GameContext context = new GameContext();
        readonly GameAssets assets = new GameAssets();
        readonly HudTransfer hud = new HudTransfer();
        readonly Dictionary<string, byte[]> web = new Dictionary<string, byte[]>();
        readonly Dictionary<string, CommandResult> completed = new Dictionary<string, CommandResult>();
        readonly Queue<string> completedOrder = new Queue<string>();
        readonly PerformanceView performance = new PerformanceView();
        LocalServer server;
        InventoryService inventory;
        CombatHudService combat;
        MapService maps;
        ConnectionNotice notice;
        Snapshot latest;
        PlayerAvatar observedPlayer;
        FloorGenerator observedFloor;
        CommandResult lastCommand;
        Command pendingCommand;
        Vector2 pendingDestination;
        float pendingUntil, nextCapture, nextMap, nextAsset, lastLease = -100;
        long sequence;
        bool requestedHud, hudReady, combatReady, previousRunInBackground, healthy;
        bool roomDirty = true, mapDirty = true, forcePush = true;
        string tab = "hud", failure, channelFailure, stateSignature;

        bool ClientActive => server != null && server.HasSubscribers && Time.unscaledTime - lastLease < 2.5f;

        void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            var reader = new ItemReader(assets);
            inventory = new InventoryService(reader, new ComboReader(assets));
            combat = new CombatHudService(assets, reader);
            maps = new MapService(assets);
            notice = new ConnectionNotice(transform);
            try
            {
                LoadWebResources();
                server = new LocalServer(Resource);
                Capture();
                UnityEngine.Debug.Log("[SephiriaCompanion] READY version=" + JsonProtocol.Version);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[SephiriaCompanion] START: " + ex);
                enabled = false;
            }
        }

        void LoadWebResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith("Web.", StringComparison.Ordinal)) continue;
                using (var stream = assembly.GetManifestResourceStream(name))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    web[name.Substring(4)] = memory.ToArray();
                }
            }
        }

        byte[] Resource(string name) => web.TryGetValue(name, out var bytes) || assets.TryGet(name, out bytes) ? bytes : null;

        void Update()
        {
            if (server == null) return;
            for (int count = 0; count < 4 && server.TryTake(out var raw); count++) Execute(raw);
            notice.Update();
            ObservePendingCommand();
            if (Time.unscaledTime >= nextCapture)
            {
                nextCapture = Time.unscaledTime + (ClientActive ? tab == "hud" ? .1f : .25f : 2f);
                Capture();
            }
            if (ClientActive && Time.unscaledTime >= nextAsset)
            {
                nextAsset = Time.unscaledTime + .08f;
                string path = assets.ProcessOne();
                if (path != null) server.AssetReady(path);
            }
        }

        void LateUpdate()
        {
            bool transfer = healthy && ClientActive && requestedHud && hudReady && latest?.status == "playing";
            hud.Apply(context.Player, transfer, transfer && maps.Minimap.available &&
                maps.Minimap.floorGuid == latest.player?.floorGuid, transfer && combatReady);
        }

        void BindSignals(PlayerAvatar player, FloorGenerator floor)
        {
            if (observedPlayer != player)
            {
                if (observedPlayer != null)
                {
                    observedPlayer.OnEnteredFloorClientside -= EnteredFloor;
                    observedPlayer.OnSetPositionClientside -= PositionSet;
                }
                observedPlayer = player;
                if (player != null)
                {
                    player.OnEnteredFloorClientside += EnteredFloor;
                    player.OnSetPositionClientside += PositionSet;
                }
                roomDirty = mapDirty = forcePush = true;
            }
            if (observedFloor != floor)
            {
                if (observedFloor != null) observedFloor.revealedAreas.OnChange -= AreaChanged;
                observedFloor = floor;
                if (floor != null) floor.revealedAreas.OnChange += AreaChanged;
                roomDirty = mapDirty = true;
            }
        }

        void EnteredFloor(string guid) { roomDirty = mapDirty = true; nextCapture = 0; }
        void PositionSet(Vector3 position, bool teleport) { if (teleport) { roomDirty = mapDirty = true; nextCapture = 0; } }
        void AreaChanged(SyncIDictionary<Vector2, bool>.Operation operation, Vector2 position, bool value)
        { roomDirty = mapDirty = true; nextCapture = 0; }

        void Capture()
        {
            var clock = Stopwatch.StartNew();
            var state = new Snapshot
            {
                session = server.Session,
                status = "menu",
                message = "status.load_save",
                lastCommand = lastCommand,
                hud = hud.State
            };
            try
            {
                var player = context.Player;
                var floor = context.Floor;
                BindSignals(player, floor);
                inventory.Connect(player);
                combat.Connect(player);
                if (player != null)
                {
                    state.player = context.ReadPlayer();
                    state.status = player.loadingScreenType == -1 ? "playing" : "loading";
                    state.message = state.status == "loading" ? "status.loading" : "";
                    if (state.status == "playing" && ClientActive) CaptureChannels(player, floor);
                    state.player.roomId = maps.CurrentRoomId;
                    if (tab != "hud") state.player.x = state.player.y = 0; // The floor map is room-based, not a live tracker.
                    state.combat = combat.Read();
                }
                state.inventoryRevision = inventory.Current.revision;
                state.mapRevision = maps.Map.revision;
                state.minimapRevision = maps.Minimap.revision;
                healthy = true;
                failure = null;
            }
            catch (Exception ex)
            {
                state.status = "error";
                state.message = "status.read_error";
                healthy = false;
                if (failure != ex.ToString()) UnityEngine.Debug.LogWarning("[SephiriaCompanion] CAPTURE: " + ex);
                failure = ex.ToString();
            }
            latest = state;
            string signature = JsonProtocol.Write(state);
            if (signature == stateSignature && !forcePush) return;
            stateSignature = signature;
            forcePush = false;
            state.sequence = ++sequence;
            state.capturedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            performance.captureMs = Math.Round(clock.Elapsed.TotalMilliseconds, 2);
            performance.peakCaptureMs = Math.Max(performance.peakCaptureMs, performance.captureMs);
            performance.assetMs = Math.Round(assets.LastWorkMs, 2);
            performance.cachedAssets = assets.Count;
            performance.queuedAssets = assets.Queued;
            state.performance = performance;
            try { performance.stateBytes = server.Publish("state", JsonProtocol.Write(state)); }
            catch (Exception ex) { healthy = false; hud.Restore(); UnityEngine.Debug.LogError("[SephiriaCompanion] JSON: " + ex); }
        }

        void CaptureChannels(PlayerAvatar player, FloorGenerator floor)
        {
            try
            {
                if (maps.CaptureMinimap(player))
                {
                    server.Publish("minimap", JsonProtocol.Write(maps.Minimap));
                    roomDirty = true;
                }
                if (roomDirty)
                {
                    if (maps.UpdateRoom(player, floor)) mapDirty = true;
                    roomDirty = false;
                }
            }
            catch (Exception ex) { ReportChannelFailure("minimap", ex); }
            try
            {
                if (combat.CaptureContent()) server.Publish("hud-content", JsonProtocol.Write(combat.Content));
            }
            catch (Exception ex) { ReportChannelFailure("hud-content", ex); }
            try
            {
                if ((tab == "inventory" || pendingCommand?.action == "swap" || pendingCommand?.action == "rotate") && inventory.Capture())
                    server.Publish("inventory", JsonProtocol.Write(inventory.Current));
            }
            catch (Exception ex) { ReportChannelFailure("inventory", ex); }
            if (tab == "map" && (mapDirty || Time.unscaledTime >= nextMap))
            {
                nextMap = Time.unscaledTime + 5;
                mapDirty = false;
                try
                {
                    maps.UpdateRoom(player, floor);
                    if (maps.CaptureFloor(player, floor)) server.Publish("floor-map", JsonProtocol.Write(maps.Map));
                }
                catch (Exception ex)
                {
                    ReportChannelFailure("floor-map", ex);
                    maps.FloorFailed(player.currentFloorGuid, "map.loading");
                    server.Publish("floor-map", JsonProtocol.Write(maps.Map));
                }
            }
        }

        void ReportChannelFailure(string channel, Exception error)
        {
            string identity = channel + ":" + error.GetType().Name;
            if (channelFailure != identity) UnityEngine.Debug.LogWarning("[SephiriaCompanion] " + channel + ": " + error);
            channelFailure = identity;
        }

        void Execute(string raw)
        {
            Command command = null;
            try
            {
                command = JsonProtocol.ReadCommand(raw);
                if (command == null || string.IsNullOrEmpty(command.id) || command.id.Length > 80) throw new InvalidOperationException("command.invalid");
                if (command.session != server.Session) throw new InvalidOperationException("connection.stale");
                if (command.action == "lease") { RenewLease(command); return; }
                if (command.action == "refresh")
                {
                    inventory.Invalidate(); combat.Invalidate(); maps.Invalidate();
                    roomDirty = mapDirty = forcePush = true; nextCapture = 0; return;
                }
                if (completed.TryGetValue(command.id, out var previous)) { lastCommand = previous; forcePush = true; return; }
                if (pendingCommand != null)
                {
                    if (pendingCommand.id == command.id) return;
                    throw new InvalidOperationException("command.busy");
                }
                switch (command.action)
                {
                    case "ping": notice.Show(command.language); Complete(command.id, true, "command.ping_ok"); break;
                    case "swap": inventory.Swap(command, context.Player); BeginPending(command, 2); break;
                    case "rotate": inventory.Rotate(command, context.Player); BeginPending(command, 2); break;
                    case "travel": pendingDestination = maps.Travel(command, context.Player, context.Floor); BeginPending(command, 5); break;
                    default: throw new InvalidOperationException("command.invalid");
                }
            }
            catch (Exception ex) { if (command?.action != "lease") Complete(command?.id ?? "", false, ex.Message); }
            nextCapture = 0;
        }

        void RenewLease(Command command)
        {
            if (command.renderedSequence <= 0 || command.renderedSequence > sequence || command.renderedSequence < sequence - 30) return;
            string nextTab = command.tab == "map" || command.tab == "inventory" || command.tab == "settings" ? command.tab : "hud";
            if (nextTab != tab)
            {
                tab = nextTab;
                if (tab == "inventory") inventory.Invalidate();
                if (tab == "map") mapDirty = true;
                forcePush = true;
                nextCapture = 0;
            }
            requestedHud = command.hideHud;
            hudReady = command.hudReady;
            combatReady = command.combatReady;
            lastLease = Time.unscaledTime;
        }

        void BeginPending(Command command, float timeout)
        {
            pendingCommand = command;
            pendingUntil = Time.unscaledTime + timeout;
            lastCommand = new CommandResult { id = command.id, ok = true, pending = true, message = "command.pending" };
        }

        void ObservePendingCommand()
        {
            if (pendingCommand == null) return;
            var player = context.Player;
            bool observed = pendingCommand.action == "swap" ? inventory.SwapObserved(pendingCommand) :
                pendingCommand.action == "rotate" ? inventory.RotationObserved(pendingCommand) :
                player != null && ScreenFader.Instance != null && !ScreenFader.Instance.IsFading && player.loadingScreenType == -1 &&
                Vector2.Distance(player.transform.position, pendingDestination) < 3;
            if (observed)
            {
                Complete(pendingCommand.id, true, pendingCommand.action == "travel" ? "command.travel_ok" : "");
                pendingCommand = null;
                roomDirty = mapDirty = true;
                nextCapture = 0;
            }
            else if (Time.unscaledTime > pendingUntil || player == null)
            {
                Complete(pendingCommand.id, false, "command.unconfirmed");
                inventory.Invalidate();
                pendingCommand = null;
                nextCapture = 0;
            }
        }

        void Complete(string id, bool ok, string message)
        {
            lastCommand = new CommandResult { id = id, ok = ok, message = message };
            if (string.IsNullOrEmpty(id)) return;
            completed[id] = lastCommand;
            completedOrder.Enqueue(id);
            while (completedOrder.Count > 64) completed.Remove(completedOrder.Dequeue());
        }

        void OnDestroy()
        {
            BindSignals(null, null);
            hud.Restore();
            inventory?.Dispose();
            combat?.Dispose();
            server?.Dispose();
            server = null;
            Application.runInBackground = previousRunInBackground;
        }
    }
}
