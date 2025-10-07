using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RandomFarm", "Kris+ChatGPT", "1.2.0")]
    [Description("Overrides gathering to give random items, with Tea/Pie boosts and Happy Hour.")]
    public class RandomFarm : RustPlugin
    {
        #region Permissions & Commands

        private const string PermHappyHour = "randomfarm.happyhour";

        // Permission for admin actions (deleting and repopulating spawns)
        private const string PermAdmin = "randomfarm.admin";

        [ChatCommand("happyhour")]
        private void CmdHappyHour(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PermHappyHour))
            {
                PrintToChat(player, "<color=#ff6666>[RandomFarm]</color> You lack permission.");
                return;
            }

            if (args.Length == 0)
            {
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Usage: /happyhour <start|stop|status>");
                return;
            }

            var arg = args[0].ToLower();
            if (arg == "start")
            {
                if (happyHourActive)
                {
                    PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Happy Hour already active.");
                    return;
                }
                StartHappyHour();
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Forced Happy Hour started.");
            }
            else if (arg == "stop")
            {
                if (!happyHourActive)
                {
                    PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Happy Hour is not active.");
                    return;
                }
                StopHappyHour();
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Forced Happy Hour stopped.");
            }
            else if (arg == "status")
            {
                PrintToChat(player, $"<color=#ffcc00>[RandomFarm]</color> Happy Hour active: <b>{happyHourActive}</b>");
            }
            else
            {
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Usage: /happyhour <start|stop|status>");
            }
        }

        /// <summary>
        /// General RandomFarm command handler. Allows admins to delete and repopulate all monument
        /// loot spawns via chat. Usage: /randomfarm del to delete spawns; /randomfarm pop to
        /// repopulate them. Requires the randomfarm.admin permission or server admin status.
        /// </summary>
        [ChatCommand("randomfarm")]
        private void CmdRandomFarm(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            // Only allow players with admin permission or server admin status to use these commands
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                PrintToChat(player, "<color=#ff6666>[RandomFarm]</color> You lack permission to use this command.");
                return;
            }
            if (args.Length == 0)
            {
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Usage: /randomfarm <del|pop>");
                return;
            }
            var sub = args[0].ToLower();
            if (sub == "del" || sub == "delete")
            {
                // Delete all monument loot prefabs. This uses the server console 'del' command
                // which removes entities matching the supplied path.
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "del assets/bundled/prefabs/radtown/");
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Loot containers removed. Use /randomfarm pop to respawn.");
            }
            else if (sub == "pop" || sub == "respawn")
            {
                // Force a respawn of all spawn groups to refill crates and barrels. See spawn.fill_groups for details.
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "spawn.fill_groups");
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Loot containers respawned.");
            }
            else
            {
                PrintToChat(player, "<color=#ffcc00>[RandomFarm]</color> Unknown subcommand. Usage: /randomfarm <del|pop>");
            }
        }

        #endregion

        #region Config

        private ConfigData config;

        private class ConfigData
        {
            public Dictionary<string, ItemEntry> Items = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase);
            public TeaConfig Tea = new TeaConfig();
            public PieConfig Pie = new PieConfig();
            public HappyHourConfig HappyHour = new HappyHourConfig();
            public int BuffDurationMinutes = 30;
            public bool OverrideAllGather = true; // if false, you can allow vanilla on bodies, etc., but default is override all

            // NEW: Per-prefab loot tables for crates/boxes. Each entry maps the prefab short name
            // to a dictionary of items with their ItemEntry (weight, amounts, etc.). When defined,
            // these override the default rolling logic for that specific prefab.
            public Dictionary<string, Dictionary<string, ItemEntry>> PrefabLoot = new Dictionary<string, Dictionary<string, ItemEntry>>(StringComparer.OrdinalIgnoreCase);

            // NEW: Barrel-specific loot table. Any loot container whose prefab name contains "barrel"
            // will roll from this dictionary instead of the default crate logic.
            public Dictionary<string, ItemEntry> BarrelLoot = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase);

            // NEW: NPC body loot table. When an NPC corpse is created the default loot will be replaced
            // with items rolled from this dictionary (using weights, min/max amounts). Players are not affected.
            public Dictionary<string, ItemEntry> NpcLoot = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase);

            // NEW: Loot tables for hackable crates (oil rig/other monuments). The key should match the
            // short prefab name of the hackable crate. If present, the dictionary of items defines the
            // loot pool for that crate.
            public Dictionary<string, Dictionary<string, ItemEntry>> HackableLoot = new Dictionary<string, Dictionary<string, ItemEntry>>(StringComparer.OrdinalIgnoreCase);

            // NEW: Loot tables for cargo hackable crates. These can differ from oil rig or other hackables.
            public Dictionary<string, Dictionary<string, ItemEntry>> CargoHackableLoot = new Dictionary<string, Dictionary<string, ItemEntry>>(StringComparer.OrdinalIgnoreCase);
        }

        private class ItemEntry
        {
            public string DisplayName = "";
            public bool Enabled = true;
            public float Weight = 1f;
            public int MinAmount = 1;
            public int MaxAmount = 1;
            // When set, this entry will run the specified console command instead
            // of giving a physical item. Use placeholders {steamid} and {playername}
            // to insert the player's Steam ID or display name into the command.
            public string Command { get; set; } = null;
        }

        private class TeaConfig
        {
            // Applies to any farming action (random drops), not just that resource type, by design of "Random Farm".
            public float BasicDoubleChance = 0.25f; // 25% chance to double
            public float AdvancedMultiplier = 2f;    // 2x
            public float PureMultiplier = 3f;        // 3x
            public bool EnableWoodTea = true;
            public bool EnableOreTea = true;
            public bool EnableScrapTea = true;
        }

        private class PieConfig
        {
            public bool EnableBearPie = true;
            public float BearPieMultiplier = 1.5f; // +150%
        }

		private class HappyHourConfig
		{
			public bool Enabled = true;
			public int IntervalMinutes = 120;   // every 2 hours
			public int DurationMinutes = 10;    // lasts 10 minutes

			// NEW: prefer multiplier semantics during HH
			public bool UseMultiplier = true;   // if true, use Multiplier; if false, use ForcedStackAmount
			public float Multiplier = 5f;       // x5 the normal rolled amount

			// Legacy/optional: fixed stack count mode (UseMultiplier=false)
			public int ForcedStackAmount = 5;

			public string StartMessage = "<color=#ffcc00>[RandomFarm]</color> <b>Happy Hour</b> started! All drops are x5 for 10 minutes!";
			public string EndMessage = "<color=#ffcc00>[RandomFarm]</color> <b>Happy Hour</b> ended. Thanks for playing!";
		}

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();

            // Populate default per-prefab loot tables. These tables mirror the structure used
            // for normal farming so you can control rarity and stack sizes. They have been
            // carefully balanced to reduce trash and provide appropriate rewards for each
            // crate type.
            var crateNormal = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["scrap"] = new ItemEntry { DisplayName = "Scrap", Enabled = true, Weight = 4f, MinAmount = 10, MaxAmount = 30 },
                ["wood"] = new ItemEntry { DisplayName = "Wood", Enabled = true, Weight = 3f, MinAmount = 200, MaxAmount = 600 },
                ["stones"] = new ItemEntry { DisplayName = "Stones", Enabled = true, Weight = 3f, MinAmount = 200, MaxAmount = 600 },
                ["cloth"] = new ItemEntry { DisplayName = "Cloth", Enabled = true, Weight = 2f, MinAmount = 10, MaxAmount = 30 },
                ["rope"] = new ItemEntry { DisplayName = "Rope", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 3 },
                ["gears"] = new ItemEntry { DisplayName = "Gears", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["metalpipe"] = new ItemEntry { DisplayName = "Metal Pipe", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["metalblade"] = new ItemEntry { DisplayName = "Metal Blade", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["ammo.pistol"] = new ItemEntry { DisplayName = "Pistol Ammo", Enabled = true, Weight = 1.5f, MinAmount = 15, MaxAmount = 40 },
                ["pistol.revolver"] = new ItemEntry { DisplayName = "Revolver", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["pistol.semiauto"] = new ItemEntry { DisplayName = "Semi Pistol", Enabled = true, Weight = 0.4f, MinAmount = 1, MaxAmount = 1 },
                ["axe.salvaged"] = new ItemEntry { DisplayName = "Salvaged Axe", Enabled = true, Weight = 0.3f, MinAmount = 1, MaxAmount = 1 },
                ["icepick.salvaged"] = new ItemEntry { DisplayName = "Salvaged Icepick", Enabled = true, Weight = 0.3f, MinAmount = 1, MaxAmount = 1 },
                ["hammer.salvaged"] = new ItemEntry { DisplayName = "Salvaged Hammer", Enabled = true, Weight = 0.3f, MinAmount = 1, MaxAmount = 1 }
            };
            var crateTools = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["hammer.salvaged"] = new ItemEntry { DisplayName = "Salvaged Hammer", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["axe.salvaged"] = new ItemEntry { DisplayName = "Salvaged Axe", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["icepick.salvaged"] = new ItemEntry { DisplayName = "Salvaged Icepick", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["salvaged.cleaver"] = new ItemEntry { DisplayName = "Salvaged Cleaver", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["salvaged.sword"] = new ItemEntry { DisplayName = "Salvaged Sword", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["rope"] = new ItemEntry { DisplayName = "Rope", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 3 },
                ["gears"] = new ItemEntry { DisplayName = "Gears", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 2 },
                ["metalpipe"] = new ItemEntry { DisplayName = "Metal Pipe", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 2 },
                ["metalblade"] = new ItemEntry { DisplayName = "Metal Blade", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 2 }
            };
            var crateMilitary = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["riflebody"] = new ItemEntry { DisplayName = "Rifle Body", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["smgbody"] = new ItemEntry { DisplayName = "SMG Body", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["semibody"] = new ItemEntry { DisplayName = "Semi Body", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["tech.trash"] = new ItemEntry { DisplayName = "Tech Trash", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["targeting.computer"] = new ItemEntry { DisplayName = "Targeting Computer", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["cctv.camera"] = new ItemEntry { DisplayName = "CCTV Camera", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["metal.refined"] = new ItemEntry { DisplayName = "HQ Metal", Enabled = true, Weight = 1f, MinAmount = 3, MaxAmount = 10 },
                ["explosive.satchel"] = new ItemEntry { DisplayName = "Satchel", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 2 },
                ["ammo.rifle"] = new ItemEntry { DisplayName = "5.56 Ammo", Enabled = true, Weight = 1.5f, MinAmount = 20, MaxAmount = 60 },
                ["ammo.rifle.explosive"] = new ItemEntry { DisplayName = "Explosive 5.56", Enabled = true, Weight = 0.5f, MinAmount = 5, MaxAmount = 15 },
                ["weapon.mod.lasersight"] = new ItemEntry { DisplayName = "Laser Sight", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["weapon.mod.holosight"] = new ItemEntry { DisplayName = "Holosight", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["weapon.mod.silencer"] = new ItemEntry { DisplayName = "Silencer", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["supply.signal"] = new ItemEntry { DisplayName = "Supply Signal", Enabled = true, Weight = 0.2f, MinAmount = 1, MaxAmount = 1 }
            };
            // Weapon crate defaults – focused on firearms and attachments
            var crateWeapon = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["pistol.revolver"] = new ItemEntry { DisplayName = "Revolver", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["pistol.semiauto"] = new ItemEntry { DisplayName = "Semi Pistol", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["smg.thompson"] = new ItemEntry { DisplayName = "Thompson", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 1 },
                ["smg.mp5"] = new ItemEntry { DisplayName = "MP5A4", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["shotgun.pump"] = new ItemEntry { DisplayName = "Pump Shotgun", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 1 },
                ["rifle.semiautomatic"] = new ItemEntry { DisplayName = "Semi Rifle", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["rifle.ak"] = new ItemEntry { DisplayName = "AK", Enabled = true, Weight = 0.25f, MinAmount = 1, MaxAmount = 1 },
                ["ammo.pistol"] = new ItemEntry { DisplayName = "Pistol Ammo", Enabled = true, Weight = 2f, MinAmount = 20, MaxAmount = 60 },
                ["ammo.rifle"] = new ItemEntry { DisplayName = "5.56 Ammo", Enabled = true, Weight = 1.5f, MinAmount = 20, MaxAmount = 60 },
                ["ammo.pistol.hv"] = new ItemEntry { DisplayName = "HV Pistol Ammo", Enabled = true, Weight = 0.5f, MinAmount = 10, MaxAmount = 30 },
                ["ammo.rifle.hv"] = new ItemEntry { DisplayName = "HV 5.56", Enabled = true, Weight = 0.5f, MinAmount = 10, MaxAmount = 30 },
                ["weapon.mod.lasersight"] = new ItemEntry { DisplayName = "Laser Sight", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["weapon.mod.holosight"] = new ItemEntry { DisplayName = "Holosight", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["weapon.mod.silencer"] = new ItemEntry { DisplayName = "Silencer", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["explosive.satchel"] = new ItemEntry { DisplayName = "Satchel", Enabled = true, Weight = 0.3f, MinAmount = 1, MaxAmount = 1 }
            };

            // Heli crate defaults – extremely buffed with high-value loot
            var heliCrate = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["autoturret"] = new ItemEntry { DisplayName = "Auto Turret", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["rocket.launcher"] = new ItemEntry { DisplayName = "Rocket Launcher", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["ammo.rocket.basic"] = new ItemEntry { DisplayName = "Rocket", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 3 },
                ["ammo.rocket.hv"] = new ItemEntry { DisplayName = "HV Rocket", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["ammo.rocket.fire"] = new ItemEntry { DisplayName = "Inc Rocket", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["ammo.rocket.smoke"] = new ItemEntry { DisplayName = "Smoke Rocket", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 2 },
                ["explosive.timed"] = new ItemEntry { DisplayName = "C4", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 2 },
                ["explosive.satchel"] = new ItemEntry { DisplayName = "Satchel", Enabled = true, Weight = 1.5f, MinAmount = 2, MaxAmount = 4 },
                ["lmg.m249"] = new ItemEntry { DisplayName = "M249", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["rifle.l96"] = new ItemEntry { DisplayName = "L96 Rifle", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["rifle.ak"] = new ItemEntry { DisplayName = "AK", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["smg.mp5"] = new ItemEntry { DisplayName = "MP5A4", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 1 },
                ["targeting.computer"] = new ItemEntry { DisplayName = "Targeting Computer", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["cctv.camera"] = new ItemEntry { DisplayName = "CCTV Camera", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["tech.trash"] = new ItemEntry { DisplayName = "Tech Trash", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["metal.refined"] = new ItemEntry { DisplayName = "HQ Metal", Enabled = true, Weight = 2f, MinAmount = 10, MaxAmount = 30 },
                ["supply.signal"] = new ItemEntry { DisplayName = "Supply Signal", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 2 },
                ["oretea.pure"] = new ItemEntry { DisplayName = "Pure Ore Tea", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["woodtea.pure"] = new ItemEntry { DisplayName = "Pure Wood Tea", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["scraptea.pure"] = new ItemEntry { DisplayName = "Pure Scrap Tea", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["ammo.rifle.explosive"] = new ItemEntry { DisplayName = "Explosive 5.56", Enabled = true, Weight = 1f, MinAmount = 10, MaxAmount = 20 },
                ["ammo.rifle.hv"] = new ItemEntry { DisplayName = "HV 5.56", Enabled = true, Weight = 1f, MinAmount = 10, MaxAmount = 20 },
                ["ammo.grenadelauncher.he"] = new ItemEntry { DisplayName = "40mm HE Grenade", Enabled = true, Weight = 0.5f, MinAmount = 2, MaxAmount = 4 },
                ["ammo.rocket.sam"] = new ItemEntry { DisplayName = "SAM Ammo", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 }
            };

            // Hackable (oil rig) crate defaults – extremely buffed, even more than heli
            var hackableCrate = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["autoturret"] = new ItemEntry { DisplayName = "Auto Turret", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["rocket.launcher"] = new ItemEntry { DisplayName = "Rocket Launcher", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["ammo.rocket.basic"] = new ItemEntry { DisplayName = "Rocket", Enabled = true, Weight = 3f, MinAmount = 2, MaxAmount = 4 },
                ["ammo.rocket.hv"] = new ItemEntry { DisplayName = "HV Rocket", Enabled = true, Weight = 2f, MinAmount = 2, MaxAmount = 3 },
                ["ammo.rocket.fire"] = new ItemEntry { DisplayName = "Inc Rocket", Enabled = true, Weight = 1.5f, MinAmount = 2, MaxAmount = 3 },
                ["ammo.rocket.smoke"] = new ItemEntry { DisplayName = "Smoke Rocket", Enabled = true, Weight = 1.5f, MinAmount = 2, MaxAmount = 3 },
                ["explosive.timed"] = new ItemEntry { DisplayName = "C4", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 2 },
                ["explosive.satchel"] = new ItemEntry { DisplayName = "Satchel", Enabled = true, Weight = 2f, MinAmount = 3, MaxAmount = 6 },
                ["lmg.m249"] = new ItemEntry { DisplayName = "M249", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["rifle.l96"] = new ItemEntry { DisplayName = "L96 Rifle", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 },
                ["rifle.ak"] = new ItemEntry { DisplayName = "AK", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 1 },
                ["smg.mp5"] = new ItemEntry { DisplayName = "MP5A4", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 1 },
                ["targeting.computer"] = new ItemEntry { DisplayName = "Targeting Computer", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["cctv.camera"] = new ItemEntry { DisplayName = "CCTV Camera", Enabled = true, Weight = 2f, MinAmount = 1, MaxAmount = 1 },
                ["tech.trash"] = new ItemEntry { DisplayName = "Tech Trash", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 2 },
                ["metal.refined"] = new ItemEntry { DisplayName = "HQ Metal", Enabled = true, Weight = 3f, MinAmount = 20, MaxAmount = 40 },
                ["supply.signal"] = new ItemEntry { DisplayName = "Supply Signal", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 2 },
                ["oretea.pure"] = new ItemEntry { DisplayName = "Pure Ore Tea", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["woodtea.pure"] = new ItemEntry { DisplayName = "Pure Wood Tea", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["scraptea.pure"] = new ItemEntry { DisplayName = "Pure Scrap Tea", Enabled = true, Weight = 1.5f, MinAmount = 1, MaxAmount = 1 },
                ["ammo.rifle.explosive"] = new ItemEntry { DisplayName = "Explosive 5.56", Enabled = true, Weight = 1.5f, MinAmount = 20, MaxAmount = 40 },
                ["ammo.rifle.hv"] = new ItemEntry { DisplayName = "HV 5.56", Enabled = true, Weight = 1.5f, MinAmount = 20, MaxAmount = 40 },
                ["ammo.grenadelauncher.he"] = new ItemEntry { DisplayName = "40mm HE Grenade", Enabled = true, Weight = 1f, MinAmount = 2, MaxAmount = 4 },
                ["ammo.rocket.sam"] = new ItemEntry { DisplayName = "SAM Ammo", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 }
            };

            // Cargo hackable crate defaults – similar to hackable but slightly less weighted
            var cargoHackableCrate = new Dictionary<string, ItemEntry>(hackableCrate, StringComparer.OrdinalIgnoreCase);

            // Assign tables to prefabs
            config.PrefabLoot["crate_normal"] = crateNormal;
            config.PrefabLoot["crate_tools"] = crateTools;
            config.PrefabLoot["crate_military"] = crateMilitary;
            config.PrefabLoot["heli_crate"] = heliCrate;
            config.PrefabLoot["weapon_crate"] = crateWeapon;

            // Default barrel loot: barrels (including coloured variants) will roll from these weighted entries
            config.BarrelLoot.Clear();
            config.BarrelLoot["scrap"] = new ItemEntry { DisplayName = "Scrap", Enabled = true, Weight = 5f, MinAmount = 5, MaxAmount = 15 };
            config.BarrelLoot["wood"] = new ItemEntry { DisplayName = "Wood", Enabled = true, Weight = 3f, MinAmount = 100, MaxAmount = 300 };
            config.BarrelLoot["cloth"] = new ItemEntry { DisplayName = "Cloth", Enabled = true, Weight = 3f, MinAmount = 5, MaxAmount = 15 };
            config.BarrelLoot["ammo.pistol"] = new ItemEntry { DisplayName = "Pistol Ammo", Enabled = true, Weight = 2f, MinAmount = 10, MaxAmount = 30 };
            config.BarrelLoot["pistol.revolver"] = new ItemEntry { DisplayName = "Revolver", Enabled = true, Weight = 1f, MinAmount = 1, MaxAmount = 1 };
            config.BarrelLoot["pistol.semiauto"] = new ItemEntry { DisplayName = "Semi Pistol", Enabled = true, Weight = 0.8f, MinAmount = 1, MaxAmount = 1 };
            config.BarrelLoot["rope"] = new ItemEntry { DisplayName = "Rope", Enabled = true, Weight = 1.2f, MinAmount = 1, MaxAmount = 3 };
            config.BarrelLoot["gears"] = new ItemEntry { DisplayName = "Gears", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 };
            config.BarrelLoot["metalpipe"] = new ItemEntry { DisplayName = "Metal Pipe", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 };
            config.BarrelLoot["metalblade"] = new ItemEntry { DisplayName = "Metal Blade", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 };

            // Default NPC body loot: NPC corpses will roll from these weighted entries
            config.NpcLoot.Clear();
            config.NpcLoot["scrap"] = new ItemEntry { DisplayName = "Scrap", Enabled = true, Weight = 5f, MinAmount = 10, MaxAmount = 40 };
            config.NpcLoot["wood"] = new ItemEntry { DisplayName = "Wood", Enabled = true, Weight = 2f, MinAmount = 100, MaxAmount = 300 };
            config.NpcLoot["lowgradefuel"] = new ItemEntry { DisplayName = "Low Grade", Enabled = true, Weight = 2f, MinAmount = 10, MaxAmount = 30 };
            config.NpcLoot["cloth"] = new ItemEntry { DisplayName = "Cloth", Enabled = true, Weight = 2f, MinAmount = 10, MaxAmount = 30 };
            config.NpcLoot["ammo.pistol"] = new ItemEntry { DisplayName = "Pistol Ammo", Enabled = true, Weight = 2f, MinAmount = 10, MaxAmount = 30 };
            config.NpcLoot["ammo.rifle"] = new ItemEntry { DisplayName = "5.56 Ammo", Enabled = true, Weight = 1.5f, MinAmount = 10, MaxAmount = 30 };
            config.NpcLoot["gears"] = new ItemEntry { DisplayName = "Gears", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 };
            config.NpcLoot["rope"] = new ItemEntry { DisplayName = "Rope", Enabled = true, Weight = 0.5f, MinAmount = 1, MaxAmount = 2 };

            // Default hackable crate loot: oil rig and other hackable crates
            config.HackableLoot["oilcrate_hackable"] = hackableCrate;

            // Default cargo hackable crate loot: cargo ship hackable crates
            config.CargoHackableLoot["cargocrate_hackable"] = cargoHackableCrate;

            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) throw new Exception("Null config");

                // Ensure new loot tables are initialised if missing from older configs
                bool needsSave = false;
                if (config.PrefabLoot == null)
                {
                    config.PrefabLoot = new Dictionary<string, Dictionary<string, ItemEntry>>(StringComparer.OrdinalIgnoreCase);
                    needsSave = true;
                }
                if (config.BarrelLoot == null)
                {
                    config.BarrelLoot = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase);
                    needsSave = true;
                }
                if (config.NpcLoot == null)
                {
                    config.NpcLoot = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase);
                    needsSave = true;
                }
                if (config.HackableLoot == null)
                {
                    config.HackableLoot = new Dictionary<string, Dictionary<string, ItemEntry>>(StringComparer.OrdinalIgnoreCase);
                    needsSave = true;
                }
                if (config.CargoHackableLoot == null)
                {
                    config.CargoHackableLoot = new Dictionary<string, Dictionary<string, ItemEntry>>(StringComparer.OrdinalIgnoreCase);
                    needsSave = true;
                }
                if (needsSave)
                {
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning("Config corrupt or missing; creating new.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region State / Buffs

        private class BuffState
        {
            public float TeaChanceDouble = 0f;  // 0..1
            public float TeaMultiplier = 1f;    // 1, 2, 3...
            public float PieMultiplier = 1f;    // 1.5x for Bear Pie default
            public double ExpiresAt = 0;        // time since startup (UnityEngine.Time.realtimeSinceStartup) or Unix time
        }

        private readonly Dictionary<ulong, BuffState> playerBuffs = new Dictionary<ulong, BuffState>();
        private bool happyHourActive;
        private Timer happyTimer;
        private Timer happyEndTimer;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermHappyHour, this);
            // Register admin permission for custom commands
            permission.RegisterPermission(PermAdmin, this);
        }

        private void OnServerInitialized()
        {
            // Populate Items with all Rust items on first run / missing entries
            bool changed = false;
            foreach (var def in ItemManager.itemList)
            {
                if (def == null || string.IsNullOrEmpty(def.shortname)) continue;
                if (!config.Items.ContainsKey(def.shortname))
                {
                    config.Items[def.shortname] = new ItemEntry
                    {
                        DisplayName = def.displayName?.english ?? def.shortname,
                        Enabled = true,
                        Weight = 1f,
                        MinAmount = 1,
                        MaxAmount = Math.Max(1, def.stackable > 0 ? Math.Min(def.stackable, 5) : 1) // small default stack
                    };
                    changed = true;
                }
            }
            if (changed) SaveConfig();

            ScheduleNextHappyHour();
        }

        private void Unload()
        {
            happyTimer?.Destroy();
            happyEndTimer?.Destroy();
        }

        /// <summary>
        /// Override node hits (trees/rocks/ores) and corpse/body dispensers.
        /// Returning non-null cancels vanilla gather【docs: OnDispenserGather】.
        /// </summary>
        private object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!config.OverrideAllGather) return null;
            if (player == null) return null;

            GiveRandomItem(player);
            return true; // block vanilla
        }

        /// <summary>
        /// Override collectibles (plants, pickups, etc.).
        /// </summary>
		private object OnCollectiblePickup(CollectibleEntity entity, BasePlayer player, bool collect)
		{
			if (!config.OverrideAllGather) return null;
			if (player == null || entity == null) return null;

			// Give random item instead of vanilla
			GiveRandomItem(player);

			// Remove the loose node so it can't be spammed
			entity.Kill(BaseNetworkable.DestroyMode.Gib); // or DestroyMode.None if you don't want gibs

			// Block vanilla pickup
			return true;
		}


        /// <summary>
        /// Track teas/pies consumed; we don't block usage so return null.
        /// </summary>
        private int? OnItemUse(Item item, int amountToUse)
        {
            if (item == null || amountToUse <= 0) return null;
            var player = item.GetOwnerPlayer();
            if (player == null) return null;

            var sn = item.info?.shortname?.ToLower() ?? string.Empty;
            var name = item.info?.displayName?.english?.ToLower() ?? string.Empty;

            // TEAS
            if ((sn.Contains("tea") || name.Contains("tea")))
            {
                // Determine tier by display name markers
                bool isBasic = name.Contains("basic");
                bool isAdvanced = name.Contains("advanced");
                bool isPure = name.Contains("pure");

                // Only care about wood/ore/scrap teas
                bool isWood = sn.Contains("wood") || name.Contains("wood");
                bool isOre = sn.Contains("ore") || name.Contains("ore");
                bool isScrap = sn.Contains("scrap") || name.Contains("scrap");

                if ((isWood && config.Tea.EnableWoodTea) ||
                    (isOre && config.Tea.EnableOreTea) ||
                    (isScrap && config.Tea.EnableScrapTea))
                {
                    var bs = GetOrCreateBuff(player.userID);

                    // Basic = chance to double; Advanced/Pure = fixed multipliers
                    if (isBasic)
                    {
                        bs.TeaChanceDouble = Mathf.Max(bs.TeaChanceDouble, config.Tea.BasicDoubleChance);
                        bs.TeaMultiplier = Mathf.Max(bs.TeaMultiplier, 1f);
                    }
                    else if (isAdvanced)
                    {
                        bs.TeaMultiplier = Mathf.Max(bs.TeaMultiplier, config.Tea.AdvancedMultiplier);
                        bs.TeaChanceDouble = Mathf.Max(bs.TeaChanceDouble, 0f);
                    }
                    else if (isPure)
                    {
                        bs.TeaMultiplier = Mathf.Max(bs.TeaMultiplier, config.Tea.PureMultiplier);
                        bs.TeaChanceDouble = Mathf.Max(bs.TeaChanceDouble, 0f);
                    }

                    bs.ExpiresAt = Time.realtimeSinceStartup + (config.BuffDurationMinutes * 60f);
                    playerBuffs[player.userID] = bs;
                }
            }

            // PIE (Bear Pie boost)
            if ((sn.Contains("pie") || name.Contains("pie")) && (sn.Contains("bear") || name.Contains("bear")))
            {
                if (config.Pie.EnableBearPie)
                {
                    var bs = GetOrCreateBuff(player.userID);
                    bs.PieMultiplier = Mathf.Max(bs.PieMultiplier, config.Pie.BearPieMultiplier);
                    bs.ExpiresAt = Time.realtimeSinceStartup + (config.BuffDurationMinutes * 60f);
                    playerBuffs[player.userID] = bs;
                }
            }

            return null;
        }

        #endregion

        #region Core Logic

        private BuffState GetOrCreateBuff(ulong userId)
        {
            BuffState bs;
            if (!playerBuffs.TryGetValue(userId, out bs) || bs == null)
            {
                bs = new BuffState { TeaMultiplier = 1f, PieMultiplier = 1f, TeaChanceDouble = 0f, ExpiresAt = 0 };
                playerBuffs[userId] = bs;
            }
            return bs;
        }

        private void GiveRandomItem(BasePlayer player)
        {
            if (player == null || player.IsDead()) return;

            // Select a random item entry (including items with commands)
            var (key, entry) = RollItemEntry();
            if (entry == null) return;

            // Determine quantity based on Min/Max and buffs
            int amount = UnityEngine.Random.Range(entry.MinAmount, entry.MaxAmount + 1);

            // Apply buffs if active and not expired
            float finalMultiplier = 1f;
            float doubleChance = 0f;

            BuffState bs;
            if (playerBuffs.TryGetValue(player.userID, out bs) && bs != null)
            {
                if (Time.realtimeSinceStartup > bs.ExpiresAt)
                {
                    // expired
                    playerBuffs.Remove(player.userID);
                }
                else
                {
                    // Tea
                    finalMultiplier *= bs.TeaMultiplier;
                    doubleChance = Mathf.Clamp01(bs.TeaChanceDouble);
                    // Pie
                    finalMultiplier *= bs.PieMultiplier;
                    // Double proc for Basic tea (chance)
                    if (doubleChance > 0f && UnityEngine.Random.Range(0f, 1f) < doubleChance)
                        finalMultiplier *= 2f;
                }
            }

            // Happy Hour overrides everything to forced stack amount
            int baseAmount = amount;
            if (happyHourActive)
            {
                if (config.HappyHour.UseMultiplier)
                {
                    amount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * config.HappyHour.Multiplier));
                }
                else
                {
                    amount = Mathf.Max(1, config.HappyHour.ForcedStackAmount);
                }
            }
            else
            {
                amount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * finalMultiplier));
            }

            // If a command is specified, run it instead of giving a physical item
            if (!string.IsNullOrEmpty(entry.Command))
            {
                // Replace placeholders
                string cmd = entry.Command.Replace("{steamid}", player.UserIDString)
                                          .Replace("{playername}", player.displayName);
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), cmd);
                return;
            }

            // Otherwise, spawn the physical item if it exists
            var def = ItemManager.FindItemDefinition(key);
            if (def == null) return;

            var give = ItemManager.Create(def, amount, 0);
            if (give == null) return;
            player.GiveItem(give);
        }

        /// <summary>
        /// Hook called when any entity is spawned in the world. We use this
        /// to intercept the spawning of loot containers (crates, barrels,
        /// hackables, cargo crates, etc.) and customise their contents. To
        /// allow vanilla loot to populate first, we defer our modifications
        /// by a short timer.
        /// </summary>
        private void OnEntitySpawned(BaseEntity entity)
        {
            var crate = entity as LootContainer;
            if (crate == null) return;
            // Only buff containers that have inventories
            if (crate.inventory == null) return;
            timer.Once(0.1f, () => BuffLootContainer(crate));
        }

        /// <summary>
        /// Applies custom loot logic to various containers. The number of rolls
        /// and quantity multipliers are increased for weapon crates, cargo
        /// crates and hackable/codelocked crates. Normal crates and barrels
        /// receive a modest buff. Commands are skipped so only physical
        /// items are inserted. Existing container contents are cleared.
        /// </summary>
        private void BuffLootContainer(LootContainer crate)
        {
            if (crate == null || crate.inventory == null) return;
            // Determine crate type via prefab short name (lowercase for easier matching)
            string name = crate.ShortPrefabName?.ToLower() ?? string.Empty;

            // Determine the number of rolls and multiplier based on crate category. These values
            // mirror the original logic: hackable and elite crates receive the biggest boost,
            // weapon/military crates get a moderate boost, standard crates get a small boost,
            // and barrels get minimal boosts.
            int rolls = 0;
            float multiplier = 1f;
            if (name.Contains("hackable") || name.Contains("codelock") || name.Contains("codelocked") || name.Contains("elite") || name.Contains("cargocrate") || name.Contains("bradley") || name.Contains("chinook") || name.Contains("supplycrate"))
            {
                rolls = 12;
                multiplier = 3f;
            }
            else if (name.Contains("weapon") || name.Contains("military") || name.Contains("crate_tools") || name.Contains("crate_normal") || name.Contains("crate_underwater_advanced"))
            {
                rolls = 6;
                multiplier = 2f;
            }
            else if (name.Contains("crate"))
            {
                rolls = 4;
                multiplier = 1.5f;
            }
            else if (name.Contains("barrel") || name.Contains("loot") || name.Contains("cardbox") || name.Contains("cardboard"))
            {
                rolls = 3;
                multiplier = 1f;
            }
            else
            {
                // Unknown container type: skip customisation
                return;
            }

            // Determine which loot table to use for this container, if any.
            // Precedence: cargo hackable > hackable > per-prefab > barrels.  If no
            // specific table is found and this is a crate-type container, fall back
            // to the normal crate table (crate_normal) to avoid pulling from the
            // global Items dictionary which would add a lot of scrap/wood/cloth.
            Dictionary<string, ItemEntry> table = null;
            // Cargo hackable crates have unique loot tables
            if (config.CargoHackableLoot != null && config.CargoHackableLoot.TryGetValue(name, out var cargoTable))
            {
                table = cargoTable;
            }
            // Other hackable crates
            else if (config.HackableLoot != null && config.HackableLoot.TryGetValue(name, out var hackTable))
            {
                table = hackTable;
            }
            // Per-prefab crate tables
            else if (config.PrefabLoot != null && config.PrefabLoot.TryGetValue(name, out var prefabTable))
            {
                table = prefabTable;
            }
            // Barrel-type containers share a single barrel table
            else if (name.Contains("barrel") && config.BarrelLoot != null)
            {
                table = config.BarrelLoot;
            }
            // If no table matched and this looks like a crate, fall back to the normal crate table
            else if (name.Contains("crate") && config.PrefabLoot != null && config.PrefabLoot.TryGetValue("crate_normal", out var defaultCrateTable))
            {
                table = defaultCrateTable;
            }

            // Clear the existing vanilla contents so only our custom loot spawns
            crate.inventory.Clear();

            // Insert random items based on the selected table (or global Items if none matched).
            for (int i = 0; i < rolls; i++)
            {
                string shortName;
                ItemEntry entry;
                if (table != null)
                {
                    (shortName, entry) = RollFromTable(table, true);
                }
                else
                {
                    // fallback to global items (trees/ore) if no crate-specific table matched
                    (shortName, entry) = RollItemEntry(true);
                }
                if (entry == null || string.IsNullOrEmpty(shortName)) continue;
                var def = ItemManager.FindItemDefinition(shortName);
                if (def == null) continue;
                int baseAmount = UnityEngine.Random.Range(entry.MinAmount, entry.MaxAmount + 1);
                int finalAmount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));
                var item = ItemManager.Create(def, finalAmount);
                if (item == null) continue;
                crate.inventory.Insert(item);
            }
        }

        /// <summary>
        /// Intercepts the population of NPC corpses to replace their default loot with items
        /// rolled from the configured NpcLoot table. Only NPC corpses are affected; player
        /// corpses remain untouched. We cast the corpse to LootableCorpse to access the
        /// containers field. If no entries are configured or the corpse is not from an NPC,
        /// this hook returns null to allow default behaviour.
        /// </summary>
        private BaseCorpse OnCorpsePopulate(BasePlayer npcPlayer, BaseCorpse corpse)
        {
            try
            {
                // Ensure we have a valid NPC player and configured loot entries
                if (npcPlayer == null || !npcPlayer.IsNpc) return null;
                if (corpse == null) return null;
                if (config?.NpcLoot == null || config.NpcLoot.Count == 0) return null;

                // Cast to LootableCorpse to access the inventory containers
                var lootable = corpse as LootableCorpse;
                if (lootable == null || lootable.containers == null || lootable.containers.Length == 0) return null;

                // Clear all existing items from the corpse containers
                foreach (var container in lootable.containers)
                {
                    container?.Clear();
                }

                // Determine number of items to roll for NPC loot; use a modest number (3)
                int rolls = 3;
                for (int i = 0; i < rolls; i++)
                {
                    var (shortName, entry) = RollFromTable(config.NpcLoot, true);
                    if (entry == null || string.IsNullOrEmpty(shortName)) continue;
                    var def = ItemManager.FindItemDefinition(shortName);
                    if (def == null) continue;
                    int amount = UnityEngine.Random.Range(entry.MinAmount, entry.MaxAmount + 1);
                    var item = ItemManager.Create(def, amount);
                    if (item == null) continue;
                    // Attempt to insert into the first container; if insertion fails, remove the item
                    var primary = lootable.containers[0];
                    if (primary != null)
                    {
                        // Use Insert to add the item to the container. Insert returns a bool; if false the item will be dropped
                        if (!primary.Insert(item))
                        {
                            item.Remove();
                        }
                    }
                    else
                    {
                        item.Remove();
                    }
                }
                // Return the corpse to override default loot population
                return corpse;
            }
            catch
            {
                // In case of any exceptions, do not block default corpse population
                return null;
            }
        }

        /// <summary>
        /// Selects a random item entry from the configuration based on weights.
        /// Optionally excludes entries that define a Command so that only
        /// physical items are returned (useful for crate loot). Returns
        /// both the key (short name) and the entry. If no entries match,
        /// returns (null, null).
        /// </summary>
        private (string, ItemEntry) RollItemEntry(bool skipCommands = false)
        {
            // Build a pool of enabled items with weight > 0
            var pool = config.Items.Where(kv => kv.Value.Enabled && kv.Value.Weight > 0f && (!skipCommands || string.IsNullOrEmpty(kv.Value.Command))).ToList();
            if (pool.Count == 0) return (null, null);
            // Compute total weight
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++) totalWeight += pool[i].Value.Weight;
            // Roll a random value between 0 and total weight
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            // Pick based on weights
            foreach (var kv in pool)
            {
                if (roll <= kv.Value.Weight)
                {
                    return (kv.Key, kv.Value);
                }
                roll -= kv.Value.Weight;
            }
            // Fallback to the last entry if something goes wrong
            var last = pool[pool.Count - 1];
            return (last.Key, last.Value);
        }

        /// <summary>
        /// Rolls a random item entry from the provided weighted table. Works similarly
        /// to RollItemEntry but uses the supplied dictionary instead of the global Items
        /// dictionary. Optionally skips entries that define a Command, which is useful
        /// for container loot where only physical items should spawn.
        /// </summary>
        private (string, ItemEntry) RollFromTable(Dictionary<string, ItemEntry> table, bool skipCommands = false)
        {
            if (table == null || table.Count == 0) return (null, null);
            var pool = table.Where(kv => kv.Value != null && kv.Value.Enabled && kv.Value.Weight > 0f && (!skipCommands || string.IsNullOrEmpty(kv.Value.Command))).ToList();
            if (pool.Count == 0) return (null, null);
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++) totalWeight += pool[i].Value.Weight;
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            foreach (var kv in pool)
            {
                if (roll <= kv.Value.Weight)
                {
                    return (kv.Key, kv.Value);
                }
                roll -= kv.Value.Weight;
            }
            var last = pool[pool.Count - 1];
            return (last.Key, last.Value);
        }

        #endregion

        #region Happy Hour

        private void ScheduleNextHappyHour()
        {
            happyTimer?.Destroy();
            happyEndTimer?.Destroy();

            if (!config.HappyHour.Enabled) return;

            // schedule the next start
            happyTimer = timer.Once(config.HappyHour.IntervalMinutes * 60f, StartHappyHour);
        }

        private void StartHappyHour()
        {
            if (!config.HappyHour.Enabled) return;
            if (happyHourActive) return;

            happyHourActive = true;
            PrintToChat(config.HappyHour.StartMessage);

            // auto end after duration
            happyEndTimer?.Destroy();
            happyEndTimer = timer.Once(config.HappyHour.DurationMinutes * 60f, StopHappyHour);
        }

        private void StopHappyHour()
        {
            if (!happyHourActive) return;
            happyHourActive = false;
            PrintToChat(config.HappyHour.EndMessage);

            // schedule next
            ScheduleNextHappyHour();
        }

        #endregion
    }
}
