// Based on https://github.com/louthy/language-ext/tree/v4-latest
// Copyright (c) 2014-2022 Paul Louth
// Licensed under MIT license https://github.com/louthy/language-ext/blob/v4-latest/LICENSE.md
using System;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

namespace Eryph.Core;

public static partial class EryphPrelude
{
    // The @catch in LanguageExt 4 only supports predicates for exceptions.
    // The @catchError adds support for predicates for errors.

    /// <summary>
    /// Catch an error if the predicate matches.
    /// </summary>
    public static AffCatch<RT, A> @catchError<RT, A>(Func<Error, bool> predicate, Func<Error, Aff<RT, A>> fail)
        where RT : struct, HasCancel<RT> =>
        new(predicate, fail);

    /// <summary>
    /// Catch an error if the predicate matches.
    /// </summary>
    public static AffCatch<A> @catchError<A>(Func<Error, bool> predicate, Func<Error, Aff<A>> fail) =>
        new(predicate, fail);

    /// <summary>
    /// Catch an error if the predicate matches.
    /// </summary>
    public static EffCatch<RT, A> @catchError<RT, A>(Func<Error, bool> predicate, Func<Error, Eff<RT, A>> fail)
        where RT : struct =>
        new(predicate, fail);

    /// <summary>
    /// Catch an error if the predicate matches.
    /// </summary>
    public static EffCatch<A> @catchError<A>(Func<Error, bool> predicate, Func<Error, Eff<A>> fail) =>
        new(predicate, fail);
}
