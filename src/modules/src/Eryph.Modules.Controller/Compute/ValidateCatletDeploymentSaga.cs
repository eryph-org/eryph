using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

/// <summary>
/// This saga is responsible for validating that the catlet
/// can be deployed to the selected host agent. This includes
/// checking the architecture, the configured networks and
/// ensuring that all disk genes are available and that the
/// datastore and environment exist.
/// </summary>
public class ValidateCatletDeploymentSaga
{
}
