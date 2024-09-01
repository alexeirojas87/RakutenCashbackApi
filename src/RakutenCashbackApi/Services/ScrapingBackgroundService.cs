
using Microsoft.Playwright;
using RakutenCashbackApi.Endpoints;
using RakutenCashbackApi.Models;
using System.Collections.Concurrent;
using System.Globalization;

namespace RakutenCashbackApi.Services
{
    public class ScrapingBackgroundService(ILogger<ScrapingBackgroundService> logger) : BackgroundService
    {
        private readonly TimeSpan _scrapeInterval = TimeSpan.FromMinutes(10);
        private readonly ILogger<ScrapingBackgroundService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await DoScrappingAsync(stoppingToken);
                RakutenEndpoints.DtoStores = new ConcurrentBag<StoreDto>(result);

                _logger.LogInformation("Updated elements. Next update in {Interval}.", _scrapeInterval);
                await Task.Delay(_scrapeInterval, stoppingToken);
            }
        }

        private static async Task<List<StoreDto>> DoScrappingAsync(CancellationToken stoppingToken)
        {
            var elements = new List<StoreDto>();
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync("https://www.rakuten.com/f/allstores");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            while (!stoppingToken.IsCancellationRequested)
            {
                var sortButtonIsVisible = await page.GetByRole(AriaRole.Button, new() { Name = "Sort" }).IsVisibleAsync();

                if (sortButtonIsVisible)
                    break;

                await page.EvaluateAsync("window.scrollBy(0, window.innerHeight)");

                await Task.Delay(1000, stoppingToken);
            }
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sort" }).ClickAsync();
            await page.GetByRole(AriaRole.Option, new() { Name = "Cash Back" }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(5000, stoppingToken);
            var items = await page.QuerySelectorAllAsync(".css-0");

            foreach (var item in items)
            {
                var spans = await item.QuerySelectorAllAsync("span");

                if (spans.Count >= 2)
                {
                    var name = (await spans[0].InnerTextAsync())?.Trim() ?? "Name not found";
                    var cashBackAmount = double.MinValue;

                    for (int i = 1; i < spans.Count; i++)
                    {
                        var text = await spans[i].InnerTextAsync();

                        if (text.Contains("Cash Back"))
                        {
                            var cashBackText = text.Replace("% Cash Back", "").Trim();
                            if (double.TryParse(cashBackText, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedCashBackAmount))
                            {
                                cashBackAmount = parsedCashBackAmount;
                                break;
                            }
                        }
                    }
                    if (cashBackAmount > 0)
                    {
                        var linkReference = await item.QuerySelectorAsync("a");
                        var href = linkReference is not null ? await linkReference.GetAttributeAsync("href") : string.Empty;
                        elements.Add(new StoreDto { CashBackAmount = cashBackAmount, Name = name, ReferenceUri = href ?? string.Empty });
                    }
                }
            }
            await browser.CloseAsync();
            return elements;
        }
    }
}
