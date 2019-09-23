using NLog;
using Sandbox.Game.World;
using System;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace ALE_GridBackup {

    [Category("gridbackup")]
    public class TestCommands : CommandModule {

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public GridBackupPlugin Plugin => (GridBackupPlugin) Context.Plugin;

        [Command("list", "This is a Test Command.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void List(string playernameOrSteamId, string gridNameOrEntityId = null) {

            MyIdentity player = PlayerUtils.GetIdentityByNameOrId(playernameOrSteamId);

            if (player == null)
                Context.Respond("Player not found!");

        }

        [Command("restore", "This is a Test Command.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Restore(string playernameOrSteamId, string gridNameOrEntityId, int backupNumber = 1) {

            MyIdentity player = PlayerUtils.GetIdentityByNameOrId(playernameOrSteamId);

            if (player == null)
                Context.Respond("Player not found!");
        }

        [Command("save", "This is a Test Command.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Save(string gridNameOrEntityId) {

        }

        [Command("run", "This is a Test Command.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Run() {

            try {

                Plugin.StartBackup();

                Context.Respond("Backup creation started");

            } catch(Exception e) {
                Log.Error(e, "Error while Running Backup");
            }
        }
    }
}
