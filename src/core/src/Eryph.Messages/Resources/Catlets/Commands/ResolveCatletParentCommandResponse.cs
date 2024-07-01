using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class ResolveCatletParentCommandResponse
{
    public string ParentId { get; set; }

    public CatletConfig Config { get; set; }
}
