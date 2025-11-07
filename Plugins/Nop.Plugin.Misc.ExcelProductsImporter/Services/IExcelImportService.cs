// Services/IExcelImportService.cs
using Microsoft.AspNetCore.Http;
using Nop.Plugin.Misc.ExcelProductsImporter.Models;

namespace Nop.Plugin.Misc.ExcelProductsImporter.Services
{
    public interface IExcelImportService
    {
        Task<ImportResultModel> ImportAsync(IFormFile file);
    }
}