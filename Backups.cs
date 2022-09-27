using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using SDG.Unturned;

namespace Pustalorc.Plugins.Backups;

public sealed class Backups : RocketPlugin<BackupsConfiguration>
{
    private string BackupFolder { get; }
    private BackgroundWorker BackupWorker { get; }
    private TimeSpan BackupDelay { get; set; }
    private TimeSpan SaveWait { get; set; }

    public Backups()
    {
        BackupWorker = new BackgroundWorker();
        BackupWorker.WorkerSupportsCancellation = true;
        BackupWorker.DoWork += RunBackupWorker;
        BackupWorker.RunWorkerCompleted += BackupWorkerCompleted;
        BackupFolder = Path.Combine(Directory, "Backups");
    }

    protected override void Load()
    {
        BackupDelay = new TimeSpan(0, 0, Configuration.Instance.BackupIntervalMinutes, 0);
        SaveWait = new TimeSpan(0, 0, 0, Configuration.Instance.PreBackupSaveWaitSeconds);

        if (Level.isLoaded)
            BackupWorker.RunWorkerAsync();
        else
            Level.onLevelLoaded += LevelLoaded;
    }

    protected override void Unload()
    {
        Level.onLevelLoaded += LevelLoaded;
        BackupWorker.CancelAsync();
    }

    private void LevelLoaded(int level)
    {
        BackupWorker.RunWorkerAsync();
    }

    private void RunBackupWorker(object sender, DoWorkEventArgs e)
    {
        while (!e.Cancel)
            Task.Run(Backup).Wait();
    }

    // This is REQUIRED in order for background worker to not eat and hide the exceptions.
    private static void BackupWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        switch (e.Error)
        {
            case null:
                return;
            case AggregateException { InnerException: { } } aggregateException:
                throw aggregateException.InnerException;
            default:
                throw e.Error;
        }
    }

    private async Task Backup()
    {
        await Task.Delay(BackupDelay);
        TaskDispatcher.QueueOnMainThread(SaveManager.save);
        await Task.Delay(SaveWait);

        var backupFolderName = DateTime.Now.ToFileTime().ToString();
        var finalBackupFolder = Path.Combine(BackupFolder, backupFolderName);

        await BackupLevel(finalBackupFolder);
        await BackupPlayers(finalBackupFolder);
        await ZipBackup(BackupFolder, finalBackupFolder, backupFolderName);

        Logger.Log($"A new backup has been created. Backup ID: {backupFolderName}.");

        var backupDirectoryInfo = new DirectoryInfo(BackupFolder);
        foreach (var backupFile in backupDirectoryInfo.GetFiles("*.zip").Where(backup =>
                     (backup.CreationTimeUtc - DateTime.UtcNow).TotalDays >=
                     Configuration.Instance.RemoveBackupsOlderThanDays))
            backupFile.Delete();
    }

    private static Task BackupLevel(string finalBackupFolder)
    {
        var levelFolderPath = ReadWrite.PATH + Path.Combine(ServerSavedata.directory, Provider.serverID, "Level", Level.info.name);
        var levelBackupFolder = Path.Combine(finalBackupFolder, "Level");
        if (!System.IO.Directory.Exists(levelBackupFolder))
            System.IO.Directory.CreateDirectory(levelBackupFolder);

        foreach (var fileToBackup in System.IO.Directory.GetFiles(levelFolderPath, "*.dat",
                     SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(fileToBackup);

            File.Copy(fileToBackup, Path.Combine(levelBackupFolder, fileName));
        }

        return Task.CompletedTask;
    }

    private static Task BackupPlayers(string finalBackupFolder)
    {
        var playerFolderPath = ReadWrite.PATH + Path.Combine(ServerSavedata.directory, Provider.serverID, "Players");
        var playerBackupFolder = Path.Combine(finalBackupFolder, "Players");

        if (!System.IO.Directory.Exists(playerBackupFolder))
            System.IO.Directory.CreateDirectory(playerBackupFolder);

        foreach (var player in System.IO.Directory.GetDirectories(playerFolderPath))
        {
            var playerDataFolder = Path.Combine(player, Level.info.name, "Player");

            if (!System.IO.Directory.Exists(playerDataFolder))
                continue;

            var playerFolderInfo = new DirectoryInfo(player);
            var folderMiddlePath = Path.Combine(playerFolderInfo.Name, Level.info.name, "Player");
            var backupFolder = Path.Combine(playerBackupFolder, folderMiddlePath);

            if (!System.IO.Directory.Exists(backupFolder))
                System.IO.Directory.CreateDirectory(backupFolder);

            foreach (var fileToBackup in System.IO.Directory.GetFiles(playerDataFolder, "*.dat",
                         SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(fileToBackup);

                File.Copy(fileToBackup, Path.Combine(backupFolder, fileName));
            }
        }

        return Task.CompletedTask;
    }

    private static Task ZipBackup(string backupFolder, string finalBackupFolder, string backupFolderName)
    {
        ZipFile.CreateFromDirectory(finalBackupFolder, Path.Combine(backupFolder, backupFolderName + ".zip"),
            CompressionLevel.Optimal, false);

        return Task.CompletedTask;
    }
}