namespace ServiceLib.Handler.Data;

/// <summary>
/// First-run defaults and one-time, versioned migrations for settings this fork
/// (targeting users in Russia) changes relative to upstream v2rayN.
/// The applied step is persisted in <see cref="ConstItem.ForkMigrationVersion"/>, so each
/// step runs once per config and later user changes (e.g. turning fragmentation off or
/// deleting the RU routing preset) are never overridden.
/// </summary>
public static class ForkMigrations
{
    /// <summary>
    /// Latest migration step. Bump it and add a step to <see cref="MigrateAsync"/> when
    /// a new one-time migration is needed.
    /// </summary>
    public const int CurrentVersion = 1;

    public const string ChromeFingerprint = "chrome";
    public const string ChromeUserAgent = "chrome";

    /// <summary>
    /// Brand-new install (no config file yet): behave as if the user had picked the Russia
    /// regional preset. Only the source URLs are set here: geo files and sing-box rule sets come
    /// from runetfreedom and "import advanced rules" uses the runetfreedom routing template.
    /// Nothing is downloaded, so first startup works offline; the routing itself is seeded by
    /// <see cref="ConfigDbHandler.InitBuiltinRouting"/> with the embedded RU preset as default,
    /// and the builtin DNS (Yandex-based direct/bootstrap) is kept instead of the DNS templates.
    /// </summary>
    public static void ApplyFirstRunDefaults(Config config)
    {
        config.ConstItem ??= new ConstItem();
        SetRegionalPresetSources(config.ConstItem, EPresetType.Russia);
    }

    /// <summary>
    /// Sets the geo/rule-set/routing-template source URLs exactly as
    /// <see cref="ConfigDbHandler.ApplyRegionalPreset"/> does for the given preset.
    /// </summary>
    public static void SetRegionalPresetSources(ConstItem constItem, EPresetType type)
    {
        var index = type switch
        {
            EPresetType.Russia => 1,
            EPresetType.Iran => 2,
            _ => 0,
        };
        constItem.GeoSourceUrl = Global.GeoFilesSources[index];
        constItem.SrsSourceUrl = Global.SingboxRulesetSources[index];
        constItem.RouteRulesTemplateSourceUrl = Global.RoutingRulesSources[index];
    }

    /// <summary>
    /// Runs the pending one-time migration steps and records the applied version.
    /// </summary>
    /// <param name="config">Loaded configuration.</param>
    /// <param name="loadRoutingItems">Loads the stored routing items.</param>
    /// <param name="addRuRoutingItem">Adds the RU routing preset with the given sort order.</param>
    /// <returns>True when the config changed and must be saved.</returns>
    public static async Task<bool> MigrateAsync(
        Config config,
        Func<Task<IReadOnlyList<RoutingItem>>> loadRoutingItems,
        Func<int, Task> addRuRoutingItem)
    {
        config.ConstItem ??= new ConstItem();
        var fromVersion = config.ConstItem.ForkMigrationVersion;
        if (fromVersion >= CurrentVersion)
        {
            return false;
        }

        if (fromVersion < 1)
        {
            // Step 1: Variant C stealth defaults for users upgrading from upstream.
            config.CoreBasicItem ??= new CoreBasicItem();
            ApplyStealthDefaults(config.CoreBasicItem);

            // ... and the RU routing preset (not made default: the active route is kept).
            // An empty table is a fresh database: InitBuiltinRouting seeds it, RU preset included.
            var routingItems = await loadRoutingItems();
            if (NeedsRuRoutingPreset(routingItems))
            {
                await addRuRoutingItem(routingItems.Count + 1);
            }
        }

        config.ConstItem.ForkMigrationVersion = CurrentVersion;
        return true;
    }

    /// <summary>
    /// Migration step 1 for core settings: Chrome TLS fingerprint and User-Agent only where the
    /// user has none, and TLS-ClientHello fragmentation enabled.
    /// </summary>
    public static void ApplyStealthDefaults(CoreBasicItem coreBasicItem)
    {
        if (coreBasicItem.DefFingerprint.IsNullOrEmpty())
        {
            coreBasicItem.DefFingerprint = ChromeFingerprint;
        }
        if (coreBasicItem.DefUserAgent.IsNullOrEmpty())
        {
            coreBasicItem.DefUserAgent = ChromeUserAgent;
        }
        coreBasicItem.EnableFragment = true;
    }

    /// <summary>
    /// True when the database already has routing items (locked legacy items, which
    /// InitBuiltinRouting removes, do not count) but none of them is the RU preset.
    /// </summary>
    public static bool NeedsRuRoutingPreset(IReadOnlyList<RoutingItem> routingItems)
    {
        return routingItems.Any(item => !item.Locked)
            && !routingItems.Any(item => item.Remarks?.Contains(ConfigDbHandler.RuRoutingRemarksTag) == true);
    }
}
