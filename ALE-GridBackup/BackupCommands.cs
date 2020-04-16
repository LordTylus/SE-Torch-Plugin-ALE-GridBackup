using ALE_Core.Cooldown;
using ALE_Core.GridExport;
using ALE_Core.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using VRageMath;

namespace ALE_GridBackup {

    [Category("gridbackup")]
    public class TestCommands : CommandModule {

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public GridBackupPlugin Plugin => (GridBackupPlugin) Context.Plugin;

        [Command("list", "Lists all Backups for the given player and/or grid.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void List(string playernameOrSteamId, string gridNameOrEntityId = null) {

            MyIdentity player = PlayerUtils.GetIdentityByNameOrId(playernameOrSteamId);

            if (player == null) {
                Context.Respond("Player not found!");
                return;
            }

            string path = Plugin.CreatePath();
            path = Plugin.CreatePathForPlayer(path, player.IdentityId);

            DirectoryInfo gridDir = new DirectoryInfo(path);
            DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

            StringBuilder sb = new StringBuilder();

            string gridname = null;

            if (gridNameOrEntityId == null) {

                int i = 1;
                foreach (var file in dirList) {

                    string dateString = Utilities.GenerateDateString(file);

                    sb.AppendLine((i++) + "      " + file.Name + " - " + dateString);
                }

            } else {

                string folder = Utilities.FindFolderName(dirList, gridNameOrEntityId);

                gridname = folder;

                if (gridname == null) {
                    Context.Respond("Grid not found!");
                    return;
                }

                path = Path.Combine(path, folder);
                gridDir = new DirectoryInfo(path);
                FileInfo[] fileList = gridDir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                var query = fileList.OrderByDescending(file => file.CreationTime);

                int i = 1;
                foreach (var file in query)
                    sb.AppendLine((i++) +"      "+file.Name+" "+(file.Length/1024.0).ToString("#,##0.00")+" kb");
            }

            if (Context.Player == null) {

                Context.Respond($"Backed up Grids for Player {player.DisplayName}");

                if(gridname != null)
                    Context.Respond($"Grid {gridname}");

                Context.Respond(sb.ToString());

            } else {

                if (gridname != null)
                    ModCommunication.SendMessageTo(new DialogMessage("Backed up Grids", $"Grid {gridname}", sb.ToString()), Context.Player.SteamUserId);
                else
                    ModCommunication.SendMessageTo(new DialogMessage("Backed up Grids", $"Player {player.DisplayName}", sb.ToString()), Context.Player.SteamUserId);
            }
        }

        [Command("restore", "Restores the given grid from the backups.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Restore(string playernameOrSteamId, string gridNameOrEntityId, int backupNumber = 1, bool keepOriginalPosition = false, bool force = false) {

            MyIdentity player = PlayerUtils.GetIdentityByNameOrId(playernameOrSteamId);

            if (player == null) {
                Context.Respond("Player not found!");
                return;
            }

            string path = Plugin.CreatePath();
            path = Plugin.CreatePathForPlayer(path, player.IdentityId);

            DirectoryInfo gridDir = new DirectoryInfo(path);
            DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

            StringBuilder sb = new StringBuilder();

            string folder = Utilities.FindFolderName(dirList, gridNameOrEntityId);

            if (folder == null) {
                Context.Respond("Grid not found!");
                return;
            }

            path = Path.Combine(path, folder);
            gridDir = new DirectoryInfo(path);
            FileInfo[] fileList = gridDir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            List<FileInfo> query = new List<FileInfo>(fileList.OrderByDescending(f => f.CreationTime));

            if(backupNumber > query.Count || backupNumber < 1) { 
                Context.Respond("Backup not found! Check if the number is in range!");
                return;
            }

            FileInfo file = query[backupNumber - 1];

            path = Path.Combine(path, file.Name);

            var playerPosition = Vector3D.Zero;

            if (!keepOriginalPosition) {

                if (Context.Player == null) {
                    Context.Respond("Console can only paste on the same location. Check the syntax on the plugin page!");
                    return;
                }

                var executingPlayer = ((MyPlayer)Context.Player).Identity;

                if (executingPlayer.Character == null) {
                    Context.Respond("Player has no character to spawn the grid close to!");
                    return;
                }

                playerPosition = executingPlayer.Character.PositionComp.GetPosition();
            }

            var result = GridManager.LoadGrid(path, playerPosition, keepOriginalPosition, force);

            if (result == GridImportResult.OK) {

                Context.Respond("Restore Complete!");

            } else {

                GridImportResultWriter.WriteResult(Context, result);
                Context.Respond("Restore Failed!");
            }
        }

        [Command("save", "Saves the grid defined by name, or you are looking at manually.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Save(string gridNameOrEntityId = null) {

            MyCharacter character = null;

            if (gridNameOrEntityId == null) {

                if (Context.Player == null) {
                    Context.Respond("You need to enter a Grid name where the grid will be spawned at.");
                    return;
                }

                var player = ((MyPlayer)Context.Player).Identity;

                if (player.Character == null) {
                    Context.Respond("Player has no character to spawn the grid close to!");
                    return;
                }

                character = player.Character;
            }

            List<MyCubeGrid> grids = GridFinder.FindGridList(gridNameOrEntityId, character, Plugin.Config.BackupConnections);

            if (grids == null) {
                Context.Respond("Multiple grids found. Try to rename them first or try a different subgrid for identification!");
                return;
            }

            if (grids.Count == 0) {
                Context.Respond("No grids found. Check your viewing angle or try the correct name!");
                return;
            }

            MyCubeGrid biggestGrid = null;

            foreach (var grid in grids)
                if (biggestGrid == null || biggestGrid.BlocksCount < grid.BlocksCount)
                    biggestGrid = grid;

            /* No biggest grid should not be possible, unless the gridgroup only had projections -.- just skip it. */
            if (biggestGrid == null) {
                Context.Respond("Grid incompatible!");
                return;
            }

            long playerId;

            /* No owner at all? hard to believe. but okay skip it. */
            if (biggestGrid.BigOwners.Count == 0)
                playerId = 0;
            else
                playerId = biggestGrid.BigOwners[0];

            if (BackupQueue.BackupSignleGridStatic(playerId, grids, Plugin.CreatePath(), null, Plugin, false))
                Context.Respond("Export Complete!");
            else
                Context.Respond("Export Failed!");
        }

        [Command("run", "Starts the Backup task manually.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Run() {

            try {

                if(Plugin.StartBackupManually())
                    Context.Respond("Backup creation started!");
                else
                    Context.Respond("Backup already running!");

            } catch(Exception e) {
                Log.Error(e, "Error while starting Backup");
            }
        }

        [Command("clearup", "Deletes all Backups older than the given amount of days.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Clearup(int days) {

            try {

                if(days <= 0) {
                    Context.Respond("Days "+days+" is invalid. It must be positive and non zero.");
                    return;
                }

                ulong steamId = PlayerUtils.GetSteamId(Context.Player);

                string command = "Clearup_"+ days;

                if (!CheckConformation(steamId, days, command))
                    return;

                Utilities.DeleteBackupsOlderThan(Plugin, days);

                Context.Respond("Started deleting of backups older than "+days+" days.");

            } catch (Exception e) {
                Log.Error(e, "Error while starting Backup");
            }
        }

        private bool CheckConformation(ulong steamId, long days, string command) {

            var cooldownKey = new SteamIdCooldownKey(steamId);

            var cooldownManager = Plugin.CooldownManager;
            if (!cooldownManager.CheckCooldown(cooldownKey, command, out _)) {
                cooldownManager.StopCooldown(cooldownKey);
                return true;
            }

            Context.Respond("Are you sure you want to delete all Backups older than " + days + " days? Enter the command again within 30 seconds to confirm.");
            cooldownManager.StartCooldown(cooldownKey, command, 30 * 1000);

            return false;
        }
    }
}
