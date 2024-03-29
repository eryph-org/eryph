﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging
{
    public static class ConvergeHelpers
    {
        public static async Task<Either<Error, TypedPsObject<TSub>>> GetOrCreateInfoAsync<T, TSub>(
            TypedPsObject<T> parentInfo,
            Expression<Func<T, IList<TSub>>> listProperty,
            Func<TypedPsObject<TSub>, bool> predicateFunc,
            Func<Task<Either<PowershellFailure, Seq<TypedPsObject<TSub>>>>> creatorFunc)
        {
            var result = parentInfo.GetList(listProperty, predicateFunc).ToArray();

            if (result.Length() != 0)
                return Prelude.Try(result.Single()).Try().Match<Either<Error, TypedPsObject<TSub>>>(
                    Fail: ex => Prelude.Left(Error.New(ex)),
                    Succ: r => Prelude.Right(r)
                );


            var creatorResult = await creatorFunc().ToError().ConfigureAwait(false);

            var res = creatorResult.Bind(
                seq => seq.HeadOrNone()
                    .ToEither(() => Error.New("Object creation was successful, but no result was returned.")));

            return res;
        }

        public static Task<IEnumerable<TRes>> FindAndApply<T, TSub, TRes>(TypedPsObject<T> parentInfo,
            Expression<Func<T, IList<TSub>>> listProperty,
            Func<TypedPsObject<TSub>, bool> predicateFunc,
            Func<TypedPsObject<TSub>, Task<TRes>> applyFunc)
        {
            return parentInfo.GetList(listProperty, predicateFunc).ToArray().Map(applyFunc)
                .TraverseSerial(l => l);
        }

    }
}