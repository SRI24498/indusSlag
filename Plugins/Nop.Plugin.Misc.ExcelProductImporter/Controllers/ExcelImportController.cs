using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.ExcelProductImporter.Services;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
public class ExcelImportController : BasePluginController
{
    private readonly IExcelImportService _importService;
    private readonly INotificationService _notificationService;

    public ExcelImportController(IExcelImportService importService, INotificationService notificationService)
    {
        _importService = importService;
        _notificationService = notificationService;
    }

    public IActionResult Import()
    {
        return View("~/Plugins/Misc.ExcelProductImporter/Views/ExcelImport/Import.cshtml");
    }

    [HttpPost]
    public async Task<IActionResult> Import(IFormFile csvFile)
    {
        if (csvFile == null || csvFile.Length == 0 || !csvFile.FileName.EndsWith(".csv"))
        {
            _notificationService.ErrorNotification("Please upload a valid .csv file.");
            return RedirectToAction(nameof(Import));
        }

        var result = await _importService.ImportAsync(csvFile);
        _notificationService.SuccessNotification(
            $"Imported {result.ProductsCreated} products, " +
            $"{result.CategoriesCreated} new categories, " +
            $"{result.ManufacturersCreated} new manufacturers.");

        return RedirectToAction(nameof(Import));
    }
}