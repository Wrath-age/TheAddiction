using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Toggle", "Talha", "1.0.6")]
    [Description("Toggle your admin status")]
    public class AdminToggle : RustPlugin
    {
        private const string perm = "admintoggle.use";

        // Holds saved states (position and inventory) for players when they toggle into admin mode.
        private readonly Dictionary<ulong, SavedAdminState> savedStates = new();

        // Class for storing a player's inventory and position when toggling to admin
        private class SavedAdminState
        {
            public Vector3 Position;
            public List<ItemData> Main = new();
            public List<ItemData> Belt = new();
            public List<ItemData> Wear = new();

            // Player vital stats at the time of toggling into admin mode.  These values
            // include the player's current health, hunger (calories) and thirst
            // (hydration).  When the player toggles back to normal mode these values
            // are restored to avoid the admin experience affecting survival stats.
            public float Health;
            public float Hunger;
            public float Thirst;
        }

        // Minimal representation of an item to allow saving and restoring
        private class ItemData
        {
            public int ItemId;
            public int Amount;
            public ulong Skin;
        }
        
        private void Init() { permission.RegisterPermission(perm, this); }
        
        private void Message(BasePlayer player, string key)
        {
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString));
            player.ChatMessage(message);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ToPlayer"] = "You switched to player mode!",
                ["ToAdmin"] = "You switched to admin mode!"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ToPlayer"] = "Oyuncu moduna geçiş yaptın!",
                ["ToAdmin"] = "Admin moduna geçiş yaptın!"
            }, this, "tr");
        }

        [ChatCommand("admin")]
        private void Toggle(BasePlayer player)
        {
            // Ensure the caller has permission to toggle their admin status
            if (!player.IPlayer.HasPermission(perm))
                return;

            // When disabling admin mode we should revert any temporary admin tools (noclip/vanish, god mode, radar etc.)
            if (player.IsAdmin)
            {
                // Disable noclip if currently flying
                if (player.IsFlying)
                {
                    player.SendConsoleCommand("noclip");
                }

                // Attempt to remove vanish state from supported vanish plugins
                try
                {
                    // Prefer BetterVanish (custom implementation) but fall back to Vanish if loaded
                    var vanish = plugins.Find("BetterVanish") ?? plugins.Find("Vanish");
                    vanish?.Call("Reappear", player);
                }
                catch
                {
                    // ignore any errors from missing vanish plugins
                }

                // If the AdminRadar plugin is loaded, remove the radar UI for this player
                try
                {
                    var radar = plugins.Find("AdminRadar");
                    radar?.Call("DestroyRadar", player);
                }
                catch
                {
                    // ignore if AdminRadar isn't present or errors
                }

                // Attempt to disable god mode via the Godmode plugin if available
                try
                {
                    var god = plugins.Find("Godmode");
                    god?.Call("DisableGodmode", player.userID);
                }
                catch
                {
                    // ignore if Godmode isn't present or errors
                }

                // Restore the player's saved position and inventory if it was saved when entering admin mode
                if (savedStates.TryGetValue(player.userID, out var state))
                {
                    // Teleport the player back to their original location
                    try
                    {
                        // Teleport may not exist on older versions; fallback to setting position and forcing network update
                        if (!player.IsDead())
                        {
                            player.Teleport(state.Position);
                        }
                    }
                    catch
                    {
                        player.MovePosition(state.Position);
                        player.SendNetworkUpdateImmediate();
                    }

                    // Clear current inventory
                    player.inventory.containerMain.Clear();
                    player.inventory.containerBelt.Clear();
                    player.inventory.containerWear.Clear();

                    // Restore main items
                    foreach (var itemData in state.Main)
                    {
                        var item = ItemManager.CreateByItemID(itemData.ItemId, itemData.Amount, itemData.Skin);
                        if (item != null)
                        {
                            player.inventory.GiveItem(item, player.inventory.containerMain);
                        }
                    }
                    // Restore belt items
                    foreach (var itemData in state.Belt)
                    {
                        var item = ItemManager.CreateByItemID(itemData.ItemId, itemData.Amount, itemData.Skin);
                        if (item != null)
                        {
                            player.inventory.GiveItem(item, player.inventory.containerBelt);
                        }
                    }
                    // Restore worn items
                    foreach (var itemData in state.Wear)
                    {
                        var item = ItemManager.CreateByItemID(itemData.ItemId, itemData.Amount, itemData.Skin);
                        if (item != null)
                        {
                            player.inventory.GiveItem(item, player.inventory.containerWear);
                        }
                    }

                    savedStates.Remove(player.userID);

                    // Restore the player's vital statistics (health, hunger and thirst).
                    // Health is assigned directly and metabolic values are written if the
                    // metabolism component exists.  This ensures the player returns to
                    // the exact same state they were in before toggling into admin mode.
                    try
                    {
                        player.health = state.Health;
                        if (player.metabolism != null)
                        {
                            if (player.metabolism.calories != null)
                                player.metabolism.calories.value = state.Hunger;
                            if (player.metabolism.hydration != null)
                                player.metabolism.hydration.value = state.Thirst;
                        }
                    }
                    catch
                    {
                        // ignore any errors when restoring metabolism values
                    }
                }

                // Reset auth level and remove admin flags. Auth level 0 represents a normal player
                player.Connection.authLevel = 0;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);

                // Remove the player from the built‑in admin group to avoid inheriting admin permissions on reconnect
                permission.RemoveUserGroup(player.UserIDString, "admin");

                // Remove any stored entry in the server users file
                ServerUsers.Set(player.userID, ServerUsers.UserGroup.None, player.displayName, string.Empty);
                ServerUsers.Save();

                // Force a network update so other clients know this player is no longer an admin
                player.SendNetworkUpdateImmediate();

                Message(player, "ToPlayer");
            }
            else
            {
                // We are elevating the player to admin. Save their current location and inventory
                var state = new SavedAdminState
                {
                    Position = player.transform.position,
                    // Capture the player's current health and metabolic values
                    Health = player.health,
                    Hunger = player.metabolism?.calories?.value ?? 0f,
                    Thirst = player.metabolism?.hydration?.value ?? 0f
                };
                // Save main inventory
                foreach (var item in player.inventory.containerMain.itemList)
                {
                    state.Main.Add(new ItemData { ItemId = item.info.itemid, Amount = item.amount, Skin = item.skin });
                }
                // Save belt inventory
                foreach (var item in player.inventory.containerBelt.itemList)
                {
                    state.Belt.Add(new ItemData { ItemId = item.info.itemid, Amount = item.amount, Skin = item.skin });
                }
                // Save worn items
                foreach (var item in player.inventory.containerWear.itemList)
                {
                    state.Wear.Add(new ItemData { ItemId = item.info.itemid, Amount = item.amount, Skin = item.skin });
                }
                savedStates[player.userID] = state;

                // Heal the player fully on entering admin mode.  Restore the health, hunger and thirst
                // values upon exit using the saved state.  This ensures admins start with maximum
                // stats while in admin mode without permanently affecting survival gameplay.
                try
                {
                    // Fully heal the player's health to their maximum.  Use the MaxHealth()
                    // method exposed on BasePlayer to obtain the maximum health.  If the
                    // method is unavailable fall back to the current health to avoid
                    // compile errors on older Rust builds.
                    float maxHealth;
                    try
                    {
                        maxHealth = player.MaxHealth();
                    }
                    catch
                    {
                        // fallback to current health if MaxHealth() is not defined
                        maxHealth = player.health;
                    }
                    player.health = maxHealth;

                    // Set hunger and thirst to their configured maximums if the metabolism component exists
                    if (player.metabolism != null)
                    {
                        if (player.metabolism.calories != null)
                            player.metabolism.calories.value = player.metabolism.calories.max;
                        if (player.metabolism.hydration != null)
                            player.metabolism.hydration.value = player.metabolism.hydration.max;
                    }
                }
                catch
                {
                    // Ignore any errors if properties are not available on this Rust build
                }

                // Elevate the user to full owner level (auth level 2) to grant access to all admin commands
                player.Connection.authLevel = 2;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);

                // Assign the player to the admin group if not already present
                permission.AddUserGroup(player.UserIDString, "admin");

                // Persist the change in the server users file
                ServerUsers.Set(player.userID, ServerUsers.UserGroup.Owner, player.displayName, string.Empty);
                ServerUsers.Save();

                // Send a network update so the client immediately receives their new admin status
                player.SendNetworkUpdateImmediate();

                Message(player, "ToAdmin");

                // Automatically enable noclip, vanish, god mode and radar when entering admin mode
                // Turn on noclip if not already enabled
                try
                {
                    player.SendConsoleCommand("noclip");
                }
                catch
                {
                    // ignore if noclip command fails
                }

                // Vanish the player using BetterVanish or Vanish plugin
                try
                {
                    var vanishPlugin = plugins.Find("BetterVanish") ?? plugins.Find("Vanish");
                    // Prefer BetterVanish's Disappear method but fall back to generic Vanish command
                    vanishPlugin?.Call("Disappear", player);
                }
                catch
                {
                    // ignore if vanish plugin isn't present or errors
                }

                // Enable god mode for the player via the Godmode plugin if available
                try
                {
                    var god = plugins.Find("Godmode");
                    god?.Call("EnableGodmode", player.userID);
                }
                catch
                {
                    // ignore if Godmode isn't present or errors
                }

                // Turn on the AdminRadar UI for this player, if the plugin is installed.  Use the
                // default radar command implementation (no arguments) which activates the ESP with
                // the player's existing filter settings.  Then toggle the online‑only boxes and
                // vision lines so admins immediately see player inventories and where players are
                // looking.  If AdminRadar isn't loaded these calls are silently ignored.
                try
                {
                    var radar = plugins.Find("AdminRadar");
                    if (radar != null)
                    {
                        // Activate the radar UI with no additional filters.  Using an empty
                        // argument array will enable the radar without toggling any extra
                        // filters such as vision.  Admins can still manually toggle
                        // options via the /radar command if desired.
                        radar.Call("TurnRadarOn", player, Array.Empty<string>());
                    }
                }
                catch
                {
                    // ignore any errors from AdminRadar
                }
            }
        }
    }
}