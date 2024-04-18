using System;

namespace Eryph.Modules.Controller.Seeding;

public class SeederException : Exception
{
    public SeederException(string message)
        : base(message)
    {
    }

    public SeederException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
