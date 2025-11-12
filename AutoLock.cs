using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Lock", "birthdates", "2.1.9")]
    [Description("Automatically adds a codelock to a lockable entity with a set pin")]
    public class AutoLock : RustPlugin
    {
        #region Variables 
        private const string permission_use = "autolock.use";
        private readonly Dictionary<BasePlayer, CodeLock> AwaitingResponse = new Dictionary<BasePlayer, CodeLock>();

        #endregion

        #region Hooks
        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(permission_use, this);
            _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

            cmd.AddChatCommand("autolock", this, ChatCommand);
            cmd.AddChatCommand("al", this, ChatCommand);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var Player = plan.GetOwnerPlayer();
            if (Player == null) return;
            if (!permission.UserHasPermission(Player.UserIDString, permission_use)) return;
            var Entity = go.ToBaseEntity() as DecayEntity;
            if (Entity == null) return;
            if (_config.Disabled.Contains(Entity.PrefabName))
            {
                return;
            }
            if (!_data.Codes.ContainsKey(Player.UserIDString))
            {
                _data.Codes.Add(Player.UserIDString, new PlayerData
                {
                    Code = GetRandomCode(),
                    Enabled = true,
                });
            }
            var pCode = _data.Codes[Player.UserIDString];
            if (!pCode.Enabled || !HasCodeLock(Player)) return;
            var S = Entity as StorageContainer;
            if(S?.inventorySlots < 12) return;
            if (!S && !(Entity is AnimatedBuildingBlock)) return;
            if (Entity.IsLocked()) return;
            var Code = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
            Code.Spawn();
            Code.code = pCode.Code;
            Code.SetParent(Entity, Entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
            Entity.SetSlot(BaseEntity.Slot.Lock, Code);
            Code.SetFlag(BaseEntity.Flags.Locked, true);
            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab", Code.transform.position);
            Code.whitelistPlayers.Add(Player.userID);
            TakeCodeLock(Player);
            Player.ChatMessage(string.Format(lang.GetMessage("CodeAdded", this, Player.UserIDString), Player.net.connection.info.GetBool("global.streamermode") ? "****" : pCode.Code));
        }

        private string GetRandomCode() => Core.Random.Range(1000, 9999).ToString();

        private void OnServerShutdown() => Unload();

        private void Unload()
        {
            SaveData();
            foreach (var Lock in AwaitingResponse.Values.Where(Lock => !Lock.IsDestroyed))
            {
                Lock.Kill();
            }
        }
        #endregion

        #region Command

        private void ChatCommand(BasePlayer Player, string Label, string[] Args)
        {
            if (!permission.UserHasPermission(Player.UserIDString, permission_use))
            {
                Player.ChatMessage(lang.GetMessage("NoPermission", this, Player.UserIDString));
                return;
            }
            if (Args.Length < 1)
            {
                Player.ChatMessage(string.Format(lang.GetMessage("InvalidArgs", this, Player.UserIDString), Label));
                return;
            }
            if (!_data.Codes.ContainsKey(Player.UserIDString))
            {
                _data.Codes.Add(Player.UserIDString, new PlayerData
                {
                    Code = GetRandomCode(),
                    Enabled = true,
                });
            }
            switch (Args[0].ToLower())
            {
                case "code":
                    OpenCodeLockUI(Player);
                    break;
                case "toggle":
                    Player.ChatMessage(lang.GetMessage(Toggle(Player) ? "Enabled" : "Disabled", this, Player.UserIDString));
                    break;
                default:
                    Player.ChatMessage(string.Format(lang.GetMessage("InvalidArgs", this, Player.UserIDString), Label));
                    break;
            }
        }

        private bool HasCodeLock(BasePlayer Player) => Player.inventory.FindItemID(1159991980) != null;

        private void TakeCodeLock(BasePlayer Player) => Player.inventory.Take(null, 1159991980, 1);

        private void OpenCodeLockUI(BasePlayer Player)
        {
            var Lock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", Player.eyes.position + new Vector3(0, -3, 0)) as CodeLock;
            Lock.Spawn();
            Lock.SetFlag(BaseEntity.Flags.Locked, true);
            Lock.ClientRPCPlayer(null, Player, "EnterUnlockCode");
            if (AwaitingResponse.ContainsKey(Player)) AwaitingResponse.Remove(Player);
            AwaitingResponse.Add(Player, Lock);
            if (AwaitingResponse.Count == 1)
            {
                Subscribe("OnCodeEntered");
            }
            timer.In(20f, () =>
            {
                if (!Lock.IsDestroyed) Lock.Kill();
            });
        }

        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (!AwaitingResponse.ContainsKey(player)) return null;
            var A = AwaitingResponse[player];
            if (A != codeLock)
            {
                if (!A.IsDestroyed) A.Kill();
                AwaitingResponse.Remove(player);
                return null;
            }
            var pData = _data.Codes[player.UserIDString];
            pData.Code = code;
            player.ChatMessage(string.Format(lang.GetMessage("CodeUpdated", this, player.UserIDString), player.net.connection.info.GetBool("global.streamermode") ? "****" : code));

            var Prefab = A.effectCodeChanged;
            if (!A.IsDestroyed) A.Kill();
            AwaitingResponse.Remove(player);

            Effect.server.Run(Prefab.resourcePath, player.transform.position);
            if (AwaitingResponse.Count < 1)
            {
                Unsubscribe("OnCodeEntered");
            }
            return null;
        }

        private bool Toggle(BasePlayer Player)
        {
            var Data = _data.Codes[Player.UserIDString];
            var newToggle = !Data.Enabled;
            Data.Enabled = newToggle;
            return newToggle;
        }
        #endregion

        #region Configuration & Language

        private ConfigFile _config;
        private Data _data;

        private class PlayerData
        {
            public string Code;
            public bool Enabled;
        }

        private class Data
        {
            public readonly Dictionary<string, PlayerData> Codes = new Dictionary<string, PlayerData>();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"CodeAdded", "Codelock placed with code {0}."},
                {"Disabled", "You have disabled auto locks."},
                {"Enabled", "You have enabled auto locks."},
                {"CodeUpdated", "Your new code is {0}."},
                {"NoPermission", "You don't have permission."},
                {"InvalidArgs", "/{0} code|toggle|hide"}
            }, this);
        }

        public class ConfigFile
        {
            [JsonProperty("Disabled Items (Prefabs)")]
            public List<string> Disabled;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    Disabled = new List<string>
                    {
                        "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"
                    }
                };
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
