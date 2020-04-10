using System.Windows;
using System.Windows.Controls;

namespace ALE_GridBackup {

    public partial class BackupControl : UserControl {
        private GridBackupPlugin Plugin { get; }

        private BackupControl() {
            InitializeComponent();
        }

        public BackupControl(GridBackupPlugin plugin) : this() {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e) {
            Plugin.Save();
        }
    }
}
