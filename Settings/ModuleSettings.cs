using System;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Input;
using Blish_HUD.Settings;

using Microsoft.Xna.Framework.Input;

using SongbookOfTyria.Services;

namespace SongbookOfTyria.Settings
{
    public enum StatusType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class StatusChangedEventArgs : EventArgs
    {
        public string Message { get; }
        public StatusType Type { get; }

        public StatusChangedEventArgs(string message, StatusType type)
        {
            Message = message;
            Type = type;
        }
    }

    public sealed class ModuleSettings : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<ModuleSettings>();

        private readonly SettingEntry<bool> _enableGuildAuthSetting;
        private readonly SettingEntry<string> _gw2ApiKeySetting;
        private readonly SettingCollection _settingsForView;
        private readonly object _verifyLock = new object();

        private TabsService _tabsService;
        private TextureService _textureService;
        private GuildAuthService _guildAuthService;
        private volatile bool _isVerifyingApiKey;

        public SettingEntry<KeyBinding> NoteC { get; private set; }
        public SettingEntry<KeyBinding> NoteD { get; private set; }
        public SettingEntry<KeyBinding> NoteE { get; private set; }
        public SettingEntry<KeyBinding> NoteF { get; private set; }
        public SettingEntry<KeyBinding> NoteG { get; private set; }
        public SettingEntry<KeyBinding> NoteA { get; private set; }
        public SettingEntry<KeyBinding> NoteB { get; private set; }
        public SettingEntry<KeyBinding> NoteCHigh { get; private set; }
        public SettingEntry<KeyBinding> OctaveDown { get; private set; }
        public SettingEntry<KeyBinding> OctaveUp { get; private set; }

        public SettingEntry<KeyBinding> SharpCs { get; private set; }
        public SettingEntry<KeyBinding> SharpDs { get; private set; }
        public SettingEntry<KeyBinding> SharpFs { get; private set; }
        public SettingEntry<KeyBinding> SharpGs { get; private set; }
        public SettingEntry<KeyBinding> SharpAs { get; private set; }

        public bool EnableGuildAuth => _enableGuildAuthSetting.Value;
        public string Gw2ApiKey => _gw2ApiKeySetting.Value;
        public SettingCollection SettingsForView => _settingsForView;

        public void SetEnableGuildAuth(bool value)
        {
            _enableGuildAuthSetting.Value = value;
        }

        public void SetGw2ApiKey(string value)
        {
            _gw2ApiKeySetting.Value = value;
        }

        public event EventHandler<StatusChangedEventArgs> CacheStatusChanged;
        public event EventHandler<StatusChangedEventArgs> AuthStatusChanged;

        public ModuleSettings(SettingCollection settings)
        {
            _settingsForView = settings.AddSubCollection("GuildAuth", false);

            _enableGuildAuthSetting = _settingsForView.DefineSetting(
                "EnableGuildAuth",
                false,
                () => "Enable OPUS Guild Authentication",
                () => "When enabled, uses your GW2 API key to verify OPUS guild membership and unlock private tabs.");

            _gw2ApiKeySetting = _settingsForView.DefineSetting(
                "Gw2ApiKey",
                string.Empty,
                () => "GW2 API Key",
                () => "Enter your GW2 API key with 'account' permission. Get one at https://account.arena.net/applications");

            _enableGuildAuthSetting.SettingChanged += OnEnableGuildAuthSettingChanged;
            _gw2ApiKeySetting.SettingChanged += OnGw2ApiKeySettingChanged;

            // will be added in later version
            //DefineInstrumentKeys(settings);
            //DefinePianoSharps(settings);
        }

        private void DefineInstrumentKeys(SettingCollection settings)
        {
            var instrumentKeys = settings.AddSubCollection("InstrumentKeys", true, () => "Instrument Keys");

            NoteC = instrumentKeys.DefineSetting("KeyNoteC",
                new KeyBinding(Keys.D1),
                () => "Note 1 (C)",
                () => "Key for note C - match to Weapon Skill 1");

            NoteD = instrumentKeys.DefineSetting("KeyNoteD",
                new KeyBinding(Keys.D2),
                () => "Note 2 (D)",
                () => "Key for note D - match to Weapon Skill 2");

            NoteE = instrumentKeys.DefineSetting("KeyNoteE",
                new KeyBinding(Keys.D3),
                () => "Note 3 (E)",
                () => "Key for note E - match to Weapon Skill 3");

            NoteF = instrumentKeys.DefineSetting("KeyNoteF",
                new KeyBinding(Keys.D4),
                () => "Note 4 (F)",
                () => "Key for note F - match to Weapon Skill 4");

            NoteG = instrumentKeys.DefineSetting("KeyNoteG",
                new KeyBinding(Keys.D5),
                () => "Note 5 (G)",
                () => "Key for note G - match to Weapon Skill 5");

            NoteA = instrumentKeys.DefineSetting("KeyNoteA",
                new KeyBinding(Keys.D6),
                () => "Note 6 (A)",
                () => "Key for note A - match to Healing Skill");

            NoteB = instrumentKeys.DefineSetting("KeyNoteB",
                new KeyBinding(Keys.D7),
                () => "Note 7 (B)",
                () => "Key for note B - match to Utility Skill 1");

            NoteCHigh = instrumentKeys.DefineSetting("KeyNoteCHigh",
                new KeyBinding(Keys.D8),
                () => "Note 8 (C High)",
                () => "Key for high C - match to Utility Skill 2");

            OctaveDown = instrumentKeys.DefineSetting("KeyOctaveDown",
                new KeyBinding(Keys.D9),
                () => "Octave Down",
                () => "Key to shift octave down - match to Utility Skill 3");

            OctaveUp = instrumentKeys.DefineSetting("KeyOctaveUp",
                new KeyBinding(Keys.D0),
                () => "Octave Up",
                () => "Key to shift octave up - match to Elite Skill");
        }

        private void DefinePianoSharps(SettingCollection settings)
        {
            var pianoSharps = settings.AddSubCollection("PianoSharps", true, () => "Piano Sharp Notes");

            SharpCs = pianoSharps.DefineSetting("KeySharpCs",
                new KeyBinding(ModifierKeys.Shift, Keys.D1),
                () => "F1 - C#/Db",
                () => "Key for C sharp / D flat");

            SharpDs = pianoSharps.DefineSetting("KeySharpDs",
                new KeyBinding(ModifierKeys.Shift, Keys.D2),
                () => "F2 - D#/Eb",
                () => "Key for D sharp / E flat");

            SharpFs = pianoSharps.DefineSetting("KeySharpFs",
                new KeyBinding(ModifierKeys.Shift, Keys.D3),
                () => "F3 - F#/Gb",
                () => "Key for F sharp / G flat");

            SharpGs = pianoSharps.DefineSetting("KeySharpGs",
                new KeyBinding(ModifierKeys.Shift, Keys.D5),
                () => "F4 - G#/Ab",
                () => "Key for G sharp / A flat");

            SharpAs = pianoSharps.DefineSetting("KeySharpAs",
                new KeyBinding(ModifierKeys.Shift, Keys.D6),
                () => "F5 - A#/Bb",
                () => "Key for A sharp / B flat");
        }

        public int? GetMidiNoteFromKey(Keys key, ModifierKeys modifiers, int octaveOffset)
        {
            if (MatchesKeyBinding(OctaveDown.Value, key, modifiers)) return -100;
            if (MatchesKeyBinding(OctaveUp.Value, key, modifiers)) return -101;

            int? baseNote = null;

            if (modifiers.HasFlag(ModifierKeys.Shift) || modifiers.HasFlag(ModifierKeys.Alt))
            {
                if (MatchesKeyBinding(SharpCs.Value, key, modifiers)) baseNote = 49;
                else if (MatchesKeyBinding(SharpDs.Value, key, modifiers)) baseNote = 51;
                else if (MatchesKeyBinding(SharpFs.Value, key, modifiers)) baseNote = 54;
                else if (MatchesKeyBinding(SharpGs.Value, key, modifiers)) baseNote = 56;
                else if (MatchesKeyBinding(SharpAs.Value, key, modifiers)) baseNote = 58;
            }

            if (!baseNote.HasValue)
            {
                if (MatchesKeyBinding(NoteC.Value, key, modifiers)) baseNote = 48;
                else if (MatchesKeyBinding(NoteD.Value, key, modifiers)) baseNote = 50;
                else if (MatchesKeyBinding(NoteE.Value, key, modifiers)) baseNote = 52;
                else if (MatchesKeyBinding(NoteF.Value, key, modifiers)) baseNote = 53;
                else if (MatchesKeyBinding(NoteG.Value, key, modifiers)) baseNote = 55;
                else if (MatchesKeyBinding(NoteA.Value, key, modifiers)) baseNote = 57;
                else if (MatchesKeyBinding(NoteB.Value, key, modifiers)) baseNote = 59;
                else if (MatchesKeyBinding(NoteCHigh.Value, key, modifiers)) baseNote = 60;
            }

            if (baseNote.HasValue)
            {
                return baseNote.Value + (octaveOffset * 12);
            }

            return null;
        }

        private static bool MatchesKeyBinding(KeyBinding binding, Keys key, ModifierKeys activeModifiers)
        {
            if (binding.PrimaryKey != key) return false;
            return binding.ModifierKeys == ModifierKeys.None || activeModifiers.HasFlag(binding.ModifierKeys);
        }

        public void InitializeServices(
            TabsService tabsService,
            TextureService textureService,
            GuildAuthService guildAuthService)
        {
            _tabsService = tabsService;
            _textureService = textureService;
            _guildAuthService = guildAuthService;
        }

        public async Task InitializeGuildAuthAsync()
        {
            try
            {
                if (_enableGuildAuthSetting.Value && !string.IsNullOrWhiteSpace(_gw2ApiKeySetting.Value))
                {
                    await VerifyGuildMembershipAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "InitializeGuildAuthAsync: Error");
            }
        }

        public async Task RefreshDataAsync()
        {
            if (_tabsService != null)
            {
                await _tabsService.RefreshTabsAsync().ConfigureAwait(false);
            }
        }

        private async void OnEnableGuildAuthSettingChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (e.NewValue)
            {
                if (string.IsNullOrWhiteSpace(_gw2ApiKeySetting.Value))
                {
                    RaiseAuthStatus("Please enter your GW2 API key to enable guild authentication.", StatusType.Warning);
                    return;
                }
                await VerifyGuildMembershipAsync();
            }
            else
            {
                _guildAuthService?.ClearAuth();
                RaiseAuthStatus("Guild authentication disabled.", StatusType.Info);
            }
        }

        private async void OnGw2ApiKeySettingChanged(object sender, ValueChangedEventArgs<string> e)
        {
            if (_enableGuildAuthSetting.Value && !string.IsNullOrWhiteSpace(e.NewValue))
            {
                await VerifyGuildMembershipAsync();
            }
            else if (string.IsNullOrWhiteSpace(e.NewValue))
            {
                _guildAuthService?.ClearAuth();
                RaiseAuthStatus("", StatusType.Info);
            }
        }

        private async Task VerifyGuildMembershipAsync()
        {
            lock (_verifyLock)
            {
                if (_isVerifyingApiKey || _guildAuthService == null)
                {
                    return;
                }
                _isVerifyingApiKey = true;
            }

            RaiseAuthStatus("Verifying API key...", StatusType.Info);

            try
            {
                var apiKey = _gw2ApiKeySetting.Value;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Logger.Warn("VerifyGuildMembershipAsync: No API key configured");
                    RaiseAuthStatus("No API key configured.", StatusType.Warning);
                    return;
                }

                var response = await _guildAuthService.VerifyApiKeyAsync(apiKey);

                if (response == null)
                {
                    RaiseAuthStatus("Failed to verify API key.", StatusType.Error);
                    return;
                }

                if (!response.Valid)
                {
                    RaiseAuthStatus(response.Message ?? "Invalid API key.", StatusType.Error);
                    return;
                }

                if (response.InOpusGuild)
                {
                    RaiseAuthStatus($"Welcome, {response.AccountName}! Private tabs unlocked.", StatusType.Success);
                }
                else
                {
                    RaiseAuthStatus($"Verified as {response.AccountName}, but not an OPUS guild member.", StatusType.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "VerifyGuildMembershipAsync: Error");
                RaiseAuthStatus("Error verifying guild membership.", StatusType.Error);
            }
            finally
            {
                _isVerifyingApiKey = false;
            }
        }

        private void RaiseAuthStatus(string message, StatusType type)
        {
            AuthStatusChanged?.Invoke(this, new StatusChangedEventArgs(message, type));
        }

        public void RaiseCacheStatus(string message, StatusType type)
        {
            CacheStatusChanged?.Invoke(this, new StatusChangedEventArgs(message, type));
        }

        public void Dispose()
        {
            if (_enableGuildAuthSetting != null)
            {
                _enableGuildAuthSetting.SettingChanged -= OnEnableGuildAuthSettingChanged;
            }

            if (_gw2ApiKeySetting != null)
            {
                _gw2ApiKeySetting.SettingChanged -= OnGw2ApiKeySettingChanged;
            }
        }
    }
}
