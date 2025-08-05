using IdGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller;

internal class StorageIdentifierGenerator(
    IIdGenerator<long> idGenerator) 
    : IStorageIdentifierGenerator
{
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int Base = 36;

    public string Generate()
    {
        var id = idGenerator.CreateId();

        return ToBase36String(id);
    }

    private static string ToBase36String(BigInteger subject)
    {
        var result = new StringBuilder();

        do
        {
            result.Insert(0, Digits[(int)(subject % Base)]);
            subject /= Base;
        } while (subject > 0);

        return result.ToString();
    }
}
