using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;
using AutoMapper;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using LanguageExt;

namespace Eryph.VmManagement
{
    public class TypedPsObject<T> : Record<TypedPsObject<T>>
    {
        public TypedPsObject(PSObject psObject)
        {
            PsObject = psObject;
            Value = TypedPsObjectMapping.Map<T>(psObject);
        }

        public T Value { get; }
        public PSObject PsObject { get; }

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

        public TypedPsObject<TProp> GetProperty<TProp>(Expression<Func<T, TProp>> property)
        {
            var paramType = property.Parameters[0].Type; // first parameter of expression

            var propertyMemberInfo = paramType.GetMember((property.Body as MemberExpression)?.Member.Name)[0];
            var propertyValue = PsObject.Properties[propertyMemberInfo.Name].Value;

            return new TypedPsObject<TProp>(new PSObject(propertyValue));
        }

        public Seq<TypedPsObject<TSub>> GetList<TSub>(
            Expression<Func<T, IList<TSub>>> listProperty,
            Func<TSub, bool> predicateFunc)
        {
            return GetList(listProperty).Where(y => predicateFunc(y.Value));
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


    internal static class TypedPsObjectMapping
    {
        private static IMapper _mapper;

        private static void EnsureMapper(PSObject psoObject)
        {
            if (_mapper != null)
                return;

            var powershellAssembly = psoObject.BaseObject.GetType().Assembly;

            Type GetPsType(string name)
            {
                return powershellAssembly.GetType($"Microsoft.HyperV.PowerShell.{name}", true);
            }

            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateProfile("Powershell", c =>
                {
                    c.CreateMap(GetPsType("VirtualMachine"), typeof(PlannedVirtualMachineInfo));

                    c.CreateMap(GetPsType("HardDiskDrive"), typeof(HardDiskDriveInfo));
                    c.CreateMap(GetPsType("HardDiskDrive"), typeof(PlannedHardDiskDriveInfo))
                        .ForMember("Size", m => m.Ignore());

                    c.CreateMap(GetPsType("DvdDrive"), typeof(DvdDriveInfo));

                    c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(VMNetworkAdapter));
                    c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(ConnectedVMNetworkAdapter));

                    c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(PlannedVMNetworkAdapter));

                    c.CreateMap(GetPsType(nameof(VMNetworkAdapterVlanSetting)), typeof(VMNetworkAdapterVlanSetting));
                });
            });

            _mapper = new Mapper(config);
        }

        public static T Map<T>(PSObject psObject)
        {
            EnsureMapper(psObject);

            // ReSharper disable once RedundantCast
            return _mapper.Map<T>((object) psObject);
        }
    }
}