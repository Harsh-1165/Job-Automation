namespace JobAutomation.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base("NOT_FOUND", message)
    {
    }
}
