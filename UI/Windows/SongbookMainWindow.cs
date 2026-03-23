using System.IO;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;
using SongbookOfTyria.UI.Views;

namespace SongbookOfTyria.UI.Windows
{
    public class SongbookMainWindow : TabbedWindow2
    {
        private const int WindowWidth = 920;
        private const int WindowHeight = 660;
        private const int MinWindowHeight = 400;
        private const int MaxWindowHeight = 1200;
        private const int WindowBackgroundAssetId = 155985;
        private const string ModuleName = "Songbook of Tyria";
        private const string MainWindowId = "SongbookOfTyria_Main_Window";

        private readonly TabsCacheService _tabsCacheService;
        private readonly TextureService _textureService;
        private readonly UserSettingsService _userSettingsService;
        private readonly GuildAuthService _guildAuthService;
        private readonly string _cacheDirectory;
        private readonly AudioService _audioService;

        private Tab _aboutTab;
        private Tab _tabLibraryTab;
        private SongLibraryView _tabLibraryView;
        private TabDetailWindow _currentDetailWindow;

        public SongbookMainWindow(
            TabsCacheService tabsCacheService,
            TextureService textureService,
            UserSettingsService userSettingsService,
            GuildAuthService guildAuthService,
            string cacheDirectory)
            : base(
                AsyncTexture2D.FromAssetId(WindowBackgroundAssetId),
                new Rectangle(40, 26, 920, 660),
                new Rectangle(60, 50, 880, 600))
        {
            _tabsCacheService = tabsCacheService;
            _textureService = textureService;
            _userSettingsService = userSettingsService;
            _guildAuthService = guildAuthService;
            _cacheDirectory = cacheDirectory;
            Emblem = _textureService.GetEmblem();

            _audioService = new AudioService(_cacheDirectory);

            InitializeWindow();
            BuildTabs();
        }

        private void InitializeWindow()
        {
            Parent = GameService.Graphics.SpriteScreen;
            Title = ModuleName;
            Location = new Point(300, 300);
            SavesPosition = true;
            CanResize = true;
            Id = MainWindowId;
        }

        private void BuildTabs()
        {
            var aboutIcon = _textureService.GetAboutIcon();
            var tabLibraryIcon = _textureService.GetSongLibraryIcon();

            _tabLibraryView = new SongLibraryView(
                _tabsCacheService,
                _textureService,
                _userSettingsService,
                _guildAuthService);

            _tabLibraryView.TabClicked += OnTabClicked;

            _aboutTab = new Tab(aboutIcon, () => new AboutView(), "About");
            _tabLibraryTab = new Tab(tabLibraryIcon, () => _tabLibraryView, "Songbook");

            Tabs.Add(_tabLibraryTab);
            Tabs.Add(_aboutTab);


            TabChanged += OnTabChanged;
            UpdateSubtitle();
        }

        private void OnTabChanged(object sender, ValueChangedEventArgs<Tab> e)
        {
            UpdateSubtitle();
        }

        private void UpdateSubtitle()
        {
            if (SelectedTab == _aboutTab)
            {
                Subtitle = "How to play";
            }
            else if (SelectedTab == _tabLibraryTab)
            {
                Subtitle = "Tabs";
            }
        }

        public async Task RefreshTabListAsync()
        {
            await _tabLibraryView.RefreshTabListAsync();
        }

        private async void OnTabClicked(object sender, MusicTab musicTab)
        {
            try
            {
                _currentDetailWindow?.Dispose();

                var fullMusicTab = await _tabsCacheService.GetTabDetailsAsync(musicTab);
                var tabToDisplay = fullMusicTab ?? musicTab;

                _currentDetailWindow = new TabDetailWindow(
                    tabToDisplay,
                    _textureService,
                    _audioService,
                    _userSettingsService);

                _currentDetailWindow.Show();
            }
            finally
            {
                _tabLibraryView?.SetTabOpeningComplete();
            }
        }

        protected override Point HandleWindowResize(Point newSize)
        {
            return new Point(
                WindowWidth,
                MathHelper.Clamp(newSize.Y, MinWindowHeight, MaxWindowHeight));
        }

        protected override void DisposeControl()
        {
            _currentDetailWindow?.Dispose();
            _audioService?.Dispose();

            TabChanged -= OnTabChanged;

            if (_tabLibraryView != null)
            {
                _tabLibraryView.TabClicked -= OnTabClicked;
            }

            base.DisposeControl();
        }
    }
}
