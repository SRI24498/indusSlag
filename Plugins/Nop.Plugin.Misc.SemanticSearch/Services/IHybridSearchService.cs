// File: /Plugins/Misc.SemanticSearch/Services/IHybridSearchService.cs
using Nop.Plugin.Misc.SemanticSearch.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.SemanticSearch.Services
{
    public interface IHybridSearchService
    {
        Task<IList<HybridSearchResultDto>> SearchAsync(string query, int storeId = 0);
    }
}