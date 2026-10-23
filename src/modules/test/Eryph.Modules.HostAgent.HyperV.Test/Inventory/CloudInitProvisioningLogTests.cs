using Eryph.Modules.HostAgent.Inventory;

namespace Eryph.Modules.HostAgent.HyperV.Test.Inventory;

public class CloudInitProvisioningLogTests
{
    private const long Incarnation = 1_700_000_000;

    [Fact]
    public void Decode_OrdersEventsByTimestampAndRendersResults()
    {
        var kvp = new Dictionary<string, string>
        {
            [Key("finish", "modules-config/set_hostname", "aaaaaaaa-0000-0000-0000-000000000001")] =
                Value("modules-config/set_hostname", "finish", "2026-07-13T10:11:14+00:00", "SUCCESS", "done"),
            [Key("start", "init-network", "aaaaaaaa-0000-0000-0000-000000000002")] =
                Value("init-network", "start", "2026-07-13T10:11:12+00:00"),
            [Key("start", "modules-config/set_hostname", "aaaaaaaa-0000-0000-0000-000000000003")] =
                Value("modules-config/set_hostname", "start", "2026-07-13T10:11:13+00:00"),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events.Select(e => e.Name).Should().ContainInOrder(
            "init-network", "modules-config/set_hostname", "modules-config/set_hostname");
        result.Events[0].Type.Should().Be("start");
        result.Events[2].Type.Should().Be("finish");
        result.Events[2].Result.Should().Be("SUCCESS");
        result.Events[2].Message.Should().Be("done");
    }

    [Fact]
    public void Decode_ReassemblesSplitMessageByUuidAndMsgIndex()
    {
        var uuid = "bbbbbbbb-0000-0000-0000-000000000001";
        var kvp = new Dictionary<string, string>
        {
            // Chunks are intentionally out of order to prove ordering by msg_i.
            [Key("finish", "modules-config/runcmd", uuid) + "|1"] =
                ValueChunk("modules-config/runcmd", "finish", "2026-07-13T10:11:20+00:00", "FAIL", 1, "world!"),
            [Key("finish", "modules-config/runcmd", uuid) + "|0"] =
                ValueChunk("modules-config/runcmd", "finish", "2026-07-13T10:11:20+00:00", "FAIL", 0, "Hello, "),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events.Should().ContainSingle();
        result.Events[0].Message.Should().Be("Hello, world!");
        result.Events[0].Result.Should().Be("FAIL");
    }

    [Fact]
    public void Decode_KeepsOnlyLatestIncarnation()
    {
        var kvp = new Dictionary<string, string>
        {
            [$"CLOUD_INIT|{Incarnation}|start|init-local|cccccccc-0000-0000-0000-000000000001"] =
                Value("init-local", "start", "2026-07-13T09:00:00+00:00"),
            [$"CLOUD_INIT|{Incarnation + 10}|start|init-local|cccccccc-0000-0000-0000-000000000002"] =
                Value("init-local", "start", "2026-07-13T10:00:00+00:00"),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events.Should().ContainSingle();
        result.Events[0].Incarnation.Should().Be(Incarnation + 10);
    }

    [Fact]
    public void Decode_RendersFailureLineWithMessage()
    {
        var kvp = new Dictionary<string, string>
        {
            [Key("finish", "modules-config/runcmd", "dddddddd-0000-0000-0000-000000000001")] =
                Value("modules-config/runcmd", "finish", "2026-07-13T10:11:20+00:00", "FAIL", "boom"),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.RenderedText.Should().Contain("modules-config/runcmd: finish FAIL - boom");
    }

    [Fact]
    public void Decode_TreatsOffsetLessTimestampAsUtc()
    {
        var kvp = new Dictionary<string, string>
        {
            // cloud-init may emit a ts without an explicit offset; it must be read
            // as UTC, not shifted by the host's local offset.
            [Key("start", "init-local", "ffffffff-0000-0000-0000-000000000001")] =
                Value("init-local", "start", "2026-07-13T10:11:12.500000"),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events[0].Timestamp.Should().Be(
            new DateTimeOffset(2026, 7, 13, 10, 11, 12, 500, TimeSpan.Zero));
        result.RenderedText.Should().Contain("2026-07-13T10:11:12.500000+00:00");
    }

    [Fact]
    public void Decode_NormalizesTimestampWithNonUtcOffsetToUtc()
    {
        var kvp = new Dictionary<string, string>
        {
            [Key("start", "init-local", "ffffffff-0000-0000-0000-000000000002")] =
                Value("init-local", "start", "2026-07-13T12:11:12+02:00"),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events[0].Timestamp.Should().Be(
            new DateTimeOffset(2026, 7, 13, 10, 11, 12, TimeSpan.Zero));
    }

    [Fact]
    public void Decode_IgnoresUnrelatedKeysAndMalformedJson()
    {
        var kvp = new Dictionary<string, string>
        {
            ["eryph.provisioning.state"] = "completed",
            [Key("start", "init-local", "eeeeeeee-0000-0000-0000-000000000001")] = "{ not valid json",
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events.Should().BeEmpty();
        result.RenderedText.Should().BeEmpty();
    }

    [Fact]
    public void Decode_CloudInit26KeysWithVmId_KeepsEventsSeparate()
    {
        // Captured from an Ubuntu 24.04 guest (cloud-init 26.1). The key carries
        // the VM id (SMBIOS uuid) before the event uuid; every event shares it.
        const string vmId = "fc4c5ccb-f6b5-4539-afa8-f1d4123491c9";
        var kvp = new Dictionary<string, string>
        {
            [$"CLOUD_INIT|1792004908|start|init-local/check-cache|{vmId}|31de84f8-465d-4e89-ba9e-89d69ea15e16"] =
                """{"name":"init-local/check-cache","type":"start","ts":"2026-10-14T19:08:29.675059+00:00","msg":"attempting to read from cache [check]"}""",
            [$"CLOUD_INIT|1792004908|finish|init-local/check-cache|{vmId}|fef511f0-5f7b-4ba2-a3f3-64f8b59e9cfa"] =
                """{"name":"init-local/check-cache","type":"finish","ts":"2026-10-14T19:08:29.675292+00:00","result":"SUCCESS","duration":0.0002322999999999631,"msg":"no cache found"}""",
            [$"CLOUD_INIT|1792004908|finish|init-local/search-NoCloud|{vmId}|d1a2feef-30da-47f8-8f8d-892b92c18952"] =
                """{"name":"init-local/search-NoCloud","type":"finish","ts":"2026-10-14T19:08:29.751296+00:00","result":"SUCCESS","duration":0.06702149999999985,"msg":"found local data from DataSourceNoCloud"}""",
            [$"CLOUD_INIT|1792004908|finish|modules-final|{vmId}|34b53da5-10ba-480f-81cc-5848c5ecd5ce"] =
                """{"name":"modules-final","type":"finish","ts":"2026-10-14T19:08:34.684982+00:00","result":"SUCCESS","duration":0.08474350000000008,"msg":"running modules for final"}""",
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events.Select(e => e.Message).Should().Equal(
            "attempting to read from cache [check]",
            "no cache found",
            "found local data from DataSourceNoCloud",
            "running modules for final");
        result.RenderedText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(4);
    }

    [Fact]
    public void Decode_CloudInit26SplitMessage_ReassemblesByEventUuid()
    {
        const string vmId = "fc4c5ccb-f6b5-4539-afa8-f1d4123491c9";
        var baseKey = $"CLOUD_INIT|{Incarnation}|finish|modules-config/runcmd|{vmId}|bbbbbbbb-0000-0000-0000-000000000002";
        var kvp = new Dictionary<string, string>
        {
            [baseKey + "|1"] =
                ValueChunk("modules-config/runcmd", "finish", "2026-07-13T10:11:20+00:00", "FAIL", 1, "world!"),
            [baseKey + "|0"] =
                ValueChunk("modules-config/runcmd", "finish", "2026-07-13T10:11:20+00:00", "FAIL", 0, "Hello, "),
            [$"CLOUD_INIT|{Incarnation}|start|modules-config/runcmd|{vmId}|bbbbbbbb-0000-0000-0000-000000000003"] =
                Value("modules-config/runcmd", "start", "2026-07-13T10:11:19+00:00"),
        };

        var result = CloudInitProvisioningLog.Decode(kvp);

        result.Events.Should().HaveCount(2);
        result.Events[1].Message.Should().Be("Hello, world!");
    }

    private static string Key(string type, string name, string uuid) =>
        $"CLOUD_INIT|{Incarnation}|{type}|{name}|{uuid}";

    private static string Value(
        string name, string type, string ts, string? result = null, string? msg = null)
    {
        var fields = new List<string>
        {
            $"\"name\":\"{name}\"",
            $"\"type\":\"{type}\"",
            $"\"ts\":\"{ts}\"",
        };
        if (result is not null)
            fields.Add($"\"result\":\"{result}\"");
        if (msg is not null)
            fields.Add($"\"msg\":\"{msg}\"");
        return "{" + string.Join(",", fields) + "}";
    }

    private static string ValueChunk(
        string name, string type, string ts, string result, int msgIndex, string msg) =>
        $"{{\"name\":\"{name}\",\"type\":\"{type}\",\"ts\":\"{ts}\",\"result\":\"{result}\",\"msg_i\":{msgIndex},\"msg\":\"{msg}\"}}";
}
