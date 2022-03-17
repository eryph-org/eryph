using System.Collections.Generic;

namespace Eryph.Modules.VmHostAgent.Images;

internal interface IImageSourceFactory
{
    IEnumerable<string> RemoteSources { get; }
    IImageSource CreateNew(string name);
    ILocalImageSource CreateLocal();

}