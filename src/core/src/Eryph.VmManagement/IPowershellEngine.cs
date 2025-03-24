using System;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface IPowershellEngine
{
    EitherAsync<Error, Option<TypedPsObject<T>>> GetObjectAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<Error, Seq<TypedPsObject<T>>> GetObjectsAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<Error, Option<T>> GetObjectValueAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<Error, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);

    EitherAsync<Error, Unit> RunAsync(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default);
    
    ITypedPsObjectMapping ObjectMapping { get; }
}
