using System;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement;

public interface IPowershellEngine
{
    EitherAsync<PowershellFailure, Option<TypedPsObject<T>>> GetObjectAsync<T>(
        PsCommandBuilder builder);

    EitherAsync<PowershellFailure, Seq<TypedPsObject<T>>> GetObjectsAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null);

    EitherAsync<PowershellFailure, Option<T>> GetObjectValueAsync<T>(
        PsCommandBuilder builder);

    EitherAsync<PowershellFailure, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null);

    Task<Either<PowershellFailure, Unit>> RunAsync(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null);
    
    ITypedPsObjectMapping ObjectMapping { get; }
}
