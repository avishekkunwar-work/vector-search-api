using Microsoft.AspNetCore.Mvc;
using VectorSearch.Services;

namespace VectorSearch.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorSearchController: ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IQueryService _queryService;

    public VectorSearchController(IEmbeddingService embeddingService, IQueryService queryService)
    {
        _embeddingService = embeddingService;
        _queryService = queryService;
    }

    [HttpPost("generate-and-store-embeddings")]
    public async Task<IActionResult> GenerateAndStore()
    {
        var allTables = await _embeddingService.GetAllTablesAsync();

        var data = new List<(string tableName, string rowId, string text, float[] embedding)>();

        foreach (var table in allTables)
        {
            foreach (var row in table.Value)
            {
                var dict = (IDictionary<string, object>)row;

                string rowId = dict.ContainsKey("Id") ? dict["Id"].ToString() : Guid.NewGuid().ToString();

                string text = $"Table: {table.Key} | {string.Join(", ", dict.Select(kv => $"{kv.Key}: {kv.Value}"))}";

                if (string.IsNullOrWhiteSpace(text))
                    continue; // Skip empty text

                var embedding = await _embeddingService.GetEmbeddingAsync(text);

                // Skip if empty or wrong dimension
                if (embedding == null || embedding.Length != 768)
                {
                    Console.WriteLine($"Skipping row from {table.Key}: invalid embedding size ({embedding?.Length ?? 0})");
                    continue;
                }

                data.Add((table.Key, rowId, text, embedding));
            }
        }

        if (data.Count == 0)
            return BadRequest("No valid embeddings were generated.");

         var embeddingResponse=await _embeddingService.BatchInsertAsync(data);

        return Ok(embeddingResponse);
    }


    [HttpPost("ask")]
    public async Task<IActionResult> AskQuestionAsync([FromBody] string question)
    {
        var reasoningResponse = await _queryService.AnswerUserQueryAsync(question);

        return Ok(reasoningResponse);
    }

}
