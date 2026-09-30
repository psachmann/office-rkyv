namespace OfficeRkyv.Desktop.Hosting;

/// <summary>
/// Where the desktop app keeps things, by the XDG base directory rules: data (the database), config (the
/// settings file) and the runtime directory (the API's socket). A variable that is unset, empty or not an
/// absolute path is ignored, as the specification says.
/// </summary>
public sealed record DesktopPaths(string DataDirectory, string ConfigDirectory, string RuntimeDirectory)
{
    private const string AppDirectory = "office-rkyv";

    public string DatabasePath => Path.Combine(DataDirectory, "office-rkyv.db");

    public string SettingsPath => Path.Combine(ConfigDirectory, "settings.json");

    public string SocketPath => Path.Combine(RuntimeDirectory, "api.sock");

    public static DesktopPaths FromEnvironment() =>
        Resolve(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <param name="environment">Looks up an environment variable.</param>
    /// <param name="home">The user's home directory.</param>
    public static DesktopPaths Resolve(Func<string, string?> environment, string home)
    {
        string Base(string variable, string fallback) =>
            environment(variable) is { Length: > 0 } value && Path.IsPathRooted(value) ? value : fallback;

        // XDG_RUNTIME_DIR can be missing (no login session). Then a per-user directory under the temp path
        // stands in, which is safe because the directory is created readable by its owner only.
        var runtime = Base(
            "XDG_RUNTIME_DIR",
            Path.Combine(Path.GetTempPath(), $"{AppDirectory}-{Environment.UserName}")
        );

        return new DesktopPaths(
            Path.Combine(Base("XDG_DATA_HOME", Path.Combine(home, ".local", "share")), AppDirectory),
            Path.Combine(Base("XDG_CONFIG_HOME", Path.Combine(home, ".config")), AppDirectory),
            Path.Combine(runtime, AppDirectory)
        );
    }
}
