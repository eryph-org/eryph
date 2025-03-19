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

    public TypedPsObject<object> ConvertPsObject<TIn>(TypedPsObject<TIn> obj)
    {
        return new TypedPsObject<object>(obj.PsObject, this, ObjectMapping);
    }

    public EitherAsync<PowershellFailure, Seq<TypedPsObject<T>>> GetObjectsAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        var commandInput = builder.ToDictionary();
        if (GetObjectCallback is null)
            throw new InvalidOperationException("GetObjectCallback is not set");

        var result = GetObjectCallback(typeof(T), AssertCommand.Parse(commandInput));
        return result.ToAsync()
            .Map(seq => seq.Map(r => new TypedPsObject<T>(r.PsObject, this, ObjectMapping)));
    }

    public EitherAsync<PowershellFailure, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        var commandInput = builder.ToDictionary();

        if (GetValuesCallback is null)
            throw new InvalidOperationException("GetValuesCallback is not set");

        var result = GetValuesCallback(typeof(T), AssertCommand.Parse(commandInput));

        return result.Map(s => s.Map(v =>(T)v)).ToAsync();
    }

    public EitherAsync<PowershellFailure, Option<TypedPsObject<T>>> GetObjectAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        var commandInput = builder.ToDictionary();
        if (GetObjectCallback is null)
            throw new InvalidOperationException("GetObjectCallback is not set");

        var result = GetObjectCallback(typeof(T), AssertCommand.Parse(commandInput));

        return result
            .Map(seq => seq.Map(r => new TypedPsObject<T>(r.PsObject, this, ObjectMapping)))
            .Map(s => s.HeadOrNone())
            .ToAsync();
    }

    public EitherAsync<PowershellFailure, Option<T>> GetObjectValueAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        var commandInput = builder.ToDictionary();

        if (GetValuesCallback is null)
            throw new InvalidOperationException("GetValuesCallback is not set");

        var result = GetValuesCallback(typeof(T), AssertCommand.Parse(commandInput));

        return result.Map(s => s.Map(v => (T)v))
            .Map(s => s.HeadOrNone())
            .ToAsync();
    }

    public EitherAsync<PowershellFailure, Unit> RunAsync(
        PsCommandBuilder builder,
        Func<int, Task>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        var commandInput = builder.ToDictionary();
        if (RunCallback is null)
            throw new InvalidOperationException("RunCallback is not set");
        
        var result = RunCallback(AssertCommand.Parse(commandInput));
        return result.ToAsync();
    }

    public ITypedPsObjectMapping ObjectMapping { get; }
    
    public void AddPsObject(PSObject psObject) { }
}
