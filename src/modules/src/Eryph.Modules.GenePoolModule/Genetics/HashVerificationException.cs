using System;

namespace Eryph.Modules.GenePool.Genetics;

public class HashVerificationException : Exception

{
    public HashVerificationException()
    {
    }

    public HashVerificationException(string message) : base(message)
    {
    }

    public HashVerificationException(string message, Exception inner) : base(message, inner)
    {
    }
}