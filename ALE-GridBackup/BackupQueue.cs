using ALE_Core;
using ALE_Core.GridExport;
using ALE_Core.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VRage.Game;

namespace ALE_GridBackup {
    class BackupQueue {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string DAILY_PRAEFIX = "daily";

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly GridBackupPlugin Plugin;
        private readonly Stack<long> stack = new Stack<long>();

        private readonly HashSet<long> alreadyExportedGrids = new HashSet<long>();
        private int UpdateCount = 0;

        public BackupQueue(GridBackupPlugin Plugin) {
            this.Plugin = Plugin;
        }

        public bool IsEmpty() {
            return stack.Count == 0;
        }

        public long GetTimeMs() {
            return stopwatch.ElapsedMilliseconds;
        }

        public void AddToQueue(HashSet<long> playerIds) {

            stack.Clear();
            alreadyExportedGrids.Clear();

            foreach (long id in playerIds)
                stack.Push(id);

            stopwatch.Reset();
        }

        public void Update() {

            try {

                UpdateCount++;
                if (UpdateCount > Plugin.Config.DelayTicksBetweenExports)
                    UpdateCount = 0;

                if (UpdateCount != 0)
                    return;

                stopwatch.Start();

                /* We just peek, as we visit this player multiple times. */
                long playerId = stack.Peek();

                ConcurrentBag<List<MyCubeGrid>> gridGroups = GridFinder.FindGridList(playerId, Plugin.Config.BackupConnections);

                string path = Plugin.CreatePath();

                foreach (List<MyCubeGrid> grids in gridGroups) {

                    try {

                        /* 
                         * if false is returned we already exported the gird and 
                         * need to continue with the next one 
                         * 
                         * If true is returned we added a new grid to the list and therefore
                         * end this tick.
                         */
                        if(BackupSingleGrid(playerId, grids, path))
                            return;

                    } catch (Exception e) {
                        Log.Warn(e, "Could not export grids");
                    }
                }

                /* If we reach the end of this for loop this player is basically done. so off of the stack it goes */
                stack.Pop();

            } finally {
                stopwatch.Stop();
            }
        }

        public bool BackupSingleGrid(long playerId, List<MyCubeGrid> grids, string path) {
            return BackupSingleGridStatic(playerId, grids, path, alreadyExportedGrids, Plugin);
        }

        public static bool BackupSingleGridStatic(long playerId, List<MyCubeGrid> grids,
            string path, HashSet<long> alreadyExportedGrids, GridBackupPlugin plugin, bool background = true) {

            MyCubeGrid biggestGrid = null;

            long blockCount = 0;

            foreach (var grid in grids) {

                long count = grid.BlocksCount;

                blockCount += count;

                if (biggestGrid == null || biggestGrid.BlocksCount < count)
                    biggestGrid = grid;
            }

            long entityId = biggestGrid.EntityId;

            if (alreadyExportedGrids != null) {

                if (alreadyExportedGrids.Contains(entityId))
                    return false;

                alreadyExportedGrids.Add(entityId);
            }

            /* To little blocks... ignore */
            if (blockCount < plugin.Config.MinBlocksForBackup)
                return true;

            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids) {

                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }

            if(background) {

                MyAPIGateway.Parallel.StartBackground(() => {
                    BackupGrid(playerId, path, plugin, biggestGrid.DisplayName, entityId, objectBuilders);
                });

            } else {

                return BackupGrid(playerId, path, plugin, biggestGrid.DisplayName, entityId, objectBuilders);
            }

            return true;
        }

        public static bool BackupSingleGridStatic(long playerId, List<MyObjectBuilder_CubeGrid> grids, string path, GridBackupPlugin plugin) {

            MyObjectBuilder_CubeGrid biggestGrid = null;
            int blockOnBiggestGrid = 0;
            long blockCount = 0;

            foreach (var grid in grids) {

                int blocksOnGrid = grid.CubeBlocks != null ? grid.CubeBlocks.Count : 0;

                blockCount += blocksOnGrid;

                if (biggestGrid == null || blocksOnGrid > blockOnBiggestGrid) {
                    biggestGrid = grid;
                    blockOnBiggestGrid = blocksOnGrid;
                }
            }

            if(biggestGrid == null) {
                Log.Warn("Could not find biggest grid in list for manual backups!");
                return false;
            }

            /* To little blocks... ignore */
            if (blockCount < plugin.Config.MinBlocksForBackup)
                return true;

            long entityId = biggestGrid.EntityId;
            string displayName = biggestGrid.DisplayName;

            return BackupGrid(playerId, path, plugin, displayName, entityId, grids);
        }

        private static bool BackupGrid(long playerId, string path, GridBackupPlugin plugin, string gridName, long entityId, List<MyObjectBuilder_CubeGrid> objectBuilders) {

            try {

                string pathForPlayer = plugin.CreatePathForPlayer(path, playerId);

                string pathForGrid = plugin.CreatePathForGrid(pathForPlayer, gridName, entityId);
                string fileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".sbc";
                string fileNameDaily = DAILY_PRAEFIX+"_" +DateTime.Now.ToString("yyyy_MM_dd") + ".sbc";

                if (plugin.Config.NumberOfDailyBackupSaves > 0) {

                    string dailyFile = Path.Combine(pathForGrid, fileNameDaily);

                    if (!File.Exists(dailyFile))
                        GridManager.SaveGrid(dailyFile, gridName, plugin.Config.KeepOriginalOwner, plugin.Config.BackupProjections, objectBuilders);
                }

                string pathForFile = Path.Combine(pathForGrid, fileName);

                bool saved = GridManager.SaveGrid(pathForFile, gridName, plugin.Config.KeepOriginalOwner, plugin.Config.BackupProjections, objectBuilders);

                if (saved)
                    CleanUpDirectory(plugin, pathForGrid);

                return saved;

            } catch (Exception e) {
                Log.Error(e, "Error on Export Grid!");
                return false;
            }
        }

        private static void CleanUpDirectory(GridBackupPlugin plugin, string pathForGrid) {

            DirectoryInfo dir = new DirectoryInfo(pathForGrid);
            FileInfo[] fileList = dir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            var query = fileList.OrderByDescending(file => file.CreationTime);
            int numberOfFilesToKeep = plugin.Config.NumberOfBackupSaves;
            int numberOfDailyFilesToKeep = plugin.Config.NumberOfDailyBackupSaves;

            List<FileInfo> dailyFiles = new List<FileInfo>();

            int i = 0;
            foreach (var file in query) {

                if(file.Name.StartsWith(DAILY_PRAEFIX)) {
                    dailyFiles.Add(file);
                    continue;
                }

                if (i++ >= numberOfFilesToKeep)
                    file.Delete();
            }

            i = 0;
            foreach (var file in dailyFiles) 
                if (i++ >= numberOfDailyFilesToKeep)
                    file.Delete();
        }
    }
}
