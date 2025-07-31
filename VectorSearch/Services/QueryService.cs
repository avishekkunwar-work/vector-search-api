using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using VectorSearch.Constants;
using VectorSearch.Model;
using OllamaSharp;
using OllamaSharp.Models;
using Microsoft.Extensions.AI;
using System.Net.Http.Json;
using static System.Net.Mime.MediaTypeNames;

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
                var prompt = $"Context:\n{context}\n\nQuestion:\n{question} - just provide exact result, without any explanations on json only";

                //return await InvokeOpenApi(prompt);
                result.Data= await GenerateAsync(prompt);
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



    public async IAsyncEnumerable<string> StreamChatWithOllama(string message)
    {
        var requestBody = new
        {
            model = "gemma2:2b",
            prompt = message,
            stream = true,
        };

        using var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", requestBody);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string chunk = null;

            try
            {
                var json = JsonDocument.Parse(line.Trim());

                if (json.RootElement.TryGetProperty("response", out var responsePart))
                {
                    var responseText = responsePart.GetString() ?? string.Empty;

                    chunk = responseText;
                }
            }
            catch
            {
                // Optionally log or ignore
            }

            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk; // ✅ Now this is outside the try-catch block
            }
        }
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

    private async Task<string> GenerateAsync(string prompt, string model = "deepseek-r1:1.5b")
    {
        var requestBody = new
        {
            model = model,
            prompt = prompt,
            stream = false  
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(jsonResponse);
        var responseText = document.RootElement.GetProperty("response").GetString();

        var cleanResponse = Regex.Replace(responseText, "<think>.*?</think>", "", RegexOptions.Singleline).Trim();


        return cleanResponse;
    }
    #endregion
}
