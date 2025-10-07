/*
Copyright © 2025 Mabel

All rights reserved. This plugin is protected by copyright law.

You may not modify, redistribute, or resell this software without explicit written permission from the copyright owner.

For any support please message me directly via Discord `mabel8686` or join my discord https://discord.gg/YWzEJVt89V

███╗   ███╗ █████╗ ██████╗ ███████╗██╗
████╗ ████║██╔══██╗██╔══██╗██╔════╝██║
██╔████╔██║███████║██████╔╝█████╗  ██║
██║╚██╔╝██║██╔══██║██╔══██╗██╔══╝  ██║
██║ ╚═╝ ██║██║  ██║██████╔╝███████╗███████╗
╚═╝     ╚═╝╚═╝  ╚═╝╚═════╝ ╚══════╝╚══════╝
*/
using ConVar;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Server Pop", "Mabel", "1.1.5")]
    [Description("Show server pop in chat with !pop trigger.")]

    public class ServerPop : RustPlugin
    {
        static Configuration config;
        static readonly Dictionary<ulong, DateTime> cooldowns = new Dictionary<ulong, DateTime>();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Cooldown Settings")]
            public CooldownSettings CooldownSettings { get; set; }

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings ChatSettings { get; set; }

            [JsonProperty(PropertyName = "Messgae Settings")]
            public MessageSettings MessageSettings { get; set; }

            [JsonProperty(PropertyName = "Response Settings")]
            public ResponseSettings ResponseSettings { get; set; }

            [JsonProperty(PropertyName = "Connect Settings")]
            public ConnectSettings ConnectSettings { get; set; }

            [JsonProperty(PropertyName = "Wipe Response Settings")]
            public WipeSettings WipeSettings { get; set; }

            [JsonProperty(PropertyName = "Blueprint Wipe Response Settings")]
            public BpWipeSettings BpWipeSettings { get; set; }

            [JsonProperty(PropertyName = "Purge Response Settings")]
            public PurgeSettings PurgeSettings { get; set; }

            [JsonProperty(PropertyName = "Skill Tree Wipe Response Settings")]
            public SkillTreeSettings SkillTreeSettings { get; set; }

            [JsonProperty(PropertyName = "Discord Response Settings")]
            public DiscordSettings DiscordSettings { get; set; }

            public Core.VersionNumber Version { get; set; }
        }

        public class CooldownSettings
        {
            [JsonProperty(PropertyName = "Cooldown (seconds)")]
            public int cooldownSeconds { get; set; } = 60;
        }

        public class ChatSettings
        {
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string chatPrefix { get; set; }

            [JsonProperty(PropertyName = "Chat Icon SteamID")]
            public ulong chatSteamID { get; set; } = 76561199216745239;
        }

        public class MessageSettings
        {
            [JsonProperty(PropertyName = "Global Response (true = global response, false = player response)")]
            public bool globalResponse { get; set; }

            [JsonProperty(PropertyName = "Use Chat Response")]
            public bool chat { get; set; }

            [JsonProperty(PropertyName = "Use Game Tip Response")]
            public bool toast { get; set; }

            [JsonProperty(PropertyName = "Use Single Line Chat Pop Response")]
            public bool oneLine { get; set; } = false;

            [JsonProperty(PropertyName = "Value Color (HEX)")]
            public string valueColor { get; set; }
        }

        public class ResponseSettings
        {
            [JsonProperty(PropertyName = "Show Online Players")]
            public bool showOnlinePlayers { get; set; }

            [JsonProperty(PropertyName = "Show Sleeping Players")]
            public bool showSleepingPlayers { get; set; }

            [JsonProperty(PropertyName = "Show Joining Players")]
            public bool showJoiningPlayers { get; set; }

            [JsonProperty(PropertyName = "Show Queued Players")]
            public bool showQueuedPlayers { get; set; }
        }

        public class ConnectSettings
        {
            [JsonProperty(PropertyName = "Show Pop On Connect")]
            public bool showPopOnConnect { get; set; }

            [JsonProperty(PropertyName = "Show Welcome Message")]
            public bool showWelcomeMessage { get; set; }

            [JsonProperty(PropertyName = "Show Wipe On Connect")]
            public bool showWipeOnConnect { get; set; }
        }

        public class WipeSettings
        {
            [JsonProperty(PropertyName = "Wipe Timer Enabled")]
            public bool wipeTimerEnabled { get; set; }

            [JsonProperty(PropertyName = "Wipe Timer (epoch)")]
            public long wipeTimer { get; set; }
        }

        public class BpWipeSettings
        {
            [JsonProperty(PropertyName = "Blueprint Wipe Timer Enabled")]
            public bool bpWipeTimerEnabled { get; set; }

            [JsonProperty(PropertyName = "Blueprint Wipe Timer (epoch)")]
            public long bpWipeTimer { get; set; }
        }

        public class PurgeSettings
        {
            [JsonProperty(PropertyName = "Purge Timer Enabled")]
            public bool purgeTimerEnabled { get; set; }

            [JsonProperty(PropertyName = "Purge Timer (epoch)")]
            public long purgeTimer { get; set; }
        }

        public class SkillTreeSettings
        {
            [JsonProperty(PropertyName = "Skill Tree Timer Enabled")]
            public bool skillTreeTimerEnabled { get; set; }

            [JsonProperty(PropertyName = "Skill Tree Wipe Timer (epoch)")]
            public long skillTreeTimer { get; set; }
        }

        public class DiscordSettings
        {
            [JsonProperty(PropertyName = "Discord Enabled")]
            public bool discordEnabled { get; set; }

            [JsonProperty(PropertyName = "Discord Invite Link")]
            public string discordLink { get; set; }
        }

        public static Configuration DefaultConfig()
        {
            return new Configuration
            {
                CooldownSettings = new CooldownSettings()
                {
                    cooldownSeconds = 60,
                },
                ChatSettings = new ChatSettings()
                {
                    chatPrefix = "<size=16><color=#FFA500>| Server Pop |</color></size>",
                    chatSteamID = 76561199216745239,
                },
                MessageSettings = new MessageSettings()
                {
                    globalResponse = true,
                    chat = false,
                    toast = true,
                    oneLine = false,
                    valueColor = "#FFA500",
                },
                ResponseSettings = new ResponseSettings()
                {
                    showOnlinePlayers = true,
                    showSleepingPlayers = true,
                    showJoiningPlayers = true,
                    showQueuedPlayers = true,
                },
                ConnectSettings = new ConnectSettings()
                {
                    showPopOnConnect = false,
                    showWelcomeMessage = false,
                    showWipeOnConnect = false,
                },
                WipeSettings = new WipeSettings()
                {
                    wipeTimerEnabled = false,
                    wipeTimer = 0,
                },
                BpWipeSettings = new BpWipeSettings()
                {
                    bpWipeTimerEnabled = false,
                    bpWipeTimer = 0,
                },
                PurgeSettings = new PurgeSettings()
                {
                    purgeTimerEnabled = false,
                    purgeTimer = 0,
                },
                SkillTreeSettings = new SkillTreeSettings()
                {
                    skillTreeTimerEnabled = false,
                    skillTreeTimer = 0,
                },
                DiscordSettings = new DiscordSettings()
                {
                    discordEnabled = false,
                    discordLink = "",
                },
                Version = new Core.VersionNumber()
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();

                if (config.DiscordSettings == null)
                {
                    config.DiscordSettings = new DiscordSettings();
                    config.DiscordSettings.discordEnabled = false;
                    config.DiscordSettings.discordLink = "";
                }
                if (config.BpWipeSettings == null)
                {
                    config.BpWipeSettings = new BpWipeSettings();
                    config.BpWipeSettings.bpWipeTimerEnabled = false;
                    config.BpWipeSettings.bpWipeTimer = 0;
                }
                if (config.PurgeSettings == null)
                {
                    config.PurgeSettings = new PurgeSettings();
                    config.PurgeSettings.purgeTimerEnabled = false;
                    config.PurgeSettings.purgeTimer = 0;
                }
                if (config.SkillTreeSettings == null)
                {
                    config.SkillTreeSettings = new SkillTreeSettings();
                    config.SkillTreeSettings.skillTreeTimerEnabled = false;
                    config.SkillTreeSettings.skillTreeTimer = 0;
                }

                if (config.Version < Version)
                    UpdateConfig();

                Config.WriteObject(config, true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                PrintWarning("Creating new configuration file....");
                LoadDefaultConfig();
            }
        }

        private void UpdateConfig()
        {
            PrintWarning("Config update detected! Updating config values...");

            Configuration baseConfig = DefaultConfig();

            if (config.Version < new Core.VersionNumber(1, 0, 6))
                config = baseConfig;

            config.Version = Version;

            PrintWarning("Config update completed!");
        }

        protected override void LoadDefaultConfig()
        {
            config = DefaultConfig();
            PrintWarning("Default configuration has been loaded....");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Online"] = "{0} / {1} players online",
                ["Sleeping"] = "{0} players sleeping",
                ["Joining"] = "{0} players joining",
                ["Queued"] = "{0} players queued",
                ["WelcomeMessage"] = "Welcome to the server {0}!",
                ["CooldownMessage"] = "You must wait {0} seconds before using this command again.",
                ["WipeMessage"] = "Next wipe in: {0}",
                ["DiscordMessage"] = "Join Us @ {0}",
                ["OneLine"] = "{0} / {1} players with {2} joining! {3} queued",
                ["BPWipeMessage"] = "Next blueprint wipe in: {0}",
                ["PurgeMessage"] = "Purge starts in: {0}",
                ["SkillMessage"] = "Next skill tree wipe in: {0}",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Online"] = "{0} / {1} jogadores online",
                ["Sleeping"] = "{0} jogadores dormindo",
                ["Joining"] = "{0} jogadores conectando",
                ["Queued"] = "{0} jogadores na fila",
                ["WelcomeMessage"] = "Bem-vindo ao servidor {0}!",
                ["CooldownMessage"] = "Você deve esperar {0} segundos antes de usar o comando novamente.",
                ["WipeMessage"] = "Próximo wipe em: {0}",
                ["DiscordMessage"] = "Junte-se ao discord @ {0}",
                ["OneLine"] = "0} / {1} jogadores com {2} conectando! {3} na fila",
                ["BPWipeMessage"] = "Próximo wipe de blueprint em: {0}",
                ["PurgeMessage"] = "PVP começa em: {0}",
                ["SkillMessage"] = "Próximo wipe de habilidades em: {0}",
            }, this, "pt-PT");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            List<string> toastMessages = new List<string>();

            if (config.ConnectSettings.showWelcomeMessage)
            {
                string welcomeMessage = lang.GetMessage("WelcomeMessage", this, player.UserIDString);

                if (!string.IsNullOrEmpty(welcomeMessage))
                {
                    welcomeMessage = string.Format(welcomeMessage, ApplyColor(player.displayName.ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatWelcomeMessage = $"{config.ChatSettings.chatPrefix} {welcomeMessage}";
                        Player?.Message(player, chatWelcomeMessage, config.ChatSettings.chatSteamID);
                    }
                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, welcomeMessage);
                    }
                }
            }

            if (config.ConnectSettings.showPopOnConnect) timer.Once(10f, () =>
            {
                if (config.ConnectSettings.showPopOnConnect && config.MessageSettings.oneLine)
                {
                    string oneLineMessage = $"{config.ChatSettings.chatPrefix}  {lang.GetMessage("OneLine", this, player.UserIDString)}";
                    oneLineMessage = string.Format(oneLineMessage,
                        ApplyColor(BasePlayer.activePlayerList.Count.ToString(), config.MessageSettings.valueColor),
                        ApplyColor(ConVar.Server.maxplayers.ToString(), config.MessageSettings.valueColor),
                        ApplyColor(ServerMgr.Instance.connectionQueue.Joining.ToString(), config.MessageSettings.valueColor),
                        ApplyColor(ServerMgr.Instance.connectionQueue.Queued.ToString(), config.MessageSettings.valueColor)
                    );

                    Player?.Message(player, oneLineMessage.ToString(), null, config.ChatSettings.chatSteamID);
                }
                else
                {
                    if (config.ConnectSettings.showPopOnConnect && config.MessageSettings.chat)
                    {
                        SendMessage(player);
                    }
                }

                if (config.ConnectSettings.showPopOnConnect && config.MessageSettings.toast)
                {
                    if (config.ResponseSettings.showOnlinePlayers)
                    {
                        string onlineMessage = $"{lang.GetMessage("Online", this, player.UserIDString)}";
                        onlineMessage = string.Format(onlineMessage, ApplyColor(BasePlayer.activePlayerList.Count.ToString(), config.MessageSettings.valueColor), ApplyColor(ConVar.Server.maxplayers.ToString(), config.MessageSettings.valueColor));
                        toastMessages.Add(onlineMessage);
                    }

                    if (config.ResponseSettings.showSleepingPlayers)
                    {
                        string sleepingMessage = $"{lang.GetMessage("Sleeping", this, player.UserIDString)}";
                        sleepingMessage = string.Format(sleepingMessage, ApplyColor(BasePlayer.sleepingPlayerList.Count.ToString(), config.MessageSettings.valueColor));
                        toastMessages.Add(sleepingMessage);
                    }

                    if (config.ResponseSettings.showJoiningPlayers)
                    {
                        string joiningMessage = $"{lang.GetMessage("Joining", this, player.UserIDString)}";
                        joiningMessage = string.Format(joiningMessage, ApplyColor(ServerMgr.Instance.connectionQueue.Joining.ToString(), config.MessageSettings.valueColor));
                        toastMessages.Add(joiningMessage);
                    }

                    if (config.ResponseSettings.showQueuedPlayers)
                    {
                        string queuedMessage = $"{lang.GetMessage("Queued", this, player.UserIDString)}";
                        queuedMessage = string.Format(queuedMessage, ApplyColor(ServerMgr.Instance.connectionQueue.Queued.ToString(), config.MessageSettings.valueColor));
                        toastMessages.Add(queuedMessage);
                    }

                    string toastMessage = string.Join("  ", toastMessages);
                    player?.ShowToast(GameTip.Styles.Blue_Long, toastMessage);
                }
            });

            if (config.ConnectSettings.showWipeOnConnect) timer.Once(16f, () =>
            {
                if (config.WipeSettings.wipeTimerEnabled)
                {
                    string wipeTimerDisplay = GetWipeTime(config.WipeSettings);
                    string wipeMessage = lang.GetMessage("WipeMessage", this, player.UserIDString);
                    wipeMessage = string.Format(wipeMessage, wipeTimerDisplay);

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.chat)
                    {
                        string chatConnectWipeMessage = $"{config.ChatSettings.chatPrefix} {wipeMessage}";
                        Player?.Message(player, chatConnectWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, wipeMessage);
                    }
                }
            });
            timer.Once(20f, () =>
            {
                if (config.BpWipeSettings.bpWipeTimerEnabled)
                {
                    string bpWipeTimerDisplay = GetBpTime(config.BpWipeSettings);
                    string bpWipeMessage = lang.GetMessage("BPWipeMessage", this, player.UserIDString);
                    bpWipeMessage = string.Format(bpWipeMessage, bpWipeTimerDisplay);

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.chat)
                    {
                        string connectBPWipeMessage = $"{config.ChatSettings.chatPrefix} {bpWipeMessage}";
                        Player?.Message(player, connectBPWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, bpWipeMessage);
                    }
                }
            });
            timer.Once(24f, () =>
            {
                if (config.PurgeSettings.purgeTimerEnabled)
                {
                    string purgeTimerDisplay = GetPurgeTime(config.PurgeSettings);
                    string purgeMessage = lang.GetMessage("PurgeMessage", this, player.UserIDString);
                    purgeMessage = string.Format(purgeMessage, purgeTimerDisplay);

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.chat)
                    {
                        string connectPurgeWipeMessage = $"{config.ChatSettings.chatPrefix} {purgeMessage}";
                        Player?.Message(player, connectPurgeWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, purgeMessage);
                    }
                }
            });
            timer.Once(28f, () =>
            {
                if (config.SkillTreeSettings.skillTreeTimerEnabled)
                {
                    string skillTimerDisplay = GetSkillTreeTime(config.SkillTreeSettings);
                    string skillMessage = lang.GetMessage("SkillMessage", this, player.UserIDString);
                    skillMessage = string.Format(skillMessage, skillTimerDisplay);

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.chat)
                    {
                        string connectPurgeWipeMessage = $"{config.ChatSettings.chatPrefix} {skillMessage}";
                        Player?.Message(player, connectPurgeWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.ConnectSettings.showWipeOnConnect && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, skillMessage);
                    }
                }
            });
        }

        private void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (player == null || message == null) return;

            // Normalize the chat message for comparison.  Accept both !pop and /pop as triggers.
            string lowerMsg = message.Trim().ToLower();
            if (lowerMsg == "!pop" || lowerMsg == "/pop")
            {
                if (CanUseTrigger(player.userID))
                {
                    if (BasePlayer.activePlayerList == null || ServerMgr.Instance == null || ServerMgr.Instance.connectionQueue == null) return;

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.oneLine)
                    {
                        string oneLineMessage = $"{config.ChatSettings.chatPrefix}  {lang.GetMessage("OneLine", this, player.UserIDString)}";
                        oneLineMessage = string.Format(oneLineMessage,
                            ApplyColor(BasePlayer.activePlayerList.Count.ToString(), config.MessageSettings.valueColor),
                            ApplyColor(ConVar.Server.maxplayers.ToString(), config.MessageSettings.valueColor),
                            ApplyColor(ServerMgr.Instance.connectionQueue.Joining.ToString(), config.MessageSettings.valueColor),
                            ApplyColor(ServerMgr.Instance.connectionQueue.Queued.ToString(), config.MessageSettings.valueColor)
                        );

                        Player?.Message(player, oneLineMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.globalResponse && config.MessageSettings.oneLine)
                    {
                        string oneLineMessage = $"{config.ChatSettings.chatPrefix}  {lang.GetMessage("OneLine", this, player.UserIDString)}";
                        oneLineMessage = string.Format(oneLineMessage,
                            ApplyColor(BasePlayer.activePlayerList.Count.ToString(), config.MessageSettings.valueColor),
                            ApplyColor(ConVar.Server.maxplayers.ToString(), config.MessageSettings.valueColor),
                            ApplyColor(ServerMgr.Instance.connectionQueue.Joining.ToString(), config.MessageSettings.valueColor),
                            ApplyColor(ServerMgr.Instance.connectionQueue.Queued.ToString(), config.MessageSettings.valueColor)
                        );

                        Server.Broadcast(oneLineMessage.ToString(), null, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.chat || config.MessageSettings.toast)
                    {
                        SendMessage(player);
                    }

                    cooldowns[player.userID] = DateTime.Now.AddSeconds(config.CooldownSettings.cooldownSeconds);
                    return;
                }
                else
                {
                    TimeSpan remainingCooldown = cooldowns[player.userID] - DateTime.Now;
                    string cooldownMessage = lang.GetMessage("CooldownMessage", this, player.UserIDString);
                    cooldownMessage = string.Format(cooldownMessage, ApplyColor(Math.Round(remainingCooldown.TotalSeconds).ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatCooldownMessage = $"{config.ChatSettings.chatPrefix} {cooldownMessage}";
                        Player?.Message(player, chatCooldownMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, cooldownMessage);
                    }
                    return;
                }
            }
            else if (config.WipeSettings.wipeTimerEnabled && (lowerMsg == "!wipe" || lowerMsg == "/wipe"))
            {
                if (config.WipeSettings.wipeTimer <= 0) return;

                if (CanUseTrigger(player.userID))
                {
                    string wipeTimerDisplay = GetWipeTime(config.WipeSettings);
                    string wipeMessage = lang.GetMessage("WipeMessage", this, player.UserIDString);
                    wipeMessage = string.Format(wipeMessage, wipeTimerDisplay);

                    if (config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatWipeMessage = $"{config.ChatSettings.chatPrefix} {wipeMessage}";
                        Player?.Message(player, chatWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatWipeMessage = $"{config.ChatSettings.chatPrefix} {wipeMessage}";
                        Player?.Message(player, chatWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, wipeMessage);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, wipeMessage);
                    }

                    cooldowns[player.userID] = DateTime.Now.AddSeconds(config.CooldownSettings.cooldownSeconds);
                    return;
                }
                else
                {
                    TimeSpan remainingCooldown = cooldowns[player.userID] - DateTime.Now;
                    string cooldownMessage = lang.GetMessage("CooldownMessage", this, player.UserIDString);
                    cooldownMessage = string.Format(cooldownMessage, ApplyColor(Math.Round(remainingCooldown.TotalSeconds).ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatCooldownMessage = $"{config.ChatSettings.chatPrefix} {cooldownMessage}";
                        Player?.Message(player, chatCooldownMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, cooldownMessage);
                    }
                    return;
                }
            }
            else if (config.BpWipeSettings.bpWipeTimerEnabled && message.ToLower() == "!bp")
            {
                if (config.BpWipeSettings.bpWipeTimer <= 0) return;

                if (CanUseTrigger(player.userID))
                {
                    string bpWipeTimerDisplay = GetBpTime(config.BpWipeSettings);
                    string bpWipeMessage = lang.GetMessage("BPWipeMessage", this, player.UserIDString);
                    bpWipeMessage = string.Format(bpWipeMessage, bpWipeTimerDisplay);

                    if (config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatBPWipeMessage = $"{config.ChatSettings.chatPrefix} {bpWipeMessage}";
                        Player?.Message(player, chatBPWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatWipeMessage = $"{config.ChatSettings.chatPrefix} {bpWipeMessage}";
                        Player?.Message(player, chatWipeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, bpWipeMessage);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, bpWipeMessage);
                    }

                    cooldowns[player.userID] = DateTime.Now.AddSeconds(config.CooldownSettings.cooldownSeconds);
                    return;
                }
                else
                {
                    TimeSpan remainingCooldown = cooldowns[player.userID] - DateTime.Now;
                    string cooldownMessage = lang.GetMessage("CooldownMessage", this, player.UserIDString);
                    cooldownMessage = string.Format(cooldownMessage, ApplyColor(Math.Round(remainingCooldown.TotalSeconds).ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatCooldownMessage = $"{config.ChatSettings.chatPrefix} {cooldownMessage}";
                        Player?.Message(player, chatCooldownMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, cooldownMessage);
                    }
                    return;
                }
            }
            else if (config.PurgeSettings.purgeTimerEnabled && message.ToLower() == "!purge")
            {
                if (config.PurgeSettings.purgeTimer <= 0) return;

                if (CanUseTrigger(player.userID))
                {
                    string purgeTimerDisplay = GetPurgeTime(config.PurgeSettings);
                    string purgeMessage = lang.GetMessage("PurgeMessage", this, player.UserIDString);
                    purgeMessage = string.Format(purgeMessage, purgeTimerDisplay);

                    if (config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatPurgeMessage = $"{config.ChatSettings.chatPrefix} {purgeMessage}";
                        Player?.Message(player, chatPurgeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatPurgeMessage = $"{config.ChatSettings.chatPrefix} {purgeMessage}";
                        Player?.Message(player, chatPurgeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, purgeMessage);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, purgeMessage);
                    }

                    cooldowns[player.userID] = DateTime.Now.AddSeconds(config.CooldownSettings.cooldownSeconds);
                    return;
                }
                else
                {
                    TimeSpan remainingCooldown = cooldowns[player.userID] - DateTime.Now;
                    string cooldownMessage = lang.GetMessage("CooldownMessage", this, player.UserIDString);
                    cooldownMessage = string.Format(cooldownMessage, ApplyColor(Math.Round(remainingCooldown.TotalSeconds).ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatCooldownMessage = $"{config.ChatSettings.chatPrefix} {cooldownMessage}";
                        Player?.Message(player, chatCooldownMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, cooldownMessage);
                    }
                    return;
                }
            }
            else if (config.SkillTreeSettings.skillTreeTimerEnabled && message.ToLower() == "!st")
            {
                if (config.SkillTreeSettings.skillTreeTimer <= 0) return;

                if (CanUseTrigger(player.userID))
                {
                    string skillTimerDisplay = GetSkillTreeTime(config.SkillTreeSettings);
                    string skillMessage = lang.GetMessage("SkillMessage", this, player.UserIDString);
                    skillMessage = string.Format(skillMessage, skillTimerDisplay);

                    if (config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatSkillMessage = $"{config.ChatSettings.chatPrefix} {skillMessage}";
                        Player?.Message(player, chatSkillMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.chat)
                    {
                        string chatPurgeMessage = $"{config.ChatSettings.chatPrefix} {skillMessage}";
                        Player?.Message(player, chatPurgeMessage, null, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, skillMessage);
                    }

                    if (!config.MessageSettings.globalResponse && config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, skillMessage);
                    }

                    cooldowns[player.userID] = DateTime.Now.AddSeconds(config.CooldownSettings.cooldownSeconds);
                    return;
                }
                else
                {
                    TimeSpan remainingCooldown = cooldowns[player.userID] - DateTime.Now;
                    string cooldownMessage = lang.GetMessage("CooldownMessage", this, player.UserIDString);
                    cooldownMessage = string.Format(cooldownMessage, ApplyColor(Math.Round(remainingCooldown.TotalSeconds).ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatCooldownMessage = $"{config.ChatSettings.chatPrefix} {cooldownMessage}";
                        Player?.Message(player, chatCooldownMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, cooldownMessage);
                    }
                    return;
                }
            }
            else if (config.DiscordSettings.discordEnabled && message.ToLower() == "!discord")
            {
                if (config.DiscordSettings.discordLink == null) return;

                if (CanUseTrigger(player.userID))
                {
                    string discordMessage = lang.GetMessage("DiscordMessage", this, player.UserIDString);
                    discordMessage = string.Format(discordMessage, config.DiscordSettings.discordLink);

                    if (config.MessageSettings.chat)
                    {
                        string chatDiscordMessage = $"{config.ChatSettings.chatPrefix} {discordMessage}";
                        Player?.Message(player, chatDiscordMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, discordMessage);
                    }

                    cooldowns[player.userID] = DateTime.Now.AddSeconds(config.CooldownSettings.cooldownSeconds);
                    return;
                }
                else
                {
                    TimeSpan remainingCooldown = cooldowns[player.userID] - DateTime.Now;
                    string cooldownMessage = lang.GetMessage("CooldownMessage", this, player.UserIDString);
                    cooldownMessage = string.Format(cooldownMessage, ApplyColor(Math.Round(remainingCooldown.TotalSeconds).ToString(), config.MessageSettings.valueColor));

                    if (config.MessageSettings.chat)
                    {
                        string chatCooldownMessage = $"{config.ChatSettings.chatPrefix} {cooldownMessage}";
                        Player?.Message(player, cooldownMessage, config.ChatSettings.chatSteamID);
                    }

                    if (config.MessageSettings.toast)
                    {
                        player?.ShowToast(GameTip.Styles.Blue_Long, cooldownMessage);
                    }
                    return;
                }
            }
        }

        private bool CanUseTrigger(ulong userID)
        {
            if (!cooldowns.ContainsKey(userID)) return true;
            return cooldowns[userID] <= DateTime.Now;
        }
        private void SendMessage(BasePlayer player)
        {
            StringBuilder popMessage = new StringBuilder($"{config.ChatSettings.chatPrefix}\n\n");

            List<string> toastMessages = new List<string>();

            if (config.ResponseSettings.showOnlinePlayers)
            {
                string onlineMessage = $"{lang.GetMessage("Online", this, player.UserIDString)}";
                onlineMessage = string.Format(onlineMessage, ApplyColor(BasePlayer.activePlayerList.Count.ToString(), config.MessageSettings.valueColor), ApplyColor(ConVar.Server.maxplayers.ToString(), config.MessageSettings.valueColor));
                popMessage.AppendLine($"{onlineMessage}\n");
                toastMessages.Add(onlineMessage);
            }

            if (config.ResponseSettings.showSleepingPlayers)
            {
                string sleepingMessage = $"{lang.GetMessage("Sleeping", this, player.UserIDString)}";
                sleepingMessage = string.Format(sleepingMessage, ApplyColor(BasePlayer.sleepingPlayerList.Count.ToString(), config.MessageSettings.valueColor));
                popMessage.AppendLine($"{sleepingMessage}\n");
                toastMessages.Add(sleepingMessage);
            }

            if (config.ResponseSettings.showJoiningPlayers)
            {
                string joiningMessage = $"{lang.GetMessage("Joining", this, player.UserIDString)}";
                joiningMessage = string.Format(joiningMessage, ApplyColor(ServerMgr.Instance.connectionQueue.Joining.ToString(), config.MessageSettings.valueColor));
                popMessage.AppendLine($"{joiningMessage}\n");
                toastMessages.Add(joiningMessage);
            }

            if (config.ResponseSettings.showQueuedPlayers)
            {
                string queuedMessage = $"{lang.GetMessage("Queued", this, player.UserIDString)}";
                queuedMessage = string.Format(queuedMessage, ApplyColor(ServerMgr.Instance.connectionQueue.Queued.ToString(), config.MessageSettings.valueColor));
                popMessage.AppendLine($"{queuedMessage}\n");
                toastMessages.Add(queuedMessage);
            }

            if (config.MessageSettings.globalResponse && config.MessageSettings.toast && toastMessages.Count > 0)
            {
                SendToastToActivePlayers(toastMessages);
            }

            if (!config.MessageSettings.globalResponse && config.MessageSettings.toast && toastMessages.Count > 0)
            {
                string toastMessage = string.Join("  ", toastMessages);
                player?.ShowToast(GameTip.Styles.Blue_Long, toastMessage);
            }

            if (config.MessageSettings.oneLine) return;

            if (config.MessageSettings.globalResponse && config.MessageSettings.chat)
            {
                Server.Broadcast(popMessage.ToString(), null, config.ChatSettings.chatSteamID);
            }

            if (!config.MessageSettings.globalResponse && config.MessageSettings.chat)
            {
                Player.Message(player, popMessage.ToString(), null, config.ChatSettings.chatSteamID);
            }
        }

        void SendToastToActivePlayers(List<string> toastMessages)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    string toastMessage = string.Join("  ", toastMessages);
                    player?.ShowToast(GameTip.Styles.Blue_Long, toastMessage);
                }
            }
        }

        private string ApplyColor(string text, string hexColor)
        {
            return $"<color={hexColor}>{text}</color>";
        }

        private object OnBetterChat(Dictionary<string, object> messageData) => Filter(messageData);

        private object Filter(Dictionary<string, object> messageData)
        {
            IPlayer player = (IPlayer)messageData["Player"];

            if (RemoveMessage((string)messageData["Message"]))
            {
                messageData["CancelOption"] = 2;
            }

            return messageData;
        }

        private bool RemoveMessage(string message)
        {
            return message.ToLower().Contains("!pop") || message.ToLower().Contains("!wipe") || message.ToLower().Contains("!bp") || message.ToLower().Contains("!purge") || message.ToLower().Contains("!st") || message.ToLower().Contains("!discord");
        }

        private string GetWipeTime(WipeSettings timer)
        {
            if (timer.wipeTimer <= 0) return null;

            TimeSpan timeSpan = TimeSpan.FromSeconds(timer.wipeTimer - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (timeSpan.TotalSeconds <= 0)
                return ApplyColor("0", config.MessageSettings.valueColor);

            return $"{ApplyColor(timeSpan.Days.ToString(), config.MessageSettings.valueColor)} Days " +
                   $"{ApplyColor(timeSpan.Hours.ToString(), config.MessageSettings.valueColor)} Hours " +
                   $"{ApplyColor(timeSpan.Minutes.ToString(), config.MessageSettings.valueColor)} Minutes";
        }

        private string GetBpTime(BpWipeSettings timer)
        {
            if (timer.bpWipeTimer <= 0) return null;

            TimeSpan timeSpan = TimeSpan.FromSeconds(timer.bpWipeTimer - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (timeSpan.TotalSeconds <= 0)
                return ApplyColor("0", config.MessageSettings.valueColor);

            return $"{ApplyColor(timeSpan.Days.ToString(), config.MessageSettings.valueColor)} Days " +
                   $"{ApplyColor(timeSpan.Hours.ToString(), config.MessageSettings.valueColor)} Hours " +
                   $"{ApplyColor(timeSpan.Minutes.ToString(), config.MessageSettings.valueColor)} Minutes";
        }

        private string GetPurgeTime(PurgeSettings timer)
        {
            if (timer.purgeTimer <= 0) return null;

            TimeSpan timeSpan = TimeSpan.FromSeconds(timer.purgeTimer - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (timeSpan.TotalSeconds <= 0)
                return ApplyColor("0", config.MessageSettings.valueColor);

            return $"{ApplyColor(timeSpan.Days.ToString(), config.MessageSettings.valueColor)} Days " +
                   $"{ApplyColor(timeSpan.Hours.ToString(), config.MessageSettings.valueColor)} Hours " +
                   $"{ApplyColor(timeSpan.Minutes.ToString(), config.MessageSettings.valueColor)} Minutes";
        }

        private string GetSkillTreeTime(SkillTreeSettings timer)
        {
            if (timer.skillTreeTimer <= 0) return null;

            TimeSpan timeSpan = TimeSpan.FromSeconds(timer.skillTreeTimer - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (timeSpan.TotalSeconds <= 0)
                return ApplyColor("0", config.MessageSettings.valueColor);

            return $"{ApplyColor(timeSpan.Days.ToString(), config.MessageSettings.valueColor)} Days " +
                   $"{ApplyColor(timeSpan.Hours.ToString(), config.MessageSettings.valueColor)} Hours " +
                   $"{ApplyColor(timeSpan.Minutes.ToString(), config.MessageSettings.valueColor)} Minutes";
        }
    }
}