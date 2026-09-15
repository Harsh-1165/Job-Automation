namespace JobAutomation.Application.Interfaces;

public interface ISsrTargetValidator
{
    Task ValidateAsync(Uri uri, CancellationToken cancellationToken = default);
}
