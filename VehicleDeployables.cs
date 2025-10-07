using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VehicleDeployables", "Kris+ChatGPT", "1.3.0")]
    [Description("Gives players deployable items that spawn vehicles when placed or used.")]
    public class VehicleDeployables : RustPlugin
    {
        // Permission required to use the give commands
        private const string PermissionGive = "vehicledeployables.give";

        /// <summary>
        /// Defines the configuration for a single vehicle. Each entry specifies
        /// the prefab path, skin ID, display name and optional base item short
        /// name for the token. The skin ID must be unique so the plugin can
        /// identify which vehicle to spawn when the deployable is placed.
        /// </summary>
        private class VehicleDefinition
        {
            public string Prefab { get; set; }
            public ulong SkinId { get; set; }
            public string DisplayName { get; set; }
            public string ItemShortName { get; set; }
        }

        /// <summary>
        /// Configuration container. Server owners can edit
        /// oxide/config/VehicleDeployables.json to customise which vehicles are
        /// available and what item is used for the tokens. The base item
        /// defaults to the large wood storage box (short name "box.wooden.large").
        /// Each vehicle may override the base item via ItemShortName.
        /// </summary>
        private class PluginConfig
        {
            public string BaseItemShortName { get; set; } = "box.wooden.large";
            public Dictionary<string, VehicleDefinition> Vehicles { get; set; }
                = new Dictionary<string, VehicleDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mini"] = new VehicleDefinition
                    {
                        Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                        SkinId = 2906148311UL, // official skin ID for mini【849976304038856†L122-L145】
                        DisplayName = "Mini Copter",
                        ItemShortName = "box.wooden.large"
                    },
                    ["scrappy"] = new VehicleDefinition
                    {
                        Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                        SkinId = 2783365006UL, // official skin ID for scrap heli【849976304038856†L122-L145】
                        DisplayName = "Scrap Heli",
                        ItemShortName = "furnace.large"
                    },
                    ["boat"] = new VehicleDefinition
                    {
                        Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                        SkinId = 2783365250UL, // official skin ID for rowboat【849976304038856†L122-L145】
                        DisplayName = "Rowboat",
                        ItemShortName = "kayak"
                    },
                    ["motorbike"] = new VehicleDefinition
                    {
                        Prefab = "assets/content/vehicles/bikes/motorbike.prefab",
                        SkinId = 3284204457UL, // official skin ID for motorbike【849976304038856†L122-L145】
                        DisplayName = "Motorbike",
                        ItemShortName = "box.wooden.large"
                    },
                    ["bike"] = new VehicleDefinition
                    {
                        Prefab = "assets/content/vehicles/bikes/pedalbike.prefab",
                        SkinId = 3284205070UL, // official skin ID for bicycle/pedalbike【849976304038856†L122-L145】
                        DisplayName = "Bike",
                        ItemShortName = "box.wooden.large"
                    }
                };
        }

        private PluginConfig config;

        // Internal lookup built from config. Keyed by lowercase vehicle key.
        private Dictionary<string, VehicleDefinition> vehicles = new Dictionary<string, VehicleDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Called when the plugin is initialised. Registers permissions, loads
        /// configuration and builds the internal lookup.
        /// </summary>
        private void Init()
        {
            permission.RegisterPermission(PermissionGive, this);
            LoadConfigValues();
            // Build dictionary of vehicles for quick lookup
            vehicles.Clear();
            foreach (var entry in config.Vehicles)
            {
                vehicles[entry.Key.ToLowerInvariant()] = entry.Value;
            }
        }

        /// <summary>
        /// Loads configuration from file or creates defaults if missing. Ensures
        /// the base item short name and vehicle entries are valid.
        /// </summary>
        private void LoadConfigValues()
        {
            try
            {
                config = Config.ReadObject<PluginConfig>();
            }
            catch
            {
                PrintWarning("[VehicleDeployables] Creating a new configuration file.");
                config = new PluginConfig();
                SaveConfig();
            }
            // Ensure base item short name is set
            if (string.IsNullOrEmpty(config.BaseItemShortName))
            {
                config.BaseItemShortName = "box.wooden.large";
            }
            // If vehicles dictionary is null or empty, populate with defaults
            if (config.Vehicles == null || config.Vehicles.Count == 0)
            {
                config.Vehicles = new PluginConfig().Vehicles;
                PrintWarning("[VehicleDeployables] Restoring default vehicle definitions to configuration.");
                SaveConfig();
            }
        }

        /// <summary>
        /// Writes the default configuration to disk. Called by Oxide when no
        /// configuration exists on first load.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            SaveConfig();
        }

        /// <summary>
        /// Chat command to give a player a vehicle token. Usage:
        /// /vdgive <player> <vehicleKey>
        /// </summary>
        [ChatCommand("vdgive")]
        private void CmdGiveDeployable(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermissionGive))
            {
                SendReply(player, "You do not have permission to use this command.");
                return;
            }
            if (args == null || args.Length < 2)
            {
                SendReply(player, "Usage: /vdgive <player name/ID> <vehicle type>");
                return;
            }
            string targetName = args[0];
            string typeKey = args[1].ToLowerInvariant();
            if (!vehicles.ContainsKey(typeKey))
            {
                SendReply(player, $"Invalid vehicle type '{typeKey}'. Options: {string.Join(", ", vehicles.Keys)}");
                return;
            }
            BasePlayer target = FindPlayer(targetName);
            if (target == null)
            {
                SendReply(player, $"Player '{targetName}' not found or not online.");
                return;
            }
            GiveTokenInternal(target, typeKey);
            SendReply(player, $"Gave {vehicles[typeKey].DisplayName} to {target.displayName}.");
        }

        /// <summary>
        /// Console command variant for granting tokens. Usage:
        /// vehicle.give <player name/ID> <vehicle type>
        /// </summary>
        [ConsoleCommand("vehicle.give")]
        private void CmdConsoleGive(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;
            // Only allow server console or admins
            var player = arg.Player();
            if (player != null && !player.IsAdmin)
            {
                arg.ReplyWith("You do not have permission to run this command.");
                return;
            }
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Usage: vehicle.give <player name/ID> <vehicle type>");
                return;
            }
            string targetName = arg.Args[0];
            string typeKey = arg.Args[1].ToLowerInvariant();
            if (!vehicles.ContainsKey(typeKey))
            {
                arg.ReplyWith($"Invalid vehicle type '{typeKey}'. Options: {string.Join(", ", vehicles.Keys)}");
                return;
            }
            BasePlayer target = FindPlayer(targetName);
            if (target == null)
            {
                arg.ReplyWith($"Player '{targetName}' not found or not online.");
                return;
            }
            GiveTokenInternal(target, typeKey);
            arg.ReplyWith($"Gave {vehicles[typeKey].DisplayName} to {target.displayName}.");
        }

        /// <summary>
        /// Hook that other plugins can call to grant a vehicle token by key.
        /// </summary>
        [HookMethod("GiveVehicleToken")]
        public void GiveVehicleToken(BasePlayer player, string typeKey)
        {
            if (player == null || string.IsNullOrEmpty(typeKey)) return;
            GiveTokenInternal(player, typeKey.ToLowerInvariant());
        }

        /// <summary>
        /// Creates a token item and places it in the target player's inventory or drops it
        /// at their feet if the inventory is full. The token uses the configured base
        /// item or the per-vehicle item and the unique skin ID defined for the vehicle.
        /// </summary>
        private void GiveTokenInternal(BasePlayer target, string typeKey)
        {
            if (!vehicles.TryGetValue(typeKey, out var data)) return;
            // Determine which item short name to use: per-vehicle override or global
            string shortName = !string.IsNullOrEmpty(data.ItemShortName) ? data.ItemShortName : config.BaseItemShortName;
            ItemDefinition def = ItemManager.FindItemDefinition(shortName);
            if (def == null)
            {
                PrintError($"Couldn't find item definition for '{shortName}'.");
                return;
            }
            Item item = ItemManager.Create(def, 1, data.SkinId);
            if (item == null)
            {
                PrintError("Failed to create vehicle token item.");
                return;
            }
            // Set the display name for the token
            item.name = data.DisplayName;
            item.MarkDirty();
            if (!target.inventory.GiveItem(item))
            {
                // Inventory full: drop the item at the player's feet
                item.Drop(target.transform.position + Vector3.up * 0.5f, Vector3.zero);
                SendReply(target, $"Your inventory is full; {data.DisplayName} has been dropped nearby.");
            }
        }

        /// <summary>
        /// Called when any entity is spawned. We no longer use this hook for
        /// vehicle deployment because boxes and furnaces don't trigger it in a
        /// useful way. The presence of this method is retained for potential
        /// future use, but it intentionally does nothing.
        /// </summary>
        private void OnEntitySpawned(BaseEntity entity)
        {
            // No-op
        }

        /// <summary>
        /// We previously attempted to support right‑click spawning via OnItemUse,
        /// but deployable items such as boxes and furnaces don't trigger this
        /// hook reliably, and handling both placement and usage caused
        /// duplication issues. To simplify behaviour and improve stability,
        /// this plugin no longer implements OnItemUse. Instead, vehicles
        /// are spawned exclusively via OnEntityBuilt when the token is placed.
        /// </summary>
        // Removed OnItemUse implementation. See OnEntityBuilt for spawning logic.

        /// <summary>
        /// When a token is placed (the player deploys the base item, e.g. box or
        /// furnace), this hook intercepts the placement and spawns the
        /// configured vehicle instead. The original deployable entity is
        /// immediately destroyed, ensuring no box or furnace remains in the
        /// world. The vehicle's ownership is inherited from the player placing
        /// the token. See the Oxide documentation for the OnEntityBuilt hook
        /// signature【740162575078919†L104-L123】.
        /// </summary>
        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (planner == null || gameObject == null) return;
            BaseEntity entity = gameObject.ToBaseEntity();
            if (entity == null) return;
            // Retrieve the short prefab name of the placed deployable (e.g. box.wooden.large)
            string shortName = entity.ShortPrefabName;
            if (string.IsNullOrEmpty(shortName)) return;
            // Retrieve the skin ID of the placed entity to differentiate between
            // multiple vehicles sharing the same base item
            ulong skinId = entity.skinID;
            // Loop through all configured vehicles and find a match on base item and skin
            foreach (var kvp in vehicles)
            {
                var data = kvp.Value;
                // Determine which base item this vehicle uses (vehicle override or global)
                string itemShortName = !string.IsNullOrEmpty(data.ItemShortName) ? data.ItemShortName : config.BaseItemShortName;
                if (!string.Equals(itemShortName, shortName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                // If the skin does not match, skip. This ensures we only spawn
                // when the correct skin token is placed.
                if (data.SkinId != 0 && data.SkinId != skinId)
                {
                    continue;
                }
                // Determine the owner. Use the planner's owner if available; otherwise
                // fall back to the entity's OwnerID.
                BasePlayer ownerPlayer = planner.GetOwnerPlayer();
                ulong ownerID = entity.OwnerID;
                if (ownerPlayer != null)
                {
                    ownerID = ownerPlayer.userID;
                }
                // Spawn the vehicle at the deployable's position plus a small vertical offset
                Vector3 spawnPos = entity.transform.position + new Vector3(0f, 1.5f, 0f);
                Quaternion spawnRot = entity.transform.rotation;
                BaseEntity vehicle = GameManager.server.CreateEntity(data.Prefab, spawnPos, spawnRot);
                if (vehicle != null)
                {
                    // Assign ownership so players can access the vehicle's lock if applicable
                    if (ownerID != 0) vehicle.OwnerID = ownerID;
                    vehicle.Spawn();
                }
                else
                {
                    PrintError($"Failed to create vehicle for '{kvp.Key}' via OnEntityBuilt. Prefab '{data.Prefab}' not found.");
                }
                // Destroy the placeholder deployable entity
                entity.Kill();
                // Break after handling the first match
                break;
            }
        }

        /// <summary>
        /// Helper to find a player by name or SteamID. Performs a case-insensitive
        /// name match or exact ID match.
        /// </summary>
        private BasePlayer FindPlayer(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return null;
            if (ulong.TryParse(nameOrId, out ulong uid))
            {
                return BasePlayer.FindByID(uid);
            }
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.displayName.Equals(nameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return null;
        }
    }
}
