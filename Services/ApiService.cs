using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Blish_HUD;

using Newtonsoft.Json;

using SongbookOfTyria.Models;
using SongbookOfTyria.Models.Api;

namespace SongbookOfTyria.Services
{
    public sealed class ApiService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<ApiService>();

        private const string BaseUrl = "https://gw2opus.com/wp-json/blishhud/v1/";
        private const string ApiKeyHeader = "X-GW2-API-Key";
        private const int TimeoutSeconds = 30;

        private readonly HttpClient _httpClient;
        private readonly GuildAuthService _guildAuthService;

        public ApiService(GuildAuthService guildAuthService = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            _guildAuthService = guildAuthService;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            if (_guildAuthService != null &&
                _guildAuthService.IsOpusMember &&
                !string.IsNullOrEmpty(_guildAuthService.CurrentApiKey))
            {
                request.Headers.Add(ApiKeyHeader, _guildAuthService.CurrentApiKey);
            }

            return request;
        }

        public async Task<TabsResponse> GetTabsAsync(string type = "all", string beginner = "all", string search = null)
        {
            var url = $"{BaseUrl}albums?type={type}&beginner={beginner}";
            if (!string.IsNullOrEmpty(search))
            {
                url += $"&search={Uri.EscapeDataString(search)}";
            }

            try
            {
                using (var request = CreateRequest(HttpMethod.Get, url))
                {
                    var httpResponse = await _httpClient.SendAsync(request).ConfigureAwait(false);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        Logger.Warn("GetTabsAsync: HTTP request failed with status code {StatusCode}", httpResponse.StatusCode);
                        return null;
                    }

                    var response = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (string.IsNullOrEmpty(response))
                    {
                        Logger.Warn("GetTabsAsync: Response was null or empty");
                        return null;
                    }

                    var result = JsonConvert.DeserializeObject<TabsResponse>(response);

                    if (result?.Tabs != null)
                    {
                        var privateCount = result.Tabs.Count(t => t.IsPrivate);
                        var publicCount = result.Tabs.Count(t => !t.IsPrivate);
                        Logger.Debug("GetTabsAsync: Loaded {Total} tabs (Public: {PublicCount}, Private: {PrivateCount})", result.Tabs.Count, publicCount, privateCount);

                        var hasApiKey = _guildAuthService != null && 
                                       _guildAuthService.IsOpusMember && 
                                       !string.IsNullOrEmpty(_guildAuthService.CurrentApiKey);
                        Logger.Debug("GetTabsAsync: Request sent with API key: {HasApiKey}", hasApiKey);
                    }

                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Warn(ex, "GetTabsAsync: HTTP request failed");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "GetTabsAsync: Request timed out");
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Warn(ex, "GetTabsAsync: JSON deserialization failed");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "GetTabsAsync: Unexpected error");
                return null;
            }
        }

        public async Task<MusicTab> GetTabByUrlAsync(string apiUrl, bool includeSongs = true)
        {
            if (string.IsNullOrEmpty(apiUrl))
            {
                Logger.Warn("GetTabByUrlAsync: apiUrl was null or empty");
                return null;
            }

            var url = apiUrl;
            if (!url.Contains("include_songs"))
            {
                url += (url.Contains("?") ? "&" : "?") + $"include_songs={includeSongs.ToString().ToLower()}";
            }

            try
            {
                using (var request = CreateRequest(HttpMethod.Get, url))
                {
                    var httpResponse = await _httpClient.SendAsync(request).ConfigureAwait(false);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        Logger.Warn("GetTabByUrlAsync: HTTP request failed with status code {StatusCode}", httpResponse.StatusCode);
                        return null;
                    }

                    var response = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<MusicTab>(response);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Warn(ex, "GetTabByUrlAsync: HTTP request failed");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "GetTabByUrlAsync: Failed to fetch tab from {ApiUrl}", apiUrl);
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
