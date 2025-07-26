using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using VectorSearch.Constants;
using VectorSearch.Model;
using OllamaSharp;
using OllamaSharp.Models;
using Microsoft.Extensions.AI;

namespace VectorSearch.Services;

public class QueryService : IQueryService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly HttpClient _httpClient;


    public QueryService(IEmbeddingService embeddingService, HttpClient httpClient)
    {
        _embeddingService = embeddingService;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.BaseAddress = new Uri(OpenAIConfigurations.Url);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAIConfigurations.Key);
    }


    public async Task<Response<string>> AnswerUserQueryAsync(string question)
    {
        var result = new Response<string>();
        try
        {
            // Step 1: Get query embedding
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(question);

            // Step 2: Vector search
            var topMatches = await _embeddingService.SearchSimilarAsync(queryEmbedding);

            if (topMatches.Success)
            {
                // Step 3: Build context
                string context = string.Join("\n", topMatches.Data);

                // Step 4: Call LLM Modules for result -- I was using open api models like gtp-4o
                var prompt = $"Context:\n{context}\n\nQuestion:\n{question}";

                return await InvokeOpenApi(prompt);
            }
            else
            {
                result.Errors = topMatches.Errors;
                return result;
            }


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

    #region Private
    private async Task<Response<string>> InvokeOpenApi(string question)
    {
        var result = new Response<string>();
        try
        {
            var request = new
            {
                model = OpenAIConfigurations.Model,
                messages = new[]
                {
                    new { role = "system", content = OpenAIConfigurations.SystemPrompt },
                    new { role = "user", content = question }
                },
                temperature = 0.7
            };

            var jsonRequest = JsonSerializer.Serialize(request);

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(jsonResponse);

            var answer = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            result.Data = answer.Trim();
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
