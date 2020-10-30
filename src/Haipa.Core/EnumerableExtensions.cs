using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace System.Linq
{
    public static class EnumerableExtensions
    {
        // copied from https://github.com/dotnet/efcore/blob/main/src/Shared/EnumerableExtensions.cs 
        // Copyright (c) .NET Foundation. All rights reserved.
        // Licensed under the Apache License, Version 2.0. https://github.com/dotnet/efcore/blob/main/LICENSE.txt
        public static IEnumerable<T> Distinct<T>(
            [NotNull] this IEnumerable<T> source,
            [NotNull] Func<T, T, bool> comparer)
            where T : class
            => source.Distinct(new DynamicEqualityComparer<T>(comparer));

        private sealed class DynamicEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            private readonly Func<T, T, bool> _func;

            public DynamicEqualityComparer(Func<T, T, bool> func)
            {
                _func = func;
            }

            public bool Equals(T x, T y) => _func(x, y);

            public int GetHashCode(T obj) => 0;
        }

    }
}
