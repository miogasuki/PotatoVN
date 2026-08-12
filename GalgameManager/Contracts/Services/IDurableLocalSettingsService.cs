namespace GalgameManager.Contracts.Services;

/// <summary>
/// Provides a durable write boundary for critical large-setting state.
/// Kept separate from <see cref="ILocalSettingsService"/> so existing plugin
/// implementations and consumers retain the original interface contract.
/// </summary>
public interface IDurableLocalSettingsService
{
    Task SaveLargeSettingImmediatelyAsync<T>(string key, T value);
}
