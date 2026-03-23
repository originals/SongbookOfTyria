using System;
using System.Net;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Controls.Effects;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoGame.Extended.BitmapFonts;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;

namespace SongbookOfTyria.UI.Controls.Containers
{
    public class TabListCard : Panel
    {
        public const int DefaultCardHeight = 60;
        private const int ThumbnailSize = 44;
        private const int ThumbnailLeft = 8;
        private const int ThumbnailTop = 8;
        private const int TextLeft = 60;
        private const int TitleTop = 10;
        private const int SubtitleTop = 32;
        private const int FavoriteIconSize = 24;
        private const int FavoriteIconRightMargin = 42;

        public event EventHandler<MusicTab> CardClicked;
        public event EventHandler<MusicTab> FavoriteToggled;

        private readonly MusicTab _musicTab;
        private readonly TextureService _textureService;
        private readonly UserSettingsService _userSettingsService;

        private string _decodedTitle;
        private string _decodedSubtitle;
        private AsyncTexture2D _thumbnailTexture;
        private AsyncTexture2D _favoriteTexture;
        private AsyncTexture2D _privateIconTexture;
        private AsyncTexture2D _practiceModeIconTexture;
        private ScrollingHighlightEffect _scrollEffect;

        private bool _isFavorite;
        private bool _isSelected;
        private int _cachedIndex = -1;
        private Rectangle _favoriteIconBounds;
        private Rectangle _practiceModeIconBounds;
        private Rectangle _privateIconBounds;
        private bool _texturesLoaded;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                UpdateSelectedState();
            }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            private set
            {
                if (_isFavorite == value)
                {
                    return;
                }

                _isFavorite = value;
                if (_texturesLoaded)
                {
                    UpdateFavoriteTexture();
                }
            }
        }

        public TabListCard(MusicTab musicTab, TextureService textureService, UserSettingsService userSettingsService, Container parent)
        {
            _musicTab = musicTab;
            _textureService = textureService;
            _userSettingsService = userSettingsService;
            WidthSizingMode = SizingMode.Fill;
            Height = DefaultCardHeight;
            Parent = parent;

            _isFavorite = _userSettingsService?.IsFavorite(_musicTab.Id) ?? false;

            _scrollEffect = new ScrollingHighlightEffect(this);
            EffectBehind = _scrollEffect;

            Click += OnCardClick;
        }

        private void EnsureTexturesLoaded()
        {
            if (_texturesLoaded)
            {
                return;
            }

            _texturesLoaded = true;

            _decodedTitle = WebUtility.HtmlDecode(_musicTab.Name ?? string.Empty);
            if (string.IsNullOrEmpty(_decodedTitle))
            {
                _decodedTitle = "Unknown Tab";
            }
            _decodedSubtitle = WebUtility.HtmlDecode(BuildSubtitleText()) ?? string.Empty;

            if (!string.IsNullOrEmpty(_musicTab.Thumbnail))
            {
                _thumbnailTexture = _textureService?.GetRemoteTexture(_musicTab.Thumbnail);
            }

            if (_musicTab.IsPrivate)
            {
                _privateIconTexture = AsyncTexture2D.FromAssetId(733265);
            }

            if (_musicTab.PracticeMode)
            {
                _practiceModeIconTexture = AsyncTexture2D.FromAssetId(528696);
            }

            UpdateFavoriteTexture();
        }

        private string BuildSubtitleText()
        {
            var sb = new System.Text.StringBuilder(64);

            if (!string.IsNullOrEmpty(_musicTab.Genre))
            {
                sb.Append(_musicTab.Genre);
            }

            if (_musicTab.TabType?.Count > 0)
            {
                if (sb.Length > 0) sb.Append(" • ");
                sb.Append(string.Join(", ", _musicTab.TabType));
            }

            if (!string.IsNullOrEmpty(_musicTab.TabbedBy))
            {
                if (sb.Length > 0) sb.Append(" • ");
                sb.Append(_musicTab.TabbedBy);
            }

            return sb.ToString();
        }

        private void UpdateFavoriteTexture()
        {
            if (_textureService == null)
            {
                return;
            }

            _favoriteTexture = _isFavorite
                ? _textureService.GetFavoriteFilledIcon()
                : _textureService.GetFavoriteEmptyIcon();
        }

        private string TruncateText(string text, BitmapFont font, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || font.MeasureString(text).Width <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            var ellipsisWidth = font.MeasureString(ellipsis).Width;

            for (int i = text.Length - 1; i > 0; i--)
            {
                var truncated = text.Substring(0, i);
                if (font.MeasureString(truncated).Width + ellipsisWidth <= maxWidth)
                {
                    return truncated + ellipsis;
                }
            }

            return ellipsis;
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            EnsureTexturesLoaded();

            base.PaintBeforeChildren(spriteBatch, bounds);

            if (ShouldDrawDarkStripe())
            {
                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, bounds, Color.Black * 0.3f);
            }

            if (_thumbnailTexture?.HasTexture == true)
            {
                var thumbnailBounds = new Rectangle(ThumbnailLeft, ThumbnailTop, ThumbnailSize, ThumbnailSize);
                spriteBatch.DrawOnCtrl(this, _thumbnailTexture, thumbnailBounds);
            }

            var titleFont = GameService.Content.DefaultFont16;
            int maxTitleWidth = Width - TextLeft - 110;
            var displayTitle = TruncateText(_decodedTitle, titleFont, maxTitleWidth);
            spriteBatch.DrawStringOnCtrl(this, displayTitle, titleFont, new Rectangle(TextLeft, TitleTop, maxTitleWidth, 20), Color.White);

            var titleWidth = (int)titleFont.MeasureString(displayTitle).Width;
            var iconOffset = TextLeft + titleWidth + 4;

            if (_practiceModeIconTexture?.HasTexture == true)
            {
                _practiceModeIconBounds = new Rectangle(iconOffset, TitleTop, 20, 20);
                spriteBatch.DrawOnCtrl(this, _practiceModeIconTexture, _practiceModeIconBounds);
                iconOffset += 24;
            }
            else
            {
                _practiceModeIconBounds = Rectangle.Empty;
            }

            if (_privateIconTexture?.HasTexture == true)
            {
                _privateIconBounds = new Rectangle(iconOffset, TitleTop, 20, 20);
                spriteBatch.DrawOnCtrl(this, _privateIconTexture, _privateIconBounds);
            }
            else
            {
                _privateIconBounds = Rectangle.Empty;
            }

            var subtitleFont = GameService.Content.DefaultFont14;
            spriteBatch.DrawStringOnCtrl(this, _decodedSubtitle, subtitleFont, new Rectangle(TextLeft, SubtitleTop, Width - TextLeft - 80, 18), Color.LightGray);

            if (_favoriteTexture?.HasTexture == true)
            {
                int iconX = Width - FavoriteIconSize - FavoriteIconRightMargin;
                int iconY = (Height - FavoriteIconSize) / 2;
                _favoriteIconBounds = new Rectangle(iconX, iconY, FavoriteIconSize, FavoriteIconSize);
                spriteBatch.DrawOnCtrl(this, _favoriteTexture, _favoriteIconBounds);
            }
        }

        private bool ShouldDrawDarkStripe()
        {
            if (Parent == null)
            {
                return false;
            }

            if (_cachedIndex < 0)
            {
                _cachedIndex = 0;
                foreach (var child in Parent.Children)
                {
                    if (child == this)
                    {
                        break;
                    }

                    if (child is TabListCard)
                    {
                        _cachedIndex++;
                    }
                }
            }

            return _cachedIndex % 2 == 0;
        }

        public void InvalidateStripeIndex()
        {
            _cachedIndex = -1;
        }

        protected override void OnMouseEntered(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseEntered(e);
            _scrollEffect.Enable();
            UpdateTooltipForMousePosition(e.MousePosition);
        }

        protected override void OnMouseMoved(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseMoved(e);
            UpdateTooltipForMousePosition(e.MousePosition);
        }

        private void UpdateTooltipForMousePosition(Point mousePosition)
        {
            var relativePos = new Point(
                mousePosition.X - AbsoluteBounds.X,
                mousePosition.Y - AbsoluteBounds.Y);

            if (_practiceModeIconBounds != Rectangle.Empty && _practiceModeIconBounds.Contains(relativePos))
            {
                BasicTooltipText = "Practice Mode Available";
            }
            else if (_privateIconBounds != Rectangle.Empty && _privateIconBounds.Contains(relativePos))
            {
                BasicTooltipText = "Private Tab";
            }
            else if (_favoriteIconBounds != Rectangle.Empty && _favoriteIconBounds.Contains(relativePos))
            {
                BasicTooltipText = _isFavorite ? "Remove from Favorites" : "Add to Favorites";
            }
            else
            {
                BasicTooltipText = $"{_musicTab.Name}\nClick to view notation";
            }
        }

        protected override void OnMouseLeft(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseLeft(e);
            if (!_isSelected)
            {
                _scrollEffect.Disable();
            }
        }

        private void UpdateSelectedState()
        {
            _scrollEffect.ForceActive = _isSelected;
        }

        private void OnCardClick(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            var relativeMousePos = new Point(
                e.MousePosition.X - AbsoluteBounds.X,
                e.MousePosition.Y - AbsoluteBounds.Y);

            int iconX = Width - FavoriteIconSize - FavoriteIconRightMargin;
            int iconY = (Height - FavoriteIconSize) / 2;
            var favoriteHitArea = new Rectangle(iconX, iconY, FavoriteIconSize, FavoriteIconSize);

            if (favoriteHitArea.Contains(relativeMousePos))
            {
                _userSettingsService?.ToggleFavorite(_musicTab.Id);
                IsFavorite = _userSettingsService?.IsFavorite(_musicTab.Id) ?? false;
                FavoriteToggled?.Invoke(this, _musicTab);
                return;
            }

            CardClicked?.Invoke(this, _musicTab);
        }

        protected override void DisposeControl()
        {
            Click -= OnCardClick;

            EffectBehind = null;
            _scrollEffect = null;
            _thumbnailTexture = null;
            _favoriteTexture = null;
            _privateIconTexture = null;
            _practiceModeIconTexture = null;

            base.DisposeControl();
        }
    }
}
