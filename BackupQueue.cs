using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;

namespace ALE_GridBackup {
    class BackupQueue {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly GridBackupPlugin Plugin;
        private readonly Stack<long> stack = new Stack<long>();

        private readonly HashSet<long> alreadyExportedGrids = new HashSet<long>();

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
                        if(BackupSignleGrid(playerId, grids, path))
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

        public bool BackupSignleGrid(long playerId, List<MyCubeGrid> grids, string path) {

            MyCubeGrid biggestGrid = null;

            long blockCount = 0;

            foreach (var grid in grids) {

                long count = grid.BlocksCount;

                blockCount += count;

                if (biggestGrid == null || biggestGrid.BlocksCount < count)
                    biggestGrid = grid;
            }

            long entityId = biggestGrid.EntityId;

            if (alreadyExportedGrids.Contains(entityId))
                return false;

            alreadyExportedGrids.Add(entityId);

            /* To little blocks... ignore */
            if (blockCount < Plugin.Config.MinBlocksForBackup)
                return true;

            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids) {

                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }

            MyAPIGateway.Parallel.StartBackground(() => {

                try {

                    string pathForPlayer = Plugin.CreatePathForPlayer(path, playerId);

                    string gridName = biggestGrid.DisplayName;

                    string pathForGrid = Plugin.CreatePathForGrid(pathForPlayer, gridName, entityId);
                    string fileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".sbc";

                    string pathForFile = Path.Combine(pathForGrid, fileName);

                    bool saved = GridManager.SaveGrid(pathForFile, gridName, Plugin.Config.KeepOriginalOwner, Plugin.Config.BackupProjections, objectBuilders);

                    if (saved)
                        CleanUpDirectory(pathForGrid);

                } catch (Exception e) {
                    Log.Error(e, "Error on Export Grid!");
                }
            });

            return true;
        }

        private void CleanUpDirectory(string pathForGrid) {

            DirectoryInfo dir = new DirectoryInfo(pathForGrid);
            FileInfo[] fileList = dir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            var query = fileList.OrderByDescending(file => file.CreationTime);
            int numberOfFilesToKeep = Plugin.Config.NumberOfBackupSaves;

            int i = 0;
            foreach (var file in query)
                if (i++ >= numberOfFilesToKeep)
                    file.Delete();
        }
    }
}
