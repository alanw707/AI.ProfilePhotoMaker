using System;
using System.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Server=localhost,1433;Database=AIProfileMaker;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;";
        
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("Connected to database successfully!");
                
                // First, check what enhanced images exist
                string queryCount = @"
                    SELECT 
                        COUNT(*) as TotalImages,
                        COUNT(CASE WHEN Style LIKE '%Enhanced%' OR (IsGenerated = 1 AND IsOriginalUpload = 0 AND Style != 'Original') THEN 1 END) as EnhancedImages
                    FROM ProcessedImages";
                
                using (SqlCommand command = new SqlCommand(queryCount, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Console.WriteLine($"Total Images: {reader["TotalImages"]}");
                            Console.WriteLine($"Enhanced Images: {reader["EnhancedImages"]}");
                        }
                    }
                }
                
                // Show the enhanced images details
                string queryDetails = @"
                    SELECT 
                        pi.Id,
                        pi.Style,
                        pi.IsGenerated,
                        pi.IsOriginalUpload,
                        pi.ProcessedImageUrl,
                        pi.CreatedAt
                    FROM ProcessedImages pi
                    WHERE pi.Style LIKE '%Enhanced%' 
                       OR (pi.IsGenerated = 1 AND pi.IsOriginalUpload = 0 AND pi.Style != 'Original')
                    ORDER BY pi.CreatedAt DESC";
                
                Console.WriteLine("\nEnhanced Images Found:");
                using (SqlCommand command = new SqlCommand(queryDetails, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"ID: {reader["Id"]}, Style: {reader["Style"]}, Generated: {reader["IsGenerated"]}, URL: {reader["ProcessedImageUrl"]}, Created: {reader["CreatedAt"]}");
                        }
                    }
                }
                
                // Ask if user wants to delete
                Console.WriteLine("\nDo you want to delete these enhanced image records? (y/N):");
                string response = Console.ReadLine();
                
                if (response?.ToLower() == "y" || response?.ToLower() == "yes")
                {
                    string deleteQuery = @"
                        DELETE FROM ProcessedImages 
                        WHERE Style LIKE '%Enhanced%' 
                           OR (IsGenerated = 1 AND IsOriginalUpload = 0 AND Style != 'Original')";
                    
                    using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                    {
                        int deletedCount = command.ExecuteNonQuery();
                        Console.WriteLine($"Deleted {deletedCount} enhanced image records.");
                    }
                }
                else
                {
                    Console.WriteLine("No records deleted.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}