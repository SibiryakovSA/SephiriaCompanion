using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SephiriaCompanion
{
    internal sealed class GameContext
    {
        static readonly Regex Markup = new Regex("<[^>]*>", RegexOptions.Compiled);
        PlayerAvatar player;
        LevelController level;
        FloorGenerator floor;
        string floorGuid, floorName;
        float nextLookup;
        float nextNameLookup;

        public PlayerAvatar Player
        {
            get
            {
                if (player != null) return player;
                if (Time.unscaledTime < nextLookup) return null;
                nextLookup = Time.unscaledTime + .5f;
                if (PlayerSpawner.MultiplayerList != null)
                    foreach (var spawner in PlayerSpawner.MultiplayerList)
                        if (spawner != null && spawner.isLocalPlayer && spawner.PlayerAvatar != null)
                        {
                            player = spawner.PlayerAvatar;
                            level = player.GetComponent<LevelController>();
                            break;
                        }
                return player;
            }
        }

        public FloorGenerator Floor
        {
            get
            {
                var current = Player;
                if (current == null) return null;
                if (floor == null || floorGuid != current.currentFloorGuid)
                {
                    floorGuid = current.currentFloorGuid;
                    floor = FloorGenerator.FindByGuid(floorGuid);
                    floorName = ResolveFloorName(floor);
                    nextNameLookup = Time.unscaledTime + 2;
                }
                else if (floorName == "Текущий этаж" && Time.unscaledTime >= nextNameLookup)
                {
                    floorName = ResolveFloorName(floor);
                    nextNameLookup = Time.unscaledTime + 2;
                }
                return floor;
            }
        }

        public CompanionRuntime.PlayerView ReadPlayer()
        {
            var current = Player;
            if (current == null) return null;
            var currentFloor = Floor;
            return new CompanionRuntime.PlayerView
            {
                hp = current.hp,
                maxHp = current.MaxHp,
                shield = current.Shield,
                mp = current.mp,
                maxMp = current.maxMp,
                reservedMp = current.reservedMp,
                money = current.currentMoney,
                attack = current.attack,
                level = level != null ? level.currentLevel : 0,
                exp = level != null ? level.currentExp : 0,
                expRatio = level != null ? level.Ratio : 0,
                nextLevelExp = level != null && level.currentLevel < LevelController.ExpTableByLevel.Length
                    ? LevelController.ExpTableByLevel[level.currentLevel] : 0,
                sapphire = current.spawner?.LocalDataStorage != null ? current.spawner.LocalDataStorage.GetSapphire() : 0,
                dice = current.rerollDice,
                inBattle = current.IsInBattle,
                dead = current.IsDead,
                floorGuid = current.currentFloorGuid,
                floor = floorName ?? "Текущий этаж",
                x = current.transform.position.x,
                y = current.transform.position.y,
                canTravel = CanTravel(current)
            };
        }

        static string ResolveFloorName(FloorGenerator floor)
        {
            if (floor == null) return "Текущий этаж";
            string name = Text(floor.aName.ToString());
            if (!MissingLocalization(name)) return name;
            var dungeon = DungeonManager.Instance;
            FloorData data = floor.DataOnServer;
            if (data == null && dungeon != null) dungeon.generatedFloors.TryGetValue(floor.guid, out data);
            var stage = data != null && dungeon != null ? dungeon.FindStage(data.stageName) : null;
            name = stage != null ? Text(stage.aName.ToString()) : "";
            if (MissingLocalization(name)) return "Текущий этаж";
            return data.nodeProgress > 0 ? name + " · " + (data.nodeProgress + 1) : name;
        }

        static bool MissingLocalization(string name) => string.IsNullOrWhiteSpace(name) ||
            name.EndsWith("_Name", StringComparison.Ordinal) || name.StartsWith("Floor_", StringComparison.Ordinal);

        public static string Text(string text) => Markup.Replace(text ?? "", "").Trim();

        public static bool CanTravel(PlayerAvatar player) => player != null && player.loadingScreenType == -1 &&
            !player.IsDead && !player.IsInBattle && player.CanMove && player.blockMoveByInput <= 0 &&
            !player.CanFastMove.IsFalse() && ScreenFader.Instance != null && !ScreenFader.Instance.IsFading;
    }
}
