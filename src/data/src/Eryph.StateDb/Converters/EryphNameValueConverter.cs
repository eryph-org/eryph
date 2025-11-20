using Eryph.ConfigModel;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Eryph.StateDb.Converters;

public class EryphNameValueConverter<TName>()
    : ValueConverter<TName, string>(
        v => v.Value,
        v => EryphName<TName>.New(v))
    where TName : EryphName<TName>;

