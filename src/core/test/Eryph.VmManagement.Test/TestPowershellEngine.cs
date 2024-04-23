using System.Diagnostics;
using System.Management.Automation;
using LanguageExt;

namespace Eryph.VmManagement.Test;

public class TestPowershellEngine : IPowershellEngine, IPsObjectRegistry
{
    public TestPowershellEngine(ITypedPsObjectMapping mapping)
    {
        ObjectMapping =mapping;
    }

    public Func<Type,AssertCommand, Either<PowershellFailure,Seq<TypedPsObject<object>>>>? GetObjectCallback;
    public Func<AssertCommand, Either<PowershellFailure, Unit>>? RunCallback;
    public Func<Type, AssertCommand, Either<PowershellFailure, Seq<object>>>? GetValuesCallback;

    public TypedPsObject<T> ToPsObject<T>(T obj)
    {
        var psObject = new PSObject(obj);
        return new TypedPsObject<T>(psObject, this, ObjectMapping);

    }

    public Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder, Action<int>? reportProgress = null)
    {
        return GetObjectsAsync<T>(builder, (msg) =>
        {
            reportProgress?.Invoke(msg);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public Either<PowershellFailure, Unit> Run(PsCommandBuilder builder, Action<int>? reportProgress = null)
    {
        return RunAsync(builder, (msg) =>
        {
            reportProgress?.Invoke(msg);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder, Func<int, Task>? reportProgress = null)
    {
        var commandInput = builder.ToDictionary();
        var result = GetObjectCallback?.Invoke(typeof(T), AssertCommand.Parse(commandInput));
        Debug.Assert(result != null, nameof(result) + " != null");
        return Task.FromResult(result.Value.Map(seq => seq.Map( r => new TypedPsObject<T>(r.PsObject, this, ObjectMapping))));

    }

    public EitherAsync<PowershellFailure, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task>? reportProgress = null)
    {
        var commandInput = builder.ToDictionary();

        if(GetValuesCallback == null)
            throw new InvalidOperationException("GetValuesCallback is not set");

        var result = GetValuesCallback.Invoke(typeof(T), 
            AssertCommand.Parse(commandInput));

        return result.Map(s => s.Map(v =>(T) v)).ToAsync();
    }

    public Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder, Func<int, Task>? reportProgress = null)
    {
        var commandInput = builder.ToDictionary();
        var result = RunCallback?.Invoke(AssertCommand.Parse(commandInput));

        Debug.Assert(result != null, nameof(result) + " != null");
        return Task.FromResult(result.Value);
    }

    public ITypedPsObjectMapping ObjectMapping { get; }
    public void AddPsObject(PSObject psObject)
    {
            
    }
}