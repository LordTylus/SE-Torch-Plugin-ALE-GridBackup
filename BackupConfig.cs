using Torch;

namespace ALE_GridBackup {

    public class BackupConfig : ViewModel {

        private string _folderName = "GridBackups";
        private bool _playerNameOnFolders = false;
        private int _saveIntervalMinutes = 30;
        private int _numberOfBackupSaves = 5;
        private int _minBlocksForBackup = 20;
        private int _delayTicksBetweenExports = 1;
        private int _deleteBackupsOlderThanDays = 10;
        private bool _keepOriginalOwner = true;
        private bool _backupConnections = false;
        private bool _backupProjections = false;
        private bool _backupNobodyGrids = false;

        public string BackupSaveFolderName { get => _folderName; set => SetValue(ref _folderName, value); }
        public bool PlayerNameOnFolders { get => _playerNameOnFolders; set => SetValue(ref _playerNameOnFolders, value); }
        public int SaveIntervalMinutes { get => _saveIntervalMinutes; set => SetValue(ref _saveIntervalMinutes, value); }
        public int NumberOfBackupSaves { get => _numberOfBackupSaves; set => SetValue(ref _numberOfBackupSaves, value); }
        public int MinBlocksForBackup { get => _minBlocksForBackup; set => SetValue(ref _minBlocksForBackup, value); }
        public int DelayTicksBetweenExports { get => _delayTicksBetweenExports; set => SetValue(ref _delayTicksBetweenExports, value); }
        public int DeleteBackupsOlderThanDays { get => _deleteBackupsOlderThanDays; set => SetValue(ref _deleteBackupsOlderThanDays, value); }
        public bool KeepOriginalOwner { get => _keepOriginalOwner; set => SetValue(ref _keepOriginalOwner, value); }
        public bool BackupConnections { get => _backupConnections; set => SetValue(ref _backupConnections, value); }
        public bool BackupProjections { get => _backupProjections; set => SetValue(ref _backupProjections, value); }
        public bool BackupNobodyGrids { get => _backupNobodyGrids; set => SetValue(ref _backupNobodyGrids, value); }
    }
}
