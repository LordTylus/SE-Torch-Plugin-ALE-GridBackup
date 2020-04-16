using ALE_Core.Cooldown;
using ALE_Core.Utils;
using NLog;
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
using Torch.Session;

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

                int intervalMinutes = Config.SaveIntervalMinutes;

                /* interval of 0 is disabled */
                if (intervalMinutes == 0)
                    return;

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

            string fileName = Config.BackupSaveFolderName;

            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            var folder = Path.Combine(StoragePath, fileName);
            Directory.CreateDirectory(folder);

            return folder;
        }

        public string CreatePathForPlayer(string path, long playerId) {

            string folderName = playerId.ToString();

            if (Config.PlayerNameOnFolders) {

                /* 
                 * Usually all calling locations could deal with a MyIdentity instead.
                 * But I dont want to deal with NULL values to get Nobody Grids exported. 
                 */
                string playerName = PlayerUtils.GetPlayerNameById(playerId);

                foreach (var c in Path.GetInvalidFileNameChars())
                    playerName = playerName.Replace(c, '_');

                folderName = playerName + "_" + folderName;
            }

            var folder = Path.Combine(path, folderName);
            Directory.CreateDirectory(folder);

            return folder;
        }

        public string CreatePathForGrid(string pathForPlayer, string gridName, long entityId) {

            foreach (var c in Path.GetInvalidFileNameChars())
                gridName = gridName.Replace(c, '_');

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
    }
}
