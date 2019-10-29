using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using AutoMapper;
using LanguageExt;

namespace Haipa.VmManagement
{
    public class TypedPsObject<T> : Record<TypedPsObject<T>>
    {
        public T Value { get;  }
        public PSObject PsObject { get; }


        public TypedPsObject(PSObject psObject)
        {
            PsObject = psObject;
            Value = TypedPsObjectMapping.Map<T>(psObject);
        }

        public static implicit operator T(TypedPsObject<T> typed)
        {
            return typed.Value;
        }

        public TypedPsObject<T> Recreate()
        {
            return new TypedPsObject<T>(PsObject);
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
            Func<TSub, bool> predicateFunc) => GetList(listProperty).Where(y => predicateFunc(y.Value));

        public Seq<TypedPsObject<TSub>> GetList<TSub>(
            Expression<Func<T, IList<TSub>>> listProperty)
        {
            var paramType = listProperty.Parameters[0].Type; // first parameter of expression
            var property = paramType.GetMember((listProperty.Body as MemberExpression)?.Member.Name)[0];

            return
                Prelude.TryOption(((PsObject.Properties[property.Name].Value as IEnumerable)?.Cast<object>().
                    Map(x => new TypedPsObject<TSub>(new PSObject(x)))))
                .Match(
                    Fail: () => new TypedPsObject<TSub>[] { },
                    Some: x => x
                ).ToSeq();
        }

    }

    

    internal static class TypedPsObjectMapping
    {
        private static readonly IMapper Mapper;

        static TypedPsObjectMapping()
        {
            var config = new MapperConfiguration(cfg => { cfg.CreateMissingTypeMaps = true; });

            Mapper = new Mapper(config);
        }

        public static T Map<T>(object psObject)
        {
            return Mapper.Map<T>(psObject);
        }
    }


}