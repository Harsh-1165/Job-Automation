using JobAutomation.Domain.Exceptions;
using JobAutomation.Infrastructure.Scheduling;

namespace JobAutomation.UnitTests.Scheduling;

public class CronScheduleValidatorTests
{
    private readonly CronScheduleValidator _validator = new();

    [Theory]
    [InlineData("*/5 * * * *")]
    [InlineData("0 * * * *")]
    [InlineData("0 9 * * *")]
    [InlineData("0 9 * * 1-5")]
    [InlineData("30 18 * * 1")]
    public void Validate_ValidCron_DoesNotThrow(string cron)
    {
        var exception = Record.Exception(() => _validator.Validate(cron));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptySchedule_DoesNotThrow(string? cron)
    {
        var exception = Record.Exception(() => _validator.Validate(cron));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc")]
    [InlineData("* * *")]
    [InlineData("61 * * * *")]
    [InlineData("* 99 * * *")]
    public void Validate_InvalidCron_ThrowsBadRequest(string cron)
    {
        var ex = Assert.Throws<BadRequestException>(() => _validator.Validate(cron));
        Assert.NotNull(ex.Fields);
        Assert.True(ex.Fields!.ContainsKey("schedule"));
    }

    [Fact]
    public void GetNextOccurrenceUtc_ValidCron_ReturnsFutureTime()
    {
        var from = new DateTime(2026, 9, 14, 8, 0, 0, DateTimeKind.Utc);
        var next = _validator.GetNextOccurrenceUtc("0 9 * * *", from);

        Assert.NotNull(next);
        Assert.True(next > from);
    }
}
