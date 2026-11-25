namespace ServiceLib.Tests.Handler;

public class ForkMigrationsTests
{
    private const string RuPresetRemarks = "V4-Обход России(RU direct)";

    [Test]
    public async Task ApplyFirstRunDefaults_UsesRussiaRegionalPresetSources()
    {
        var config = new Config();

        ForkMigrations.ApplyFirstRunDefaults(config);

        await config.ConstItem.Should().NotBeNull();
        await config.ConstItem.GeoSourceUrl.Should().BeEqualTo(Global.GeoFilesSources[1]);
        await config.ConstItem.SrsSourceUrl.Should().BeEqualTo(Global.SingboxRulesetSources[1]);
        await config.ConstItem.RouteRulesTemplateSourceUrl.Should().BeEqualTo(Global.RoutingRulesSources[1]);
        // First run still goes through the one-time migration (it is idempotent for new configs).
        await config.ConstItem.ForkMigrationVersion.Should().BeEqualTo(0);
    }

    [Test]
    public async Task SetRegionalPresetSources_DefaultClearsSources()
    {
        var constItem = new ConstItem();
        ForkMigrations.SetRegionalPresetSources(constItem, EPresetType.Russia);

        ForkMigrations.SetRegionalPresetSources(constItem, EPresetType.Default);

        await constItem.GeoSourceUrl.Should().BeEqualTo(string.Empty);
        await constItem.SrsSourceUrl.Should().BeEqualTo(string.Empty);
        await constItem.RouteRulesTemplateSourceUrl.Should().BeEqualTo(string.Empty);
    }

    [Test]
    public async Task MigrateAsync_UpgradeFromUpstream_AppliesStealthDefaultsAndAddsRuPresetOnce()
    {
        var config = CreateUpstreamConfig(fingerprint: string.Empty, userAgent: null, enableFragment: false);
        var routingItems = new List<RoutingItem>
        {
            new() { Id = "white", Remarks = "V4-绕过大陆(Whitelist)", Sort = 1, IsActive = true },
            new() { Id = "global", Remarks = "V4-全局(Global)", Sort = 2 },
        };
        var addedSorts = new List<int>();

        var changed = await ForkMigrations.MigrateAsync(
            config,
            () => Task.FromResult<IReadOnlyList<RoutingItem>>(routingItems),
            sort =>
            {
                addedSorts.Add(sort);
                routingItems.Add(new RoutingItem { Remarks = RuPresetRemarks, Sort = sort });
                return Task.CompletedTask;
            });

        await changed.Should().BeTrue();
        await config.CoreBasicItem.DefFingerprint.Should().BeEqualTo("chrome");
        await config.CoreBasicItem.DefUserAgent.Should().BeEqualTo("chrome");
        await config.CoreBasicItem.EnableFragment.Should().BeTrue();
        await config.ConstItem.ForkMigrationVersion.Should().BeEqualTo(ForkMigrations.CurrentVersion);
        await addedSorts.Should().HaveCount(1);
        await addedSorts[0].Should().BeEqualTo(3);

        // The user turns fragmentation off and deletes the RU preset: later starts keep that.
        config.CoreBasicItem.EnableFragment = false;
        routingItems.RemoveAll(item => item.Remarks == RuPresetRemarks);

        var changedAgain = await ForkMigrations.MigrateAsync(
            config,
            () => Task.FromResult<IReadOnlyList<RoutingItem>>(routingItems),
            sort =>
            {
                addedSorts.Add(sort);
                return Task.CompletedTask;
            });

        await changedAgain.Should().BeFalse();
        await config.CoreBasicItem.EnableFragment.Should().BeFalse();
        await addedSorts.Should().HaveCount(1);
    }

    [Test]
    public async Task MigrateAsync_KeepsUserChosenFingerprintAndUserAgent()
    {
        var config = CreateUpstreamConfig(fingerprint: "firefox", userAgent: "safari", enableFragment: false);

        var changed = await ForkMigrations.MigrateAsync(
            config,
            () => Task.FromResult<IReadOnlyList<RoutingItem>>([]),
            _ => Task.CompletedTask);

        await changed.Should().BeTrue();
        await config.CoreBasicItem.DefFingerprint.Should().BeEqualTo("firefox");
        await config.CoreBasicItem.DefUserAgent.Should().BeEqualTo("safari");
        await config.CoreBasicItem.EnableFragment.Should().BeTrue();
    }

    [Test]
    public async Task MigrateAsync_DoesNotChangeRoutingOrSourcesOfExistingInstall()
    {
        var config = CreateUpstreamConfig(fingerprint: "chrome", userAgent: "chrome", enableFragment: true);
        var routingItems = new List<RoutingItem>
        {
            new() { Id = "ru", Remarks = RuPresetRemarks, Sort = 1 },
            new() { Id = "user", Remarks = "my route", Sort = 2, IsActive = true },
        };
        var added = false;

        await ForkMigrations.MigrateAsync(
            config,
            () => Task.FromResult<IReadOnlyList<RoutingItem>>(routingItems),
            _ =>
            {
                added = true;
                return Task.CompletedTask;
            });

        await added.Should().BeFalse();
        await routingItems[1].IsActive.Should().BeTrue();
        await config.ConstItem.GeoSourceUrl.Should().BeNull();
        await config.ConstItem.SrsSourceUrl.Should().BeNull();
        await config.ConstItem.RouteRulesTemplateSourceUrl.Should().BeNull();
    }

    [Test]
    public async Task NeedsRuRoutingPreset_OnlyForPopulatedTablesWithoutRuPreset()
    {
        await ForkMigrations.NeedsRuRoutingPreset([]).Should().BeFalse();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = "legacy", Locked = true }]).Should().BeFalse();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = "R1-" + RuPresetRemarks[3..] }]).Should().BeFalse();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = "V4-全局(Global)" }]).Should().BeTrue();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = null! }]).Should().BeTrue();
    }

    private static Config CreateUpstreamConfig(string? fingerprint, string? userAgent, bool enableFragment) => new()
    {
        CoreBasicItem = new CoreBasicItem
        {
            Loglevel = "warning",
            DefFingerprint = fingerprint!,
            DefUserAgent = userAgent!,
            EnableFragment = enableFragment,
        },
        ConstItem = new ConstItem(),
    };
}
