using ALE_Core.Cooldown;
using ALE_Core.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Session;
using VRage.Game;

namespace ALE_GridBackup {
    public class GridBackupPlugin : TorchPluginBase, IWpfPlugin {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private BackupControl _control;
        public UserControl GetControl() => _control ?? (_control = new BackupControl(this));

        private Persistent<BackupConfig> _config;
        public BackupConfig Config => _config?.Data;

        public CooldownManager CooldownManager { get; } = new CooldownManager();

        private readonly Stopwatch stopWatch = new Stopwatch();
        private readonly BackupQueue backupQueue;

        public GridBackupPlugin() {
            backupQueue = new BackupQueue(this);
        }

        public override void Init(ITorchBase torch) {
            base.Init(torch);

            SetupConfig();

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");
        }

        private void SetupConfig() {

            var configFile = Path.Combine(StoragePath, "GridBackup.cfg");

            try {

                _config = Persistent<BackupConfig>.Load(configFile);

            } catch (Exception e) {
                Log.Warn(e);
            }

            if (_config?.Data == null) {

                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<BackupConfig>(configFile, new BackupConfig());
                Save();
            }
        }

        private void SessionChanged(ITorchSession session, TorchSessionState newState) {

            if (newState == TorchSessionState.Loaded) {

                stopWatch.Start();
                Log.Info("Session loaded, start backup timer!");

            }  else if (newState == TorchSessionState.Unloading) {

                stopWatch.Stop();
                Log.Info("Session Unloading, suspend backup timer!");
            }
        }

        public override void Update() {
            base.Update();

            try {

                /* stopWatch not running? Nothing to do */
                if (!stopWatch.IsRunning)
                    return;

                /* Session not loaded? Nothing to do */
                if (Torch.CurrentSession == null || Torch.CurrentSession.State != TorchSessionState.Loaded)
                    return;

                /* If our Queue is not empty we want to run that update */
                if (!backupQueue.IsEmpty()) {

                    backupQueue.Update();

                    /* If that was the last update you can stop now */
                    if (backupQueue.IsEmpty()) {

                        Utilities.DeleteBackupsOlderThan(this, Config.DeleteBackupsOlderThanDays);

                        Log.Info("Backup took " + stopWatch.ElapsedMilliseconds + "ms (" + backupQueue.GetTimeMs() + "ms CPU)");
                        stopWatch.Restart();
                    }

                    return;
                }

                /* if our queue was empty use normal procedure */

                int intervalMinutes = Config.SaveIntervalMinutes;

                /* interval of 0 is disabled */
                if (intervalMinutes == 0)
                    return;

                /* Time not elapsed? Nothing to do */
                var elapsed = stopWatch.Elapsed;
                if (elapsed.TotalMinutes < intervalMinutes)
                    return;

                StartBackup();

            } catch(Exception e) {
                Log.Error(e, "Could not run Backup");
            }
        }

        public bool StartBackupManually() {

            if (!backupQueue.IsEmpty())
                return false;

            StartBackup();

            return true;
        }

        public void StartBackup() {

            /* Restart stopwatch for logging how long backup took */
            stopWatch.Restart();

            Log.Info("Start Backup process!");

            HashSet<long> playerIDs = new HashSet<long>();

            foreach (var identity in MySession.Static.Players.GetAllIdentities()) {

                long identityId = identity.IdentityId;

                bool isNpc = MySession.Static.Players.IdentityIsNpc(identityId);

                /* We ignore NPCs unless the config says we need to take them with us */
                if (isNpc && !Config.BackupNpcGrids)
                    continue;

                playerIDs.Add(identityId);
            }

            /* If we want to use Nobody Grids add nobody to the list */
            if (Config.BackupNobodyGrids)
                playerIDs.Add(0L);

            backupQueue.AddToQueue(playerIDs);
        }

        public string CreatePath() {

            string fileName = FileUtils.ToValidatedInput(Config.BackupSaveFolderName);

            var folder = Path.Combine(StoragePath, fileName);
            Directory.CreateDirectory(folder);

            return folder;
        }

        public string CreatePathForPlayer(string path, long playerId) {

            string folderName;

            if (Config.UseSteamId) {

                ulong steamId = MySession.Static.Players.TryGetSteamId(playerId);

                folderName = steamId.ToString();

            } else {

                folderName = playerId.ToString();
            }

            if (Config.PlayerNameOnFolders) {

                /* 
                 * Usually all calling locations could deal with a MyIdentity instead.
                 * But I dont want to deal with NULL values to get Nobody Grids exported. 
                 */
                string playerName = FileUtils.ToValidatedInput(
                    PlayerUtils.GetDisplayNameWithoutIcon(playerId));

                folderName = playerName + "_" + folderName;
            }

            var folder = Path.Combine(path, folderName);
            Directory.CreateDirectory(folder);

            return folder;
        }

        public string CreatePathForGrid(string pathForPlayer, string gridName, long entityId) {

            gridName = FileUtils.ToValidatedInput(gridName);

            var folder = Path.Combine(pathForPlayer, gridName + "_" + entityId);
            Directory.CreateDirectory(folder);

            return folder;
        }

        public void Save() {
            try {
                _config.Save();
                Log.Info("Configuration Saved.");
            } catch (IOException e) {
                Log.Warn(e, "Configuration failed to save");
            }
        }

        /// <summary>
        /// This Methods allows to Backup 1 Grid and all of its subgrids. 
        /// 
        /// The Grids are just taken and backed up as is. So its up to the caller to filter "connected"
        /// grids, or make sure the grids are connected in the first place. If two Separate grids are
        /// put in there they *can* be backed up, however it may cause problems upon restoring them.
        /// 
        /// This Method must be called on game thread in order to work, as the internal generation of
        /// ObjectBuilders must be called from game thread. The method itself does not check for it.
        /// </summary>
        /// <param name="grids">The list of connected grids you want to backup</param>
        /// <param name="biggestGrid">Out: Biggest grid of the group (the one which name and ID is used in the folder structure)</param>
        /// <param name="playerId">Out: Player ID that owns the biggest grid in the group. 0 if nobody, -1 if there is no biggest grid. Commonly happening when List is empty</param>
        /// <param name="context">Optional: When called via command context it may output stuff to the console</param>
        /// <returns>true if and only if the grids were saved correctly. false otherwise.</returns>
        public bool BackupGridsManually(List<MyCubeGrid> grids, out MyCubeGrid biggestGrid, out long playerId, CommandContext context = null) {

            biggestGrid = null;

            foreach (var grid in grids)
                if (biggestGrid == null || biggestGrid.BlocksCount < grid.BlocksCount)
                    biggestGrid = grid;

            /* No biggest grid should not be possible, unless the gridgroup only had projections -.- just skip it. */
            if (biggestGrid == null) {

                if(context != null)
                    context.Respond("Grid incompatible!");

                playerId = -1;

                return false;
            }

            /* No owner at all? hard to believe. but okay. */
            if (biggestGrid.BigOwners.Count == 0)
                playerId = 0;
            else
                playerId = biggestGrid.BigOwners[0];

            return BackupQueue.BackupSingleGridStatic(playerId, grids, CreatePath(), null, this, false);
        }

        /// <summary>
        /// This Methods allows to Backup 1 Grid and all of its subgrids. 
        /// 
        /// The Grids are just taken and backed up as is. So its up to the caller to filter "connected"
        /// grids, or make sure the grids are connected in the first place. If two Separate grids are
        /// put in there they *can* be backed up, however it may cause problems upon restoring them.
        /// 
        /// Since this Method already uses ObjectBuilders it is not needed to run on Main-Thread and 
        /// also not adviced as Writing XML to disk could take a few millisecond longer. 
        /// 
        /// Apart from that no parallelization is done so there wont be any parallel task started or anything. 
        /// </summary>
        /// <param name="grids">The list of connected grid ObjectBuilders you want to backup</param>
        /// <param name="identityId">IdentityID of the biggest owner of the grid. Can be 0 if nobody owns it.</param>
        /// <returns>true if and only if the grids were saved correctly. false otherwise.</returns>
        public bool BackupGridsManuallyWithBuilders(List<MyObjectBuilder_CubeGrid> grids, long identityId) {

            if (grids == null || grids.Count == 0) {
                Log.Warn("Grids for manual backup empty!");
                return false;
            }

            return BackupQueue.BackupSingleGridStatic(identityId, grids, CreatePath(), this);
        }
    }
}
