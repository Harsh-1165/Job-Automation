namespace JobAutomation.Domain.Exceptions;

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base("VALIDATION_ERROR", message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}
