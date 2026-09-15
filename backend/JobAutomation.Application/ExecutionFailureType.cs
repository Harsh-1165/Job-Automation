namespace JobAutomation.Application;

public enum ExecutionFailureType
{
    None = 0,
    HttpResponse = 1,
    Timeout = 2,
    Network = 3,
    RequestConstruction = 4,
    Unknown = 5
}
