using System;
using Rocket.API;

namespace Pustalorc.Plugins.Backups;

[Serializable]
public class BackupsConfiguration : IRocketPluginConfiguration
{
    public int BackupIntervalMinutes { get; set; }
    public int PreBackupSaveWaitSeconds { get; set; }
    public int RemoveBackupsOlderThanDays { get; set; }

    public void LoadDefaults()
    {
        BackupIntervalMinutes = 30;
        PreBackupSaveWaitSeconds = 5;
        RemoveBackupsOlderThanDays = 7;
    }
}