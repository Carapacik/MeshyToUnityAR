using Microsoft.Data.Sqlite;
namespace StreetArt.Server;

public sealed class PlacementStore
{
    private readonly string connectionString;
    public PlacementStore(string directory)
    {
        Directory.CreateDirectory(directory);
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "placements.db"), DefaultTimeout = 10 }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS placements (
                id TEXT PRIMARY KEY, model_id TEXT NOT NULL, latitude REAL NOT NULL,
                longitude REAL NOT NULL, scale REAL NOT NULL, rotation REAL NOT NULL, created_at TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_placements_location ON placements(latitude, longitude);
            """;
        command.ExecuteNonQuery();
        using var schema=db.CreateCommand();
        schema.CommandText="SELECT COUNT(*) FROM pragma_table_info('placements') WHERE name='accuracy_meters'";
        if(Convert.ToInt32(schema.ExecuteScalar())==0)
        {
            using var migration=db.CreateCommand();
            migration.CommandText="ALTER TABLE placements ADD COLUMN accuracy_meters REAL";
            migration.ExecuteNonQuery();
        }
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public Placement Add(PlacementInput input)
    {
        var item = new Placement(Guid.NewGuid().ToString("N"), input.ModelId, input.Latitude, input.Longitude, input.Scale, input.Rotation, DateTimeOffset.UtcNow,input.AccuracyMeters);
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO placements (id,model_id,latitude,longitude,scale,rotation,created_at,accuracy_meters) VALUES ($id,$model,$lat,$lon,$scale,$rotation,$created,$accuracy)";
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$model", item.ModelId);
        command.Parameters.AddWithValue("$lat", item.Latitude);
        command.Parameters.AddWithValue("$lon", item.Longitude);
        command.Parameters.AddWithValue("$scale", item.Scale);
        command.Parameters.AddWithValue("$rotation", item.Rotation);
        command.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$accuracy",(object?)item.AccuracyMeters ?? DBNull.Value);
        command.ExecuteNonQuery();
        return item;
    }
    public List<Placement> Nearby(double latitude, double longitude, double radius)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT * FROM placements WHERE latitude BETWEEN $min AND $max";
        command.Parameters.AddWithValue("$min", latitude - radius / 110000);
        command.Parameters.AddWithValue("$max", latitude + radius / 110000);
        using var reader = command.ExecuteReader();
        var items = new List<Placement>();
        while (reader.Read())
        {
            var item = new Placement(reader.GetString(0), reader.GetString(1), reader.GetDouble(2), reader.GetDouble(3), reader.GetFloat(4), reader.GetFloat(5), DateTimeOffset.Parse(reader.GetString(6)),reader.IsDBNull(7)?null:reader.GetFloat(7));
            if (Distance(latitude, longitude, item.Latitude, item.Longitude) <= radius) items.Add(item);
        }
        return items.OrderBy(x => Distance(latitude, longitude, x.Latitude, x.Longitude)).Take(50).ToList();
    }
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double radians = Math.PI / 180;
        double a = Math.Pow(Math.Sin((lat2-lat1)*radians/2),2) + Math.Cos(lat1*radians)*Math.Cos(lat2*radians)*Math.Pow(Math.Sin((lon2-lon1)*radians/2),2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a,0,1)));
    }
}
