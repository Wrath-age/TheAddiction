using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoSAMSites", "Kris+ChatGPT", "1.2.0")]
    [Description("Blocks player SAM placements; if the item was consumed, it is refunded.")]
    public class NoSAMSites : RustPlugin
    {
        private const string SamItemShortName = "samsite";
        private const string SamPrefabShortName = "sam_site_turret_deployed";
        private const string DenyMsg = "<color=red>SAM Sites cannot be placed on this server.</color>";

        // Attempt to block BEFORE consumption (works on builds where Deployer path is used)
        private object CanDeployItem(BasePlayer player, Deployer deployer, uint entityID)
        {
            var def = deployer?.GetItem()?.info;
            if (def == null) return null;

            if (def.shortname == SamItemShortName)
            {
                player?.ChatMessage(DenyMsg);
                return false; // cancels deploy; item should remain
            }
            return null;
        }

        // Safety net – if SAM still spawns (item already consumed), kill it and refund 1 SAM back to player
        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            var ent = go?.ToBaseEntity();
            if (ent == null) return;

            if (ent.ShortPrefabName != SamPrefabShortName) return;

            var player = planner?.GetOwnerPlayer();

            // Refund first so if something else kills the entity, player still gets the item back
            if (player != null && !player.IsDead())
            {
                var refund = ItemManager.CreateByName(SamItemShortName, 1);
                if (refund != null)
                {
                    if (!player.inventory.GiveItem(refund))
                    {
                        // Inventory full: drop at feet
                        refund.Drop(player.transform.position + Vector3.up * 0.25f, Vector3.zero);
                    }
                }

                player.ChatMessage(DenyMsg);
            }

            // Remove the placed SAM
            NextTick(() =>
            {
                if (ent != null && !ent.IsDestroyed)
                    ent.Kill();
            });

            // Returning false here helps signal cancellation to other hooks
            // (entity is already spawned, but we've killed it and refunded)
        }
    }
}
