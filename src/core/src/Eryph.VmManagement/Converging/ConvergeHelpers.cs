using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public static class ConvergeHelpers
{
    public static EitherAsync<Error, TypedPsObject<TSub>> GetOrCreateInfoAsync<T, TSub>(
        TypedPsObject<T> parentInfo,
        Expression<Func<T, IList<TSub>>> listProperty,
        Func<TypedPsObject<TSub>, bool> predicateFunc,
        Func<EitherAsync<Error, Option<TypedPsObject<TSub>>>> creatorFunc) =>
        from _ in RightAsync<Error, Unit>(unit)
        let items = parentInfo.GetList(listProperty, predicateFunc).Strict()
        from result in items.Match(
            Empty: () => from optionalCreated in creatorFunc()
                         from created in optionalCreated.ToEitherAsync(Error.New(
                             "The object was successfully created, but no result was returned."))
                         select created,
            Head: item => item,
            Tail: _ => Error.New("The predicate matched multiple objects."))
        select result;

    public static EitherAsync<Error, Seq<TRes>> FindAndApply<T, TSub, TRes>(
        TypedPsObject<T> parentInfo,
        Expression<Func<T, IList<TSub>>> listProperty,
        Func<TypedPsObject<TSub>, bool> predicateFunc,
        Func<TypedPsObject<TSub>, EitherAsync<Error, TRes>> applyFunc) =>
        from _ in RightAsync<Error, Unit>(unit)
        let items = parentInfo.GetList(listProperty, predicateFunc).Strict()
        from results in items.Map(applyFunc).SequenceSerial()
        select results;
}
