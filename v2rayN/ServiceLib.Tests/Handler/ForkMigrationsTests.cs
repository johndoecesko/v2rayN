namespace ServiceLib.Tests.Handler;

public class ForkMigrationsTests
{
    private const string RuPresetRemarks = ForkMigrations.RuRoutingRemarks;

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
        var store = new FakeRoutingStore(
            new() { Id = "white", Remarks = ForkMigrations.LegacyWhitelistRoutingRemarks, Sort = 1, IsActive = true },
            new() { Id = "global", Remarks = ForkMigrations.LegacyGlobalRoutingRemarks, Sort = 2 });

        var changed = await store.MigrateAsync(config);

        await changed.Should().BeTrue();
        await config.CoreBasicItem.DefFingerprint.Should().BeEqualTo("chrome");
        await config.CoreBasicItem.DefUserAgent.Should().BeEqualTo("chrome");
        await config.CoreBasicItem.EnableFragment.Should().BeTrue();
        await config.ConstItem.ForkMigrationVersion.Should().BeEqualTo(ForkMigrations.CurrentVersion);
        await store.AddedSorts.Should().HaveCount(1);
        await store.AddedSorts[0].Should().BeEqualTo(3);
        // Step 2 ran too: the active whitelist is gone and the RU preset added by step 1 is default.
        await store.Remarks().SequenceEqual(new[] { ForkMigrations.GlobalRoutingRemarks, RuPresetRemarks }).Should().BeTrue();
        await store.ActiveRemarks().Should().BeEqualTo(RuPresetRemarks);

        // The user turns fragmentation off and deletes the RU preset: later starts keep that.
        config.CoreBasicItem.EnableFragment = false;
        store.Items.RemoveAll(item => item.Remarks == RuPresetRemarks);

        var changedAgain = await store.MigrateAsync(config);

        await changedAgain.Should().BeFalse();
        await config.CoreBasicItem.EnableFragment.Should().BeFalse();
        await store.AddedSorts.Should().HaveCount(1);
    }

    [Test]
    public async Task MigrateAsync_KeepsUserChosenFingerprintAndUserAgent()
    {
        var config = CreateUpstreamConfig(fingerprint: "firefox", userAgent: "safari", enableFragment: false);
        var store = new FakeRoutingStore();

        var changed = await store.MigrateAsync(config);

        await changed.Should().BeTrue();
        await config.CoreBasicItem.DefFingerprint.Should().BeEqualTo("firefox");
        await config.CoreBasicItem.DefUserAgent.Should().BeEqualTo("safari");
        await config.CoreBasicItem.EnableFragment.Should().BeTrue();
    }

    [Test]
    public async Task MigrateAsync_FreshDatabase_LeavesRoutingToInitBuiltinRouting()
    {
        var config = new Config();
        ForkMigrations.ApplyFirstRunDefaults(config);
        var store = new FakeRoutingStore();

        var changed = await store.MigrateAsync(config);

        await changed.Should().BeTrue();
        await config.ConstItem.ForkMigrationVersion.Should().BeEqualTo(ForkMigrations.CurrentVersion);
        await store.Items.Should().HaveCount(0);
        await store.AddedSorts.Should().HaveCount(0);
        await store.RemovedIds.Should().HaveCount(0);
        await store.SavedIds.Should().HaveCount(0);
    }

    [Test]
    public async Task MigrateAsync_DoesNotChangeRoutingOrSourcesOfExistingInstall()
    {
        var config = CreateUpstreamConfig(fingerprint: "chrome", userAgent: "chrome", enableFragment: true);
        var store = new FakeRoutingStore(
            new() { Id = "ru", Remarks = RuPresetRemarks, Sort = 1 },
            new() { Id = "user", Remarks = "my route", Sort = 2, IsActive = true });

        await store.MigrateAsync(config);

        await store.AddedSorts.Should().HaveCount(0);
        await store.RemovedIds.Should().HaveCount(0);
        await store.SavedIds.Should().HaveCount(0);
        await store.ActiveRemarks().Should().BeEqualTo("my route");
        await config.ConstItem.GeoSourceUrl.Should().BeNull();
        await config.ConstItem.SrsSourceUrl.Should().BeNull();
        await config.ConstItem.RouteRulesTemplateSourceUrl.Should().BeNull();
    }

    [Test]
    public async Task MigrateAsync_Step2_RemovesChinaPresetsKeepsActiveUserRouteAndRenamesGlobal()
    {
        var config = CreateMigratedConfig(version: 1);
        var store = new FakeRoutingStore(
            new() { Id = "white", Remarks = ForkMigrations.LegacyWhitelistRoutingRemarks, Sort = 1 },
            new() { Id = "black", Remarks = ForkMigrations.LegacyBlacklistRoutingRemarks, Sort = 2 },
            new() { Id = "global", Remarks = ForkMigrations.LegacyGlobalRoutingRemarks, Sort = 3 },
            new() { Id = "ru", Remarks = RuPresetRemarks, Sort = 4 },
            new() { Id = "rf", Remarks = "RUv1-Все кроме РФ", Sort = 5, IsActive = true },
            new() { Id = "own", Remarks = "V4-绕过大陆(Whitelist) copy", Sort = 6 });

        var changed = await store.MigrateAsync(config);

        await changed.Should().BeTrue();
        await config.ConstItem.ForkMigrationVersion.Should().BeEqualTo(2);
        await store.RemovedIds.SequenceEqual(new[] { "white", "black" }).Should().BeTrue();
        await store.SavedIds.SequenceEqual(new[] { "global" }).Should().BeTrue();
        await store.DefaultedIds.Should().HaveCount(0);
        await store.AddedSorts.Should().HaveCount(0);
        await store.Remarks().SequenceEqual(new[]
        {
            ForkMigrations.GlobalRoutingRemarks,
            RuPresetRemarks,
            "RUv1-Все кроме РФ",
            "V4-绕过大陆(Whitelist) copy",
        }).Should().BeTrue();
        await store.ActiveRemarks().Should().BeEqualTo("RUv1-Все кроме РФ");
    }

    [Test]
    public async Task MigrateAsync_Step2_ActiveBlacklistMakesExistingRuPresetDefault()
    {
        var config = CreateMigratedConfig(version: 1);
        var store = new FakeRoutingStore(
            new() { Id = "black", Remarks = ForkMigrations.LegacyBlacklistRoutingRemarks, Sort = 1, IsActive = true },
            new() { Id = "ru", Remarks = RuPresetRemarks, Sort = 2 },
            new() { Id = "global", Remarks = ForkMigrations.GlobalRoutingRemarks, Sort = 3 });

        await store.MigrateAsync(config);

        await store.DefaultedIds.SequenceEqual(new[] { "ru" }).Should().BeTrue();
        await store.RemovedIds.SequenceEqual(new[] { "black" }).Should().BeTrue();
        await store.AddedSorts.Should().HaveCount(0);
        await store.SavedIds.Should().HaveCount(0);
        await store.ActiveRemarks().Should().BeEqualTo(RuPresetRemarks);
    }

    [Test]
    public async Task MigrateAsync_Step2_RenamesRuPresetFromVersion801()
    {
        var config = CreateMigratedConfig(version: 1);
        var store = new FakeRoutingStore(
            new() { Id = "ru", Remarks = ForkMigrations.LegacyRuRoutingRemarks, Sort = 1, IsActive = true },
            new() { Id = "global", Remarks = ForkMigrations.GlobalRoutingRemarks, Sort = 2 });

        await store.MigrateAsync(config);

        await store.AddedSorts.Should().HaveCount(0);
        await store.DefaultedIds.Should().HaveCount(0);
        await store.ActiveRemarks().Should().BeEqualTo(RuPresetRemarks);
    }

    [Test]
    public async Task MigrateAsync_Step2_ActiveWhitelistWithoutRuPresetAddsItAsDefault()
    {
        var config = CreateMigratedConfig(version: 1);
        var store = new FakeRoutingStore(
            new() { Id = "white", Remarks = ForkMigrations.LegacyWhitelistRoutingRemarks, Sort = 1, IsActive = true },
            new() { Id = "global", Remarks = ForkMigrations.LegacyGlobalRoutingRemarks, Sort = 2 });

        await store.MigrateAsync(config);

        await store.AddedSorts.SequenceEqual(new[] { 3 }).Should().BeTrue();
        await store.RemovedIds.SequenceEqual(new[] { "white" }).Should().BeTrue();
        await store.Remarks().SequenceEqual(new[] { ForkMigrations.GlobalRoutingRemarks, RuPresetRemarks }).Should().BeTrue();
        await store.ActiveRemarks().Should().BeEqualTo(RuPresetRemarks);

        // Already at the current version: nothing runs again.
        var changedAgain = await store.MigrateAsync(config);

        await changedAgain.Should().BeFalse();
        await store.AddedSorts.Should().HaveCount(1);
    }

    [Test]
    public async Task NeedsRuRoutingPreset_OnlyForPopulatedTablesWithoutRuPreset()
    {
        await ForkMigrations.NeedsRuRoutingPreset([]).Should().BeFalse();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = "legacy", Locked = true }]).Should().BeFalse();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = "R1-" + RuPresetRemarks[3..] }]).Should().BeFalse();
        await ForkMigrations.NeedsRuRoutingPreset([new RoutingItem { Remarks = ForkMigrations.LegacyGlobalRoutingRemarks }]).Should().BeTrue();
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

    private static Config CreateMigratedConfig(int version) => new()
    {
        CoreBasicItem = new CoreBasicItem { DefFingerprint = "chrome", DefUserAgent = "chrome", EnableFragment = true },
        ConstItem = new ConstItem { ForkMigrationVersion = version },
    };

    /// <summary>
    /// In-memory routing table: loads return copies (like the database), so changes only
    /// persist through the save/remove/set-default delegates.
    /// </summary>
    private sealed class FakeRoutingStore(params RoutingItem[] items)
    {
        public List<RoutingItem> Items { get; } = [.. items];
        public List<int> AddedSorts { get; } = [];
        public List<string> RemovedIds { get; } = [];
        public List<string> SavedIds { get; } = [];
        public List<string> DefaultedIds { get; } = [];

        public Task<bool> MigrateAsync(Config config) => ForkMigrations.MigrateAsync(
            config,
            () => Task.FromResult<IReadOnlyList<RoutingItem>>(Items.Select(Copy).ToList()),
            sort =>
            {
                AddedSorts.Add(sort);
                Items.Add(new RoutingItem { Id = $"added-{sort}", Remarks = RuPresetRemarks, Sort = sort });
                return Task.CompletedTask;
            },
            item =>
            {
                RemovedIds.Add(item.Id);
                Items.RemoveAll(stored => stored.Id == item.Id);
                return Task.CompletedTask;
            },
            item =>
            {
                SavedIds.Add(item.Id);
                var index = Items.FindIndex(stored => stored.Id == item.Id);
                Items[index] = Copy(item);
                return Task.CompletedTask;
            },
            item =>
            {
                DefaultedIds.Add(item.Id);
                foreach (var stored in Items)
                {
                    stored.IsActive = stored.Id == item.Id;
                }
                return Task.CompletedTask;
            });

        public List<string> Remarks() => Items.OrderBy(item => item.Sort).Select(item => item.Remarks).ToList();

        public string? ActiveRemarks() => Items.SingleOrDefault(item => item.IsActive)?.Remarks;

        private static RoutingItem Copy(RoutingItem item) => new()
        {
            Id = item.Id,
            Remarks = item.Remarks,
            Sort = item.Sort,
            IsActive = item.IsActive,
            Locked = item.Locked,
        };
    }
}
