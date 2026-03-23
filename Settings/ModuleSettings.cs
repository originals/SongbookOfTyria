using System;
using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Settings;

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

        private TabsCacheService _tabsCacheService;
        private TextureService _textureService;
        private GuildAuthService _guildAuthService;
        private volatile bool _isVerifyingApiKey;

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
        }

        public void InitializeServices(
            TabsCacheService tabsCacheService,
            TextureService textureService,
            GuildAuthService guildAuthService)
        {
            _tabsCacheService = tabsCacheService;
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
            if (_tabsCacheService != null)
            {
                await _tabsCacheService.RefreshTabsAsync().ConfigureAwait(false);
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
