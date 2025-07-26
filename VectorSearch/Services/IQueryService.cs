using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VectorSearch.Model;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace VectorSearch.Services;

public interface IQueryService
{
    Task<Response<string>> AnswerUserQueryAsync(string question);
}


