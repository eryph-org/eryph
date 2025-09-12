using System;
using System.Security.Cryptography;
using System.Text;
using Eryph.ConfigModel;

namespace Eryph.Core.Network;

public static class MacAddressGenerator
{
    private const string Prefix = "D2AB";

    /// <summary>
    /// Generates a new MAC address based on the given <paramref name="seed"/>.
    /// The same <paramref name="seed"/> will always produce the same MAC address.
    /// </summary>
    public static EryphMacAddress Generate(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return EryphMacAddress.New($"{Prefix}{Convert.ToHexString(bytes)[..8]}");
    }

    /// <summary>
    /// Generates a new random MAC address which starts with a well-defined prefix.
    /// </summary>
    public static EryphMacAddress Generate()
    {
        using var memoryOwner = System.Buffers.MemoryPool<byte>.Shared.Rent(4);
        var buffer = memoryOwner.Memory.Span[..4];
        RandomNumberGenerator.Fill(buffer);
        return EryphMacAddress.New($"{Prefix}{Convert.ToHexString(buffer)}");
    }
}
