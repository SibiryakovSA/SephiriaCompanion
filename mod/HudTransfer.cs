using System.Collections.Generic;
using UnityEngine;

namespace SephiriaCompanion
{
    public sealed class HudTransfer
    {
        sealed class Entry
        {
            readonly CanvasGroup group;
            readonly bool added, blocks, interactive;
            float alpha;
            public bool Alive => group != null;

            public Entry(GameObject target)
            {
                group = target.GetComponent<CanvasGroup>();
                added = group == null;
                if (added) group = target.AddComponent<CanvasGroup>();
                alpha = group.alpha;
                blocks = group.blocksRaycasts;
                interactive = group.interactable;
            }

            public void Hide()
            {
                if (group == null) return;
                // Preserve visibility changes made by the game while transferred
                // (for example, acquiring the first coin or unlocking sapphires).
                if (group.alpha > 0) alpha = group.alpha;
                group.alpha = 0;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            public void Restore()
            {
                if (group == null) return;
                group.alpha = alpha;
                group.blocksRaycasts = blocks;
                group.interactable = interactive;
                // A new player can bind to the same UI in this LateUpdate.
                // Remove only our added component immediately so it cannot be
                // rediscovered while scheduled for destruction.
                if (added) Object.DestroyImmediate(group);
            }
        }

        readonly Dictionary<int, Entry> health = new Dictionary<int, Entry>();
        readonly Dictionary<int, Entry> maps = new Dictionary<int, Entry>();
        readonly Dictionary<int, Entry> combat = new Dictionary<int, Entry>();
        PlayerAvatar player;
        UIManager currentManager;
        bool discovered;
        bool wasHealth, wasMap, wasCombat;
        float nextDiscovery;
        public CompanionRuntime.HudView State { get; } = new CompanionRuntime.HudView();

        static void Clear(Dictionary<int, Entry> entries)
        {
            foreach (var entry in entries.Values) entry.Restore();
            entries.Clear();
        }

        static void Add(Dictionary<int, Entry> entries, Component marker)
        {
            if (marker == null || !marker.gameObject.scene.IsValid()) return;
            int id = marker.gameObject.GetInstanceID();
            if (!entries.ContainsKey(id)) entries[id] = new Entry(marker.gameObject);
        }

        void Discover()
        {
            var manager = UIManager.Instance;
            if (manager == null) return;
            // UI_PlayerHP is the real health root. UI_TopLeftHUD is an unused marker
            // in current builds. Discover the non-UIBase root only when binding a player.
            foreach (var root in Resources.FindObjectsOfTypeAll<UI_PlayerHP>())
            {
                Add(health, root);
                Add(health, root.hpBar);
            }
            Add(health, manager.GetElement<UI_PlayerMP>());
            Add(health, manager.GetElement<UI_MoneyIndicator>());
            Add(health, manager.GetElement<UI_SapphireIndicator>());
            Add(health, manager.GetElement<UI_ExpBar>());
            Add(health, manager.GetElement<UI_DiceIndicator>());
            Add(maps, manager.GetElement<UI_HUDMapViewer>());
            Add(combat, manager.GetElement<UI_DashBar>());
            Add(combat, manager.GetElement<UI_CurrentQuickSlotHUD>());
            Add(combat, manager.GetElement<UI_SkillQuickSlotBar>());
            Add(combat, manager.GetElement<UI_PlayerEffectHUD>());
            Add(combat, manager.GetElement<UI_HUDMenu>());
            foreach (var bar in Resources.FindObjectsOfTypeAll<UI_QuickSlotBar>()) Add(combat, bar);
            discovered = health.Count > 0 && maps.Count > 0 &&
                manager.GetElement<UI_DashBar>() != null && manager.GetElement<UI_CurrentQuickSlotHUD>() != null &&
                manager.GetElement<UI_SkillQuickSlotBar>() != null && manager.GetElement<UI_HUDMenu>() != null;
        }

        public void Apply(PlayerAvatar currentPlayer, bool hideHealth, bool hideMap, bool hideCombat)
        {
            if (hideHealth != wasHealth || hideMap != wasMap || hideCombat != wasCombat) { discovered = false; nextDiscovery = 0; }
            wasHealth = hideHealth;
            wasMap = hideMap;
            wasCombat = hideCombat;
            if (currentPlayer != player || UIManager.Instance != currentManager)
            {
                Restore();
                player = currentPlayer;
                currentManager = UIManager.Instance;
                discovered = false;
                nextDiscovery = 0;
            }
            if (!hideHealth && health.Count > 0) { Clear(health); discovered = false; }
            if (!hideMap && maps.Count > 0) { Clear(maps); discovered = false; }
            if (!hideCombat && combat.Count > 0) { Clear(combat); discovered = false; }
            if ((hideHealth || hideMap || hideCombat) && !discovered && Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + 5;
                Discover();
                if (!hideHealth) Clear(health);
                if (!hideMap) Clear(maps);
                if (!hideCombat) Clear(combat);
            }
            if (hideHealth) foreach (var entry in health.Values) entry.Hide();
            if (hideMap) foreach (var entry in maps.Values) entry.Hide();
            if (hideCombat) foreach (var entry in combat.Values) entry.Hide();
            State.healthMoved = hideHealth && health.Count > 0;
            State.mapMoved = hideMap && maps.Count > 0;
            State.combatMoved = State.menuHidden = hideCombat && combat.Count > 0;
        }

        public void Restore()
        {
            Clear(health);
            Clear(maps);
            Clear(combat);
            State.healthMoved = State.mapMoved = State.combatMoved = State.menuHidden = false;
            discovered = false;
            wasHealth = wasMap = wasCombat = false;
        }
    }
}
