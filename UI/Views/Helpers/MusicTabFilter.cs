using System;
using System.Collections.Generic;
using System.Linq;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;

namespace SongbookOfTyria.UI.Views.Helpers
{
    public class MusicTabFilter
    {
        public string SearchText { get; set; } = string.Empty;

        public bool SoloOnly { get; set; }
        public bool DuetOnly { get; set; }
        public bool BandOnly { get; set; }

        public bool BeginnerOnly { get; set; }

        public bool PracticeModeOnly { get; set; }
        public bool PianoOnly { get; set; }

        public bool FavoritesOnly { get; set; }
        public UserSettingsService UserSettingsService { get; set; }

        public HashSet<string> SelectedGenres { get; set; } = new HashSet<string>();

        public HashSet<string> SelectedTabbers { get; set; } = new HashSet<string>();

        public bool PublicOnly { get; set; }
        public bool PrivateOnly { get; set; }

        public List<MusicTab> Apply(List<MusicTab> tabs)
        {
            if (tabs == null)
            {
                return new List<MusicTab>();
            }

            return tabs.Where(MatchesAllCriteria).ToList();
        }

        private bool MatchesAllCriteria(MusicTab tab)
        {
            return MatchesSearch(tab) &&
                   MatchesTabTypeFilter(tab) &&
                   MatchesDifficultyFilter(tab) &&
                   MatchesFeaturesFilter(tab) &&
                   MatchesFavoritesFilter(tab) &&
                   MatchesGenreFilter(tab) &&
                   MatchesTabberFilter(tab) &&
                   MatchesVisibilityFilter(tab);
        }

        private bool MatchesSearch(MusicTab tab)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return ContainsIgnoreCase(tab.Name, SearchText) ||
                   ContainsIgnoreCase(tab.Genre, SearchText) ||
                   ContainsIgnoreCase(tab.TabbedBy, SearchText);
        }

        private bool ContainsIgnoreCase(string source, string searchText)
        {
            return source?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesTabTypeFilter(MusicTab tab)
        {
            var hasAnyTypeFilter = SoloOnly || DuetOnly || BandOnly;
            if (!hasAnyTypeFilter)
            {
                return true;
            }

            if (tab.TabType == null || tab.TabType.Count == 0)
            {
                return false;
            }

            return (SoloOnly && tab.TabType.Contains("Solo")) ||
                   (DuetOnly && tab.TabType.Contains("Duet")) ||
                   (BandOnly && tab.TabType.Contains("Band"));
        }

        private bool MatchesDifficultyFilter(MusicTab tab)
        {
            if (!BeginnerOnly)
            {
                return true;
            }

            return tab.IsBeginner;
        }

        private bool MatchesFeaturesFilter(MusicTab tab)
        {
            if (PracticeModeOnly && !tab.PracticeMode)
            {
                return false;
            }

            if (PianoOnly && !tab.Piano)
            {
                return false;
            }

            return true;
        }

        private bool MatchesFavoritesFilter(MusicTab tab)
        {
            if (!FavoritesOnly || UserSettingsService == null)
            {
                return true;
            }

            return UserSettingsService.IsFavorite(tab.Id);
        }

        private bool MatchesGenreFilter(MusicTab tab)
        {
            if (SelectedGenres == null || SelectedGenres.Count == 0)
            {
                return true;
            }

            return !string.IsNullOrEmpty(tab.Genre) && SelectedGenres.Contains(tab.Genre);
        }

        private bool MatchesTabberFilter(MusicTab tab)
        {
            if (SelectedTabbers == null || SelectedTabbers.Count == 0)
            {
                return true;
            }

            if (tab.TabbedByMember != null && tab.TabbedByMember.Count > 0)
            {
                return tab.TabbedByMember.Any(m => SelectedTabbers.Contains(m));
            }

            if (!string.IsNullOrEmpty(tab.TabbedBy))
            {
                var tabbers = tab.TabbedBy.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tabber in tabbers)
                {
                    var trimmedTabber = tabber.Trim();
                    if (SelectedTabbers.Contains(trimmedTabber))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool MatchesVisibilityFilter(MusicTab tab)
        {
            var hasAnyVisibilityFilter = PublicOnly || PrivateOnly;
            if (!hasAnyVisibilityFilter)
            {
                return true;
            }

            return (PublicOnly && !tab.IsPrivate) ||
                   (PrivateOnly && tab.IsPrivate);
        }
    }
}
