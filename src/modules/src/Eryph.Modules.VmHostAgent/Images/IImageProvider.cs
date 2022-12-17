using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper.Configuration.Conventions;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

public interface IImageProvider

{
    Task<Either<PowershellFailure, PrepareVirtualMachineImageResponse>> ProvideImage(string imageName, Func<string,Task<Unit>> reportProgress, CancellationToken cancel);
}