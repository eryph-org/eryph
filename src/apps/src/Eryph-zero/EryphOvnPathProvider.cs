using System;

namespace Eryph.Runtime.Zero;

public interface IEryphOvnPathProvider
{
    string OvnRunPath { get; }

    void SetOvnRunPath(string ovnRunPath);
}

public class EryphOvnPathProvider : IEryphOvnPathProvider
{
    private string? _ovnRunPath;

    public EryphOvnPathProvider()
    {
    }

    public EryphOvnPathProvider(string ovnRunPath)
    {
        _ovnRunPath = ovnRunPath;
    }

    public string OvnRunPath => _ovnRunPath ?? throw new InvalidOperationException(
        "The OVN run dir has not been provided yet.");

    public void SetOvnRunPath(string ovnRunPath)
    {
        _ovnRunPath = ovnRunPath;
    }
}
