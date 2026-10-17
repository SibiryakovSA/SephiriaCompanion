using System;

namespace SephiriaCompanion
{
    // Transport objects contain only values; no Unity objects cross the HTTP boundary.
    public sealed partial class CompanionRuntime
    {
        [Serializable]
        public sealed class Snapshot
        {
            public int protocol = 3;
            public string version = JsonProtocol.Version, session, status, message;
            public long sequence, capturedAt, inventoryRevision, mapRevision, minimapRevision;
            public PlayerView player;
            public CombatView combat;
            public CommandResult lastCommand;
            public HudView hud;
            public PerformanceView performance;
        }

        [Serializable]
        public sealed class PlayerView
        {
            public float hp, maxHp, shield, x, y, expRatio;
            public int mp, maxMp, reservedMp, money, attack, level, exp, nextLevelExp, sapphire, dice;
            public bool inBattle, dead, canTravel;
            public string floor, floorGuid;
            public string roomId;
        }

        [Serializable]
        public sealed class InventoryView
        {
            public long revision;
            public bool available;
            public int width, height, capacity, extraItems;
            public string floorGuid, error = "";
            public ItemView[] items = Array.Empty<ItemView>();
            public ComboView[] combos = Array.Empty<ComboView>();
        }

        [Serializable]
        public sealed class ItemView
        {
            public int id, entityId, x, y, quantity, maxQuantity;
            public string name, description, kind, icon;
            public string effects, conditions, penalty;
            public int level = -1, maxLevel = -1, rotation;
            public bool active = true, unique, canRotate;
            public TabletCell[] cells = Array.Empty<TabletCell>();
        }

        [Serializable]
        public sealed class ComboView
        {
            public string id, name, description, icon;
            public int count;
            public ComboTier[] tiers = Array.Empty<ComboTier>();
        }

        [Serializable] public sealed class ComboTier { public int count; public string description; public bool active; }

        [Serializable]
        public sealed class TabletCell
        {
            public int x, y, amount;
            public string effect, criteria;
            public bool met, worldX, worldY;
        }

        [Serializable]
        public sealed class HudContent
        {
            public long revision;
            public string floorGuid, playerIcon, dashIcon;
            public System.Collections.Generic.Dictionary<string, string> icons = new System.Collections.Generic.Dictionary<string, string>();
            public QuickItem[] potions = Array.Empty<QuickItem>();
            public SkillInfo[] skills = Array.Empty<SkillInfo>();
            public EffectInfo[] effects = Array.Empty<EffectInfo>();
        }

        [Serializable] public sealed class QuickItem { public int slot; public ItemView item; }
        [Serializable]
        public sealed class SkillInfo
        {
            public string id, name, description, icon;
            public int slot;
            public bool weapon;
        }
        [Serializable] public sealed class EffectInfo { public string id, name, description, icon, kind; }
        [Serializable]
        public sealed class CombatView
        {
            public bool available;
            public int dashAvailable, dashMax, selectedPotion = -1;
            public float dashRecharge;
            public SkillState[] skills = Array.Empty<SkillState>();
            public EffectState[] effects = Array.Empty<EffectState>();
        }
        [Serializable]
        public sealed class SkillState
        {
            public string id;
            public float cooldown;
            public int ammo, maxAmmo, manaCost;
            public bool insufficientMana;
        }
        [Serializable] public sealed class EffectState { public string id, value; public float fill; }

        [Serializable]
        public sealed class MapView
        {
            public long revision;
            public bool available;
            public string floorGuid, error = "";
            public string currentRoomId, playerIcon;
            public RoomView[] rooms = Array.Empty<RoomView>();
            public GraphicView[] graphics = Array.Empty<GraphicView>();
        }

        [Serializable]
        public sealed class RoomView
        {
            public string id;
            public float x, y, width, height, worldX, worldY, worldWidth, worldHeight;
            public bool travel, unknown, passage;
        }

        [Serializable]
        public sealed class GraphicView
        {
            public float x, y, width, height, rotation;
            public string image, color;
            public string[] frames = Array.Empty<string>();
            public string roomId;
            public string[] selectedFrames = Array.Empty<string>();
        }

        [Serializable]
        public sealed class MinimapView
        {
            public long revision;
            public bool available;
            public string floorGuid, image, error = "";
            public float x, y, width, height;
        }

        [Serializable]
        public sealed class HudView
        {
            public bool healthMoved, mapMoved, combatMoved, menuHidden;
        }

        [Serializable]
        public sealed class PerformanceView
        {
            public double captureMs, peakCaptureMs, assetMs;
            public int stateBytes, cachedAssets, queuedAssets;
        }

        [Serializable]
        public sealed class Command
        {
            public string id, session, action, tab, floorGuid, roomId, language;
            public long renderedSequence, revision;
            public bool hideHud, hudReady, combatReady;
            public int fromX, fromY, toX, toY, sourceId, targetId, rotation;
        }

        [Serializable]
        public sealed class CommandResult
        {
            public string id, message;
            public bool ok, pending;
        }
    }
}
