using JobAutomation.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.UnitTests.Infrastructure;

public static class SqliteTestDb
{
    public static async Task<(ApplicationDbContext Db, SqliteConnection Connection)> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return (db, connection);
    }

    public static async Task SaveAndClearAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }
}
