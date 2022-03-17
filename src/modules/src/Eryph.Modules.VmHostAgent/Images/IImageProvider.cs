using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

public interface IImageProvider

{
    Task<Either<PowershellFailure, string>> ProvideImage(string imageName, Func<string,Task<Unit>> reportProgress, CancellationToken cancel);
}