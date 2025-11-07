using System.Drawing;
using System.Text.Json;
using Npgsql.Internal;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Nop.Plugin.Misc.SemanticSearch.Services
{
    public class VectorSearchService
    {
        private readonly QdrantClient _client;
        private const string CollectionName = "products-new";

        public VectorSearchService()
        {
            //_client = new QdrantClient("http://localhost:6333"); // local Qdrant
            _client = new QdrantClient(host: "localhost", port: 6334);
            InitializeCollectionAsync();

        }

        public async Task InitializeCollectionAsync()
        {
            var exists = await _client.CollectionExistsAsync(CollectionName);
            if (!exists)
            {
                var vectorParams = new VectorParams
                {
                    Size = 768,
                    Distance = Distance.Cosine
                };
                await _client.CreateCollectionAsync(CollectionName, vectorParams);
            }
        }

        public async Task<List<(int ProductId, float Distance)>> SearchAsync(List<float> queryVector, int limit = 10)
        {
            // Convert to ReadOnlyMemory<float> (expected by gRPC client)
            var vectorMemory = new ReadOnlyMemory<float>(queryVector.ToArray());

            // Pass null for filter, specify limit as the 4th argument
            var result = await _client.SearchAsync(
                collectionName: CollectionName,
                vector: vectorMemory,
                filter: null,
                limit: (uint)limit
            );


            return result
                .Select(r => (
                    ProductId: (int)r.Id.Num,   // Cast .Num to int
                    Distance: r.Score
                ))
                .ToList();
            //return result.Select(r => (int)r.Id.Num).ToList();
        }

        public async Task InsertProductAsync(
            int productId,
            List<float> vector,
            string name,
            string description,
            string[]? specifications = null,
            string[]? attributes = null)
        {
            var vectors = new Vectors { Vector = new Vector() };
            vectors.Vector.Data.AddRange(vector);

            var point = new PointStruct
            {
                Id = new PointId { Num = (ulong)productId },
                Vectors = vectors
            };

            point.Payload.Add("name", name);
            point.Payload.Add("description", description ?? "");

            if (specifications?.Length > 0)
                point.Payload.Add("specifications", JsonSerializer.Serialize(specifications));

            if (attributes?.Length > 0)
                point.Payload.Add("attributes", JsonSerializer.Serialize(attributes));

            await _client.UpsertAsync(CollectionName, new[] { point }, wait: true);
        }

        /// <summary>
        /// Returns product IDs with similarity distance (lower = better match)
        /// </summary>
        public async Task<List<(int ProductId, float Distance)>> SearchWithScoresAsync(
            List<float> queryVector,
            int limit = 10)
                {
                    var vectorMemory = new ReadOnlyMemory<float>(queryVector.ToArray());
                    var result = await _client.SearchAsync(
                        collectionName: CollectionName,
                        vector: vectorMemory,
                        filter: null,
                        limit: (uint)limit
                    );

                    return result
                        .Select(r => (
                            ProductId: (int)r.Id.Num,   // Cast .Num to int
                            Distance: r.Score
                        ))
                        .ToList();
                }

    }
}
