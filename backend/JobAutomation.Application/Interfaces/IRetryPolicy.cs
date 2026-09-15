using JobAutomation.Application;
using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Interfaces;

public interface IRetryPolicy
{
    RetryDecision Evaluate(Job job, HttpJobExecutionResult result, int currentAttemptNumber);
}
