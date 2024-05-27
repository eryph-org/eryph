using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eryph.StateDb;

internal static class Conversions
{
    public static PropertyBuilder<ISet<TEnum>> HasSetConversion<TEnum>(
        this PropertyBuilder<ISet<TEnum>> builder)
        where TEnum : struct, Enum =>
        builder.HasConversion(
            v => string.Join(',', v.Order()),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Map(Enum.Parse<TEnum>)
                .ToHashSet(),
            new ValueComparer<ISet<TEnum>>(
                (a, b) => a == b || a != null && b != null && a.SetEquals(b),
                s => s.Fold(0, HashCode.Combine),
                s => s.ToHashSet()));

    public static PropertyBuilder<IList<string>> HasListConversion(
        this PropertyBuilder<IList<string>> builder) =>
        builder.HasConversion(
            v => SerializeList(v),
            v => DeserializeList<string>(v),
            new ValueComparer<IList<string>>(
                (a, b) => a == b || a != null && b != null && a.SequenceEqual(b),
                l => l.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                l => l.ToList()));

    private static string? SerializeList<T>(IList<T> value) =>
        // EF Core converters cannot convert between null and not null.
        // See https://github.com/dotnet/efcore/issues/13850.
        value.Count > 0 ? JsonSerializer.Serialize(value) : "";

    private static IList<T> DeserializeList<T>(string? value) =>
        string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<List<T>>(value) ?? [];
}
