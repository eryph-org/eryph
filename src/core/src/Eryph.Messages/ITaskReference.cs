using System;
using Eryph.Resources;

namespace Eryph.Messages;

public interface ITaskReference
{
    TaskReferenceType ReferenceType { get; }
    string ReferenceId { get; }
    string ProjectName { get; }
}