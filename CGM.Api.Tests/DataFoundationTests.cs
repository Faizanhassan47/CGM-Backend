using CGM.Api.Data;
using CGM.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Tests;

public sealed class DataFoundationTests
{
    [Fact]
    public void Measurement_model_uses_composite_pk_and_history_indexes()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(GlucoseMeasurementEntity))!;

        Assert.Equal(2, entity.FindPrimaryKey()!.Properties.Count);
        var indexes = entity.GetIndexes().Select(x => x.GetDatabaseName()).ToHashSet();
        Assert.Contains("IX_GlucoseMeasurements_User_Time", indexes);
        Assert.Contains("IX_GlucoseMeasurements_Device_Time", indexes);
    }

    [Fact]
    public void Audit_alert_history_and_reporting_models_are_registered()
    {
        using var db = new CgmDbContext(new DbContextOptionsBuilder<CgmDbContext>()
            .UseInMemoryDatabase("ModelOnly").Options);
        Assert.NotNull(db.Model.FindEntityType(typeof(AuditLogEntity)));
        Assert.NotNull(db.Model.FindEntityType(typeof(AlertHistoryEntity)));
        Assert.NotNull(db.Model.FindEntityType(typeof(DailyGlucoseSummaryEntity)));
    }

    private static CgmDbContext CreateDb() => new(new DbContextOptionsBuilder<CgmDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
