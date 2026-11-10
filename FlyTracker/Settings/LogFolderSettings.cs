using System.IO;

namespace FlyTracker.Settings;

internal sealed class LogFolderSettings(string dataDirectory)
{
    private readonly string file = Path.Combine(dataDirectory, "logs.path");

    public string Read() => File.Exists(file)
        ? File.ReadAllText(file)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs", "Gamelogs");

    public void Save(string folder) => File.WriteAllText(file, folder);
}
