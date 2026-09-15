namespace JobAutomation.Domain.Exceptions;

public class BadRequestException : DomainException
{
    public IReadOnlyDictionary<string, string[]>? Fields { get; }

    public BadRequestException(
        string message,
        IReadOnlyDictionary<string, string[]>? fields = null)
        : base("BAD_REQUEST", message)
    {
        Fields = fields;
    }
}
