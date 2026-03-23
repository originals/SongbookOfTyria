using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;

using Microsoft.Xna.Framework;

namespace SongbookOfTyria.UI.Views
{
    public class AboutView : View
    {
        private const int LeftPadding = 50;
        private const int DefaultSpacing = 10;

        protected override void Build(Container buildPanel)
        {
            var aboutPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Width = buildPanel.ContentRegion.Width - LeftPadding - DefaultSpacing,
                Height = buildPanel.ContentRegion.Height,
                Location = new Point(LeftPadding, 0),
                Parent = buildPanel,
                ControlPadding = new Vector2(0, DefaultSpacing),
                OuterControlPadding = new Vector2(0, DefaultSpacing)
            };

            new Label
            {
                Text = "How to play is under construction, check back later",
                Font = GameService.Content.DefaultFont32,
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = aboutPanel
            };

            new Label
            {
                Text = "\"Hi guys\" - Netorzin.3189",
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.LightGray,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = aboutPanel
            };
        }
    }
}
