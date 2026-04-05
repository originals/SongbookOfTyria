using System;
using System.Diagnostics;
using System.Linq;
using System.Net;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;
using SongbookOfTyria.Utilities;

namespace SongbookOfTyria.UI.Controls
{
    public sealed class TabDetailsPanel : FlowPanel
    {
        private const int ThumbnailMaxWidth = 240;
        private const int ThumbnailMaxHeight = 220;
        private const int DetailLabelWidth = 120;
        private const int DetailValueWidth = 120;
        private const int PracticeModeIconAssetId = 528696;

        private static readonly Logger Logger = Logger.GetLogger<TabDetailsPanel>();

        private readonly MusicTab _musicTab;
        private readonly TextureService _textureService;
        private readonly AsyncTexture2D _practiceModeIconTexture;
        private EventHandler<ValueChangedEventArgs<Microsoft.Xna.Framework.Graphics.Texture2D>> _textureSwappedHandler;
        private AsyncTexture2D _arrangerTexture;
        private bool _lastCollapsedState;

        public event EventHandler<bool> CollapsedChanged;

        public TabDetailsPanel(
            MusicTab musicTab,
            TextureService textureService,
            int panelWidth,
            bool collapsed)
        {
            _musicTab = musicTab;
            _textureService = textureService;
            _lastCollapsedState = collapsed;

            if (_musicTab.PracticeMode)
            {
                _practiceModeIconTexture = AsyncTexture2D.FromAssetId(PracticeModeIconAssetId);
            }

            ShowBorder = true;
            Title = "Details";
            CanCollapse = true;
            Width = panelWidth;
            HeightSizingMode = SizingMode.AutoSize;
            FlowDirection = ControlFlowDirection.SingleTopToBottom;
            ControlPadding = new Vector2(0, 5);
            OuterControlPadding = new Vector2(0, 0);

            BuildContent();

            Collapsed = collapsed;
            Title = collapsed ? " " : "Details";

            Resized += OnResized;
        }

        private void BuildContent()
        {
            BuildThumbnail();
            AddDetailRow("Genre:", _musicTab.Genre);

            if (_musicTab.IsBeginner)
            {
                AddDetailRow("Beginner Tab:", "Yes");
            }

            if (_musicTab.TabType != null && _musicTab.TabType.Count > 0)
            {
                AddDetailRow("Song type:", string.Join(", ", _musicTab.TabType));
            }

            AddDetailRow("Released Date:", _musicTab.ReleaseDate);
            AddDetailRow("Tabbed by:", GetTabberName());

            if (_musicTab.TabberInfo != null && _musicTab.TabberInfo.Count > 0)
            {
                AddArrangerPicture(_musicTab.TabberInfo[0]);
            }

            AddViewOnWebsiteButton();
        }

        private void BuildThumbnail()
        {
            if (string.IsNullOrEmpty(_musicTab.Thumbnail))
            {
                return;
            }

            var thumbnailTexture = _textureService.GetRemoteTexture(_musicTab.Thumbnail);
            if (thumbnailTexture == null)
            {
                return;
            }

            var detailsInnerWidth = Width;

            var imageSize = ImageSizeCalculator.CalculateAspectRatioSize(
                thumbnailTexture.Width,
                thumbnailTexture.Height,
                detailsInnerWidth - 20,
                ThumbnailMaxHeight);

            var imageContainer = new Panel
            {
                Width = detailsInnerWidth,
                Height = imageSize.Y + 15,
                Parent = this
            };

            var centeredX = (imageContainer.Width - imageSize.X) / 2;

            new Image(thumbnailTexture)
            {
                Size = imageSize,
                Location = new Point(centeredX, 10),
                Parent = imageContainer
            };
        }

        private string GetTabberName()
        {
            if (!string.IsNullOrEmpty(_musicTab.TabbedBy))
            {
                return _musicTab.TabbedBy;
            }

            var arranger = _musicTab.TabberInfo?.FirstOrDefault();
            if (arranger != null)
            {
                var name = !string.IsNullOrEmpty(arranger.DisplayName) ? arranger.DisplayName : arranger.Username;
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            if (_musicTab.TabbedByMember?.Count > 0)
            {
                return string.Join(", ", _musicTab.TabbedByMember);
            }

            return null;
        }

        private void AddDetailRow(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var rowPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                Width = Width,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(5, 0),
                OuterControlPadding = new Vector2(15, 0),
                Parent = this
            };

            new Label
            {
                Text = label,
                Font = GameService.Content.DefaultFont14,
                Width = DetailLabelWidth,
                AutoSizeHeight = true,
                Parent = rowPanel
            };

            new Label
            {
                Text = WebUtility.HtmlDecode(value),
                Font = GameService.Content.DefaultFont14,
                Width = DetailValueWidth,
                AutoSizeHeight = true,
                WrapText = true,
                Parent = rowPanel
            };
        }

        private void AddArrangerPicture(TabberInfo tabberInfo)
        {
            if (string.IsNullOrEmpty(tabberInfo.PictureUrl))
            {
                return;
            }

            _arrangerTexture = _textureService.GetRemoteTexture(tabberInfo.PictureUrl);
            if (_arrangerTexture == null)
            {
                return;
            }

            var picturePanel = new Panel
            {
                Width = Width,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = this
            };

            var arrangerImage = new Image(_arrangerTexture)
            {
                Size = new Point(ThumbnailMaxHeight, ThumbnailMaxHeight),
                Location = new Point(10, 10),
                Parent = picturePanel
            };

            if (_arrangerTexture.HasTexture)
            {
                UpdateArrangerImageSize(arrangerImage, picturePanel, _arrangerTexture.Texture);
            }

            _textureSwappedHandler = (sender, e) =>
            {
                if (e.NewValue != null)
                {
                    UpdateArrangerImageSize(arrangerImage, picturePanel, e.NewValue);
                }
            };
            _arrangerTexture.TextureSwapped += _textureSwappedHandler;
        }

        private void UpdateArrangerImageSize(
            Image image,
            Panel panel,
            Microsoft.Xna.Framework.Graphics.Texture2D texture)
        {
            var imageSize = ImageSizeCalculator.CalculateAspectRatioSize(
                texture.Width,
                texture.Height,
                ThumbnailMaxHeight,
                ThumbnailMaxHeight);

            image.Size = imageSize;
            image.Location = new Point((panel.Width - imageSize.X) / 2, 10);
            panel.Height = imageSize.Y + 35;
        }

        private void AddViewOnWebsiteButton()
        {
            if (string.IsNullOrEmpty(_musicTab.Url))
            {
                return;
            }

            var buttonPanel = new Panel
            {
                Width = Width,
                Height = 45,
                Parent = this
            };

            var buttonWidth = 140;
            var iconSize = 20;
            var iconSpacing = 4;
            var totalWidth = _musicTab.PracticeMode ? buttonWidth + iconSize + iconSpacing : buttonWidth;
            var startX = (buttonPanel.Width - totalWidth) / 2;

            var viewButton = new StandardButton
            {
                Text = "View on Website",
                Width = buttonWidth,
                Location = new Point(startX, 10),
                Parent = buttonPanel
            };

            if (_musicTab.PracticeMode && _practiceModeIconTexture != null)
            {
                new Image(_practiceModeIconTexture)
                {
                    Size = new Point(iconSize, iconSize),
                    Location = new Point(startX + buttonWidth + iconSpacing, 12),
                    BasicTooltipText = "Practice Mode Available",
                    Parent = buttonPanel
                };
            }

            viewButton.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _musicTab.Url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to open URL: {Url}", _musicTab.Url);
                }
            };
        }

        private void OnResized(object sender, ResizedEventArgs e)
        {
            Title = Collapsed ? " " : "Details";
        }

        public void RebuildContent()
        {
            var children = Children.ToArray();
            foreach (var child in children)
            {
                child.Parent = null;
                child.Dispose();
            }

            BuildContent();
            Invalidate();
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (Collapsed != _lastCollapsedState)
            {
                _lastCollapsedState = Collapsed;
                CollapsedChanged?.Invoke(this, Collapsed);
            }
        }

        protected override void DisposeControl()
        {
            Resized -= OnResized;

            if (_arrangerTexture != null && _textureSwappedHandler != null)
            {
                _arrangerTexture.TextureSwapped -= _textureSwappedHandler;
            }

            base.DisposeControl();
        }
    }
}
