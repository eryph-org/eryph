using System;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Thrown when a component enrollment request cannot be satisfied — e.g. it is not authorized by
/// the <see cref="IComponentEnrollmentPolicy"/> or carries an invalid public key.
/// </summary>
public sealed class ComponentEnrollmentException : Exception
{
    public ComponentEnrollmentException(string message)
        : base(message)
    {
    }

    public ComponentEnrollmentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
