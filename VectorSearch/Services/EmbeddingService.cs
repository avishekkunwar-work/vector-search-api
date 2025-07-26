using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Npgsql;
using VectorSearch.Constants;
using VectorSearch.Model;

namespace VectorSearch.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public EmbeddingService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    #region Connections
    private SqlConnection SqlConnection() => new SqlConnection(_config.GetConnectionString("SqlConnection"));
    private NpgsqlConnection PgConnection() => new NpgsqlConnection(_config.GetConnectionString("PgConnection"));

    #endregion

    #region Methods
    public async Task<Dictionary<string, IEnumerable<dynamic>>> GetAllTablesAsync()
    {
        var result = new Dictionary<string, IEnumerable<dynamic>>();

        using var connection = SqlConnection();

        await connection.OpenAsync();

        // Get all user table names or filter tables according to need

        var tableNames = await connection.QueryAsync<string>(@"
                SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = DB_NAME()
            ");

        foreach (var table in tableNames)
        {
            // IMPORTANT: Use brackets to safely escape table names

            var query = $"SELECT * FROM [{table}]";

            var rows = await connection.QueryAsync(query);
            result[table] = rows;
        }

        return result;
    }
    public async Task<Response<bool>> BatchInsertAsync(IEnumerable<(string tableName, string rowId, string text, float[] embedding)> data)
    {
        var result = new Response<bool>();
        try
        {
            await using var conn = PgConnection();

            await conn.OpenAsync();

            await using var transaction = await conn.BeginTransactionAsync();

            var sql = @"
                        INSERT INTO test.vectorindex (tablename, rowid, originaltext, embedding)
                        VALUES (@tableName, @rowId, @originaltext, @embedding)";

            foreach (var item in data)
            {
                if (item.embedding == null || item.embedding.Length != 768)
                    continue;

                await using var cmd = new NpgsqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("tableName", item.tableName);
                cmd.Parameters.AddWithValue("rowId", item.rowId);
                cmd.Parameters.AddWithValue("originaltext", item.text);
                cmd.Parameters.AddWithValue("embedding", item.embedding);

                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            result.Message = "Embeddings generated and stored in Postgres successfully";
        }
        catch (Exception ex)
        {
            result.Errors.Add(new Error()
            {
                Type = ex.GetType().Name,
                Message = ex.Message
            });
        }

        return result;
    }
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var request = new { model = Embeddings.Model, prompt = text };

        var response = await _httpClient.PostAsJsonAsync(Embeddings.Api, request);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        dynamic obj = JsonConvert.DeserializeObject(json);

        return ((IEnumerable<dynamic>)obj.embedding).Select(x => (float)x).ToArray();
    }
    public async Task<Response<List<string>>> SearchSimilarAsync(float[] queryEmbedding, int topN = 5)
    {
        var result= new Response<List<string>>();
        try
        {
            //vector to PostgreSQL vector literal string
            string vectorLiteral = "[" + string.Join(",", queryEmbedding.Select(x => x.ToString("G9", System.Globalization.CultureInfo.InvariantCulture))) + "]";

            //Euclidean distance <-> //Cosine similarity <=>
            var sql = @"
                SELECT originaltext
                FROM test.vectorindex where embedding <=> CAST(@vectorParam AS vector) < 0.5
                ORDER BY embedding <=> CAST(@vectorParam AS vector) 
                LIMIT @topN";

            await using var conn = PgConnection();
            await conn.OpenAsync();

            var response = await conn.QueryAsync<string>(sql, new { vectorParam = vectorLiteral, topN });
            result.Data=response.ToList();
        }
        catch (Exception ex)
        {
            result.Errors.Add(new Error()
            {
                Type = ex.GetType().Name,
                Message = ex.Message
            });
        }

        return result;
    }

    #endregion
}
