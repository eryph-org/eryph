using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Eryph.Resources.Machines;

namespace Eryph.Modules.HostAgent.Inventory;

/// <summary>
/// Decodes the guest's cloud-init provisioning telemetry from its Hyper-V KVP
/// pool. The guest writes events under keys of the form
/// <c>CLOUD_INIT|&lt;incarnation&gt;|&lt;type&gt;|&lt;name&gt;|&lt;uuid&gt;</c>
/// (matching cloud-init's <c>HyperVKvpReportingHandler</c>), splitting oversized
/// values across <c>…|&lt;index&gt;</c> subkeys. This decoder reassembles those
/// chunks (grouping by uuid, ordering by <c>msg_i</c>), keeps only the current
/// boot (the highest incarnation), and produces both the reassembled events and a
/// rendered, human-readable text log.
/// </summary>
internal static class CloudInitProvisioningLog
{
    private const string KeyPrefix = "CLOUD_INIT";

    public static ProvisioningLogResult Decode(IReadOnlyDictionary<string, string> guestData)
    {
        // Group all entries by their event uuid so split messages reassemble.
        var groups = new Dictionary<string, List<RawEntry>>(StringComparer.Ordinal);
        foreach (var (key, value) in guestData)
        {
            var entry = ParseEntry(key, value);
            if (entry is null)
                continue;

            if (!groups.TryGetValue(entry.Uuid, out var list))
            {
                list = [];
                groups[entry.Uuid] = list;
            }

            list.Add(entry);
        }

        var events = groups.Values
            .Select(ToEvent)
            .ToList();

        // Keep only the current boot's events (the highest incarnation).
        if (events.Count > 0)
        {
            var latestIncarnation = events.Max(e => e.Incarnation);
            events = events.Where(e => e.Incarnation == latestIncarnation).ToList();
        }

        var orderedEvents = events
            .OrderBy(e => e.Timestamp ?? DateTimeOffset.MaxValue)
            // A start and its finish can share a timestamp; render start first.
            .ThenBy(e => string.Equals(e.Type, "finish", StringComparison.Ordinal) ? 1 : 0)
            // Deterministic tiebreak so events with equal/absent timestamps do not
            // fall back to (unspecified) dictionary enumeration order.
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();

        return new ProvisioningLogResult(orderedEvents, Render(orderedEvents));
    }

    private static ProvisioningLogEvent ToEvent(List<RawEntry> chunks)
    {
        // Reassemble the message from the chunks ordered by their msg_i index
        // (a single, non-split entry has index 0).
        var ordered = chunks.OrderBy(c => c.MsgIndex ?? 0).ToList();
        var message = string.Concat(ordered.Select(c => c.Message ?? ""));
        var first = ordered[0];

        return new ProvisioningLogEvent
        {
            Incarnation = first.Incarnation,
            Name = first.Name,
            Type = first.Type,
            Result = first.Result,
            Message = string.IsNullOrEmpty(message) ? null : message,
            Timestamp = first.Timestamp,
        };
    }

    private static RawEntry? ParseEntry(string key, string value)
    {
        // CLOUD_INIT|<incarnation>|<type>|<name>|<uuid>[|<index>]
        // Names never contain '|', so a simple split is unambiguous.
        var segments = key.Split('|');
        if (segments.Length is < 5 or > 6)
            return null;
        if (!string.Equals(segments[0], KeyPrefix, StringComparison.Ordinal))
            return null;
        if (!long.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var incarnation))
            return null;

        var uuid = segments[4];

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(value);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        if (root.ValueKind is not JsonValueKind.Object)
            return null;

        return new RawEntry(
            Incarnation: incarnation,
            Uuid: uuid,
            Name: GetString(root, "name") ?? segments[3],
            Type: GetString(root, "type") ?? segments[2],
            Result: GetString(root, "result"),
            Message: GetString(root, "msg"),
            MsgIndex: GetInt(root, "msg_i"),
            Timestamp: ParseTimestamp(GetString(root, "ts")));
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int? GetInt(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var prop)
        && prop.ValueKind is JsonValueKind.Number
        && prop.TryGetInt32(out var value)
            ? value
            : null;

    private static DateTimeOffset? ParseTimestamp(string? ts) =>
        !string.IsNullOrEmpty(ts)
        // A ts with an explicit offset is honoured and normalized to UTC; a ts
        // without one is assumed UTC (never host-local) so Render's ToUniversalTime
        // cannot shift it by the host's offset.
        && DateTimeOffset.TryParse(
            ts, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static string Render(IReadOnlyList<ProvisioningLogEvent> events)
    {
        var builder = new StringBuilder();
        foreach (var e in events)
        {
            var timestamp = e.Timestamp?.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.ffffffK", CultureInfo.InvariantCulture);

            builder.Append(timestamp ?? "(no timestamp)");
            builder.Append("  ");
            builder.Append(e.Name);
            builder.Append(": ");
            builder.Append(e.Type);
            if (!string.IsNullOrEmpty(e.Result))
            {
                builder.Append(' ');
                builder.Append(e.Result);
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
                builder.Append(" - ");
                builder.Append(e.Message);
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private sealed record RawEntry(
        long Incarnation,
        string Uuid,
        string Name,
        string Type,
        string? Result,
        string? Message,
        int? MsgIndex,
        DateTimeOffset? Timestamp);
}

internal sealed record ProvisioningLogResult(
    IReadOnlyList<ProvisioningLogEvent> Events,
    string RenderedText);
