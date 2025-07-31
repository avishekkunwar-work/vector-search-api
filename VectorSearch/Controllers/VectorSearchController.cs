using Microsoft.AspNetCore.Mvc;
using OllamaSharp.Models.Chat;
using Serilog;
using System.Text;
using VectorSearch.Services;
using static VectorSearch.Model.ChatRequest;

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


    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequestLLM request)
    {
        Response.ContentType = "text/event-stream";

        Response.Headers.Append("Cache-Control", "no-cache");

        Response.Headers.Append("X-Accel-Buffering", "no");

        await Response.StartAsync(); // Start streaming immediately

        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Message);

        var topMatches = await _embeddingService.SearchSimilarAsync(queryEmbedding);

        if (topMatches.Success)
        {
            string context = string.Join("\n", topMatches.Data);

            var systemPrompt = $"You are a backend model that returns only direct answers based on tabular input.\nDo not explain your reasoning.\nDo not return SQL or markdown.\nRespond only with a well-formed paragraph in plain English, using proper spacing and punctuation.";

            var prompt = $"{systemPrompt}\n\nContext:\n{context}\n\nQuestion :\n{request.Message}";

            StringBuilder sb = new();

            await foreach (var chunk in _queryService.StreamChatWithOllama(prompt))
            {
                sb.Append(chunk); // accumulate chunks to reconstruct the full response progressively

                string fullResponse = sb.ToString();

                var buffer = Encoding.UTF8.GetBytes($"data: {fullResponse}\n\n");
                await Response.Body.WriteAsync(buffer, 0, buffer.Length);
                await Response.Body.FlushAsync();
            }
        }
        else
        {
            var fallback = "Sorry, I couldn’t find anything similar in memory. Proceeding without context.\n";
            var buffer = Encoding.UTF8.GetBytes($"data: {fallback}\n\n");
            await Response.Body.WriteAsync(buffer, 0, buffer.Length);
            await Response.Body.FlushAsync();
        }
       
    }

}
