using FlyTracker.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace FlyTracker.Persistence;

public sealed class FlightStore
{
    private readonly string connectionString;

    public FlightStore(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = file }.ToString();
        using var db = Open();
        using var init = db.CreateCommand();
        init.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS flights(id INTEGER PRIMARY KEY,pilot TEXT NOT NULL,start INTEGER NOT NULL,
                end INTEGER,title TEXT NOT NULL,note TEXT NOT NULL DEFAULT '');
            CREATE UNIQUE INDEX IF NOT EXISTS one_active ON flights((1)) WHERE end IS NULL;
            CREATE TABLE IF NOT EXISTS events(pilot TEXT,session TEXT,position INTEGER,time INTEGER,
                kind TEXT,subject TEXT,detail TEXT,damage INTEGER,PRIMARY KEY(pilot,session,position));
            CREATE INDEX IF NOT EXISTS by_time ON events(pilot,time);
            CREATE TABLE IF NOT EXISTS cursors(pilot TEXT,session TEXT,position INTEGER,PRIMARY KEY(pilot,session));
            """;

        init.ExecuteNonQuery();
    }

    public SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }

    public List<Flight> History()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id,pilot,start,end,title,note FROM flights ORDER BY start DESC,id DESC";
        using var rows = cmd.ExecuteReader();
        var flights = new List<Flight>();
        while (rows.Read())
            flights.Add(new(rows.GetInt64(0), rows.GetString(1), rows.GetInt64(2),
                rows.IsDBNull(3) ? null : rows.GetInt64(3), rows.GetString(4), rows.GetString(5)));

        return flights;
    }

    public long Start(string pilot, long now)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO flights(pilot,start,title) VALUES($pilot,$now,'Вылет ' || strftime('%d.%m %H:%M',$now,'unixepoch'));
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("$pilot", pilot);
        cmd.Parameters.AddWithValue("$now", now);
        return (long)cmd.ExecuteScalar()!;
    }

    public void Save(Flight flight)
    {
        if (flight.End <= flight.Start || flight.End > DateTimeOffset.UtcNow.ToUnixTimeSeconds() || string.IsNullOrWhiteSpace(flight.Title))
            throw new ArgumentException("Проверьте название и границы вылета. Конец должен быть позже начала и не в будущем.");

        using var db = Open();
        using var tx = db.BeginTransaction();
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("$id", flight.Id);
        cmd.Parameters.AddWithValue("$pilot", flight.Pilot);
        cmd.Parameters.AddWithValue("$start", flight.Start);
        cmd.Parameters.AddWithValue("$end", (object?)flight.End ?? DBNull.Value);
        cmd.CommandText = """
            SELECT count(*) FROM flights WHERE id<>$id AND pilot=$pilot
            AND start<coalesce($end,9223372036854775807) AND coalesce(end,9223372036854775807)>$start
            """;

        if ((long)cmd.ExecuteScalar()! != 0) throw new ArgumentException("Для этого пилота уже есть вылет в выбранном интервале.");
        cmd.CommandText = "UPDATE flights SET start=$start,end=$end,title=$title,note=$note WHERE id=$id";
        cmd.Parameters.AddWithValue("$title", flight.Title.Trim());
        cmd.Parameters.AddWithValue("$note", flight.Note);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public List<Observation> Events(Flight flight)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT time,kind,subject,detail,damage FROM events
            WHERE pilot=$pilot AND time >= $start AND time < $end ORDER BY time,session,position
            """;

        cmd.Parameters.AddWithValue("$pilot", flight.Pilot);
        cmd.Parameters.AddWithValue("$start", flight.Start);
        cmd.Parameters.AddWithValue("$end", flight.End ?? long.MaxValue);
        using var rows = cmd.ExecuteReader();
        var result = new List<Observation>();
        while (rows.Read())
            result.Add(new(rows.GetInt64(0), rows.GetString(1), rows.GetString(2), rows.GetString(3),
                rows.IsDBNull(4) ? null : rows.GetInt32(4)));

        return result;
    }
}
