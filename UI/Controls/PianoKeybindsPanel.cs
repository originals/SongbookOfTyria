using System;

using Blish_HUD;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;

namespace SongbookOfTyria.UI.Controls
{
    public sealed class PianoKeybindsPanel : FlowPanel
    {
        private const int DefaultHeight = 120;

        private const string CircledOne = "①";
        private const string CircledTwo = "②";
        private const string CircledThree = "③";
        private const string CircledFour = "④";
        private const string CircledFive = "⑤";

        private readonly UserSettingsService _userSettingsService;
        private readonly Action _onKeybindsApplied;

        private TextBox _keybindCsDb;
        private TextBox _keybindDsEb;
        private TextBox _keybindFsGb;
        private TextBox _keybindGsAb;
        private TextBox _keybindAsBb;

        private PianoKeybinds _pianoKeybinds;
        private bool _lastCollapsedState;

        public PianoKeybinds Keybinds => _pianoKeybinds;

        public event EventHandler<bool> CollapsedChanged;

        public PianoKeybindsPanel(
            int sectionWidth,
            bool collapsed,
            PianoKeybinds initialKeybinds,
            UserSettingsService userSettingsService,
            Action onKeybindsApplied)
        {
            _userSettingsService = userSettingsService;
            _onKeybindsApplied = onKeybindsApplied;
            _pianoKeybinds = initialKeybinds ?? new PianoKeybinds();
            _lastCollapsedState = collapsed;

            ShowBorder = true;
            Title = "Piano Keybinds";
            CanCollapse = true;
            Width = sectionWidth;
            Height = DefaultHeight;
            HeightSizingMode = SizingMode.Standard;  // Prevent auto-sizing
            FlowDirection = ControlFlowDirection.SingleTopToBottom;
            ControlPadding = new Vector2(0, 5);
            OuterControlPadding = new Vector2(0, 12);

            BuildContent(sectionWidth);

            Collapsed = collapsed;

            Resized += OnResized;
        }

        private void BuildContent(int sectionWidth)
        {
            var keybindsRowWidth = 5 * 75 + 4 * 5;
            var keybindsRowPadding = (sectionWidth - keybindsRowWidth) / 2;

            var presetsRowWidth = 80 + 80 + 80 + 70 + 3 * 5;
            var presetsRowPadding = (sectionWidth - presetsRowWidth) / 2;

            var keybindsWrapper = new Panel
            {
                Width = sectionWidth,
                Height = 26,
                Parent = this
            };

            var keybindsRow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                Width = keybindsRowWidth,
                Height = 26,
                Location = new Point(keybindsRowPadding, 0),
                ControlPadding = new Vector2(5, 0),
                Parent = keybindsWrapper
            };

            _keybindCsDb = CreateKeybindInput(keybindsRow, "F1:", _pianoKeybinds.CsDb);
            _keybindDsEb = CreateKeybindInput(keybindsRow, "F2:", _pianoKeybinds.DsEb);
            _keybindFsGb = CreateKeybindInput(keybindsRow, "F3:", _pianoKeybinds.FsGb);
            _keybindGsAb = CreateKeybindInput(keybindsRow, "F4:", _pianoKeybinds.GsAb);
            _keybindAsBb = CreateKeybindInput(keybindsRow, "F5:", _pianoKeybinds.AsBb);

            var presetsWrapper = new Panel
            {
                Width = sectionWidth,
                Height = 30,
                Parent = this
            };

            var presetsRow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                Width = presetsRowWidth,
                Height = 30,
                Location = new Point(presetsRowPadding, 0),
                ControlPadding = new Vector2(5, 0),
                Parent = presetsWrapper
            };

            CreatePresetButton(presetsRow, "Default", "Preset: Circled Numbers (displayed as 1-5)", 80, PianoKeybinds.CreateDefault);
            CreatePresetButton(presetsRow, "'1 '2 '3...", "Preset: Apostrophe", 80, PianoKeybinds.CreateApostrophe);
            CreatePresetButton(presetsRow, "#1 #2 #3...", "Preset: Hashtag", 80, PianoKeybinds.CreateHashtag);

            var applyButton = new StandardButton
            {
                Text = "Apply",
                BasicTooltipText = "Apply keybinds to notation",
                Width = 70,
                Parent = presetsRow
            };
            applyButton.Click += OnApplyClicked;
        }

        private TextBox CreateKeybindInput(FlowPanel parent, string label, string value)
        {
            var container = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                Width = 75,
                Height = 26,
                ControlPadding = new Vector2(2, 0),
                Parent = parent
            };

            var labelContainer = new Panel
            {
                Width = 25,
                Height = 26,
                Parent = container
            };

            new Label
            {
                Text = label,
                Font = GameService.Content.DefaultFont12,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 5),
                Parent = labelContainer
            };

            return new TextBox
            {
                Text = ToDisplayFormat(value),
                Width = 45,
                MaxLength = 3,
                Parent = container
            };
        }

        private void CreatePresetButton(FlowPanel parent, string text, string tooltip, int width, Func<PianoKeybinds> presetFactory)
        {
            var button = new StandardButton
            {
                Text = text,
                BasicTooltipText = tooltip,
                Width = width,
                Parent = parent
            };
            button.Click += (s, e) => ApplyPresetToInputs(presetFactory());
        }

        private void ApplyPresetToInputs(PianoKeybinds preset)
        {
            if (_keybindCsDb != null) _keybindCsDb.Text = ToDisplayFormat(preset.CsDb);
            if (_keybindDsEb != null) _keybindDsEb.Text = ToDisplayFormat(preset.DsEb);
            if (_keybindFsGb != null) _keybindFsGb.Text = ToDisplayFormat(preset.FsGb);
            if (_keybindGsAb != null) _keybindGsAb.Text = ToDisplayFormat(preset.GsAb);
            if (_keybindAsBb != null) _keybindAsBb.Text = ToDisplayFormat(preset.AsBb);
        }

        private void OnApplyClicked(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            _pianoKeybinds = new PianoKeybinds
            {
                CsDb = ToStorageFormat(_keybindCsDb?.Text) ?? CircledOne,
                DsEb = ToStorageFormat(_keybindDsEb?.Text) ?? CircledTwo,
                FsGb = ToStorageFormat(_keybindFsGb?.Text) ?? CircledThree,
                GsAb = ToStorageFormat(_keybindGsAb?.Text) ?? CircledFour,
                AsBb = ToStorageFormat(_keybindAsBb?.Text) ?? CircledFive
            };

            _userSettingsService?.SavePianoKeybinds(_pianoKeybinds);
            _onKeybindsApplied?.Invoke();
        }

        private void OnResized(object sender, ResizedEventArgs e)
        {
            if (Collapsed != _lastCollapsedState)
            {
                _lastCollapsedState = Collapsed;
                CollapsedChanged?.Invoke(this, Collapsed);
            }
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
            base.DisposeControl();
        }

        private static string ToDisplayFormat(string value)
        {
            if (string.IsNullOrEmpty(value)) return "1";
            return value
                .Replace(CircledOne, "1")
                .Replace(CircledTwo, "2")
                .Replace(CircledThree, "3")
                .Replace(CircledFour, "4")
                .Replace(CircledFive, "5");
        }

        private static string ToStorageFormat(string displayValue)
        {
            if (string.IsNullOrEmpty(displayValue)) return CircledOne;

            switch (displayValue)
            {
                case "1": return CircledOne;
                case "2": return CircledTwo;
                case "3": return CircledThree;
                case "4": return CircledFour;
                case "5": return CircledFive;
                default: return displayValue;
            }
        }
    }
}
