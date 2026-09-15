namespace JobAutomation.Domain.Exceptions;

public class SsrValidationException : DomainException
{
    public SsrValidationException()
        : base("SSRF_BLOCKED", "The target URL is not allowed.")
    {
    }
}
