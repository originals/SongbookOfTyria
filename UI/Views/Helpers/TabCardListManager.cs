using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Blish_HUD.Controls;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;
using SongbookOfTyria.UI.Controls.Containers;

namespace SongbookOfTyria.UI.Views.Helpers
{
    public class TabCardListManager : IDisposable
    {
        private readonly TextureService _textureService;
        private readonly UserSettingsService _userSettingsService;
        private readonly Dictionary<string, TabListCard> _tabCards = new Dictionary<string, TabListCard>();

        private FlowPanel _cardsPanel;
        private CancellationTokenSource _renderCts;
        private bool _isRenderingCards;
        private bool _disposed;

        public event EventHandler<MusicTab> CardClicked;
        public event EventHandler<MusicTab> FavoriteToggled;
        public event EventHandler RenderingStarted;
        public event EventHandler RenderingCompleted;

        public bool IsRendering => _isRenderingCards;

        public TabCardListManager(TextureService textureService, UserSettingsService userSettingsService)
        {
            _textureService = textureService;
            _userSettingsService = userSettingsService;
        }

        public void SetCardsPanel(FlowPanel cardsPanel)
        {
            _cardsPanel = cardsPanel;
        }

        public void RefreshCards(List<MusicTab> displayedTabs)
        {
            if (_cardsPanel == null)
            {
                return;
            }

            CancelCurrentRender();
            _renderCts = new CancellationTokenSource();

            if (displayedTabs == null || displayedTabs.Count == 0)
            {
                _cardsPanel.ClearChildren();
                RenderingCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            _ = RefreshCardsInternalAsync(displayedTabs, _renderCts.Token);
        }

        private void CancelCurrentRender()
        {
            var oldCts = _renderCts;
            if (oldCts != null)
            {
                try
                {
                    oldCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    oldCts.Dispose();
                }
            }
        }

        private async Task RefreshCardsInternalAsync(List<MusicTab> displayedTabs, CancellationToken cancellationToken)
        {
            _isRenderingCards = true;
            bool spinnerShown = false;

            try
            {
                var tabsToDisplay = displayedTabs.ToList();
                var tabsNeedingCards = tabsToDisplay
                    .Where(t => !_tabCards.ContainsKey(GetCardKey(t)))
                    .ToList();

                if (tabsNeedingCards.Count > 20)
                {
                    spinnerShown = true;
                    RenderingStarted?.Invoke(this, EventArgs.Empty);
                }

                await CreateCardsInBatchesAsync(tabsNeedingCards, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ReorderCards(tabsToDisplay);
                await WaitForCardsLayoutAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isRenderingCards = false;
                if (spinnerShown || !cancellationToken.IsCancellationRequested)
                {
                    RenderingCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private async Task CreateCardsInBatchesAsync(List<MusicTab> tabsNeedingCards, CancellationToken cancellationToken)
        {
            const int batchSize = 10;

            for (int i = 0; i < tabsNeedingCards.Count; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var batch = tabsNeedingCards.Skip(i).Take(batchSize);
                foreach (var tab in batch)
                {
                    var key = GetCardKey(tab);
                    var card = new TabListCard(tab, _textureService, _userSettingsService, null);
                    card.CardClicked += OnCardClicked;
                    card.FavoriteToggled += OnFavoriteToggled;
                    _tabCards[key] = card;
                }

                if (tabsNeedingCards.Count > batchSize)
                {
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private void ReorderCards(List<MusicTab> tabsToDisplay)
        {
            using (_cardsPanel.SuspendLayoutContext())
            {
                _cardsPanel.ClearChildren();
                foreach (var tab in tabsToDisplay)
                {
                    var key = GetCardKey(tab);
                    if (_tabCards.TryGetValue(key, out var card))
                    {
                        card.InvalidateStripeIndex();
                        card.Parent = _cardsPanel;
                    }
                }
            }
        }

        private async Task WaitForCardsLayoutAsync(CancellationToken cancellationToken)
        {
            const int maxWaitMs = 3000;
            const int checkIntervalMs = 16;
            int elapsedMs = 0;

            await Task.Delay(checkIntervalMs, cancellationToken);
            elapsedMs += checkIntervalMs;

            while (elapsedMs < maxWaitMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var children = _cardsPanel?.Children?.ToArray();
                if (children != null && children.Length > 0)
                {
                    var visibleChildren = children.Where(c => c.Visible).ToArray();
                    if (visibleChildren.Length > 0)
                    {
                        var firstChild = visibleChildren.First();
                        var lastChild = visibleChildren.Last();

                        if (visibleChildren.Length == 1)
                        {
                            if (firstChild.Height > 0)
                            {
                                return;
                            }
                        }
                        else
                        {
                            if (firstChild.Height > 0 && lastChild.Height > 0 && lastChild.Top > firstChild.Top)
                            {
                                return;
                            }
                        }
                    }
                }

                await Task.Delay(checkIntervalMs, cancellationToken);
                elapsedMs += checkIntervalMs;
            }
        }

        private static string GetCardKey(MusicTab tab) => $"tab:{tab.Id}";

        private void OnCardClicked(object sender, MusicTab tab)
        {
            CardClicked?.Invoke(this, tab);
        }

        private void OnFavoriteToggled(object sender, MusicTab tab)
        {
            FavoriteToggled?.Invoke(this, tab);
        }

        public void ClearAllCards()
        {
            foreach (var card in _tabCards.Values)
            {
                card.CardClicked -= OnCardClicked;
                card.FavoriteToggled -= OnFavoriteToggled;
                card.Dispose();
            }
            _tabCards.Clear();
            _cardsPanel?.ClearChildren();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            CancelCurrentRender();
            _renderCts = null;

            ClearAllCards();
        }
    }
}
