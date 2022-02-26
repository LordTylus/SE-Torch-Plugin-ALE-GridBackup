﻿using ALE_Core.Cooldown;
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
    public class BackupCommands : CommandModule {

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

            StringBuilder sb = new StringBuilder();
            int i = 1;
            string gridname = null;

            if (gridNameOrEntityId == null) {

                Utilities.AddListEntriesToSb(Plugin, sb, player.IdentityId, i, false, out _);

            } else {

                List<long> playerIdentityList = new List<long>();
                playerIdentityList.Add(player.IdentityId);

                var relevantGrids = Utilities.FindRelevantGrids(Plugin, playerIdentityList);

                Utilities.AddListEntriesToSb(relevantGrids, sb, gridNameOrEntityId, out gridname);

                if (gridname == null) {
                    Context.Respond("Grid not found!");
                    return;
                }
            }

            if (Context.Player == null) {

                Context.Respond($"Backed up Grids for Player {player.DisplayName} #{player.IdentityId}");

                if(gridname != null)
                    Context.Respond($"Grid {gridname}");

                Context.Respond(sb.ToString());

            } else {

                if (gridname != null)
                    ModCommunication.SendMessageTo(new DialogMessage("Backed up Grids", $"Grid {gridname}", sb.ToString()), Context.Player.SteamUserId);
                else
                    ModCommunication.SendMessageTo(new DialogMessage("Backed up Grids", $"Player {player.DisplayName} #{player.IdentityId}", sb.ToString()), Context.Player.SteamUserId);
            }
        }

        [Command("list faction", "Lists all Backups for the given faction and/or grid.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void ListFaction(string factionTag, string gridNameOrEntityId = null) {

            IMyFaction faction = FactionUtils.GetIdentityByTag(factionTag);

            if (faction == null) {
                Context.Respond("Player not found!");
                return;
            }

            StringBuilder sb = new StringBuilder();
            string gridname = null;
            int i = 1;

            var factionMembers = faction.Members;

            if (gridNameOrEntityId == null) {

                foreach (long playerIdentity in factionMembers.Keys) {

                    MyIdentity identity = PlayerUtils.GetIdentityById(playerIdentity);

                    Utilities.AddListEntriesToSb(Plugin, sb, playerIdentity, i, true, out i);
                }

            } else {

                var relevantGrids = Utilities.FindRelevantGrids(Plugin, factionMembers.Keys);

                Utilities.AddListEntriesToSb(relevantGrids, sb, gridNameOrEntityId, out gridname);

                if (gridname == null) {
                    Context.Respond("Grid not found!");
                    return;
                }
            }

            if (Context.Player == null) {

                Context.Respond($"Backed up Grids for Faction {faction.Name} [{faction.Tag}]");

                if (gridname != null)
                    Context.Respond($"Grid {gridname}");

                Context.Respond(sb.ToString());

            } else {

                if (gridname != null)
                    ModCommunication.SendMessageTo(new DialogMessage("Backed up Grids", $"Grid {gridname}", sb.ToString()), Context.Player.SteamUserId);
                else
                    ModCommunication.SendMessageTo(new DialogMessage("Backed up Grids", $"Faction {faction.Name} [{faction.Tag}]", sb.ToString()), Context.Player.SteamUserId);
            }
        }

        [Command("find", "Looks which players have a backup maching the provided grid.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Find(string gridNameOrEntityId = null) {

            string basePath = Plugin.CreatePath();

            var identities = MySession.Static.Players.GetAllIdentities();

            StringBuilder sb = new StringBuilder();
            int i = 1;

            foreach (var identitiy in identities) {

                long playerId = identitiy.IdentityId;

                string path = Plugin.CreatePathForPlayer(basePath, playerId);

                DirectoryInfo gridDir = new DirectoryInfo(path);
                DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

                List<DirectoryInfo> filteredGrids = new List<DirectoryInfo>();

                foreach (DirectoryInfo grid in dirList) {

                    if (!Utilities.Matches(grid, gridNameOrEntityId))
                        continue;

                    filteredGrids.Add(grid);
                }

                if (filteredGrids.Count == 0)
                    continue;

                string factionTag = FactionUtils.GetPlayerFactionTag(playerId);

                if (factionTag != "")
                    factionTag = " [" + factionTag + "]";

                sb.AppendLine(identitiy.DisplayName + factionTag);

                foreach (DirectoryInfo grid in filteredGrids) { 

                    string dateString = Utilities.GenerateDateString(grid);

                    sb.AppendLine((i++) + "      " + grid + " - " + dateString);
                }
            }

            if (Context.Player == null) {

                Context.Respond($"Find results");
                Context.Respond($"for grids maching {gridNameOrEntityId}");

                Context.Respond(sb.ToString());

            } else {

                ModCommunication.SendMessageTo(new DialogMessage("Find results", $"for grids maching {gridNameOrEntityId}", sb.ToString()), Context.Player.SteamUserId);
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

            List<long> playerIdentityList = new List<long>();
            playerIdentityList.Add(player.IdentityId);

            var relevantGrids = Utilities.FindRelevantGrids(Plugin, playerIdentityList);

            Utilities.FindPathToRestore(Plugin, relevantGrids, gridNameOrEntityId, backupNumber,
                out string path, out bool gridFound, out bool outOfBounds);

            if (outOfBounds) {
                Context.Respond("Backup not found! Check if the number is in range!");
                return;
            }

            if (!gridFound) {
                Context.Respond("Grid not found!");
                return;
            }

            ImportPath(path, keepOriginalPosition, force);
        }

        [Command("restore faction", "Restores the given grid from the backups.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void RestoreFaction(string factionTag, string gridNameOrEntityId, int backupNumber = 1, bool keepOriginalPosition = false, bool force = false) {

            IMyFaction faction = FactionUtils.GetIdentityByTag(factionTag);

            if (faction == null) {
                Context.Respond("Player not found!");
                return;
            }

            var factionMembers = faction.Members;

            var relevantGrids = Utilities.FindRelevantGrids(Plugin, factionMembers.Keys);

            Utilities.FindPathToRestore(Plugin, relevantGrids, gridNameOrEntityId, backupNumber,
                out string path, out bool gridFound, out bool outOfBounds);

            if (outOfBounds) {
                Context.Respond("Backup not found! Check if the number is in range!");
                return;
            }

            if (!gridFound) {
                Context.Respond("Grid not found!");
                return;
            }

            ImportPath(path, keepOriginalPosition, force);
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

            if (Plugin.BackupGridsManually(grids, out MyCubeGrid biggestGrid, out long playerId, Context))
                Context.Respond("Export Complete for Grid "+biggestGrid.DisplayName+" (EntityID: #"+biggestGrid.EntityId+") for PlayerID: #"+ playerId);
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


        private void ImportPath(string path, bool keepOriginalPosition, bool force) {

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

            var executor = Context.Player;
            var executerName = "Server";

            if (executor != null)
                executerName = executor.DisplayName;

            var result = GridManager.LoadGrid(path, playerPosition, keepOriginalPosition, force);

            var file = new FileInfo(path);

            if (result == GridImportResult.OK) {

                Log.Info(executerName + " restored backup from path: " + path);

                Context.Respond("Restored " + file.Name + " successfully!");

            } else {

                Log.Info(executerName + " failed to restore backup from path: " + path);

                GridImportResultWriter.WriteResult(Context, result);
                Context.Respond("Restore of " + file.Name + " failed!");
            }
        }
    }
}
