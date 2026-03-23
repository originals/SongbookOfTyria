using System;
using System.Threading.Tasks;

using Blish_HUD;

using SongbookOfTyria.Models;
using SongbookOfTyria.Models.Api;

namespace SongbookOfTyria.Services
{
    public sealed class TabsCacheService
    {
        private static readonly Logger Logger = Logger.GetLogger<TabsCacheService>();

        private readonly ApiService _apiService;

        private TabsResponse _cachedTabsResponse;

        public event EventHandler<TabsResponse> TabsLoaded;

        public TabsCacheService(ApiService apiService)
        {
            _apiService = apiService;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await PreloadTabsFromApiAsync();
        }

        private async Task PreloadTabsFromApiAsync()
        {
            try
            {
                Logger.Info("Preloading tabs from API...");
                var freshResponse = await _apiService.GetTabsAsync().ConfigureAwait(false);

                if (freshResponse != null)
                {
                    _cachedTabsResponse = freshResponse;
                    Logger.Info("Preloaded {TabCount} tabs from API", freshResponse.Tabs?.Count ?? 0);
                    TabsLoaded?.Invoke(this, freshResponse);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to preload tabs from API");
            }
        }

        public async Task RefreshTabsAsync()
        {
            _cachedTabsResponse = null;
            await PreloadTabsFromApiAsync().ConfigureAwait(false);
        }

        public bool HasCachedTabs => _cachedTabsResponse?.Tabs != null && _cachedTabsResponse.Tabs.Count > 0;

        public TabsResponse GetCachedTabs() => _cachedTabsResponse;

        public async Task<TabsResponse> GetTabsAsync(string type = "all", string beginner = "all", string search = null)
        {
            // Return cached data immediately if available - don't wait for API
            if (_cachedTabsResponse?.Tabs != null && _cachedTabsResponse.Tabs.Count > 0)
            {
                // Refresh in background for next time
                _ = RefreshTabsInBackgroundAsync(type, beginner, search);
                return _cachedTabsResponse;
            }

            // No cache available, must fetch from API
            var freshResponse = await _apiService.GetTabsAsync(type, beginner, search).ConfigureAwait(false);

            if (freshResponse == null)
            {
                return _cachedTabsResponse;
            }

            _cachedTabsResponse = freshResponse;
            return freshResponse;
        }

        private async Task RefreshTabsInBackgroundAsync(string type, string beginner, string search)
        {
            try
            {
                var freshResponse = await _apiService.GetTabsAsync(type, beginner, search).ConfigureAwait(false);

                if (freshResponse != null)
                {
                    _cachedTabsResponse = freshResponse;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Background refresh failed");
            }
        }

        public async Task<MusicTab> GetTabDetailsAsync(MusicTab basicTab)
        {
            if (basicTab == null || string.IsNullOrEmpty(basicTab.ApiUrl))
            {
                return basicTab;
            }

            Logger.Debug("GetTabDetailsAsync: Fetching fresh data from API for tab {0} (Id: {1}): {2}", 
                basicTab.Name, basicTab.Id, basicTab.ApiUrl);

            var freshTab = await _apiService.GetTabByUrlAsync(basicTab.ApiUrl, includeSongs: true);
            if (freshTab == null)
            {
                Logger.Warn("GetTabDetailsAsync: API returned null, falling back to basic tab data");
                return basicTab;
            }

            Logger.Debug("GetTabDetailsAsync: Got fresh data (LastUpdated: {0}, HasNotation: {1})", 
                freshTab.LastUpdated, !string.IsNullOrEmpty(freshTab.NotationBlishhud));

            return freshTab;
        }

        public void ClearInMemoryCache()
        {
            _cachedTabsResponse = null;
            Logger.Info("In-memory cache cleared");
        }
    }
}
