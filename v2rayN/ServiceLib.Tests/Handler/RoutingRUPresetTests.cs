namespace ServiceLib.Tests.Handler;

[NotInParallel]
public class RoutingRUPresetTests
{
    private static void SetupDb()
    {
        TestDatabase.Initialize();
        SQLiteHelper.Instance.CreateTable<RoutingItem>();
    }

    private static async Task Cleanup()
    {
        foreach (var item in await ConfigDbQuery.RoutingItems())
        {
            await SQLiteHelper.Instance.DeleteAsync(item);
        }
    }

    private static async Task AssertAllRUPresets(IReadOnlyList<RoutingItem> items)
    {
        foreach (var item in items)
        {
            await item.Remarks.Should().Contain("RU");
        }
    }

    [Test]
    public async Task InitBuiltinRouting_CreatesOnlyRUPresetAndActivatesIt()
    {
        SetupDb();
        await Cleanup();

        await ConfigDbHandler.InitBuiltinRouting();

        var items = await ConfigDbQuery.RoutingItems();
        await items.Should().NotBeEmpty();
        await AssertAllRUPresets(items);

        var active = items.Where(t => t.IsActive).ToList();
        await active.Should().HaveCount(1);
        await active[0].Remarks.Should().Contain("RU");

        // Idempotent: re-initialization must not create duplicates.
        var count = items.Count;
        await ConfigDbHandler.InitBuiltinRouting();
        await (await ConfigDbQuery.RoutingItems()).Should().HaveCount(count);
    }

    [Test]
    public async Task InitBuiltinRouting_RemovesLegacyPresets()
    {
        SetupDb();
        await Cleanup();

        await SQLiteHelper.Instance.ReplaceAsync(new RoutingItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Remarks = "V4-Белый(Whitelist)",
            Url = string.Empty,
            Enabled = true,
        });
        await SQLiteHelper.Instance.ReplaceAsync(new RoutingItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Remarks = "V4-Чёрный(Blacklist)",
            Url = string.Empty,
            Enabled = true,
        });
        await SQLiteHelper.Instance.ReplaceAsync(new RoutingItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Remarks = "V4-Глобальный(Global)",
            Url = string.Empty,
            Enabled = true,
        });

        await ConfigDbHandler.InitBuiltinRouting();

        var items = await ConfigDbQuery.RoutingItems();
        await items.Should().NotBeEmpty();
        await AssertAllRUPresets(items);
        var ruRemarks = items.Select(t => t.Remarks).Where(r => r.Contains("RU")).ToList();
        await ruRemarks.Should().HaveCount(items.Count);
    }
}
