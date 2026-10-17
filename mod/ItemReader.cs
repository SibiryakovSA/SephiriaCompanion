using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SephiriaCompanion
{
    internal sealed class ItemReader
    {
        readonly GameAssets assets;
        public ItemReader(GameAssets assets) { this.assets = assets; }

        public static string Plain(string value) => GameContext.Text(KeywordDatabase.Convert(value ?? "", useColor: false, useSprite: false));

        public CompanionRuntime.ItemView Read(NewItemOwnInstance item, PlayerAvatar player)
        {
            var entity = item.Entity;
            var result = new CompanionRuntime.ItemView
            {
                id = item.InstanceID,
                entityId = item.EntityID,
                x = item.XIdx,
                y = item.YIdx,
                name = Plain(item.Name),
                description = Plain(item.Context),
                quantity = item.Quantity,
                maxQuantity = entity != null ? ItemDatabase.MaxStackCount(entity.type) : 1,
                icon = assets.Sprite(entity?.Icon),
                kind = "item"
            };
            if (item.Charm != null) ReadCharm(result, item.Charm, player);
            else if (item.StoneTablet != null) ReadTablet(result, item.StoneTablet, player.Inventory);
            else if (entity != null && entity.resourcePrefab != null)
            {
                var potion = entity.resourcePrefab.GetComponent<PotionEffect>();
                if (potion != null) { result.kind = "potion"; result.effects = Plain(potion.GetEffectString("• ", "", player)); }
            }
            return result;
        }

        void ReadCharm(CompanionRuntime.ItemView result, Charm_Basic charm, PlayerAvatar player)
        {
            result.kind = "artifact";
            result.level = charm.DisplayedLevel;
            result.maxLevel = charm.maxLevel;
            result.active = charm.IsEffectEnabled;
            result.unique = charm.isUniqueEffect;
            int overflow = Math.Max(0, charm.DisplayedLevel - charm.maxLevel);
            result.effects = Plain(charm.BuildEffectString(player, "• ", "", charm.DisplayedLevel, overflow, false, false));
            result.conditions = charm.criteria != null ? Plain(charm.criteria.ToString()) : "";
            result.penalty = Plain(charm.GetPenaltyString());
            if (charm is IMagicCharm magic && magic.ContainedMagic != null)
            {
                var skill = magic.ContainedMagic.magicPrefab.GetComponent<ActiveSkill>();
                if (skill != null) result.effects += "\n\n" + Plain(magic.ContainedMagic.Name) + "\n" +
                    Plain(skill.GetEffectString(charm.DisplayedLevel, false, 0)) + "\n" +
                    Plain(skill.GetSpecsHeader(charm.DisplayedLevel)) + "\n" + Plain(skill.GetSpecsValue(charm.DisplayedLevel));
            }
            if (charm is ICompanionCharm companion)
            {
                int index = charm.LevelToIdx(charm.DisplayedLevel);
                result.effects += "\n\n" + Plain(companion.GetCompanionName(index)) + "\n" +
                    Plain(companion.GetCompanionDescription(index)) + "\n" + Plain(companion.GetCompanionParamHeader(index)) +
                    "\n" + Plain(companion.GetCompanionParamValue(index));
            }
        }

        void ReadTablet(CompanionRuntime.ItemView result, StoneTablet tablet, GridInventory bag)
        {
            result.kind = "tablet";
            result.active = tablet.IsApplied;
            result.rotation = tablet.rotation;
            result.canRotate = DungeonManager.IsTabletRotatable(tablet);
            var origin = new ItemPosition((sbyte)result.x, (sbyte)result.y);
            var cells = new List<CompanionRuntime.TabletCell>();
            var criteria = StoneTablet.ParseQuery(tablet.GetConditionQuery(result.id), bag.Width, bag.Height,
                bag.CurrentInventoryStorage, origin, tablet.rotation, out var readable);
            bool allowedPosition = false;
            foreach (var entry in criteria)
                if (entry.value == "PLACED" && entry.position.x == result.x && entry.position.y == result.y) allowedPosition = true;
            foreach (var entry in criteria)
            {
                var condition = new StoneTablet.AdditionCriteriaData(entry);
                var occupant = bag.FindItem(entry.position);
                bool met = condition.effectType == StoneTablet.CriteriaType.AnyItem ? occupant != null :
                    condition.effectType == StoneTablet.CriteriaType.OnlyCharm ? occupant?.Entity?.type == EItemType.Charm :
                    condition.effectType != StoneTablet.CriteriaType.Placed || allowedPosition;
                cells.Add(new CompanionRuntime.TabletCell
                {
                    x = entry.position.x,
                    y = entry.position.y,
                    criteria = condition.effectType.ToString(),
                    met = met,
                    worldX = entry.isXWorldPosition,
                    worldY = entry.isYWorldPosition
                });
            }
            result.conditions = ReadConditions(readable);
            foreach (var entry in StoneTablet.ParseQuery(tablet.GetQuery(result.id), bag.Width, bag.Height,
                bag.CurrentInventoryStorage, origin, tablet.rotation, out _))
            {
                var effect = new StoneTablet.AdditionEffectData(entry);
                cells.Add(new CompanionRuntime.TabletCell
                {
                    x = entry.position.x,
                    y = entry.position.y,
                    effect = effect.effectType.ToString(),
                    amount = effect.levelParam,
                    worldX = entry.isXWorldPosition,
                    worldY = entry.isYWorldPosition
                });
            }
            result.cells = cells.ToArray();
        }

        static string ReadConditions(List<StoneTablet.HumanReadableAddition> readable)
        {
            var tooltip = UIManager.Instance?.GetElement<UI_StoneTabletTooltip>();
            if (tooltip == null || readable == null) return "";
            var result = new StringBuilder();
            foreach (var addition in readable)
            {
                string condition = addition.criteria, area = addition.areaName;
                foreach (var pair in tooltip.criteriaLocalizations) if (pair.key == condition) { condition = pair.localized.ToString(); break; }
                foreach (var pair in tooltip.areaLocalizations) if (pair.key == area) { area = pair.localized.ToString(); break; }
                result.AppendLine(Plain(condition + " " + area));
            }
            return result.ToString().Trim();
        }
    }
}
