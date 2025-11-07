// File: /Plugins/Misc.SemanticSearch/Models/HybridSearchResultDto.cs
namespace Nop.Plugin.Misc.SemanticSearch.Models
{
    public class HybridSearchResultDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortDescription { get; set; }
        public string FullDescription { get; set; }
        public decimal Price { get; set; }
        public double Score { get; set; }
    }
}