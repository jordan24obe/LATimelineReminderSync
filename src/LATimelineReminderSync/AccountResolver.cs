using Microsoft.Extensions.Logging;

namespace LATimelineReminderSync;

/// <summary>
/// Resolves the WoW SavedVariables path by auto-discovering the account folder
/// under the WoW install directory.
/// </summary>
public static class AccountResolver
{
    private static readonly string RetailWtfAccountPath =
        Path.Combine("_retail_", "WTF", "Account");

    /// <summary>
    /// Resolves the full SavedVariables directory path for the WoW addon.
    /// </summary>
    /// <param name="wowInstallDir">Root WoW installation directory.</param>
    /// <param name="accountName">Optional explicit account name override.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Full path to the SavedVariables folder, or null on failure.</returns>
    public static string? Resolve(string wowInstallDir, string? accountName, ILogger logger)
    {
        var accountRoot = Path.Combine(wowInstallDir, RetailWtfAccountPath);

        if (!Directory.Exists(accountRoot))
        {
            logger.LogError(
                "WoW account directory does not exist: {AccountRoot}. " +
                "Verify that WoWInstallDir is correct and WoW has been launched at least once.",
                accountRoot);
            return null;
        }

        var accounts = Directory.GetDirectories(accountRoot)
            .Select(d => Path.GetFileName(d))
            .Where(name => !string.Equals(name, "SavedVariables", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (accounts.Count == 0)
        {
            logger.LogError(
                "No account folders found under {AccountRoot}. " +
                "Ensure WoW has been launched and logged in at least once.",
                accountRoot);
            return null;
        }

        string resolvedAccount;

        if (accounts.Count == 1)
        {
            resolvedAccount = accounts[0];
            logger.LogInformation("Auto-discovered WoW account: {Account}", resolvedAccount);
        }
        else if (!string.IsNullOrWhiteSpace(accountName))
        {
            var match = accounts.FirstOrDefault(a =>
                string.Equals(a, accountName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                logger.LogError(
                    "Configured AccountName '{AccountName}' not found. " +
                    "Available accounts: {Accounts}",
                    accountName, string.Join(", ", accounts));
                return null;
            }

            resolvedAccount = match;
            logger.LogInformation("Using configured WoW account: {Account}", resolvedAccount);
        }
        else
        {
            logger.LogError(
                "Multiple WoW accounts found but no AccountName configured. " +
                "Set AccountName in appsettings.json to one of: {Accounts}",
                string.Join(", ", accounts));
            return null;
        }

        return Path.Combine(accountRoot, resolvedAccount, "SavedVariables");
    }
}
