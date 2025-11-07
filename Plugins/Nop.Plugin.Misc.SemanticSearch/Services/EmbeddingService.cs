using System.Text;
using System.Text.Json;

namespace Nop.Plugin.Misc.SemanticSearch.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelName;

        public EmbeddingService(string modelName = "nomic-embed-text")
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:11434")
            };
            _modelName = modelName;
        }

        public async Task<List<float>> GenerateEmbeddingAsync(string text)
        {
            var payload = new
            {
                model = _modelName,
                prompt = text
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("http://localhost:11434/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Ollama returns: { "embedding": [0.123, 0.456, ...] }
            var embeddingArray = root.GetProperty("embedding");
            var embedding = new List<float>(embeddingArray.GetArrayLength());

            foreach (var value in embeddingArray.EnumerateArray())
            {
                embedding.Add(value.GetSingle());
            }

            return embedding;
        }
    }
}
