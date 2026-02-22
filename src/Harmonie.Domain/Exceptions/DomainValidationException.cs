using Harmonie.Domain.Common;

namespace Harmonie.Domain.Exceptions;

public class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message) { }
}