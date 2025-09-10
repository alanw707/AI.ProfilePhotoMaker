using System;
using System.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Server=localhost,1433;Database=AIProfileMaker;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;";

        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "preview";
        Console.WriteLine($"Cleanup mode: {mode} (options: preview | delete-temp-enhanced)");

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("Connected to database successfully!");

                if (mode == "delete-temp-enhanced")
                {
                    // Target only temporary enhanced uploads accidentally persisted
                    string previewQuery = @"
                        SELECT 
                            pi.Id,
                            pi.Style,
                            pi.IsGenerated,
                            pi.IsOriginalUpload,
                            pi.OriginalImageUrl,
                            pi.ProcessedImageUrl,
                            pi.CreatedAt
                        FROM ProcessedImages pi
                        WHERE (pi.Style = 'Enhanced' OR pi.Style = '')
                          AND (pi.OriginalImageUrl IS NULL OR pi.OriginalImageUrl = '')
                          AND (pi.ProcessedImageUrl LIKE '%/enhanced/%' OR pi.ProcessedImageUrl LIKE '%\\\\enhanced\\\\%')
                        ORDER BY pi.CreatedAt DESC";

                    int candidateCount = 0;
                    using (SqlCommand command = new SqlCommand(previewQuery, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        Console.WriteLine("\nTemp enhanced upload candidates:");
                        while (reader.Read())
                        {
                            candidateCount++;
                            Console.WriteLine($"ID: {reader["Id"]}, Style: {reader["Style"]}, URL: {reader["ProcessedImageUrl"]}, Created: {reader["CreatedAt"]}");
                        }
                    }

                    Console.WriteLine($"\nCandidates found: {candidateCount}");
                    if (candidateCount == 0)
                    {
                        return;
                    }

                    Console.Write("\nDelete these records? (y/N): ");
                    string response = Console.ReadLine() ?? string.Empty;
                    if (response.Equals("y", StringComparison.OrdinalIgnoreCase) || response.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    {
                        string deleteQuery = @"
                            DELETE FROM ProcessedImages 
                            WHERE (Style = 'Enhanced' OR Style = '')
                              AND (OriginalImageUrl IS NULL OR OriginalImageUrl = '')
                              AND (ProcessedImageUrl LIKE '%/enhanced/%' OR ProcessedImageUrl LIKE '%\\\\enhanced\\\\%')";

                        using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, connection))
                        {
                            int deleted = deleteCmd.ExecuteNonQuery();
                            Console.WriteLine($"Deleted {deleted} records.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No records deleted.");
                    }
                }
                else
                {
                    // Preview summary for all images including enhanced
                    string queryCount = @"
                        SELECT 
                            COUNT(*) as TotalImages,
                            COUNT(CASE WHEN Style LIKE '%Enhanced%' THEN 1 END) as EnhancedImages,
                            COUNT(CASE WHEN (Style = 'Enhanced' OR Style = '') AND (OriginalImageUrl IS NULL OR OriginalImageUrl = '') AND (ProcessedImageUrl LIKE '%/enhanced/%' OR ProcessedImageUrl LIKE '%\\\\enhanced\\\\%') THEN 1 END) as TempEnhancedCandidates
                        FROM ProcessedImages";

                    using (SqlCommand command = new SqlCommand(queryCount, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Console.WriteLine($"Total Images: {reader["TotalImages"]}");
                            Console.WriteLine($"Enhanced Images (any): {reader["EnhancedImages"]}");
                            Console.WriteLine($"Temp Enhanced Candidates: {reader["TempEnhancedCandidates"]}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
