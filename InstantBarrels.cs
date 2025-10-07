namespace Oxide.Plugins
{
    using UnityEngine;
    [Info("Instant Barrels", "Mevent", "1.0.3")]
    [Description("Allows you to destroy barrels and roadsigns with one hit")]
    public class InstantBarrels : CovalencePlugin
    {
        #region Fields

        private const string PermUse = "InstantBarrels.use";

        private const string PermRoadSigns = "InstantBarrels.roadsigns";

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);

            permission.RegisterPermission(PermRoadSigns, this);
        }

        private void OnEntityTakeDamage(LootContainer container, HitInfo info)
        {
            if (container == null || info == null) return;

            var player = info.InitiatorPlayer;
            if (player == null) return;

            var cov = player.IPlayer;
            if (cov == null) return;

            if (cov.HasPermission(PermUse) && container.ShortPrefabName.Contains("barrel") ||
                cov.HasPermission(PermRoadSigns) && container.ShortPrefabName.Contains("roadsign"))
                info.damageTypes.ScaleAll(1000f);
        }

        /// <summary>
        /// When a barrel or road sign is destroyed, transfer the loot directly into the
        /// attacker's inventory.  If their inventory is full, any remaining items will be
        /// dropped at the barrel's position.  This runs when the entity dies.
        /// </summary>
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var container = entity as LootContainer;
            if (container == null) return;

            // Only proceed for barrels or road signs
            string prefab = container.ShortPrefabName;
            if (string.IsNullOrEmpty(prefab)) return;
            bool isBarrel = prefab.Contains("barrel");
            bool isRoadSign = prefab.Contains("roadsign");
            if (!isBarrel && !isRoadSign) return;

            BasePlayer player = info?.InitiatorPlayer;
            if (player == null) return;

            var cov = player.IPlayer;
            if (cov == null) return;

            // Check permissions based on the type of container
            if (!(cov.HasPermission(PermUse) && isBarrel) && !(cov.HasPermission(PermRoadSigns) && isRoadSign))
                return;

            // Copy items into an array to avoid modification during iteration
            var items = container.inventory.itemList.ToArray();
            foreach (var item in items)
            {
                if (TryGiveItem(player, item))
                {
                    SendPickupNote(player, item);
                }
                else
                {
                    item.Drop(entity.transform.position + UnityEngine.Vector3.up * 0.25f, UnityEngine.Vector3.zero);
                }
            }
            // Clear the original container to avoid duplicate item drops
            container.inventory.Clear();
        }

        #endregion

        private bool TryGiveItem(BasePlayer player, Item item)
        {
            if (player == null || item == null) return false;
            if (player.inventory.GiveItem(item, player.inventory.containerMain)) return true;
            if (player.inventory.GiveItem(item, player.inventory.containerBelt)) return true;
            if (player.inventory.GiveItem(item, player.inventory.containerWear)) return true;
            return false;
        }

        private void SendPickupNote(BasePlayer player, Item item)
        {
            var def = item?.info;
            if (player == null || def == null || item.amount <= 0) return;
            player.Command("note.inv", def.itemid, item.amount);
        }
    }
}
