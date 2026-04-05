using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Models.Api;
using SongbookOfTyria.Services;
using SongbookOfTyria.UI.Views.Helpers;

namespace SongbookOfTyria.UI.Views
{
    public class SongLibraryView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<SongLibraryView>();

        private const int MenuPanelWidth = 240;
        private const int CardVerticalSpacing = 4;
        private const int VerticalPadding = 10;
        private const int LeftPadding = 50;

        private readonly TabsService _tabsService;
        private readonly UserSettingsService _userSettingsService;
        private readonly GuildAuthService _guildAuthService;
        private readonly TextureService _textureService;
        private readonly MusicTabFilter _filter;
        private readonly MusicTabSorter _sorter;
        private TabCardListManager _cardManager;

        private FlowPanel _filterPanel;
        private Panel _contentContainer;
        private Panel _headerSection;
        private FlowPanel _cardsPanel;
        private TextBox _searchBox;
        private LoadingSpinner _loadingSpinner;
        private Label _errorLabel;
        private Container _parentContainer;

        private Checkbox _soloCheckbox;
        private Checkbox _duetCheckbox;
        private Checkbox _bandCheckbox;
        private Checkbox _beginnerCheckbox;
        private Checkbox _pianoCheckbox;
        private Checkbox _practiceModeCheckbox;
        private Checkbox _favoritesCheckbox;
        private StandardButton _resetButton;

        private FlowPanel _visibilityPanel;
        private Checkbox _publicCheckbox;
        private Checkbox _privateCheckbox;

        private FlowPanel _genrePanel;
        private readonly Dictionary<string, Checkbox> _genreCheckboxes = new Dictionary<string, Checkbox>();

        private FlowPanel _tabTypePanel;
        private FlowPanel _featuresPanel;

        private FlowPanel _tabberPanel;
        private readonly Dictionary<string, Checkbox> _tabberCheckboxes = new Dictionary<string, Checkbox>();

        private StandardButton _sortByNameButton;
        private StandardButton _sortByDateButton;

        private List<MusicTab> _allTabs;
        private List<MusicTab> _displayedTabs;
        private bool _isLoading;
        private bool _hasLoaded;
        private bool _isOpeningTab;
        private string _errorMessage;

        public event EventHandler<MusicTab> TabClicked;

        private readonly List<Checkbox> _filterCheckboxes = new List<Checkbox>();

        public SongLibraryView(
            TabsService tabsService,
            TextureService textureService,
            UserSettingsService userSettingsService,
            GuildAuthService guildAuthService)
        {
            _tabsService = tabsService;
            _textureService = textureService;
            _userSettingsService = userSettingsService;
            _guildAuthService = guildAuthService;
            _filter = new MusicTabFilter { UserSettingsService = userSettingsService };
            _sorter = new MusicTabSorter();

            var savedState = _userSettingsService.GetFilterState();
            RestoreFilterState(savedState);

            if (_guildAuthService != null)
            {
                _guildAuthService.AuthStatusChanged += OnAuthStatusChanged;
            }
        }

        private void InitializeCardManager()
        {
            if (_cardManager != null)
            {
                _cardManager.CardClicked -= OnCardClicked;
                _cardManager.FavoriteToggled -= OnFavoriteToggled;
                _cardManager.RenderingStarted -= OnRenderingStarted;
                _cardManager.RenderingCompleted -= OnRenderingCompleted;
                _cardManager.Dispose();
            }

            _cardManager = new TabCardListManager(_textureService, _userSettingsService);
            _cardManager.CardClicked += OnCardClicked;
            _cardManager.FavoriteToggled += OnFavoriteToggled;
            _cardManager.RenderingStarted += OnRenderingStarted;
            _cardManager.RenderingCompleted += OnRenderingCompleted;
        }

        protected override void Build(Container buildPanel)
        {
            InitializeCardManager();

            _parentContainer = buildPanel;
            _parentContainer.Resized += OnParentResized;

            BuildFilterPanel(buildPanel);
            BuildContentPanel(buildPanel);
            BuildLoadingControls(buildPanel);

            RestoreOrLoadTabs();
        }

        private void OnParentResized(object sender, ResizedEventArgs e)
        {
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (_parentContainer == null)
            {
                return;
            }

            int availableHeight = _parentContainer.ContentRegion.Height - VerticalPadding * 2;
            int contentWidth = _parentContainer.ContentRegion.Width - MenuPanelWidth - LeftPadding - 30;

            if (_filterPanel != null)
            {
                _filterPanel.Height = availableHeight;
            }

            if (_contentContainer != null)
            {
                _contentContainer.Size = new Point(contentWidth, availableHeight);
            }

            if (_cardsPanel != null)
            {
                _cardsPanel.Width = _contentContainer?.ContentRegion.Width ?? contentWidth;
                UpdateCardsPanelLayout();
            }

            UpdateSpinnerPosition();
        }

        private void BuildFilterPanel(Container parent)
        {
            _filterPanel = new FlowPanel
            {
                ShowBorder = true,
                Size = new Point(MenuPanelWidth, parent.ContentRegion.Height - VerticalPadding * 2),
                Location = new Point(LeftPadding, VerticalPadding),
                Parent = parent,
                CanScroll = true,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 5),
                OuterControlPadding = new Vector2(-2, 7)
            };

            var headerPanel = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = 30,
                Parent = _filterPanel
            };

            var filtersHeader = new Label
            {
                Text = "Filters",
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(20, 2),
                Parent = headerPanel
            };

            _resetButton = new StandardButton
            {
                Text = "Reset",
                Width = 60,
                Height = 26,
                Location = new Point(MenuPanelWidth - 85, 0),
                Parent = headerPanel,
                Enabled = false
            };
            _resetButton.Click += OnResetFiltersClicked;

            BuildTabTypeSection();
            BuildFeaturesSection();
            BuildVisibilitySection();
            BuildGenreSection();
            BuildTabberSection();

            RestoreCheckboxStates();
        }

        private void RestoreFilterState(FilterState savedState)
        {
            _filter.SearchText = savedState.SearchText ?? string.Empty;
            _filter.BeginnerOnly = savedState.BeginnerOnly;
            _filter.SoloOnly = savedState.SoloOnly;
            _filter.DuetOnly = savedState.DuetOnly;
            _filter.BandOnly = savedState.BandOnly;
            _filter.PianoOnly = savedState.PianoOnly;
            _filter.PracticeModeOnly = savedState.PracticeModeOnly;
            _filter.FavoritesOnly = savedState.FavoritesOnly;

            bool isOpusMember = _guildAuthService != null && _guildAuthService.IsOpusMember;
            _filter.PublicOnly = isOpusMember && savedState.PublicOnly;
            _filter.PrivateOnly = isOpusMember && savedState.PrivateOnly;

            _filter.SelectedGenres.Clear();
            if (savedState.SelectedGenres != null)
            {
                foreach (var genre in savedState.SelectedGenres)
                {
                    _filter.SelectedGenres.Add(genre);
                }
            }

            _filter.SelectedTabbers.Clear();
            if (savedState.SelectedTabbers != null)
            {
                foreach (var tabber in savedState.SelectedTabbers)
                {
                    _filter.SelectedTabbers.Add(tabber);
                }
            }

            _sorter.Mode = savedState.SortMode;
            _sorter.Ascending = savedState.SortAscending;
        }

        private void RestoreCheckboxStates()
        {
            var savedState = _userSettingsService.GetFilterState();

            SetCheckboxState(_beginnerCheckbox, savedState.BeginnerOnly);
            SetCheckboxState(_soloCheckbox, savedState.SoloOnly);
            SetCheckboxState(_duetCheckbox, savedState.DuetOnly);
            SetCheckboxState(_bandCheckbox, savedState.BandOnly);
            SetCheckboxState(_pianoCheckbox, savedState.PianoOnly);
            SetCheckboxState(_practiceModeCheckbox, savedState.PracticeModeOnly);
            SetCheckboxState(_favoritesCheckbox, savedState.FavoritesOnly);
            SetCheckboxState(_publicCheckbox, savedState.PublicOnly);
            SetCheckboxState(_privateCheckbox, savedState.PrivateOnly);

            RestorePanelCollapsedStates(savedState.CollapsedPanels);

            UpdateSortButtonStates();
        }

        private static void SetCheckboxState(Checkbox checkbox, bool isChecked)
        {
            if (checkbox != null) checkbox.Checked = isChecked;
        }

        private void RestorePanelCollapsedStates(Dictionary<string, bool> collapsedPanels)
        {
            if (collapsedPanels == null || collapsedPanels.Count == 0)
            {
                return;
            }

            if (_tabTypePanel != null && collapsedPanels.TryGetValue("Tab Type", out var tabTypeCollapsed))
            {
                _tabTypePanel.Collapsed = tabTypeCollapsed;
            }

            if (_featuresPanel != null && collapsedPanels.TryGetValue("Features", out var featuresCollapsed))
            {
                _featuresPanel.Collapsed = featuresCollapsed;
            }

            if (_visibilityPanel != null && collapsedPanels.TryGetValue("Visibility", out var visibilityCollapsed))
            {
                _visibilityPanel.Collapsed = visibilityCollapsed;
            }

            if (_genrePanel != null && collapsedPanels.TryGetValue("Genre", out var genreCollapsed))
            {
                _genrePanel.Collapsed = genreCollapsed;
            }

            if (_tabberPanel != null && collapsedPanels.TryGetValue("Tabbed By", out var tabberCollapsed))
            {
                _tabberPanel.Collapsed = tabberCollapsed;
            }
        }

        private void SavePanelCollapsedStates()
        {
            var savedState = _userSettingsService.GetFilterState();

            if (_tabTypePanel != null)
            {
                savedState.CollapsedPanels["Tab Type"] = _tabTypePanel.Collapsed;
            }

            if (_featuresPanel != null)
            {
                savedState.CollapsedPanels["Features"] = _featuresPanel.Collapsed;
            }

            if (_visibilityPanel != null)
            {
                savedState.CollapsedPanels["Visibility"] = _visibilityPanel.Collapsed;
            }

            if (_genrePanel != null)
            {
                savedState.CollapsedPanels["Genre"] = _genrePanel.Collapsed;
            }

            if (_tabberPanel != null)
            {
                savedState.CollapsedPanels["Tabbed By"] = _tabberPanel.Collapsed;
            }

            _userSettingsService.SaveFilterState(savedState);
        }

        private void RestoreGenreAndTabberCheckboxes()
        {
            var savedState = _userSettingsService.GetFilterState();

            foreach (var kvp in _genreCheckboxes)
            {
                kvp.Value.Checked = savedState.SelectedGenres?.Contains(kvp.Key) ?? false;
            }

            foreach (var kvp in _tabberCheckboxes)
            {
                kvp.Value.Checked = savedState.SelectedTabbers?.Contains(kvp.Key) ?? false;
            }
        }

        private void SaveFilterState()
        {
            var filterState = new FilterState
            {
                SearchText = _searchBox?.Text ?? string.Empty,
                BeginnerOnly = _beginnerCheckbox?.Checked ?? false,
                SoloOnly = _soloCheckbox?.Checked ?? false,
                DuetOnly = _duetCheckbox?.Checked ?? false,
                BandOnly = _bandCheckbox?.Checked ?? false,
                PianoOnly = _pianoCheckbox?.Checked ?? false,
                PracticeModeOnly = _practiceModeCheckbox?.Checked ?? false,
                FavoritesOnly = _favoritesCheckbox?.Checked ?? false,
                PublicOnly = _publicCheckbox?.Checked ?? false,
                PrivateOnly = _privateCheckbox?.Checked ?? false,
                SelectedGenres = new List<string>(_filter.SelectedGenres),
                SelectedTabbers = new List<string>(_filter.SelectedTabbers),
                SortMode = _sorter.Mode,
                SortAscending = _sorter.Ascending
            };

            _userSettingsService.SaveFilterState(filterState);
        }

        private void OnResetFiltersClicked(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            ResetCheckbox(_beginnerCheckbox);
            ResetCheckbox(_soloCheckbox);
            ResetCheckbox(_duetCheckbox);
            ResetCheckbox(_bandCheckbox);
            ResetCheckbox(_pianoCheckbox);
            ResetCheckbox(_practiceModeCheckbox);
            ResetCheckbox(_favoritesCheckbox);
            ResetCheckbox(_publicCheckbox);
            ResetCheckbox(_privateCheckbox);

            foreach (var checkbox in _genreCheckboxes.Values)
            {
                checkbox.Checked = false;
            }
            _filter.SelectedGenres.Clear();

            foreach (var checkbox in _tabberCheckboxes.Values)
            {
                checkbox.Checked = false;
            }
            _filter.SelectedTabbers.Clear();

            if (_searchBox != null)
            {
                _searchBox.Text = string.Empty;
            }

            ApplyFiltersAndSort();
        }

        private static void ResetCheckbox(Checkbox checkbox)
        {
            if (checkbox != null) checkbox.Checked = false;
        }

        private void BuildTabTypeSection()
        {
            _tabTypePanel = CreateFilterSection("Tab Type");
            _soloCheckbox = CreateFilterCheckbox("Solo", _tabTypePanel);
            _duetCheckbox = CreateFilterCheckbox("Duet", _tabTypePanel);
            _bandCheckbox = CreateFilterCheckbox("Band", _tabTypePanel);
            AddBottomSpacer(_tabTypePanel);
        }

        private void BuildFeaturesSection()
        {
            _featuresPanel = CreateFilterSection("Features");
            _beginnerCheckbox = CreateFilterCheckbox("Beginner Friendly", _featuresPanel);
            _favoritesCheckbox = CreateFilterCheckbox("Favorites", _featuresPanel);
            _pianoCheckbox = CreateFilterCheckbox("Piano", _featuresPanel);
            _practiceModeCheckbox = CreateFilterCheckbox("Practice Mode", _featuresPanel);
            AddBottomSpacer(_featuresPanel);
        }

        private void BuildVisibilitySection()
        {
            if (_guildAuthService == null || !_guildAuthService.IsOpusMember)
            {
                return;
            }

            _visibilityPanel = CreateFilterSection("Visibility");
            _publicCheckbox = CreateFilterCheckbox("Public", _visibilityPanel);
            _privateCheckbox = CreateFilterCheckbox("Private", _visibilityPanel);
            AddBottomSpacer(_visibilityPanel);
        }

        private void OnAuthStatusChanged(object sender, GuildAuthStatusChangedEventArgs e)
        {
            if (e.IsOpusMember)
            {
                if (_visibilityPanel == null && _filterPanel != null)
                {
                    RebuildDynamicFilterSections();
                }
            }
            else
            {
                RemoveVisibilitySection();
            }
        }

        private void RebuildDynamicFilterSections()
        {
            RemoveGenreSection();
            RemoveTabberSection();

            BuildVisibilitySection();
            BuildGenreSection();
            BuildTabberSection();

            if (_allTabs != null)
            {
                PopulateGenreCheckboxes();
                PopulateTabberCheckboxes();
            }

            _filterPanel?.Invalidate();
        }

        private void RemoveGenreSection()
        {
            foreach (var checkbox in _genreCheckboxes.Values)
            {
                checkbox.CheckedChanged -= OnGenreCheckboxChanged;
            }
            _genreCheckboxes.Clear();

            _genrePanel?.Dispose();
            _genrePanel = null;
        }

        private void RemoveTabberSection()
        {
            foreach (var checkbox in _tabberCheckboxes.Values)
            {
                checkbox.CheckedChanged -= OnTabberCheckboxChanged;
            }
            _tabberCheckboxes.Clear();

            _tabberPanel?.Dispose();
            _tabberPanel = null;
        }

        private void RemoveVisibilitySection()
        {
            if (_visibilityPanel == null)
            {
                return;
            }

            if (_publicCheckbox != null)
            {
                _publicCheckbox.CheckedChanged -= OnFilterCheckboxChanged;
                _filterCheckboxes.Remove(_publicCheckbox);
                _publicCheckbox.Dispose();
                _publicCheckbox = null;
            }

            if (_privateCheckbox != null)
            {
                _privateCheckbox.CheckedChanged -= OnFilterCheckboxChanged;
                _filterCheckboxes.Remove(_privateCheckbox);
                _privateCheckbox.Dispose();
                _privateCheckbox = null;
            }

            _visibilityPanel.Dispose();
            _visibilityPanel = null;

            _filter.PublicOnly = false;
            _filter.PrivateOnly = false;

            if (_allTabs != null)
            {
                ApplyFiltersAndSort();
            }
        }

        private void BuildGenreSection()
        {
            _genrePanel = CreateFilterSection("Genre");
        }

        private void BuildTabberSection()
        {
            _tabberPanel = CreateFilterSection("Tabbed By");
        }

        private FlowPanel CreateFilterSection(string title)
        {
            return new FlowPanel
            {
                ShowBorder = true,
                Title = title,
                Width = MenuPanelWidth - 17,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = _filterPanel,
                CanCollapse = true,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 5),
                OuterControlPadding = new Vector2(10, 10)
            };
        }

        private Checkbox CreateFilterCheckbox(string text, Container parent)
        {
            var checkbox = new Checkbox
            {
                Text = text,
                Parent = parent
            };
            checkbox.CheckedChanged += OnFilterCheckboxChanged;
            _filterCheckboxes.Add(checkbox);
            return checkbox;
        }

        private void AddBottomSpacer(Container parent)
        {
            new Panel { Width = 1, Height = 5, Parent = parent };
        }

        private void PopulateGenreCheckboxes()
        {
            if (_allTabs == null || _genrePanel == null || _genreCheckboxes == null)
            {
                return;
            }

            foreach (var checkbox in _genreCheckboxes.Values)
            {
                checkbox.CheckedChanged -= OnGenreCheckboxChanged;
            }
            _genreCheckboxes.Clear();
            _genrePanel.ClearChildren();

            var genreCounts = _allTabs
                .Where(t => !string.IsNullOrEmpty(t.Genre))
                .GroupBy(t => t.Genre)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key);

            foreach (var genreCount in genreCounts)
            {
                if (string.IsNullOrEmpty(genreCount.Key)) continue;

                var checkbox = new Checkbox
                {
                    Text = $"{genreCount.Key} ({genreCount.Value})",
                    Parent = _genrePanel
                };
                checkbox.CheckedChanged += OnGenreCheckboxChanged;
                _genreCheckboxes[genreCount.Key] = checkbox;
            }

            new Panel { Width = 1, Height = 5, Parent = _genrePanel };

            RestoreGenreAndTabberCheckboxes();
        }

        private void OnGenreCheckboxChanged(object sender, CheckChangedEvent e)
        {
            _filter.SelectedGenres.Clear();
            foreach (var kvp in _genreCheckboxes)
            {
                if (kvp.Value.Checked)
                {
                    _filter.SelectedGenres.Add(kvp.Key);
                }
            }

            ApplyFiltersAndSort();
        }

        private void PopulateTabberCheckboxes()
        {
            if (_allTabs == null || _tabberPanel == null || _tabberCheckboxes == null)
            {
                return;
            }

            foreach (var checkbox in _tabberCheckboxes.Values)
            {
                checkbox.CheckedChanged -= OnTabberCheckboxChanged;
            }
            _tabberCheckboxes.Clear();
            _tabberPanel.ClearChildren();

            var tabberCounts = new Dictionary<string, int>();
            var excludedTabbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "abyss", "nes", "none" };

            foreach (var tab in _allTabs)
            {
                if (tab.TabbedByMember != null && tab.TabbedByMember.Count > 0)
                {
                    foreach (var member in tab.TabbedByMember)
                    {
                        if (!string.IsNullOrEmpty(member) && 
                            !excludedTabbers.Contains(member))
                        {
                            if (!tabberCounts.ContainsKey(member))
                            {
                                tabberCounts[member] = 0;
                            }
                            tabberCounts[member]++;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(tab.TabbedBy))
                {
                    var tabbers = tab.TabbedBy.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tabber in tabbers)
                    {
                        var trimmedTabber = tabber.Trim();
                        if (!string.IsNullOrEmpty(trimmedTabber) &&
                            !excludedTabbers.Contains(trimmedTabber))
                        {
                            if (!tabberCounts.ContainsKey(trimmedTabber))
                            {
                                tabberCounts[trimmedTabber] = 0;
                            }
                            tabberCounts[trimmedTabber]++;
                        }
                    }
                }
            }

            foreach (var tabberCount in tabberCounts.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key))
            {
                var checkbox = new Checkbox
                {
                    Text = $"{tabberCount.Key} ({tabberCount.Value})",
                    Parent = _tabberPanel
                };
                checkbox.CheckedChanged += OnTabberCheckboxChanged;
                _tabberCheckboxes[tabberCount.Key] = checkbox;
            }

            new Panel { Width = 1, Height = 5, Parent = _tabberPanel };
        }

        private void OnTabberCheckboxChanged(object sender, CheckChangedEvent e)
        {
            _filter.SelectedTabbers.Clear();
            foreach (var kvp in _tabberCheckboxes)
            {
                if (kvp.Value.Checked)
                {
                    _filter.SelectedTabbers.Add(kvp.Key);
                }
            }

            ApplyFiltersAndSort();
        }

        private void UpdateFilterCounts()
        {
            if (_allTabs == null) return;

            var currentFiltered = _displayedTabs ?? _allTabs;

            UpdateCheckboxText(_beginnerCheckbox, "Beginner Friendly", currentFiltered.Count(t => t.IsBeginner));
            UpdateCheckboxText(_soloCheckbox, "Solo", currentFiltered.Count(t => t.TabType != null && t.TabType.Contains("Solo")));
            UpdateCheckboxText(_duetCheckbox, "Duet", currentFiltered.Count(t => t.TabType != null && t.TabType.Contains("Duet")));
            UpdateCheckboxText(_bandCheckbox, "Band", currentFiltered.Count(t => t.TabType != null && t.TabType.Contains("Band")));
            UpdateCheckboxText(_pianoCheckbox, "Piano", currentFiltered.Count(t => t.Piano));
            UpdateCheckboxText(_practiceModeCheckbox, "Practice Mode", currentFiltered.Count(t => t.PracticeMode));
            UpdateCheckboxText(_favoritesCheckbox, "Favorites", _userSettingsService != null 
                ? currentFiltered.Count(t => _userSettingsService.IsFavorite(t.Id)) 
                : 0);
            UpdateCheckboxText(_publicCheckbox, "Public", currentFiltered.Count(t => !t.IsPrivate));
            UpdateCheckboxText(_privateCheckbox, "Private", currentFiltered.Count(t => t.IsPrivate));

            foreach (var kvp in _genreCheckboxes)
            {
                int genreCount = currentFiltered.Count(t => t.Genre == kvp.Key);
                kvp.Value.Text = $"{kvp.Key} ({genreCount})";
            }

            foreach (var kvp in _tabberCheckboxes)
            {
                int tabberCount = currentFiltered.Count(t => 
                    t.TabbedBy == kvp.Key || 
                    (t.TabbedByMember != null && t.TabbedByMember.Contains(kvp.Key)));
                kvp.Value.Text = $"{kvp.Key} ({tabberCount})";
            }
        }

        private static void UpdateCheckboxText(Checkbox checkbox, string label, int count)
        {
            if (checkbox != null) checkbox.Text = $"{label} ({count})";
        }

        private void OnFilterCheckboxChanged(object sender, CheckChangedEvent e)
        {
            ApplyFiltersAndSort();
        }

        private void BuildContentPanel(Container parent)
        {
            int contentLeft = LeftPadding + MenuPanelWidth + 6;
            int contentWidth = parent.ContentRegion.Width - MenuPanelWidth - LeftPadding - 30;

            _contentContainer = new Panel
            {
                Size = new Point(contentWidth, parent.ContentRegion.Height - VerticalPadding * 2),
                Location = new Point(contentLeft, VerticalPadding),
                ShowBorder = true,
                Parent = parent
            };

            BuildHeaderSection();
            BuildCardsPanel();
        }

        private void BuildHeaderSection()
        {
            _headerSection = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = 50,
                Parent = _contentContainer
            };

            _searchBox = new TextBox
            {
                Width = 250,
                Height = 30,
                Location = new Point(10, 10),
                PlaceholderText = "Search songs...",
                Text = _filter.SearchText ?? string.Empty,
                Parent = _headerSection
            };
            _searchBox.TextChanged += OnSearchTextChanged;

            var sortLabel = new Label
            {
                Text = "Sort:",
                Font = GameService.Content.DefaultFont14,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(280, 15),
                Parent = _headerSection
            };

            _sortByNameButton = new StandardButton
            {
                Text = "Name A-Z",
                Width = 100,
                Height = 30,
                Location = new Point(320, 10),
                Parent = _headerSection
            };
            _sortByNameButton.Click += OnSortByNameClicked;

            _sortByDateButton = new StandardButton
            {
                Text = "Date",
                Width = 110,
                Height = 30,
                Location = new Point(430, 10),
                Parent = _headerSection
            };
            _sortByDateButton.Click += OnSortByDateClicked;

            UpdateSortButtonStates();
        }

        private void BuildCardsPanel()
        {
            _cardsPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Size = new Point(_contentContainer.ContentRegion.Width, _contentContainer.ContentRegion.Height - _headerSection.Height),
                Location = new Point(0, _headerSection.Height),
                ControlPadding = new Vector2(0, CardVerticalSpacing),
                CanScroll = true,
                Parent = _contentContainer
            };
            _cardManager.SetCardsPanel(_cardsPanel);
        }

        private void UpdateCardsPanelLayout()
        {
            if (_cardsPanel == null || _contentContainer == null || _headerSection == null)
            {
                return;
            }

            _cardsPanel.Location = new Point(0, _headerSection.Height);
            _cardsPanel.Height = _contentContainer.ContentRegion.Height - _headerSection.Height;
        }

        private void BuildLoadingControls(Container parent)
        {
            _loadingSpinner = new LoadingSpinner
            {
                Parent = _contentContainer,
                Visible = false,
                ZIndex = 100
            };
            UpdateSpinnerPosition();

            _errorLabel = new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont18,
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = _contentContainer,
                Visible = false,
                ZIndex = 100
            };
        }

        private void UpdateSpinnerPosition()
        {
            if (_loadingSpinner == null || _contentContainer == null)
            {
                return;
            }

            int centerX = (_contentContainer.ContentRegion.Width - _loadingSpinner.Width) / 2;
            int centerY = (_contentContainer.ContentRegion.Height - _loadingSpinner.Height) / 2;

            _loadingSpinner.Location = new Point(centerX, centerY);

            if (_errorLabel != null)
            {
                _errorLabel.Location = new Point(centerX, centerY);
            }
        }

        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            ApplyFiltersAndSort();
        }

        private void OnSortByNameClicked(object sender, EventArgs e)
        {
            if (_sorter.Mode == SortMode.Name)
            {
                _sorter.Ascending = !_sorter.Ascending;
            }
            else
            {
                _sorter.Mode = SortMode.Name;
                _sorter.Ascending = true;
            }

            UpdateSortButtonStates();
            ApplyFiltersAndSort();
        }

        private void OnSortByDateClicked(object sender, EventArgs e)
        {
            if (_sorter.Mode == SortMode.ReleaseDate)
            {
                _sorter.Ascending = !_sorter.Ascending;
            }
            else
            {
                _sorter.Mode = SortMode.ReleaseDate;
                _sorter.Ascending = false;
            }

            UpdateSortButtonStates();
            ApplyFiltersAndSort();
        }

        private void UpdateSortButtonStates()
        {
            if (_sortByNameButton != null)
            {
                _sortByNameButton.Text = _sorter.Mode == SortMode.Name
                    ? (_sorter.Ascending ? "Name A-Z" : "Name Z-A")
                    : "Name";
            }

            if (_sortByDateButton != null)
            {
                _sortByDateButton.Text = _sorter.Mode == SortMode.ReleaseDate
                    ? (_sorter.Ascending ? "Date Old-New" : "Date New-Old")
                    : "Date";
            }
        }

        private void RestoreOrLoadTabs()
        {
            var cachedResponse = _tabsService.GetCachedTabs();
            if (cachedResponse?.Tabs != null && cachedResponse.Tabs.Count > 0)
            {
                _allTabs = cachedResponse.Tabs;
                _displayedTabs = new List<MusicTab>(_allTabs);
                _hasLoaded = true;
                PopulateGenreCheckboxes();
                PopulateTabberCheckboxes();
                ApplyFiltersAndSort();
                if (_errorLabel != null)
                {
                    _errorLabel.Visible = false;
                }
                return;
            }

            if (_hasLoaded && _allTabs != null)
            {
                RestoreCachedTabs();
            }
            else if (_hasLoaded && _errorMessage != null)
            {
                ShowError(_errorMessage);
            }
            else if (_isLoading)
            {
                ShowLoading();
            }
            else if (!_hasLoaded)
            {
                RefreshTabListWithErrorHandling();
            }
        }

        private void RestoreCachedTabs()
        {
            _displayedTabs = new List<MusicTab>(_allTabs);
            ApplyFiltersAndSort();
            SetControlVisible(_errorLabel, false);
        }

        private void ShowError(string message)
        {
            SetControlVisible(_loadingSpinner, false);

            if (_errorLabel != null)
            {
                _errorLabel.Text = message;
                _errorLabel.Visible = true;
            }

            SetControlVisible(_cardsPanel, false);
        }

        private void ShowLoading()
        {
            SetControlVisible(_loadingSpinner, true);
            SetControlVisible(_errorLabel, false);
        }

        private static void SetControlVisible(Control control, bool visible)
        {
            if (control != null) control.Visible = visible;
        }

        private void ApplyFiltersAndSort()
        {
            if (_allTabs == null || _cardsPanel == null)
            {
                return;
            }

            UpdateFilterFromCheckboxes();
            _filter.SearchText = _searchBox?.Text ?? string.Empty;

            _displayedTabs = _filter.Apply(_allTabs);
            _displayedTabs = _sorter.Apply(_displayedTabs);

            UpdateFilterCounts();
            UpdateResetButtonState();
            RefreshCardList();
            SaveFilterState();
        }

        private void UpdateResetButtonState()
        {
            if (_resetButton == null)
            {
                return;
            }

            bool hasActiveFilters = !string.IsNullOrEmpty(_searchBox?.Text) ||
                                    (_beginnerCheckbox?.Checked ?? false) ||
                                    (_soloCheckbox?.Checked ?? false) ||
                                    (_duetCheckbox?.Checked ?? false) ||
                                    (_bandCheckbox?.Checked ?? false) ||
                                    (_pianoCheckbox?.Checked ?? false) ||
                                    (_practiceModeCheckbox?.Checked ?? false) ||
                                    (_favoritesCheckbox?.Checked ?? false) ||
                                    (_publicCheckbox?.Checked ?? false) ||
                                    (_privateCheckbox?.Checked ?? false) ||
                                    _filter.SelectedGenres.Count > 0 ||
                                    _filter.SelectedTabbers.Count > 0;

            _resetButton.Enabled = hasActiveFilters;
        }

        private void UpdateFilterFromCheckboxes()
        {
            _filter.BeginnerOnly = _beginnerCheckbox?.Checked ?? false;
            _filter.SoloOnly = _soloCheckbox?.Checked ?? false;
            _filter.DuetOnly = _duetCheckbox?.Checked ?? false;
            _filter.BandOnly = _bandCheckbox?.Checked ?? false;
            _filter.PianoOnly = _pianoCheckbox?.Checked ?? false;
            _filter.PracticeModeOnly = _practiceModeCheckbox?.Checked ?? false;
            _filter.FavoritesOnly = _favoritesCheckbox?.Checked ?? false;
            _filter.PublicOnly = _publicCheckbox?.Checked ?? false;
            _filter.PrivateOnly = _privateCheckbox?.Checked ?? false;
        }

        private void RefreshCardList()
        {
            if (_cardsPanel == null)
            {
                return;
            }

            if (_displayedTabs == null || _displayedTabs.Count == 0)
            {
                _cardsPanel.ClearChildren();
                ShowEmptyMessage();
                HideLoadingSpinner();
                return;
            }

            _cardManager?.RefreshCards(_displayedTabs);
        }

        private void OnRenderingStarted(object sender, EventArgs e)
        {
            ShowLoadingSpinner();
        }

        private void OnRenderingCompleted(object sender, EventArgs e)
        {
            HideLoadingSpinner();
        }

        private void ShowLoadingSpinner()
        {
            SetControlVisible(_loadingSpinner, true);
            SetControlVisible(_cardsPanel, false);
        }

        private void HideLoadingSpinner()
        {
            SetControlVisible(_loadingSpinner, false);
            SetControlVisible(_cardsPanel, true);
        }

        private void OnFavoriteToggled(object sender, MusicTab tab)
        {
            UpdateFilterCounts();
        }

        private void OnCardClicked(object sender, MusicTab tab)
        {
            if (_isOpeningTab || TabClicked == null) return;

            _isOpeningTab = true;
            TabClicked.Invoke(this, tab);
        }

        public void SetTabOpeningComplete()
        {
            _isOpeningTab = false;
        }

        private void ShowEmptyMessage()
        {
            var emptyLabel = new Label
            {
                Text = "No songs found",
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.LightGray,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 10),
                Parent = _cardsPanel
            };
        }

        private async void RefreshTabListWithErrorHandling()
        {
            try
            {
                await RefreshTabListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh tab list");
                _errorMessage = "Failed to load tabs.";
                _isLoading = false;
                _hasLoaded = true;
                HandleLoadError();
            }
        }

        public async Task RefreshTabListAsync()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;
            _errorMessage = null;

            PrepareForLoading();

            var tabsResponse = await _tabsService.GetTabsAsync().ConfigureAwait(false);

            _isLoading = false;
            _hasLoaded = true;

            if (tabsResponse == null)
            {
                HandleLoadError();
                return;
            }

            ProcessTabsResponse(tabsResponse);

            ShowLoadedTabs();
        }

        private void PrepareForLoading()
        {
            if (_loadingSpinner != null)
            {
                _loadingSpinner.Visible = true;
            }

            if (_errorLabel != null)
            {
                _errorLabel.Visible = false;
            }

            _cardManager?.ClearAllCards();
        }

        private void HandleLoadError()
        {
            _errorMessage = "Failed to load tabs.";
            if (_loadingSpinner != null)
            {
                _loadingSpinner.Visible = false;
            }

            if (_errorLabel != null)
            {
                _errorLabel.Text = _errorMessage;
                _errorLabel.Visible = true;
            }
        }

        private void ProcessTabsResponse(TabsResponse response)
        {
            _allTabs = response.Tabs;
            _displayedTabs = new List<MusicTab>(_allTabs);

            UpdateFilterCounts();
            PopulateGenreCheckboxes();
            PopulateTabberCheckboxes();

            if (_allTabs != null && _cardsPanel != null)
            {
                ApplyFiltersAndSort();
            }
        }

        private void ShowLoadedTabs()
        {
            if (_errorLabel != null)
            {
                _errorLabel.Visible = false;
            }
        }

        protected override void Unload()
        {
            SavePanelCollapsedStates();

            if (_parentContainer != null)
                _parentContainer.Resized -= OnParentResized;

            if (_searchBox != null)
                _searchBox.TextChanged -= OnSearchTextChanged;

            if (_resetButton != null)
                _resetButton.Click -= OnResetFiltersClicked;

            if (_sortByNameButton != null)
                _sortByNameButton.Click -= OnSortByNameClicked;

            if (_sortByDateButton != null)
                _sortByDateButton.Click -= OnSortByDateClicked;

            foreach (var checkbox in _filterCheckboxes)
                checkbox.CheckedChanged -= OnFilterCheckboxChanged;
            _filterCheckboxes.Clear();

            foreach (var checkbox in _genreCheckboxes.Values)
                checkbox.CheckedChanged -= OnGenreCheckboxChanged;
            _genreCheckboxes.Clear();

            foreach (var checkbox in _tabberCheckboxes.Values)
                checkbox.CheckedChanged -= OnTabberCheckboxChanged;
            _tabberCheckboxes.Clear();

            _cardManager?.ClearAllCards();

            base.Unload();
        }
    }
}
