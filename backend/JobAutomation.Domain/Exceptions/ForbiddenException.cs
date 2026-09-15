namespace JobAutomation.Domain.Exceptions;

public class ForbiddenException : DomainException
{
    public ForbiddenException(string message)
        : base("FORBIDDEN", message)
    {
    }
}
