using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt.Common;

namespace Eryph.Core;

public static class ErrorUtils
{
    public static string PrintError(Error error) => error switch
    {
        ManyErrors me => string.Join("\n", me.Errors.Map(PrintError)),
        Exceptional ee => ee.ToException().ToString(),
        _ => error.Message
             + error.Inner.Map(i => $"{Environment.NewLine}{PrintError(i)}").IfNone(""),
    };
}
