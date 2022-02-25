using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Threading.Tasks;
using Eryph.Core;
using LanguageExt;

namespace Eryph.VmManagement
{
    public class TypedPsObject<T> : Record<TypedPsObject<T>>, ITypedPsObject
    {
        public TypedPsObject(PSObject psObject)
        {
            PsObject = psObject;
            Value = TypedPsObjectMapping.Map<T>(psObject);
            TraceContext.Current.Write(TypedPsObjectTraceData.FromObject(this));
        }

        public T Value { get; }

        [PrivateIdentifier]
        public PSObject PsObject { get; }

        object ITypedPsObject.Value => Value;

        public static implicit operator T(TypedPsObject<T> typed)
        {
            return typed.Value;
        }

        public TypedPsObject<T> Recreate()
        {
            return new TypedPsObject<T>(PsObject);
        }

        public TypedPsObject<TNew> Cast<TNew>()
        {
            return new TypedPsObject<TNew>(PsObject);
        }

        public TR Map<TR>(Func<T, TR> mapperFunc)
        {
            return mapperFunc(Value);
        }

        public TR Map<TR>(Func<TypedPsObject<T>, TR> mapperFunc)
        {
            return mapperFunc(this);
        }

        public Either<PowershellFailure, TypedPsObject<TNew>> CastSafe<TNew>()
        {
            return Prelude.Try(() => new TypedPsObject<TNew>(PsObject))
                .ToEither(l => new PowershellFailure { Message = l.Message });

        }

        public Task<Either<PowershellFailure, TypedPsObject<TNew>>> CastSafeAsync<TNew>()
        {
            return CastSafe<TNew>().ToAsync().ToEither();
        }

        public TypedPsObject<TProp> GetProperty<TProp>(Expression<Func<T, TProp>> property)
        {
            var paramType = property.Parameters[0].Type; // first parameter of expression

            var propertyMemberInfo = paramType.GetMember((property.Body as MemberExpression)?.Member.Name)[0];
            var propertyValue = PsObject.Properties[propertyMemberInfo.Name].Value;

            return new TypedPsObject<TProp>(new PSObject(propertyValue));
        }

        public Seq<TypedPsObject<TSub>> GetList<TSub>(
            Expression<Func<T, IList<TSub>>> listProperty,
            Func<TypedPsObject<TSub>, bool> predicateFunc)
        {
            return GetList(listProperty).Where(predicateFunc);
        }

        public Seq<TypedPsObject<TSub>> GetList<TSub>(
            Expression<Func<T, IList<TSub>>> listProperty)
        {
            var paramType = listProperty.Parameters[0].Type; // first parameter of expression
            var property = paramType.GetMember((listProperty.Body as MemberExpression)?.Member.Name)[0];

            return
                Prelude.TryOption((PsObject.Properties[property.Name].Value as IEnumerable)?.Cast<object>()
                        .Map(x => new TypedPsObject<TSub>(new PSObject(x))))
                    .Match(
                        Fail: () => new TypedPsObject<TSub>[] { },
                        Some: x => x
                    ).ToSeq();
        }


    }
}