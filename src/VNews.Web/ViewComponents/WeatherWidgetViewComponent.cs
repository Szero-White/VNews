using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.ViewComponents;

public class WeatherWidgetViewComponent : ViewComponent
{
    private readonly IWeatherService _weatherService;

    public WeatherWidgetViewComponent(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var snapshot = await _weatherService.GetCurrentAsync();
        return View(snapshot);
    }
}
