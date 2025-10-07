diff --git a/Kits.cs b/Kits.cs
index 30644b3994e4d40082310929004b79fe2652eea6..26961dd65cdfca609d801bd455df865d6d7f507f 100644
--- a/Kits.cs
+++ b/Kits.cs
@@ -51,50 +51,67 @@ namespace Oxide.Plugins
             cmd.AddConsoleCommand(Configuration.Command, this, "ccmdKit");
         }
 
         protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);
 
         private void OnServerInitialized()
         {
             LastWipeTime = SaveRestore.SaveCreatedTime.Subtract(Epoch).TotalSeconds;
 
             kitData.RegisterImages(ImageLibrary);
 
             CheckForShortnameUpdates();
 
             if (Configuration.AutoKits.Count == 0)
                 Unsubscribe(nameof(OnPlayerRespawned));
             
             if (!Configuration.OwnedSkins)
                 Debug.LogWarning("[Kits] WARNING! As of August 7th 2025, granting access to paid skins that users do not own is against Rust's Terms of Service and can result in your server being delisted or worse.\n" +
                                  "If you continue to allow users to use paid skins, you do so at your own risk!\n" +
                                  "You can prevent users access to skins they do not own by enabling 'Only show/give players skins that they are allowed to use' in the config\n" +
                                  "https://facepunch.com/legal/servers");
             else if (!PlayerDLCAPI)
                 Debug.LogWarning("[Kits] - PlayerDLCAPI plugin is not loaded, skin ownership checks will not work!");
         }
 
+        private void OnPluginLoaded(Plugin plugin)
+        {
+            if (plugin?.Name != nameof(ImageLibrary))
+                return;
+
+            ImageLibrary = plugin;
+
+            if (kitData?.IsValid ?? false)
+                kitData.RegisterImages(ImageLibrary);
+        }
+
+        private void OnPluginUnloaded(Plugin plugin)
+        {
+            if (plugin?.Name == nameof(ImageLibrary))
+                ImageLibrary = null;
+        }
+
         private void OnNewSave(string filename)
         {
             if (Configuration.WipeData)
                 playerData.Wipe();
         }
 
         private void OnServerSave() => SavePlayerData();
 
         private void OnPlayerRespawned(BasePlayer player)
         {
             if (!player)
                 return;
 
             if ((Interface.Oxide.CallDeprecatedHook("canRedeemKit", "CanRedeemKit", _deprecatedHookTime, player) ?? Interface.Oxide.CallHook("CanRedeemKit", player)) != null)
                 return;
 
             if (Interface.Oxide.CallHook("CanRedeemAutoKit", player) != null)
                 return;
 
             if (Configuration.AllowAutoToggle && !playerData[player.userID].ClaimAutoKits)
             {
                 player.ChatMessage(Message("Error.AutoKitDisabled", player.userID));
                 return;
             }
 
