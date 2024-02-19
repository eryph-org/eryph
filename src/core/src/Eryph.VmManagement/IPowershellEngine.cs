using System;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement;

public interface IPowershellEngine
{
    Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder,
        Action<int> reportProgress = null);

    Either<PowershellFailure, Unit> Run(PsCommandBuilder builder, Action<int> reportProgress = null);

    Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder,
        Func<int, Task> reportProgress = null);

    EitherAsync<PowershellFailure, Seq<T>> GetObjectValuesAsync<T>(PsCommandBuilder builder,
        Func<int, Task> reportProgress = null);

    Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder, Func<int, Task> reportProgress = null);
    ITypedPsObjectMapping ObjectMapping { get; }
}