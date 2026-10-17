using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaCompanion
{
    internal sealed class CombatHudService : IDisposable
    {
        readonly GameAssets assets;
        readonly ItemReader reader;
        PlayerAvatar player;
        ItemController items;
        IntegratedActionController actions;
        UI_SkillQuickSlotBar skillBar;
        UI_PlayerEffectHUD effectRoot;
        UI_EffectHUD_Basic[] effects = Array.Empty<UI_EffectHUD_Basic>();
        bool dirty = true;
        long revision;
        float nextDiscovery;
        string floorGuid, signature;
        public CompanionRuntime.HudContent Content { get; private set; } = new CompanionRuntime.HudContent();

        public CombatHudService(GameAssets assets, ItemReader reader) { this.assets = assets; this.reader = reader; }

        public void Connect(PlayerAvatar current)
        {
            if (current != player)
            {
                Dispose();
                player = current;
                if (player != null)
                {
                    items = player.GetComponent<ItemController>();
                    actions = player.GetComponent<IntegratedActionController>();
                    if (items != null) items.OnQuickSlotSelectedClientside += Selected;
                    if (actions != null) actions.OnUpdatedQuickSlot += Invalidate;
                    player.Inventory.OnItemUpdatedForClient += ItemChanged;
                    player.OnEffectHUDCreated += EffectCreated;
                    player.OnEffectHUDDestroyed += EffectDestroyed;
                }
                nextDiscovery = 0;
                dirty = true;
            }
            if (current != null && current.currentFloorGuid != floorGuid)
            {
                floorGuid = current.currentFloorGuid;
                dirty = true;
            }
        }

        void Selected(ItemController.QuickSlot slot, bool manually) { dirty = true; }
        void ItemChanged(NewItemOwnInstance item, ItemPosition position) { dirty = true; }
        void EffectCreated(string id, string name) { dirty = true; }
        void EffectDestroyed(string name) { dirty = true; }
        public void Invalidate() { dirty = true; }

        public bool CaptureContent()
        {
            if (player == null) return false;
            if (Time.unscaledTime >= nextDiscovery)
            {
                // Slow fallback also picks up changes in the native controller layout.
                nextDiscovery = Time.unscaledTime + 5;
                dirty = true;
            }
            if (!dirty) return false;
            var manager = UIManager.Instance;
            skillBar = manager != null ? manager.GetElement<UI_SkillQuickSlotBar>() : null;
            effectRoot = manager != null ? manager.GetElement<UI_PlayerEffectHUD>() : null;
            effects = effectRoot != null ? effectRoot.GetComponentsInChildren<UI_EffectHUD_Basic>(true) : Array.Empty<UI_EffectHUD_Basic>();
            var content = new CompanionRuntime.HudContent { floorGuid = floorGuid };
            var potions = new List<CompanionRuntime.QuickItem>();
            if (items != null)
                for (int index = 0; index < items.quickSlotTable.Count; index++)
                {
                    var slot = items.quickSlotTable[index];
                    var item = player.Inventory.FindItem(player.Inventory.IdxToPos(slot.idx));
                    if (item != null) potions.Add(new CompanionRuntime.QuickItem { slot = index, item = reader.Read(item, player) });
                }
            content.potions = potions.ToArray();
            var skills = new List<CompanionRuntime.SkillInfo>();
            if (actions != null)
            {
                ReadSkillInfo(actions.quickSlots, false, skills);
                ReadSkillInfo(actions.quickSlotsWeapon, true, skills);
            }
            content.skills = skills.ToArray();
            var effectInfo = new List<CompanionRuntime.EffectInfo>();
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                effectInfo.Add(new CompanionRuntime.EffectInfo
                {
                    id = effect.gameObject.name,
                    name = ItemReader.Plain(effect.effectName.ToString()),
                    description = ItemReader.Plain(effect.effectFlavorText.ToString()),
                    kind = effect.effectType.ToString(),
                    icon = Icon(effect, effect.whiteOverlayImage)
                });
            }
            content.effects = effectInfo.ToArray();
            var minimap = manager?.GetElement<UI_HUDMapViewer>();
            content.playerIcon = minimap != null ? Icon(minimap.playerIcon) : null;
            var dash = manager?.GetElement<UI_DashBar>();
            content.dashIcon = dash?.valueSprites != null && dash.valueSprites.Length > 0 ? assets.Sprite(dash.valueSprites[0]) : null;
            content.icons["money"] = Icon(manager?.GetElement<UI_MoneyIndicator>());
            content.icons["sapphire"] = Icon(manager?.GetElement<UI_SapphireIndicator>());
            content.icons["dice"] = Icon(manager?.GetElement<UI_DiceIndicator>());
            var health = manager?.GetElement<UI_HPBar>();
            content.icons["hp"] = health != null ? assets.Sprite(health.carrotHeartImage?.sprite) : null;
            string nextSignature = JsonProtocol.Write(content);
            dirty = false;
            if (nextSignature == signature) return false;
            signature = nextSignature;
            content.revision = ++revision;
            Content = content;
            return true;
        }

        UI_SkillQuickSlotElement NativeSlot(int index, bool weapon)
        {
            if (skillBar == null) return null;
            bool joystick = skillBar.joystickSkillSlotBar != null && skillBar.joystickSkillSlotBar.activeSelf;
            var pool = weapon ? (joystick ? skillBar.poolWeaponJoystick : skillBar.poolWeapon) :
                (joystick ? skillBar.poolJoystick : skillBar.pool);
            return pool != null && index < pool.Length ? pool[index] : null;
        }

        void ReadSkillInfo(QuickSlotData[] slots, bool weapon, List<CompanionRuntime.SkillInfo> result)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                var native = NativeSlot(index, weapon);
                if (slot == null || slot.IsEmpty || weapon && (native == null || !native.gameObject.activeSelf)) continue;
                string id = (weapon ? "w" : "s") + index;
                string name = "skill.weapon_" + slot.weaponButton, description = "";
                Sprite icon = native != null ? native.iconImage?.sprite : null;
                if (slot.magic != null && slot.magic.ContainedMagic != null)
                {
                    name = ItemReader.Plain(slot.magic.ContainedMagic.Name);
                    icon = slot.magic.ContainedMagic.icon;
                    var magic = slot.magic.ContainedMagic.magicPrefab.GetComponent<ActiveSkill>();
                    if (magic != null) description = ItemReader.Plain(magic.GetEffectString(slot.magic.DisplayedLevel, false, 0));
                }
                else if (slot.active != null)
                {
                    name = ItemReader.Plain(slot.active.GetNameString());
                    description = ItemReader.Plain(slot.active.GetEffectString()) + "\n" +
                        ItemReader.Plain(slot.active.GetEffectHeaderString()) + "\n" + ItemReader.Plain(slot.active.GetEffectValueString());
                    icon = slot.active.GetIcon();
                }
                result.Add(new CompanionRuntime.SkillInfo
                {
                    id = id,
                    slot = index,
                    weapon = weapon,
                    name = name,
                    description = description,
                    icon = assets.Sprite(icon)
                });
            }
        }

        string Icon(Component root, Image excluded = null)
        {
            if (root == null) return null;
            Image fallback = null;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == excluded || image.sprite == null) continue;
                if (fallback == null) fallback = image;
                if (image.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) return assets.Sprite(image.sprite);
            }
            return assets.Sprite(fallback?.sprite);
        }

        public CompanionRuntime.CombatView Read()
        {
            if (player == null) return new CompanionRuntime.CombatView();
            var dash = player.CurrentDashModule;
            var result = new CompanionRuntime.CombatView
            {
                available = true,
                dashMax = dash != null ? dash.MaxDashCount : 0,
                dashAvailable = dash != null ? Math.Max(0, dash.MaxDashCount - dash.currentDashCount) : 0,
                dashRecharge = dash != null && dash.currentDashCount > 0 ? dash.cooldownTimer.Ratio : 1,
                selectedPotion = items != null ? items.SelectedQuickSlotIdx : -1
            };
            var skills = new List<CompanionRuntime.SkillState>();
            if (actions != null) foreach (var info in Content.skills)
                {
                    var slots = info.weapon ? actions.quickSlotsWeapon : actions.quickSlots;
                    if (slots == null || info.slot < 0 || info.slot >= slots.Length) continue;
                    var slot = slots[info.slot];
                    if (slot == null || slot.IsEmpty) continue;
                    var state = new CompanionRuntime.SkillState { id = info.id };
                    if (slot.magic != null)
                    {
                        state.cooldown = slot.magic.CooldownRatio;
                        state.ammo = slot.magic.currentAmmo;
                        state.maxAmmo = slot.magic.maxAmmo;
                        state.manaCost = slot.magic.GetCost(player, slot.magic.CurrentLevelToIdx());
                        state.insufficientMana = player.MP < state.manaCost;
                    }
                    else if (slot.active != null) state.cooldown = slot.active.GetCooldownRatio();
                    skills.Add(state);
                }
            result.skills = skills.ToArray();
            var values = new List<CompanionRuntime.EffectState>();
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                string value = effect is UI_EffectHUD_Value numeric ? numeric.valueText?.text :
                    effect is UI_EffectHUD_Stack stack ? stack.stackText?.text : "";
                float fill = effect is UI_EffectHUD_Stack meter && meter.fillImage != null ? meter.fillImage.fillAmount : 0;
                values.Add(new CompanionRuntime.EffectState { id = effect.gameObject.name, value = GameContext.Text(value), fill = fill });
            }
            result.effects = values.ToArray();
            return result;
        }

        public void Dispose()
        {
            if (items != null) items.OnQuickSlotSelectedClientside -= Selected;
            if (actions != null) actions.OnUpdatedQuickSlot -= Invalidate;
            if (player != null)
            {
                player.Inventory.OnItemUpdatedForClient -= ItemChanged;
                player.OnEffectHUDCreated -= EffectCreated;
                player.OnEffectHUDDestroyed -= EffectDestroyed;
            }
            player = null; items = null; actions = null; skillBar = null; effectRoot = null;
            effects = Array.Empty<UI_EffectHUD_Basic>();
            signature = null; floorGuid = null; dirty = true;
            Content = new CompanionRuntime.HudContent();
        }
    }
}
