using NLog;
using Sandbox.ModAPI;
using System;
using System.IO;
using System.Linq;
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

            foreach (var file in dirList) {

                var name = file.Name;
                var lastIndex = name.LastIndexOf("_");

                string gridName = name.Substring(0, lastIndex);
                string entityId = name.Substring(lastIndex + 1, name.Length - (lastIndex + 1));

                var regex = WildCardToRegular(gridNameOrEntityId);

                if (Regex.IsMatch(entityId, regex))
                    return name;

                if (Regex.IsMatch(gridName, regex))
                    return name;

                if (Regex.IsMatch(name, regex))
                    return name;
            }

            return null;
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
    }
}
