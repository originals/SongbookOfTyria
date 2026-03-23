using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Blish_HUD;

using Newtonsoft.Json;

using SongbookOfTyria.Models.Api;

namespace SongbookOfTyria.Services
{
    public sealed class GuildAuthService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<GuildAuthService>();

        private const string BaseUrl = "https://www.gw2opus.com/wp-json/blishhud/v1/";
        private const string AuthVerifyEndpoint = "auth/verify";
        private const int TimeoutSeconds = 30;

        // Format: XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXXXXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX
        private const int ExpectedApiKeyLength = 72;
        private static readonly Regex ApiKeyPattern = new Regex(
            @"^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{20}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _httpClient;

        private bool _isVerified;
        private bool _isOpusMember;
        private string _accountName;
        private string _currentApiKey;

        public bool IsVerified => _isVerified;
        public bool IsOpusMember => _isOpusMember;
        public string AccountName => _accountName;
        public string CurrentApiKey => _currentApiKey;

        public event EventHandler<GuildAuthStatusChangedEventArgs> AuthStatusChanged;

        public GuildAuthService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
        }

        public static ApiKeyValidationResult ValidateApiKeyFormat(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new ApiKeyValidationResult(false, "API key cannot be empty.");
            }

            var trimmedKey = apiKey.Trim();

            if (trimmedKey.Length != ExpectedApiKeyLength)
            {
                return new ApiKeyValidationResult(false,
                    $"API key must be {ExpectedApiKeyLength} characters (got {trimmedKey.Length}).");
            }

            if (!ApiKeyPattern.IsMatch(trimmedKey))
            {
                return new ApiKeyValidationResult(false,
                    "Invalid API key format. Please copy the full key from https://account.arena.net/applications");
            }

            return new ApiKeyValidationResult(true, null);
        }

        public async Task<AuthVerifyResponse> VerifyApiKeyAsync(string apiKey)
        {
            var validation = ValidateApiKeyFormat(apiKey);
            if (!validation.IsValid)
            {
                Logger.Warn("VerifyApiKeyAsync: {ErrorMessage}", validation.ErrorMessage);
                ResetAuthState();
                return new AuthVerifyResponse
                {
                    Valid = false,
                    Message = validation.ErrorMessage
                };
            }

            var trimmedKey = apiKey.Trim();
            var url = $"{BaseUrl}{AuthVerifyEndpoint}";
            var request = new AuthVerifyRequest(trimmedKey);

            try
            {
                var jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (string.IsNullOrEmpty(responseBody))
                {
                    Logger.Warn("VerifyApiKeyAsync: Response was null or empty");
                    ResetAuthState();
                    return null;
                }

                var authResponse = JsonConvert.DeserializeObject<AuthVerifyResponse>(responseBody);

                if (authResponse != null)
                {
                    UpdateAuthState(apiKey, authResponse);
                }

                return authResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.Warn(ex, "VerifyApiKeyAsync: HTTP request failed");
                ResetAuthState();
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, "VerifyApiKeyAsync: Request timed out");
                ResetAuthState();
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Warn(ex, "VerifyApiKeyAsync: JSON deserialization failed");
                ResetAuthState();
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "VerifyApiKeyAsync: Unexpected error");
                ResetAuthState();
                return null;
            }
        }

        private void UpdateAuthState(string apiKey, AuthVerifyResponse response)
        {
            var previousIsOpusMember = _isOpusMember;

            _currentApiKey = apiKey;
            _isVerified = response.Valid;
            _isOpusMember = response.Valid && response.InOpusGuild;
            _accountName = response.AccountName;

            if (previousIsOpusMember != _isOpusMember)
            {
                OnAuthStatusChanged();
            }

            if (_isOpusMember)
            {
                Logger.Info("GuildAuthService: User {AccountName} verified as OPUS guild member", _accountName);
            }
            else if (_isVerified)
            {
                Logger.Info("GuildAuthService: User {AccountName} verified but not an OPUS member", _accountName);
            }
        }

        private void ResetAuthState()
        {
            var previousIsOpusMember = _isOpusMember;

            _currentApiKey = null;
            _isVerified = false;
            _isOpusMember = false;
            _accountName = null;

            if (previousIsOpusMember != _isOpusMember)
            {
                OnAuthStatusChanged();
            }
        }

        public void ClearAuth()
        {
            ResetAuthState();
            Logger.Info("GuildAuthService: Authentication cleared");
        }

        private void OnAuthStatusChanged()
        {
            AuthStatusChanged?.Invoke(this, new GuildAuthStatusChangedEventArgs(_isOpusMember, _accountName));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class GuildAuthStatusChangedEventArgs : EventArgs
    {
        public bool IsOpusMember { get; }
        public string AccountName { get; }

        public GuildAuthStatusChangedEventArgs(bool isOpusMember, string accountName)
        {
            IsOpusMember = isOpusMember;
            AccountName = accountName;
        }
    }

    public class ApiKeyValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ApiKeyValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}
