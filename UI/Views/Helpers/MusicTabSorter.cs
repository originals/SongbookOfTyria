using System;
using System.Collections.Generic;
using System.Linq;

using SongbookOfTyria.Models;

namespace SongbookOfTyria.UI.Views.Helpers
{
    public enum SortMode
    {
        None,
        Name,
        ReleaseDate
    }

    public class MusicTabSorter
    {
        public SortMode Mode { get; set; } = SortMode.None;
        public bool Ascending { get; set; } = true;

        public List<MusicTab> Apply(List<MusicTab> tabs)
        {
            if (tabs == null || tabs.Count == 0 || Mode == SortMode.None)
            {
                return tabs ?? new List<MusicTab>();
            }

            switch (Mode)
            {
                case SortMode.Name:
                    return SortBy(tabs, t => t.Name);
                case SortMode.ReleaseDate:
                    return SortBy(tabs, t => t.ReleaseDate);
                default:
                    return tabs;
            }
        }

        private List<MusicTab> SortBy(List<MusicTab> tabs, Func<MusicTab, string> keySelector)
        {
            return Ascending
                ? tabs.OrderBy(t => keySelector(t) ?? string.Empty).ToList()
                : tabs.OrderByDescending(t => keySelector(t) ?? string.Empty).ToList();
        }
    }
}
