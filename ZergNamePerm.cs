using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ZergNamePerm", "Kris+ChatGPT", "1.0.1")]
    [Description("Auto-grant kits.zerg permission when player name contains TheAddiction.gg")]
    public class ZergNamePerm : CovalencePlugin
    {
        private const string ZergPerm = "kits.zerg";
        private const string Needle = "theaddiction.gg";

        private void Init()
        {
            // Register only if not already present (prevents duplicate permission warning)
            if (!permission.PermissionExists(ZergPerm))
                permission.RegisterPermission(ZergPerm, this);
        }

        private void OnUserConnected(IPlayer player)
        {
            if (player == null) return;

            var name = (player.Name ?? string.Empty).ToLowerInvariant();
            bool has = player.HasPermission(ZergPerm);
            bool wants = name.Contains(Needle);

            if (wants && !has)
            {
                permission.GrantUserPermission(player.Id, ZergPerm, this);
                player.Message("<color=#ff6a00>Zerg kit unlocked — thanks for repping TheAddiction.gg!</color>");
            }
            else if (!wants && has)
            {
                // Optional: remove perm if they remove the tag
                permission.RevokeUserPermission(player.Id, ZergPerm);
                player.Message("<color=#ff6a00>Zerg kit removed (name no longer contains TheAddiction.gg).</color>");
            }
        }
    }
}
