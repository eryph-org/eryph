using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;
using Eryph.ConfigModel;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Eryph.Modules.HostAgent;

public class PrivacyContractResolver : DefaultContractResolver
{

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var props = base.CreateProperties(type, memberSerialization);

        foreach (var prop in props)
        {
            if (prop.UnderlyingName == null) continue;
            var property = type.GetProperty(prop.UnderlyingName);
            if (property == null) continue;


            var critical = false;
            var isPii = false;
            var piiAttribute = property.GetCustomAttribute<PrivateIdentifierAttribute>();
            if (piiAttribute != null)
            {
                isPii = true;
                critical = piiAttribute.Critical;
            }

            if (prop.PropertyType == typeof(PSObject))
            {
                isPii = true;
                critical = true;
            }

            if (!isPii)
                continue;
            

            prop.ValueProvider =
                new PrivateValueProvider(property, critical);
            prop.PropertyType = typeof(PrivateIdentifier);
        }

        return props;
    }

    private class PrivateValueProvider : IValueProvider
    {
        private readonly PropertyInfo _targetProperty;
        private readonly bool _critical;

        public PrivateValueProvider(PropertyInfo targetProperty, bool critical)
        {
            _targetProperty = targetProperty;
            _critical = critical;
        }

        public object GetValue([CanBeNull] object target)
        {
            var value = _targetProperty.GetValue(target);

            if (value == null) return null;

            return new PrivateIdentifier
            {
                Value = new PrivateIdentifierValue
                {
                    Value = value,
                    Critical = _critical
                }
            };

        }

        public void SetValue(object target, object value)
        {
            
        }
    }
}