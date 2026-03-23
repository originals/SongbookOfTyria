using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;

using Microsoft.Xna.Framework.Graphics;

using SongbookOfTyria.Models;

namespace SongbookOfTyria.Services
{
    public sealed class TextureService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TextureService>();

        private const int MaxBatchSize = 10;
        private const int HttpTimeoutSeconds = 30;

        private readonly ContentsManager _contentsManager;
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private readonly ConcurrentDictionary<string, AsyncTexture2D> _remoteTextureCache;
        private readonly ConcurrentQueue<PendingTexture> _pendingTextures;

        #region Texture Names

        private const string CornerIconTexture = "songbook64x64_icon.png";
        private const string EmblemTexture = "songbook100x.png";
        private const string LogoSmallTexture = "songbook64x64.png";
        private const string PauseIconTexture = "icon_pause.png";

        #endregion

        #region GW2 Asset IDs

        private const int WindowBackgroundAssetId = 155985;
        private const int AboutIconAssetId = 440023;
        private const int SongLibraryIconAssetId = 102357;
        private const int OpenWindowIconAssetId = 155910;
        private const int PlayIconAssetId = 156998;
        private const int VolumeNotMutedIconAssetId = 156738;
        private const int VolumeMutedIconAssetId = 156739;
        private const int FavoriteFilledAssetId = 102439;
        private const int FavoriteEmptyAssetId = 102440;

        #endregion

        public TextureService(ContentsManager contentsManager, string cacheDirectory)
        {
            _contentsManager = contentsManager;
            _cacheDirectory = cacheDirectory;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds)
            };
            _remoteTextureCache = new ConcurrentDictionary<string, AsyncTexture2D>();
            _pendingTextures = new ConcurrentQueue<PendingTexture>();

            Directory.CreateDirectory(_cacheDirectory);
        }

        #region Bundled Textures

        public AsyncTexture2D GetCornerIcon() => GetBundledTexture(CornerIconTexture);
        public AsyncTexture2D GetEmblem() => GetBundledTexture(EmblemTexture);
        public AsyncTexture2D GetLogoSmall() => GetBundledTexture(LogoSmallTexture);

        private AsyncTexture2D GetBundledTexture(string textureName)
        {
            try
            {
                return _contentsManager.GetTexture(textureName);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load bundled texture {TextureName}", textureName);
                return null;
            }
        }

        #endregion

        #region GW2 Asset Textures

        public AsyncTexture2D GetWindowBackground() => GetAssetTexture(WindowBackgroundAssetId);
        public AsyncTexture2D GetAboutIcon() => GetAssetTexture(AboutIconAssetId);
        public AsyncTexture2D GetSongLibraryIcon() => GetAssetTexture(SongLibraryIconAssetId);
        public AsyncTexture2D GetOpenWindowIcon() => GetAssetTexture(OpenWindowIconAssetId);
        public AsyncTexture2D GetPlayIcon() => GetAssetTexture(PlayIconAssetId);
        public AsyncTexture2D GetPauseIcon() => GetBundledTexture(PauseIconTexture);
        public AsyncTexture2D GetVolumeIcon() => GetAssetTexture(VolumeNotMutedIconAssetId);
        public AsyncTexture2D GetVolumeMutedIcon() => GetAssetTexture(VolumeMutedIconAssetId);
        public AsyncTexture2D GetFavoriteFilledIcon() => GetAssetTexture(FavoriteFilledAssetId);
        public AsyncTexture2D GetFavoriteEmptyIcon() => GetAssetTexture(FavoriteEmptyAssetId);

        private static AsyncTexture2D GetAssetTexture(int assetId)
        {
            try
            {
                return AsyncTexture2D.FromAssetId(assetId);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load asset texture {AssetId}", assetId);
                return null;
            }
        }

        #endregion

        #region Remote Texture Caching

        public void PreloadThumbnails(IEnumerable<MusicTab> tabs)
        {
            if (tabs == null)
            {
                return;
            }

            Task.Run(async () =>
            {
                foreach (var tab in tabs)
                {
                    if (!string.IsNullOrEmpty(tab.Thumbnail) && 
                        !tab.Thumbnail.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    {
                        // Just trigger the download to cache, don't create texture yet
                        await PreloadThumbnailToCacheAsync(tab.Thumbnail).ConfigureAwait(false);
                    }
                }
            });
        }

        private async Task PreloadThumbnailToCacheAsync(string url)
        {
            try
            {
                var fileName = GetCacheFileName(url);
                var filePath = Path.Combine(_cacheDirectory, fileName);

                if (File.Exists(filePath))
                {
                    return; // Already cached
                }

                var imageData = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                Directory.CreateDirectory(_cacheDirectory);
                await Task.Run(() => File.WriteAllBytes(filePath, imageData)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to preload thumbnail {Url}", url);
            }
        }

        public AsyncTexture2D GetRemoteTexture(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (_remoteTextureCache.TryGetValue(url, out var cachedTexture))
            {
                return cachedTexture;
            }

            var asyncTexture = new AsyncTexture2D();
            _remoteTextureCache[url] = asyncTexture;

            Task.Run(() => DownloadTextureDataAsync(url, asyncTexture));

            return asyncTexture;
        }

        private async Task DownloadTextureDataAsync(string url, AsyncTexture2D asyncTexture)
        {
            try
            {
                var fileName = GetCacheFileName(url);
                var filePath = Path.Combine(_cacheDirectory, fileName);

                byte[] imageData;

                if (File.Exists(filePath))
                {
                    imageData = await Task.Run(() => File.ReadAllBytes(filePath)).ConfigureAwait(false);
                }
                else
                {
                    imageData = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                    Directory.CreateDirectory(_cacheDirectory);
                    await Task.Run(() => File.WriteAllBytes(filePath, imageData)).ConfigureAwait(false);
                }

                _pendingTextures.Enqueue(new PendingTexture(imageData, asyncTexture, fileName));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load remote texture {CacheFileName}", GetCacheFileName(url));
            }
        }

        public void ProcessPendingTextures()
        {
            var processedCount = 0;

            while (processedCount < MaxBatchSize && _pendingTextures.TryDequeue(out var pending))
            {
                try
                {
                    using (var stream = new MemoryStream(pending.ImageData))
                    {
                        var texture = TextureUtil.FromStreamPremultiplied(stream);
                        pending.AsyncTexture.SwapTexture(texture);
                        processedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to create texture {CacheFileName}", pending.CacheFileName);
                }
            }
        }

        private static string GetCacheFileName(string url)
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            var hash = url.GetHashCode().ToString("X8");
            return $"{hash}_{fileName}";
        }

        #endregion

        #region File Downloads

        public async Task DownloadFileAsync(string url, string targetPath)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                if (File.Exists(targetPath))
                {
                    return;
                }

                var data = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await Task.Run(() => File.WriteAllBytes(targetPath, data)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to download file from {Url}", url);
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _remoteTextureCache.Clear();
        }

        private class PendingTexture
        {
            public byte[] ImageData { get; }
            public AsyncTexture2D AsyncTexture { get; }
            public string CacheFileName { get; }

            public PendingTexture(byte[] imageData, AsyncTexture2D asyncTexture, string cacheFileName)
            {
                ImageData = imageData;
                AsyncTexture = asyncTexture;
                CacheFileName = cacheFileName;
            }
        }
    }
}
