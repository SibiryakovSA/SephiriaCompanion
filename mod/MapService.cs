using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaCompanion
{
    internal sealed class MapService
    {
        readonly GameAssets assets;
        readonly Dictionary<string, UI_Map_Room> roomTargets = new Dictionary<string, UI_Map_Room>();
        readonly Dictionary<string, UI_ClickToTeleport> authoredTargets = new Dictionary<string, UI_ClickToTeleport>();
        readonly Vector3[] corners = new Vector3[4];
        UI_HUDMapViewer minimap;
        UIManager minimapManager;
        string minimapFloor;
        int minimapTextureId;
        HUDMapData cachedTile;
        float nextMinimapLookup;
        long mapRevision, minimapRevision;
        string mapSignature;
        public string CurrentRoomId { get; private set; }
        public string PlayerIcon { get; private set; }

        public CompanionRuntime.MapView Map { get; private set; } = new CompanionRuntime.MapView();
        public CompanionRuntime.MinimapView Minimap { get; private set; } = new CompanionRuntime.MinimapView();

        public MapService(GameAssets assets) { this.assets = assets; }

        public void Invalidate() { cachedTile = null; nextMinimapLookup = 0; }

        public bool CaptureFloor(PlayerAvatar player, FloorGenerator floor)
        {
            var view = new CompanionRuntime.MapView { floorGuid = player.currentFloorGuid, currentRoomId = CurrentRoomId, playerIcon = PlayerIcon };
            roomTargets.Clear();
            authoredTargets.Clear();
            var map = floor != null ? floor.mapInstance : null;
            if (map == null) { view.error = "map.loading"; return SetMap(view); }
            var rooms = new List<CompanionRuntime.RoomView>();
            var graphics = new List<CompanionRuntime.GraphicView>();
            foreach (var room in map.rooms)
            {
                if (room == null || !Revealed(room, floor)) continue;
                string id = "room-" + room.GetInstanceID();
                string type = room.GetType().Name;
                bool unknown = type.EndsWith("_Unknown", StringComparison.Ordinal);
                bool passage = type.EndsWith("Road", StringComparison.Ordinal);
                var center = room.GetIconCenterAnchoredPosition();
                var size = room.GetRoomIconSize();
                var selectable = room.GetSelectable();
                var button = selectable != null ? selectable.GetComponent<Button>() : null;
                bool travel = !unknown && !passage && (button == null || button.interactable);
                rooms.Add(new CompanionRuntime.RoomView
                {
                    id = id,
                    x = center.x,
                    y = center.y,
                    width = Mathf.Max(16, size.x),
                    height = Mathf.Max(16, size.y),
                    worldX = room.bottomLeft.x,
                    worldY = room.bottomLeft.y,
                    worldWidth = room.topRight.x - room.bottomLeft.x,
                    worldHeight = room.topRight.y - room.bottomLeft.y,
                    unknown = unknown,
                    passage = passage,
                    travel = travel
                });
                if (travel) roomTargets[id] = room;
                ReadGraphics(room.transform, map.contentsChild, graphics, true, id);
            }
            if (floor is FullyDesignedFloorGenerator authored && authored.mapObject != null)
            {
                ReadGraphics(authored.mapObject, map.contentsChild, graphics, false);
                foreach (var connection in authored.mapTeleportConnections)
                {
                    if (connection.icon == null || connection.point == null ||
                        !ActiveBelow(connection.icon.transform, authored.mapObject)) continue;
                    var rect = connection.icon.GetComponent<RectTransform>();
                    if (rect == null) continue;
                    var bounds = Bounds(rect, map.contentsChild);
                    string id = "point-" + connection.icon.GetInstanceID();
                    var button = connection.icon.GetComponent<Button>();
                    bool travel = button == null || button.interactable;
                    rooms.Add(new CompanionRuntime.RoomView
                    {
                        id = id,
                        x = bounds.center.x,
                        y = bounds.center.y,
                        width = Mathf.Max(24, bounds.width),
                        height = Mathf.Max(24, bounds.height),
                        travel = travel
                    });
                    if (travel) authoredTargets[id] = connection.icon;
                }
            }
            view.rooms = rooms.ToArray();
            view.graphics = graphics.ToArray();
            view.available = rooms.Count > 0 || graphics.Count > 0;
            if (!view.available) view.error = "map.visit_room";
            return SetMap(view);
        }

        bool SetMap(CompanionRuntime.MapView view)
        {
            string signature = JsonProtocol.Write(view);
            if (signature == mapSignature) return false;
            mapSignature = signature;
            view.revision = ++mapRevision;
            Map = view;
            return true;
        }

        public bool UpdateRoom(PlayerAvatar player, FloorGenerator floor)
        {
            string id = null;
            if (player != null && floor?.mapInstance != null)
            {
                var position = player.transform.position;
                foreach (var room in floor.mapInstance.rooms)
                {
                    if (room == null || room.GetType().Name.EndsWith("_Unknown", StringComparison.Ordinal)) continue;
                    if (position.x >= room.bottomLeft.x && position.x <= room.topRight.x &&
                        position.y >= room.bottomLeft.y && position.y <= room.topRight.y)
                    {
                        id = "room-" + room.GetInstanceID();
                        if (!room.GetType().Name.EndsWith("Road", StringComparison.Ordinal)) break;
                    }
                }
            }
            if (id == CurrentRoomId) return false;
            CurrentRoomId = id;
            return true;
        }

        void ReadGraphics(Transform root, Transform coordinateSpace,
            List<CompanionRuntime.GraphicView> result, bool ignoreRootVisibility, string roomId = null)
        {
            // Only the visible map export traverses these small room hierarchies.
            // Include newly attached native icons without searching the scene.
            var animations = new Dictionary<Image, UI_Map_HandPaintedRoomImage>();
            // The native map stays inactive until the player opens it. Default
            // parent lookups omit its animation components while it is closed.
            foreach (var animation in root.GetComponentsInChildren<UI_Map_HandPaintedRoomImage>(true))
                if (animation.roomImage != null) animations[animation.roomImage] = animation;
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
            {
                if (graphic == null || !graphic.enabled || graphic.color.a < .01f ||
                    !ActiveBelow(graphic.transform, root, ignoreRootVisibility)) continue;
                if (!(graphic is Image) && !(graphic is RawImage)) continue;
                var bounds = Bounds(graphic.rectTransform, coordinateSpace);
                if (bounds.width < .1f || bounds.height < .1f) continue;
                string image = graphic is Image spriteImage ? assets.Sprite(spriteImage.overrideSprite) :
                    assets.Texture(((RawImage)graphic).texture as Texture2D);
                UI_Map_HandPaintedRoomImage animation = null;
                if (graphic is Image animatedImage) animations.TryGetValue(animatedImage, out animation);
                var frames = animation != null ? ReadFrames(animation.spriteNormal) : Array.Empty<string>();
                var selected = animation != null ? ReadFrames(animation.spriteSelected) : Array.Empty<string>();
                // Native map borders animate at 5 fps. Export all normal frames once;
                // the companion plays them locally, independent of map captures.
                if (frames.Length > 0) image = frames[0];
                result.Add(new CompanionRuntime.GraphicView
                {
                    x = bounds.center.x,
                    y = bounds.center.y,
                    width = bounds.width,
                    height = bounds.height,
                    color = "#" + ColorUtility.ToHtmlStringRGBA(graphic.color),
                    image = image,
                    frames = frames,
                    selectedFrames = selected,
                    roomId = roomId,
                    rotation = Mathf.DeltaAngle(coordinateSpace.eulerAngles.z, graphic.transform.eulerAngles.z)
                });
            }
        }

        string[] ReadFrames(IEnumerable<Sprite> sprites)
        {
            var result = new List<string>();
            foreach (var sprite in sprites) if (sprite != null) result.Add(assets.Sprite(sprite));
            return result.ToArray();
        }

        Rect Bounds(RectTransform rect, Transform space)
        {
            rect.GetWorldCorners(corners);
            Vector3 bottom = space.InverseTransformPoint(corners[0]);
            Vector3 top = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(bottom.x, top.x), Mathf.Min(bottom.y, top.y),
                Mathf.Max(bottom.x, top.x), Mathf.Max(bottom.y, top.y));
        }

        static bool ActiveBelow(Transform child, Transform root, bool ignoreRoot = false)
        {
            for (var node = child; node != null; node = node.parent)
            {
                if (node == root) return ignoreRoot || node.gameObject.activeSelf;
                if (!node.gameObject.activeSelf) return false;
            }
            return false;
        }

        static bool Revealed(UI_Map_Room room, FloorGenerator floor)
        {
            if (room.gameObject.activeSelf) return true;
            foreach (var entry in floor.revealedAreas)
                if (entry.Key.x >= room.bottomLeft.x && entry.Key.x <= room.topRight.x &&
                    entry.Key.y >= room.bottomLeft.y && entry.Key.y <= room.topRight.y) return true;
            return false;
        }

        public Vector2 Travel(CompanionRuntime.Command command, PlayerAvatar player, FloorGenerator floor)
        {
            if (!GameContext.CanTravel(player)) throw new InvalidOperationException("travel.unavailable");
            if (command.floorGuid != player.currentFloorGuid || Map.floorGuid != player.currentFloorGuid || floor == null)
                throw new InvalidOperationException("travel.stale");
            if (roomTargets.TryGetValue(command.roomId ?? "", out var room) && room != null && Revealed(room, floor))
            {
                var button = room.GetSelectable()?.GetComponent<Button>();
                if (button != null && !button.interactable) throw new InvalidOperationException("travel.locked");
                room.TeleportToRoom();
                return room.clickToTeleportPosition;
            }
            if (authoredTargets.TryGetValue(command.roomId ?? "", out var target) && target != null)
            {
                var button = target.GetComponent<Button>();
                if (button != null && !button.interactable) throw new InvalidOperationException("travel.locked");
                target.Teleport();
                foreach (var connection in ((FullyDesignedFloorGenerator)floor).mapTeleportConnections)
                    if (connection.icon == target) return connection.point.GetTeleportTargetPosition();
                return player.transform.position;
            }
            throw new InvalidOperationException("travel.choose_room");
        }

        public bool CaptureMinimap(PlayerAvatar player)
        {
            if (minimapManager != UIManager.Instance)
            { minimapManager = UIManager.Instance; minimap = null; PlayerIcon = null; minimapFloor = null; Invalidate(); }
            if (minimap == null && UIManager.Instance != null) minimap = UIManager.Instance.GetElement<UI_HUDMapViewer>();
            if (PlayerIcon == null && minimap?.playerIcon != null)
            {
                var image = minimap.playerIcon.GetComponent<Image>();
                if (image != null) PlayerIcon = assets.Sprite(image.sprite);
            }
            HUDMapData current = null;
            var position = player.transform.position;
            if (player.currentFloorGuid == minimapFloor && Time.unscaledTime < nextMinimapLookup &&
                cachedTile != null && cachedTile.sprite != null && Contains(cachedTile, position)) current = cachedTile;
            else if (minimap != null)
            {
                foreach (var tile in minimap.hudMapSprites)
                    if (tile != null && tile.sprite != null && Contains(tile, position)) { current = tile; break; }
                cachedTile = current;
                nextMinimapLookup = Time.unscaledTime + 2;
            }
            int textureId = current?.sprite?.GetInstanceID() ?? 0;
            if (player.currentFloorGuid == minimapFloor && textureId == minimapTextureId) return false;
            minimapFloor = player.currentFloorGuid;
            minimapTextureId = textureId;
            Minimap = current == null
                ? new CompanionRuntime.MinimapView { revision = ++minimapRevision, floorGuid = player.currentFloorGuid, error = "mini.loading" }
                : new CompanionRuntime.MinimapView
                {
                    revision = ++minimapRevision,
                    floorGuid = player.currentFloorGuid,
                    available = true,
                    image = assets.Texture(current.sprite),
                    x = current.bottomLeft.x,
                    y = current.bottomLeft.y,
                    width = current.topRight.x - current.bottomLeft.x,
                    height = current.topRight.y - current.bottomLeft.y
                };
            return true;
        }

        static bool Contains(HUDMapData tile, Vector3 position) => position.x >= tile.bottomLeft.x &&
            position.x <= tile.topRight.x && position.y >= tile.bottomLeft.y && position.y <= tile.topRight.y;

        public void FloorFailed(string floorGuid, string error)
        {
            Map = new CompanionRuntime.MapView { revision = ++mapRevision, floorGuid = floorGuid, error = error };
            roomTargets.Clear();
            authoredTargets.Clear();
        }
    }
}
