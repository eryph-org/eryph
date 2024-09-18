using System;

namespace Eryph.Runtime.Zero;

public interface IEryphOvsPathProvider
{
    string OvsRunPath { get; }

    void SetOvsRunPath(string ovsRunPath);
}

public class EryphOvsPathProvider : IEryphOvsPathProvider
{
    private string? _ovsRunPath;

    public EryphOvsPathProvider() { }
    
    public EryphOvsPathProvider(string ovsRunPath)
    {
        _ovsRunPath = ovsRunPath;
    }

    public string OvsRunPath => _ovsRunPath ?? throw new InvalidOperationException(
        "The OVS run dir has not been provided yet.");

    public void SetOvsRunPath(string ovsRunPath)
    {
        _ovsRunPath = ovsRunPath;
    }
}
