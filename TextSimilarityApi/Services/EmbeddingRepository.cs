using Microsoft.Data.Sqlite;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace TextSimilarityApi.Services
{
    public class EmbeddingRepository
    {
        private readonly string _connectionString = "Data Source=embeddings.db";

        public EmbeddingRepository()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Embeddings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FileName TEXT,
                    Vector TEXT,
                    Text TEXT
                );";
            tableCmd.ExecuteNonQuery();
        }

        public void SaveEmbedding(string fileName, float[] vector, string text)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Embeddings (FileName, Vector, Text)
                VALUES ($fileName, $vector, $text);";
            insertCmd.Parameters.AddWithValue("$fileName", fileName);
            insertCmd.Parameters.AddWithValue("$vector", JsonSerializer.Serialize(vector));
            insertCmd.Parameters.AddWithValue("$text", text);
            insertCmd.ExecuteNonQuery();
        }

        public List<(string FileName, float[] Vector, string Text)> GetAllEmbeddings()
        {
            var results = new List<(string, float[], string)>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT FileName, Vector, Text FROM Embeddings;";

            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                var fileName = reader.GetString(0);
                var vectorJson = reader.GetString(1);
                var text = reader.GetString(2);
                var vector = JsonSerializer.Deserialize<float[]>(vectorJson);
                results.Add((fileName, vector, text));
            }

            return results;
        }
    }
}
