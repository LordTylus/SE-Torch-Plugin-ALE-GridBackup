using ALE_Core.Utils;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ALE_GridBackup {

    class Utilities {

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static DirectoryInfo FindFolderName(List<DirectoryInfo> dirList, string gridNameOrEntityId) {

            if (int.TryParse(gridNameOrEntityId, out int index)) {

                if (index <= dirList.Count && index >= 1) {

                    var file = dirList[index - 1];

                    if (file != null)
                        return file;
                }
            }

            foreach (var file in dirList) 
                if (Matches(file, gridNameOrEntityId))
                    return file;

            return null;
        }

        public static bool Matches(DirectoryInfo file, string gridNameOrEntityId) {

            var name = file.Name;
            var lastIndex = name.LastIndexOf("_");

            if (lastIndex < 0)
                return false;

            string gridName = name.Substring(0, lastIndex);
            string entityId = name.Substring(lastIndex + 1, name.Length - (lastIndex + 1));

            var regex = WildCardToRegular(gridNameOrEntityId);

            if (Regex.IsMatch(entityId, regex))
                return true;

            if (Regex.IsMatch(gridName, regex))
                return true;

            if (Regex.IsMatch(name, regex))
                return true;

            return false;
        }

        private static string WildCardToRegular(string value) {
            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }

        public static string GenerateDateString(DirectoryInfo file) {

            try {

                var fileList = file.GetFiles("*.sbc", SearchOption.TopDirectoryOnly);
                if (fileList.Length == 0)
                    return "";

                var query = fileList.OrderByDescending(f => f.CreationTime);
                var firstFile = query.First();

                return firstFile.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");

            } catch (Exception e) {
                Log.Error(e, "Error on detecting date!");
                return "";
            }
        }

        internal static void DeleteBackupsOlderThan(GridBackupPlugin plugin, int deleteBackupsOlderThanDays) {

            if (deleteBackupsOlderThanDays <= 0)
                return;

            MyAPIGateway.Parallel.StartBackground(() => {

                Log.Info("Start deleting backups older than " + deleteBackupsOlderThanDays + " days were deleted.");

                string path = plugin.CreatePath();

                DirectoryInfo dir = new DirectoryInfo(path);
                var directoryList = dir.GetDirectories("*", SearchOption.TopDirectoryOnly);

                var checkTime = DateTime.Now.AddDays(-deleteBackupsOlderThanDays);

                foreach (var playerDir in directoryList) {

                    var gridList = playerDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

                    foreach (var gridDir in gridList) {

                        try {

                            var fileList = gridDir.GetFiles("*.sbc", SearchOption.TopDirectoryOnly);

                            foreach (var file in fileList) {

                                if (file.CreationTime < checkTime)
                                    file.Delete();
                            }

                            if (gridDir.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly).Length == 0)
                                gridDir.Delete(false);

                        } catch (Exception e) {
                            Log.Error(e, "Error on deleting backups older than " + deleteBackupsOlderThanDays + " days!");
                        }
                    }

                    if (playerDir.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly).Length == 0)
                        playerDir.Delete(false);
                }

                Log.Info("Backups older than "+deleteBackupsOlderThanDays+" days were deleted.");
            });
        }

        public static List<DirectoryInfo> FindRelevantGrids(GridBackupPlugin plugin,
            IEnumerable<long> playerIdentities) {

            List<DirectoryInfo> matchingGrids = new List<DirectoryInfo>();

            foreach(long playerIdentity in playerIdentities) {

                string path = plugin.CreatePath();
                path = plugin.CreatePathForPlayer(path, playerIdentity);

                DirectoryInfo gridDir = new DirectoryInfo(path);
                DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

                foreach (var file in dirList) {

                    var fileList = file.GetFiles("*.sbc", SearchOption.TopDirectoryOnly);
                    if (fileList.Length == 0)
                        continue;

                    matchingGrids.Add(file);
                }
            }

            return matchingGrids;
        }

        public static void AddListEntriesToSb(List<DirectoryInfo> matchingGrids, 
            StringBuilder sb, string gridNameOrEntityId,
            out string gridname) {

            gridname = null;

            DirectoryInfo gridDir = FindFolderName(matchingGrids, gridNameOrEntityId);

            if (gridDir == null)
                return;

            gridname = gridDir.Name;

            FileInfo[] fileList = gridDir.GetFiles("*.sbc", SearchOption.TopDirectoryOnly);

            var query = fileList.OrderByDescending(file => file.CreationTime);

            int i = 1;

            foreach (var file in query)
                sb.AppendLine((i++) + "      " + file.Name + " " + (file.Length / 1024.0).ToString("#,##0.00") + " kb");
        }

        public static void AddListEntriesToSb(GridBackupPlugin plugin, StringBuilder sb, 
            long playerIdentity, int startIndex, bool outputPlayerName,
            out int nextIndex) {

            nextIndex = startIndex;

            string path = plugin.CreatePath();
            path = plugin.CreatePathForPlayer(path, playerIdentity);

            DirectoryInfo gridDir = new DirectoryInfo(path);
            DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

            if (outputPlayerName && dirList.Length > 0) {

                var identity = PlayerUtils.GetIdentityById(playerIdentity);

                sb.AppendLine(identity.DisplayName);
            }

            foreach (var file in dirList) {

                string dateString = GenerateDateString(file);

                if (dateString == "")
                    continue;

                sb.AppendLine((nextIndex++) + "      " + file.Name + " - " + dateString);
            }

            if (outputPlayerName && dirList.Length > 0)
                sb.AppendLine();
        }

        public static void FindPathToRestore(GridBackupPlugin plugin,
            List<DirectoryInfo> matchingGrids, string gridNameOrEntityId, int backupNumber,
            out string path, out bool gridFound, out bool outOfBounds) {

            gridFound = false;
            outOfBounds = false;
            path = null;

            DirectoryInfo gridDir = FindFolderName(matchingGrids, gridNameOrEntityId);

            if (gridDir == null)
                return;

            path = gridDir.FullName;
            gridFound = true;

            FileInfo[] fileList = gridDir.GetFiles("*.sbc", SearchOption.TopDirectoryOnly);

            List<FileInfo> query = new List<FileInfo>(fileList.OrderByDescending(f => f.CreationTime));

            if (backupNumber > query.Count || backupNumber < 1) {
                outOfBounds = true;
                return;
            }

            FileInfo file = query[backupNumber - 1];

            path = Path.Combine(path, file.Name);
        }
    }
}
