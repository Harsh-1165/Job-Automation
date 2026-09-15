namespace JobAutomation.Domain;

public static class OutboxMessageTypes
{
    public const string ExecutionEnqueue = "ExecutionEnqueue";
    public const string RetryPreparation = "RetryPreparation";
}
