using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;

namespace Eryph.Modules.Controller;

public static class MacAddressGenerator
{
    private const string Prefix = "D2AB";

    public static EryphMacAddress Generate(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return EryphMacAddress.New($"{Prefix}{Convert.ToHexString(bytes)[..8]}");
    }

    public static EryphMacAddress Generate()
    {
        using var memoryOwner = System.Buffers.MemoryPool<byte>.Shared.Rent(4);
        var buffer = memoryOwner.Memory.Span[..4];
        RandomNumberGenerator.Fill(buffer);
        return EryphMacAddress.New($"{Prefix}{Convert.ToHexString(buffer)}");
    }
}
