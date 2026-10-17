using System;
using System.Collections.Generic;

namespace SephiriaCompanion
{
    internal sealed class InventoryService : IDisposable
    {
        readonly ItemReader reader;
        readonly ComboReader combos;
        GridInventory inventory;
        PlayerAvatar player;
        string floorGuid;
        bool dirty = true;
        long revision;
        public CompanionRuntime.InventoryView Current { get; private set; } = new CompanionRuntime.InventoryView();

        public InventoryService(ItemReader reader, ComboReader combos) { this.reader = reader; this.combos = combos; }

        public void Connect(PlayerAvatar current)
        {
            var next = current != null ? current.Inventory : null;
            string nextFloor = current != null ? current.currentFloorGuid : null;
            if (next != inventory)
            {
                Dispose();
                inventory = next;
                if (inventory != null)
                {
                    inventory.OnItemUpdatedForClient += Changed;
                    inventory.OnInventoryStorageChangedClientside += StorageChanged;
                    inventory.OnClear += Invalidate;
                    inventory.OnCharmEffectRefreshedForClient += Invalidate;
                    inventory.OnTabletRotatedClientside += Rotated;
                    inventory.OnUniquePairEnchantedClientside += Enchanted;
                    inventory.currentSetEffectCount.OnChange += ComboChanged;
                }
                dirty = true;
            }
            player = current;
            if (floorGuid != nextFloor) { floorGuid = nextFloor; dirty = true; }
        }

        void Changed(NewItemOwnInstance item, ItemPosition position) { dirty = true; }
        void StorageChanged(int before, int after) { dirty = true; }
        void Rotated(StoneTablet tablet, int rotation) { dirty = true; }
        void Enchanted(ItemPosition position) { dirty = true; }
        void ComboChanged(Mirror.SyncIDictionary<string, int>.Operation operation, string key, int value) { dirty = true; }
        public void Invalidate() { dirty = true; }

        public bool Capture()
        {
            if (!dirty) return false;
            var view = new CompanionRuntime.InventoryView { revision = ++revision, floorGuid = floorGuid };
            if (inventory == null) view.error = "inventory.loading";
            else
            {
                view.available = true;
                view.width = inventory.Width;
                view.height = inventory.Height;
                view.capacity = inventory.CurrentInventoryStorage;
                var items = new List<CompanionRuntime.ItemView>();
                foreach (var pair in inventory.inventoryMatrix)
                {
                    if (pair.Value == null) continue;
                    if (!ValidCell(pair.Key.x, pair.Key.y))
                    {
                        view.extraItems++;
                        continue;
                    }
                    items.Add(reader.Read(pair.Value, player));
                }
                view.items = items.ToArray();
                view.combos = combos.Read(player);
            }
            Current = view;
            dirty = false;
            return true;
        }

        bool ValidCell(int x, int y) => inventory != null && x >= 0 && y >= 0 && x < inventory.Width &&
            y < inventory.Height && y * inventory.Width + x < inventory.CurrentInventoryStorage;

        void Validate(CompanionRuntime.Command command, PlayerAvatar current)
        {
            if (current == null || current.IsDead || current.IsInBattle || current.loadingScreenType != -1)
                throw new InvalidOperationException("inventory.unavailable");
            if (inventory == null || current.Inventory != inventory || command.floorGuid != current.currentFloorGuid ||
                command.revision != Current.revision || dirty)
                throw new InvalidOperationException("inventory.stale");
            if (!ValidCell(command.fromX, command.fromY) || ItemId(command.fromX, command.fromY) != command.sourceId)
                throw new InvalidOperationException("inventory.stale");
        }

        public void Swap(CompanionRuntime.Command command, PlayerAvatar current)
        {
            Validate(command, current);
            if (!ValidCell(command.toX, command.toY) || command.fromX == command.toX && command.fromY == command.toY)
                throw new InvalidOperationException("inventory.invalid_cell");
            if (command.sourceId == 0 || ItemId(command.toX, command.toY) != command.targetId)
                throw new InvalidOperationException("inventory.stale");
            inventory.Swap((sbyte)command.fromX, (sbyte)command.fromY, (sbyte)command.toX, (sbyte)command.toY);
            dirty = true;
        }

        public void Rotate(CompanionRuntime.Command command, PlayerAvatar current)
        {
            Validate(command, current);
            var item = inventory.FindItem((sbyte)command.fromX, (sbyte)command.fromY);
            if (item?.StoneTablet == null || item.StoneTablet.rotation != command.rotation || !DungeonManager.IsTabletRotatable(item.StoneTablet))
                throw new InvalidOperationException("inventory.rotation_unavailable");
            // Same authoritative operation as the native inventory's rotate button.
            inventory.DoClickAction(new ItemPosition((sbyte)command.fromX, (sbyte)command.fromY));
            dirty = true;
        }

        public bool SwapObserved(CompanionRuntime.Command command) => inventory != null &&
            ItemId(command.toX, command.toY) == command.sourceId && ItemId(command.fromX, command.fromY) == command.targetId;

        public bool RotationObserved(CompanionRuntime.Command command)
        {
            var item = inventory != null ? inventory.FindItem((sbyte)command.fromX, (sbyte)command.fromY) : null;
            return item?.InstanceID == command.sourceId && item.StoneTablet != null && item.StoneTablet.rotation == (command.rotation + 1) % 4;
        }

        int ItemId(int x, int y) => inventory.FindItem((sbyte)x, (sbyte)y)?.InstanceID ?? 0;

        public void Dispose()
        {
            if (inventory != null)
            {
                inventory.OnItemUpdatedForClient -= Changed;
                inventory.OnInventoryStorageChangedClientside -= StorageChanged;
                inventory.OnClear -= Invalidate;
                inventory.OnCharmEffectRefreshedForClient -= Invalidate;
                inventory.OnTabletRotatedClientside -= Rotated;
                inventory.OnUniquePairEnchantedClientside -= Enchanted;
                inventory.currentSetEffectCount.OnChange -= ComboChanged;
            }
            inventory = null;
            player = null;
        }
    }
}
