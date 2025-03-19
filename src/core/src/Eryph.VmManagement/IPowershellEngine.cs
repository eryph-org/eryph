using System;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement;

public interface IPowershellEngine
{
    EitherAsync<PowershellFailure, Option<TypedPsObject<T>>> GetObjectAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<PowershellFailure, Seq<TypedPsObject<T>>> GetObjectsAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<PowershellFailure, Option<T>> GetObjectValueAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<PowershellFailure, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<PowershellFailure, Unit> RunAsync(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);
    
    ITypedPsObjectMapping ObjectMapping { get; }
}
