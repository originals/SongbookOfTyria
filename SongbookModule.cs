using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models.Api;
using SongbookOfTyria.Services;
using SongbookOfTyria.Settings;
using SongbookOfTyria.UI.Controls.Notation;
using SongbookOfTyria.UI.Utilities;
using SongbookOfTyria.UI.Views;
using SongbookOfTyria.UI.Windows;

namespace SongbookOfTyria
{
    [Export(typeof(Module))]
    public class SongbookModule : Module
    {
        private static readonly Logger Logger = Logger.GetLogger<SongbookModule>();

        private const string CacheDirectoryName = "songbook_cache";
        private const string TexturesCacheFolder = "textures";
        private const int CornerIconPriority = 1645843524;

        internal static SongbookModule ModuleInstance { get; private set; }

        internal SettingsManager SettingsManager => ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => ModuleParameters.Gw2ApiManager;

        private ApiService _apiService;
        private TextureService _textureService;
        private TabsService _tabsService;
        private GuildAuthService _guildAuthService;
        private UserSettingsService _userSettingsService;
        private ModuleSettings _moduleSettings;
        private CornerIcon _cornerIcon;
        private SongbookMainWindow _mainWindow;

        [ImportingConstructor]
        public SongbookModule([Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters)
        {
            ModuleInstance = this;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _moduleSettings = new ModuleSettings(settings);
        }

        protected override async Task LoadAsync()
        {
            var cacheDirectory = EnsureCacheDirectoryExists();
            Logger.Info("Cache directory: {Directory}", cacheDirectory);

            NotationRenderer.InitializeFonts(ContentsManager);

            var texturesCacheDirectory = Path.Combine(cacheDirectory, TexturesCacheFolder);

            _textureService = new TextureService(ContentsManager, texturesCacheDirectory);
            _guildAuthService = new GuildAuthService();
            _apiService = new ApiService(_guildAuthService);
            _tabsService = new TabsService(_apiService);
            _userSettingsService = new UserSettingsService(cacheDirectory);

            _tabsService.TabsLoaded += OnTabsLoaded;

            _moduleSettings.InitializeServices(_tabsService, _textureService, _guildAuthService);

            _guildAuthService.AuthStatusChanged += OnAuthStatusChanged;

            _mainWindow = new SongbookMainWindow(
                _tabsService,
                _textureService,
                _userSettingsService,
                _guildAuthService,
                _moduleSettings,
                cacheDirectory);

            CreateCornerIcon();

            _ = _moduleSettings.InitializeGuildAuthAsync();
        }

        private string EnsureCacheDirectoryExists()
        {
            var cacheDirectory = Path.Combine(DirectoryUtil.CachePath, CacheDirectoryName);
            Directory.CreateDirectory(cacheDirectory);
            return cacheDirectory;
        }

        private void CreateCornerIcon()
        {
            var cornerTexture = _textureService.GetCornerIcon();
            _cornerIcon = new CornerIcon
            {
                Icon = cornerTexture,
                HoverIcon = cornerTexture,
                BasicTooltipText = "Songbook of Tyria",
                Priority = CornerIconPriority,
                Parent = GameService.Graphics.SpriteScreen
            };

            _cornerIcon.Click += (s, e) => _mainWindow?.ToggleWindow();
        }

        private void OnTabsLoaded(object sender, TabsResponse response)
        {
            if (response?.Tabs != null)
            {
                _textureService.PreloadThumbnails(response.Tabs);
            }
        }

        private async void OnAuthStatusChanged(object sender, GuildAuthStatusChangedEventArgs e)
        {
            if (_tabsService != null)
            {
                await _tabsService.RefreshTabsAsync().ConfigureAwait(false);
            }
            if (_mainWindow != null)
            {
                await _mainWindow.RefreshTabListAsync().ConfigureAwait(false);
            }
        }

        public override IView GetSettingsView()
        {
            return new ModuleSettingsView(_moduleSettings);
        }

        protected override void Update(GameTime gameTime)
        {
            _textureService?.ProcessPendingTextures();
        }

        protected override void Unload()
        {
            SafeUnsubscribe(() => _tabsService.TabsLoaded -= OnTabsLoaded);
            SafeUnsubscribe(() => _guildAuthService.AuthStatusChanged -= OnAuthStatusChanged);

            SafeDispose(_moduleSettings);
            SafeDispose(_guildAuthService);
            SafeDispose(_cornerIcon);
            SafeDispose(_mainWindow);
            SafeDispose(_apiService);
            SafeDispose(_textureService);

            BitmapFontLoader.ClearCache();

            ModuleInstance = null;
        }

        private static void SafeUnsubscribe(Action unsubscribe)
        {
            try { unsubscribe(); }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error during event unsubscription");
            }
        }

        private static void SafeDispose(IDisposable disposable)
        {
            try { disposable?.Dispose(); }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error during disposal");
            }
        }
    }
}
