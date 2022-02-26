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

        public static string FindFolderName(DirectoryInfo[] dirList, string gridNameOrEntityId) {

            if (int.TryParse(gridNameOrEntityId, out int index)) {

                if (index <= dirList.Length && index >= 1) {

                    var file = dirList[index - 1];

                    if (file != null)
                        return file.Name;
                }
            }

            foreach (var file in dirList) 
                if (Matches(file, gridNameOrEntityId))
                    return file.Name;

            return null;
        }

        public static bool Matches(DirectoryInfo file, string gridNameOrEntityId) {

            var name = file.Name;
            var lastIndex = name.LastIndexOf("_");

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

                var fileList = file.GetFiles("*.*", SearchOption.TopDirectoryOnly);
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

                            var fileList = gridDir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

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

        public static void AddListEntriesToSb(GridBackupPlugin plugin, StringBuilder sb, 
            long playerIdentity, string gridNameOrEntityId, int startIndex, bool outputPlayerName,
            out string gridname, out bool gridFound, out int nextIndex) {

            nextIndex = startIndex;
            gridname = null;
            gridFound = false;

            string path = plugin.CreatePath();
            path = plugin.CreatePathForPlayer(path, playerIdentity);

            DirectoryInfo gridDir = new DirectoryInfo(path);
            DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

            if (gridNameOrEntityId == null) {

                if (outputPlayerName && dirList.Length > 0) {

                    var identity = PlayerUtils.GetIdentityById(playerIdentity);

                    sb.AppendLine(identity.DisplayName);
                }

                foreach (var file in dirList) {

                    string dateString = GenerateDateString(file);

                    sb.AppendLine((nextIndex++) + "      " + file.Name + " - " + dateString);
                }

                if (outputPlayerName && dirList.Length > 0)
                    sb.AppendLine();

            } else {

                string folder = FindFolderName(dirList, gridNameOrEntityId);

                gridname = folder;

                if (gridname == null)
                    return;

                gridFound = true;

                if (outputPlayerName) {

                    var identity = PlayerUtils.GetIdentityById(playerIdentity);

                    sb.AppendLine(identity.DisplayName);
                }

                path = Path.Combine(path, folder);
                gridDir = new DirectoryInfo(path);
                FileInfo[] fileList = gridDir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                var query = fileList.OrderByDescending(file => file.CreationTime);

                foreach (var file in query)
                    sb.AppendLine((nextIndex++) + "      " + file.Name + " " + (file.Length / 1024.0).ToString("#,##0.00") + " kb");

                if (outputPlayerName)
                    sb.AppendLine();
            }
        }

        public static void FindPathToRestore(GridBackupPlugin plugin,
            long identityId, string gridNameOrEntityId, int backupNumber,
            out string path, out bool gridFound, out bool outOfBounds) {

            gridFound = false;
            outOfBounds = false;

            path = plugin.CreatePath();
            path = plugin.CreatePathForPlayer(path, identityId);

            DirectoryInfo gridDir = new DirectoryInfo(path);
            DirectoryInfo[] dirList = gridDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

            string folder = FindFolderName(dirList, gridNameOrEntityId);

            if (folder == null)
                return;

            gridFound = true;

            path = Path.Combine(path, folder);
            gridDir = new DirectoryInfo(path);
            FileInfo[] fileList = gridDir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

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
