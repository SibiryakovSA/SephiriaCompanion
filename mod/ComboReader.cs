using System;
using System.Collections.Generic;

namespace SephiriaCompanion
{
    internal sealed class ComboReader
    {
        readonly GameAssets assets;
        public ComboReader(GameAssets assets) { this.assets = assets; }

        public CompanionRuntime.ComboView[] Read(PlayerAvatar player)
        {
            var result = new List<CompanionRuntime.ComboView>();
            foreach (var pair in player.Inventory.currentSetEffectCount)
            {
                if (pair.Value <= 0) continue;
                var category = ItemDatabase.FindItemCategory(pair.Key);
                if (category == null) continue;
                var effect = player.Inventory.FindComboEffect(pair.Key);
                if (effect == null && category.comboEffectPrefab != null)
                    effect = category.comboEffectPrefab.GetComponent<ComboEffectBase>();
                if (effect == null) continue;
                var tiers = new List<CompanionRuntime.ComboTier>();
                foreach (var tier in effect.BuildEffectData(player, false, false))
                    tiers.Add(new CompanionRuntime.ComboTier
                    {
                        count = tier.Key,
                        active = pair.Value >= tier.Key,
                        description = ItemReader.Plain(string.Join("\n", tier.Value))
                    });
                result.Add(new CompanionRuntime.ComboView
                {
                    id = pair.Key,
                    name = ItemReader.Plain(category.Name),
                    icon = assets.Sprite(category.categoryIcon),
                    count = pair.Value,
                    description = ItemReader.Plain(effect.GetDefaultEffectString()),
                    tiers = tiers.ToArray()
                });
            }
            // Stable order keeps the same effect under the user's finger after updates.
            result.Sort((left, right) => string.Compare(left.id, right.id, StringComparison.Ordinal));
            return result.ToArray();
        }
    }
}
