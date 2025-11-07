// File: /Plugins/Misc.SemanticSearch/Controllers/HybridSearchController.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.SemanticSearch.Services;
using Nop.Services.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;
using static LinqToDB.Reflection.Methods.LinqToDB.Insert;

namespace Nop.Plugin.Misc.SemanticSearch.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public class HybridSearchController : Controller
    {
        private readonly IHybridSearchService _hybridSearchService;

        public HybridSearchController(IHybridSearchService hybridSearchService)
        {
            _hybridSearchService = hybridSearchService;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            var results = await _hybridSearchService.SearchAsync(query);

            return Ok(new
            {
                query = query,
                count = results.Count,
                results
            });
        }
    }
}