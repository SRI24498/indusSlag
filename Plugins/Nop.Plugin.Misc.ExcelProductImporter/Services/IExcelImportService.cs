// Services/IExcelImportService.cs
using Microsoft.AspNetCore.Http;
using Nop.Plugin.Misc.ExcelProductImporter.Models;

namespace Nop.Plugin.Misc.ExcelProductImporter.Services
{
    public interface IExcelImportService
    {
        Task<ImportResultModel> ImportAsync(IFormFile file);
    }
}