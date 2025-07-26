
using VectorSearch.Model;

namespace VectorSearch.Services;

public interface IEmbeddingService
{
    Task<Dictionary<string, IEnumerable<dynamic>>> GetAllTablesAsync();
    Task<float[]> GetEmbeddingAsync(string text);
    Task<Response<bool>> BatchInsertAsync(IEnumerable<(string tableName, string rowId, string text, float[] embedding)> data);
    Task<Response<List<string>>> SearchSimilarAsync(float[] queryEmbedding, int topN = 5);
}

