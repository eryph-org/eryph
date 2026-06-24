using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core.Genetics;

public readonly record struct FodderWithKey(FodderKey Key, FodderConfig Config)
{
    public static Either<Error, FodderWithKey> Create(FodderConfig config) =>
        from fodderKey in FodderKey.Create(config.Name, config.Source)
        select new FodderWithKey(fodderKey, config);
}
