using System.Collections.Generic;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.Networks;

public abstract class BaseIpManager
{
    protected readonly IStateStore _stateStore;
    protected readonly IIpPoolManager _poolManager;

    protected BaseIpManager(IStateStore stateStore, IIpPoolManager poolManager)
    {
        _stateStore = stateStore;
        _poolManager = poolManager;
    }

    protected static Unit UpdatePortAssignment(NetworkPort port, IpAssignment newAssignment)
    {
        newAssignment.NetworkPortId = port.Id;

        return Unit.Default;
    }

    protected static Option<IpAssignment> CheckAssignmentExist(
        IEnumerable<IpAssignment> validAssignments,
        Subnet subnet, string poolName)
    {
        return validAssignments.Find(x => x.Subnet.Id == subnet.Id)
            .Bind(s =>
            {
                if (s is not IpPoolAssignment poolAssignment)
                    return s;

                return poolAssignment.Pool.Name == poolName
                    ? s
                    : Option<IpAssignment>.None;
            });
    }


}