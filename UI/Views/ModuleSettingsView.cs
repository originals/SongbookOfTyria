using System;

using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Input;
using Blish_HUD.Settings;
using Blish_HUD.Settings.UI.Views;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Settings;

namespace SongbookOfTyria.UI.Views
{
    public class ModuleSettingsView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<ModuleSettingsView>();

        private static readonly Color InfoColor = Color.White;
        private static readonly Color SuccessColor = new Color(100, 200, 100);
        private static readonly Color WarningColor = new Color(255, 200, 100);
        private static readonly Color ErrorColor = new Color(255, 100, 100);

        private readonly ModuleSettings _moduleSettings;
        private FlowPanel _settingsPanel;
        private Label _authStatusLabel;
        private Checkbox _enableGuildAuthCheckbox;
        private TextBox _apiKeyTextBox;
        private Panel _apiKeyPanel;

        public ModuleSettingsView(ModuleSettings moduleSettings)
        {
            _moduleSettings = moduleSettings ?? throw new ArgumentNullException(nameof(moduleSettings));
        }

        protected override void Build(Container buildPanel)
        {
            _settingsPanel = new FlowPanel
            {
                Size = buildPanel.ContentRegion.Size,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(5, 10),
                OuterControlPadding = new Vector2(10, 10),
                CanScroll = true,
                Parent = buildPanel
            };

            buildPanel.ChildAdded += (s, e) =>
            {
                if (e.ChangedChild is Scrollbar scrollbar)
                {
                    scrollbar.ZIndex = int.MaxValue;
                }
            };

            BuildGuildAuthSection();
            // will be added in later version
            //BuildKeybindSection("Instrument Keys", new[]
            //{
            //    _moduleSettings.NoteC,
            //    _moduleSettings.NoteD,
            //    _moduleSettings.NoteE,
            //    _moduleSettings.NoteF,
            //    _moduleSettings.NoteG,
            //    _moduleSettings.NoteA,
            //    _moduleSettings.NoteB,
            //    _moduleSettings.NoteCHigh,
            //    _moduleSettings.OctaveDown,
            //    _moduleSettings.OctaveUp,
            //    _moduleSettings.SharpCs,
            //    _moduleSettings.SharpDs,
            //    _moduleSettings.SharpFs,
            //    _moduleSettings.SharpGs,
            //    _moduleSettings.SharpAs
            //});

            _moduleSettings.AuthStatusChanged += OnAuthStatusChanged;
        }

        private void BuildGuildAuthSection()
        {
            var sectionPanel = new FlowPanel
            {
                Title = "OPUS Guild Authentication",
                Width = _settingsPanel.ContentRegion.Width - 50,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 5),
                OuterControlPadding = new Vector2(5, 10),
                CanCollapse = true,
                ShowBorder = true,
                Parent = _settingsPanel
            };

            var checkboxPanel = new Panel
            {
                Width = sectionPanel.ContentRegion.Width - 10,
                Height = 30,
                Parent = sectionPanel
            };

            _enableGuildAuthCheckbox = new Checkbox
            {
                Text = "Enable Guild Authentication",
                BasicTooltipText = "When enabled, uses your GW2 API key to verify OPUS guild membership and unlock private tabs.",
                Checked = _moduleSettings.EnableGuildAuth,
                Parent = checkboxPanel
            };
            _enableGuildAuthCheckbox.CheckedChanged += OnEnableGuildAuthCheckboxChanged;

            _apiKeyPanel = new Panel
            {
                Width = sectionPanel.ContentRegion.Width - 10,
                Height = 30,
                Visible = _moduleSettings.EnableGuildAuth,
                Parent = sectionPanel
            };

            new Label
            {
                Text = "GW2 API Key",
                BasicTooltipText = "Enter your GW2 API key with 'account' permission. Get one at https://account.arena.net/applications",
                AutoSizeWidth = true,
                Height = 27,
                Location = new Point(0, 0),
                Parent = _apiKeyPanel
            };

            _apiKeyTextBox = new TextBox
            {
                Text = _moduleSettings.Gw2ApiKey,
                BasicTooltipText = "Enter your GW2 API key with 'account' permission. Get one at https://account.arena.net/applications",
                Size = new Point(500, 27),
                Location = new Point(100, 0),
                Parent = _apiKeyPanel
            };
            _apiKeyTextBox.InputFocusChanged += OnApiKeyTextBoxFocusChanged;

            _authStatusLabel = new Label
            {
                Text = "",
                AutoSizeWidth = true,
                Height = 26,
                TextColor = InfoColor,
                Visible = _moduleSettings.EnableGuildAuth,
                Parent = sectionPanel
            };
        }

        private void BuildKeybindSection(string title, SettingEntry<KeyBinding>[] keybinds)
        {
            var sectionPanel = new FlowPanel
            {
                Title = title,
                Width = _settingsPanel.ContentRegion.Width - 50,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 5),
                OuterControlPadding = new Vector2(5, 10),
                CanCollapse = true,
                ShowBorder = true,
                Parent = _settingsPanel
            };

            foreach (var keybind in keybinds)
            {
                new KeybindingAssigner(keybind.Value)
                {
                    KeyBindingName = keybind.DisplayName,
                    BasicTooltipText = keybind.Description,
                    NameWidth = 100,
                    Width = 250,
                    Parent = sectionPanel
                };
            }
        }

        private void OnAuthStatusChanged(object sender, StatusChangedEventArgs e)
        {
            UpdateAuthStatus(e.Message, e.Type);
        }

        private void OnEnableGuildAuthCheckboxChanged(object sender, CheckChangedEvent e)
        {
            _moduleSettings.SetEnableGuildAuth(e.Checked);
            UpdateGuildAuthControlsVisibility(e.Checked);
        }

        private void UpdateGuildAuthControlsVisibility(bool visible)
        {
            if (_apiKeyPanel != null)
                _apiKeyPanel.Visible = visible;
            if (_authStatusLabel != null)
                _authStatusLabel.Visible = visible;
        }

        private void OnApiKeyTextBoxFocusChanged(object sender, ValueEventArgs<bool> e)
        {
            if (!e.Value)
            {
                _moduleSettings.SetGw2ApiKey(_apiKeyTextBox.Text);
            }
        }

        private void UpdateAuthStatus(string message, StatusType type)
        {
            if (_authStatusLabel == null) return;
            _authStatusLabel.Text = message;
            _authStatusLabel.TextColor = GetColorForStatus(type);
        }

        private Color GetColorForStatus(StatusType type)
        {
            switch (type)
            {
                case StatusType.Success:
                    return SuccessColor;
                case StatusType.Warning:
                    return WarningColor;
                case StatusType.Error:
                    return ErrorColor;
                default:
                    return InfoColor;
            }
        }

        protected override void Unload()
        {
            if (_enableGuildAuthCheckbox != null)
            {
                _enableGuildAuthCheckbox.CheckedChanged -= OnEnableGuildAuthCheckboxChanged;
            }

            if (_apiKeyTextBox != null)
            {
                _apiKeyTextBox.InputFocusChanged -= OnApiKeyTextBoxFocusChanged;
            }

            if (_moduleSettings != null)
            {
                _moduleSettings.AuthStatusChanged -= OnAuthStatusChanged;
            }

            _settingsPanel?.Dispose();
            base.Unload();
        }
    }
}
