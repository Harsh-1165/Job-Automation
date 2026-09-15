namespace JobAutomation.Domain.Exceptions;

public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Invalid email or password.")
        : base("UNAUTHORIZED", message)
    {
    }
}
