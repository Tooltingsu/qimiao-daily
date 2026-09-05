namespace QimiaoDaily.Data;

public sealed class QimiaoDailyPaths
{
    public string Root { get; }
    public string DataDirectory => Path.Combine(Root, "data");
    public string CacheDirectory => Path.Combine(Root, "cache");
    public string ImagesDirectory => Path.Combine(Root, "images");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string BackupDirectory => Path.Combine(Root, "backup");
    public string ReportsDirectory => Path.Combine(Root, "reports");
    public string ConfigDirectory => Path.Combine(Root, "config");
    public string DatabasePath => Path.Combine(DataDirectory, "qimiao.db");

    public QimiaoDailyPaths(string? root = null) =>
        Root = root ?? Environment.GetEnvironmentVariable("QIMIAO_DATA_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QimiaoDaily");

    public void EnsureDirectories()
    {
        foreach (var path in new[] { Root, DataDirectory, CacheDirectory, ImagesDirectory, LogsDirectory, BackupDirectory, ReportsDirectory, ConfigDirectory })
            Directory.CreateDirectory(path);
    }
}
