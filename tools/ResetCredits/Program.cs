using Microsoft.Data.SqlClient;

var connectionString = "Server=localhost,1433;Database=AIProfileMaker;User Id=sa;Password=Vg7pKr42#Local!;TrustServerCertificate=true;MultipleActiveResultSets=true;";
var userId = "72319db0-b7af-42b8-a029-c55b7baeddd3";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("Resetting credits for {0}", userId);

async Task PrintAsync(string label)
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Credits FROM UserProfiles WHERE UserId = @id";
    cmd.Parameters.AddWithValue("@id", userId);
    using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        Console.WriteLine($"{label}: credits={reader.GetInt32(0)}");
    }
    else
    {
        Console.WriteLine("User not found");
        Environment.Exit(1);
    }
}

await PrintAsync("Before");

using (var update = connection.CreateCommand())
{
    update.CommandText = "UPDATE UserProfiles SET Credits = 0, LastCreditReset = SYSUTCDATETIME() WHERE UserId = @id";
    update.Parameters.AddWithValue("@id", userId);
    Console.WriteLine($"Rows updated: {await update.ExecuteNonQueryAsync()}");
}

await PrintAsync("After");
