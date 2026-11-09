using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace FlyTracker;

public sealed class GameLogs(string folder, FlightStore store)
{
    private record Session(string Pilot, string Key, long Start);
    private readonly Dictionary<string, (long Length, Session? Session)> headers = new(StringComparer.OrdinalIgnoreCase);

    public string[] Pilots()
    {
        return Files().Select(Header).OfType<Session>().Select(s => s.Pilot)
            .Distinct().Order().ToArray();
    }

    public void Import(Flight flight)
    {
        foreach (var file in Files())
        {
            if (new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds() < flight.Start) continue;
            var session = Header(file);
            if (session == null || session.Pilot != flight.Pilot || session.Start >= flight.End) continue;
            ReadNewLines(file, session);
        }
    }

    private IEnumerable<FileInfo> Files() =>
        new DirectoryInfo(folder).EnumerateFiles("*.txt");

    private Session? Header(FileInfo file)
    {
        if (headers.TryGetValue(file.FullName, out var cached) && (cached.Session != null || file.Length == cached.Length))
            return cached.Session;

        using var stream = OpenLog(file);
        using var reader = new StreamReader(stream);
        string? pilot = null;
        long? start = null;
        for (var i = 0; i < 8 && reader.ReadLine() is { } line; i++)
        {
            if (line.StartsWith('[')) break;
            var parts = line.Trim().Split(':', 2);
            if (parts.Length != 2) continue;
            if (parts[0] is "Listener" or "Слушатель") pilot = parts[1].Trim();
            if (parts[0] is "Session Started" or "Сеанс начат" && DateTimeOffset.TryParseExact(parts[1].Trim(),
                "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                start = date.ToUnixTimeSeconds();
        }

        Session? session = string.IsNullOrEmpty(pilot) || start == null ? null :
            new(pilot, start.Value.ToString(CultureInfo.InvariantCulture), start.Value);

        headers[file.FullName] = (file.Length, session);
        return session;
    }

    private static FileStream OpenLog(FileInfo file) =>
        new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private void ReadNewLines(FileInfo file, Session session)
    {
        using var stream = OpenLog(file);
        using var db = store.Open();
        using var transaction = db.BeginTransaction();
        using var cmd = db.CreateCommand();
        cmd.Transaction = transaction;
        cmd.Parameters.AddWithValue("$pilot", session.Pilot);
        cmd.Parameters.AddWithValue("$session", session.Key);
        cmd.CommandText = "SELECT position FROM cursors WHERE pilot=$pilot AND session=$session";
        long cursor = (long?)cmd.ExecuteScalar() ?? 0;
        if (cursor >= stream.Length) return;
        stream.Position = cursor;
        cmd.CommandText = """
            INSERT OR IGNORE INTO events(pilot,session,position,time,kind,subject,detail,damage)
            VALUES($pilot,$session,$pos,$time,$kind,$subject,$detail,$damage)
            """;

        foreach (var name in new[] { "$pos", "$time", "$kind", "$subject", "$detail", "$damage" })
            cmd.Parameters.AddWithValue(name, DBNull.Value);

        foreach (var (line, end) in CompleteLines(stream))
        {
            var item = LogParser.Parse(line);
            if (item != null)
            {
                cmd.Parameters["$pos"].Value = cursor;
                cmd.Parameters["$time"].Value = item.Time;
                cmd.Parameters["$kind"].Value = item.Kind;
                cmd.Parameters["$subject"].Value = item.Subject;
                cmd.Parameters["$detail"].Value = item.Detail;
                cmd.Parameters["$damage"].Value = (object?)item.Damage ?? DBNull.Value;
                cmd.ExecuteNonQuery();
            }

            cursor = end;
        }

        cmd.CommandText = "INSERT OR REPLACE INTO cursors VALUES($pilot,$session,$pos)";
        cmd.Parameters["$pos"].Value = cursor;
        cmd.ExecuteNonQuery();
        transaction.Commit();
    }

    private static IEnumerable<(string Text, long End)> CompleteLines(FileStream stream)
    {
        long end = stream.Position;
        var buffer = new byte[16384];
        using var line = new MemoryStream();
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            int start = 0;
            for (int i = 0; i < count; i++)
            {
                if (buffer[i] != 10) continue;
                line.Write(buffer, start, i - start);
                yield return (Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r'), end + i + 1);
                line.SetLength(0);
                start = i + 1;
            }

            line.Write(buffer, start, count - start);
            end += count;
        }
    }
}
