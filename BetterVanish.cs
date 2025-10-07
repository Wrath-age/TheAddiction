using System.Collections.Generic;
using Network;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    /// <summary>
    /// A lightweight vanish implementation that allows administrators or players
    /// with permission to become invisible.  Invisible players are removed
    /// from other players' network queues and cannot be targeted by AI.
    /// This plugin exposes an IsVanished hook which other plugins can query
    /// (e.g. AdminRadar) and fires an OnBetterVanishStateChange hook when a
    /// player's vanish state toggles.
    /// </summary>
    [Info("BetterVanish", "Refactored", "1.0.0")]
    [Description("Simplified vanish functionality compatible with other admin plugins.")]
    public class BetterVanish : RustPlugin
    {
        private const string PermUse = "bettervanish.use";
        private readonly HashSet<ulong> _vanishedPlayers = new();

        #region Initialization
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }
        #endregion

        #region Commands
        [ChatCommand("vanish")]
        private void VanishCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsConnected)
            {
                return;
            }

            // Allow admin fallback for convenience
            if (!player.IPlayer.HasPermission(PermUse) && !player.IsAdmin)
            {
                SendReply(player, "You do not have permission to use vanish.");
                return;
            }

            if (IsVanished(player))
            {
                Reappear(player);
            }
            else
            {
                Disappear(player);
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Returns whether the given player is currently vanished.  Other plugins
        /// may call this via Interface.CallHook("IsVanished", player).
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player is vanished.</returns>
        public bool IsVanished(BasePlayer player)
        {
            return player != null && _vanishedPlayers.Contains(player.userID);
        }
        #endregion

        #region Vanish Logic
        private void Disappear(BasePlayer player)
        {
            if (player == null || _vanishedPlayers.Contains(player.userID))
            {
                return;
            }

            _vanishedPlayers.Add(player.userID);

            // Restrict networking so no new snapshots of this player will be sent
            player.limitNetworking = true;
            // Disable the collider so the player cannot be physically hit
            player.DisablePlayerCollider();

            // Immediately destroy the player's entity on other clients so they vanish instantly
            var connections = new List<Connection>();
            foreach (var target in BasePlayer.activePlayerList)
            {
                if (target != player && target.IsConnected)
                {
                    connections.Add(target.net.connection);
                }
            }
            // Destroy the player entity using the updated NetWrite API (Net.sv.write was removed)
            Network.NetWrite netWrite = Net.sv.StartWrite();
            if (netWrite != null)
            {
                netWrite.PacketID(Network.Message.Type.EntityDestroy);
                netWrite.EntityID(player.net.ID);
                netWrite.UInt8((byte)BaseNetworkable.DestroyMode.None);
                netWrite.Send(new SendInfo(connections));
            }
            // Destroy the held item entity if it exists
            var heldEntity = player.GetHeldEntity() as BaseEntity;
            if (heldEntity != null)
            {
                netWrite = Net.sv.StartWrite();
                if (netWrite != null)
                {
                    netWrite.PacketID(Network.Message.Type.EntityDestroy);
                    netWrite.EntityID(heldEntity.net.ID);
                    netWrite.UInt8((byte)BaseNetworkable.DestroyMode.None);
                    netWrite.Send(new SendInfo(connections));
                }
            }

            // Ensure our hooks run
            Subscribe(nameof(CanNetworkTo));
            Subscribe(nameof(CanBeTargeted));

            // Notify other plugins of the vanish state change
            Interface.CallHook("OnBetterVanishStateChange", player, true);

            // Notify the vanished player
            SendReply(player, "Vanish enabled.  You are now invisible to other players and AI.");
        }

        private void Reappear(BasePlayer player)
        {
            if (player == null || !_vanishedPlayers.Contains(player.userID))
            {
                return;
            }

            _vanishedPlayers.Remove(player.userID);

            // Re-enable networking so snapshots of this player will be sent
            player.limitNetworking = false;
            // Re-enable the player's collider
            player.EnablePlayerCollider();
            // Update network group to ensure reappearance
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();

            // Update held entity if one exists
            player.GetHeldEntity()?.SendNetworkUpdateImmediate();

            // Unsubscribe hooks if no players vanished
            if (_vanishedPlayers.Count == 0)
            {
                Unsubscribe(nameof(CanNetworkTo));
                Unsubscribe(nameof(CanBeTargeted));
            }

            // Notify other plugins of the vanish state change
            Interface.CallHook("OnBetterVanishStateChange", player, false);

            // Notify the player
            SendReply(player, "Vanish disabled.  You are now visible to other players and AI.");
        }
        #endregion

        #region Hooks

        /// <summary>
        /// Prevent NPCs from targeting vanished players. Newer Rust updates fire
        /// OnNpcTarget for scientists, animals and other AI. Returning false
        /// cancels the targeting attempt.  This complements the CanBeTargeted
        /// hook to ensure AI do not acquire a vanished player as a target.
        /// </summary>
        /// <param name="npc">The AI trying to acquire a target.</param>
        /// <param name="entity">The potential target entity.</param>
        /// <returns>False to prevent targeting, null to allow default behavior.</returns>
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            // If the entity being targeted is a player and they are vanished,
            // prevent the AI from acquiring them.  Without this hook, certain
            // NPCs (scientists, Bradley, etc.) may still fire at the last
            // known position even though the player is invisible.
            var player = entity as BasePlayer;
            if (player != null && IsVanished(player))
            {
                return false;
            }
            return null;
        }

        /// <summary>
        /// Prevent NPC player specific targeting.  Some AI call OnNpcPlayerTarget
        /// instead of OnNpcTarget.  Returning false cancels the target.  This
        /// overload exists for backwards compatibility and to catch all AI types.
        /// The signature uses BaseNpc and BasePlayer to avoid referencing NPCPlayerApex
        /// which may not be available on all server builds.
        /// </summary>
        /// <param name="npc">The AI (could be a scientist, animal, etc.).</param>
        /// <param name="player">The player being targeted.</param>
        /// <returns>False to block targeting, null to allow.</returns>
        private object OnNpcPlayerTarget(BaseNpc npc, BasePlayer player)
        {
            if (player != null && IsVanished(player))
            {
                return false;
            }
            return null;
        }
        /// <summary>
        /// Prevent vanished players from being networked to other players.
        /// </summary>
        /// <param name="entity">The network entity.</param>
        /// <param name="target">The receiver of the network update.</param>
        /// <returns>False to block, null to allow.</returns>
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            if (entity == null || target == null)
            {
                return null;
            }

            var owner = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer();
            if (owner == null || owner == target)
            {
                return null;
            }

            // If the owner is vanished, block networking to players that are not vanished themselves
            if (IsVanished(owner) && !IsVanished(target))
            {
                return false;
            }
            return null;
        }

        /// <summary>
        /// Prevent AI from targeting vanished players.
        /// </summary>
        /// <param name="entity">The entity that might be targeted (player or AI).</param>
        /// <returns>False to block targeting, null to allow.</returns>
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;
            if (player == null)
            {
                return null;
            }

            if (IsVanished(player))
            {
                return false;
            }
            return null;
        }
        #endregion
    }
}