using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;
using SongbookOfTyria.Settings;
using SongbookOfTyria.UI.Views;

namespace SongbookOfTyria.UI.Windows
{
    public class SongbookMainWindow : TabbedWindow2
    {
        private const int WindowWidth = 920;
        private const int WindowHeight = 660;
        private const int MinWindowHeight = 400;
        private const int MaxWindowHeight = 1200;
        private const string ModuleName = "Songbook of Tyria";
        private const string MainWindowId = "SongbookOfTyria_Main_Window";

        private readonly TabsService _tabsService;
        private readonly TextureService _textureService;
        private readonly UserSettingsService _userSettingsService;
        private readonly GuildAuthService _guildAuthService;
        private readonly ModuleSettings _moduleSettings;
        private readonly string _cacheDirectory;
        private readonly AudioService _audioService;
        private readonly MidiPlaybackService _midiPlaybackService;

        private Tab _aboutTab;
        private Tab _tabLibraryTab;
        private SongLibraryView _tabLibraryView;
        private TabDetailWindow _currentDetailWindow;
        private int _pendingTabIndex = -1;

        public SongbookMainWindow(
            TabsService tabsService,
            TextureService textureService,
            UserSettingsService userSettingsService,
            GuildAuthService guildAuthService,
            ModuleSettings moduleSettings,
            string cacheDirectory)
            : base(
                AsyncTexture2D.FromAssetId(TextureService.WindowBackgroundAssetId),
                new Rectangle(40, 26, 920, 660),
                new Rectangle(60, 50, 880, 600))
        {
            _tabsService = tabsService;
            _textureService = textureService;
            _userSettingsService = userSettingsService;
            _guildAuthService = guildAuthService;
            _moduleSettings = moduleSettings;
            _cacheDirectory = cacheDirectory;
            Emblem = _textureService.GetEmblem();

            _audioService = new AudioService(_cacheDirectory);
            _midiPlaybackService = new MidiPlaybackService(_cacheDirectory);

            InitializeWindow();
            BuildTabs();

             _ = LoadSoundFontAsync();
        }

        private async Task LoadSoundFontAsync()
        {
            const string soundFontUrl = "https://www.gw2opus.com/wp-content/uploads/practise-mode/assets/audio/GuildWars2.sf2";
            await _midiPlaybackService.LoadSoundFontAsync(soundFontUrl);
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
                _tabsService,
                _textureService,
                _userSettingsService,
                _guildAuthService);

            _tabLibraryView.TabClicked += OnTabClicked;

            _aboutTab = new Tab(aboutIcon, () => new AboutView(), "About");
            _tabLibraryTab = new Tab(tabLibraryIcon, () => _tabLibraryView, "Songbook");

            Tabs.Add(_aboutTab);
            Tabs.Add(_tabLibraryTab);

            TabChanged += OnTabChanged;

            var savedIndex = _userSettingsService.GetSelectedMainWindowTabIndex();
            if (savedIndex > 0 && savedIndex < Tabs.Count)
            {
                _pendingTabIndex = savedIndex;
            }

            UpdateSubtitle();
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (_pendingTabIndex >= 0 && _pendingTabIndex < Tabs.Count)
            {
                SelectedTab = Tabs.ElementAt(_pendingTabIndex);
                _pendingTabIndex = -1;
            }
        }

        private void OnTabChanged(object sender, ValueChangedEventArgs<Tab> e)
        {
            UpdateSubtitle();
            _userSettingsService.SaveSelectedMainWindowTabIndex(Tabs.IndexOf(SelectedTab));
        }

        private void UpdateSubtitle()
        {
            if (SelectedTab == _aboutTab)
            {
                Subtitle = "";
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

                var fullMusicTab = await _tabsService.GetTabDetailsAsync(musicTab);
                var tabToDisplay = fullMusicTab ?? musicTab;

                _currentDetailWindow = new TabDetailWindow(
                    tabToDisplay,
                    _textureService,
                    _audioService,
                    _userSettingsService,
                    _moduleSettings,
                    _midiPlaybackService,
                    _cacheDirectory);

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
            _midiPlaybackService?.Dispose();
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
