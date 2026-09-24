using System.ComponentModel.DataAnnotations;

namespace CGM.Api.Models.Dtos;

public record GlucoseMeasurementDto(
    int SensorId,
    decimal GlucoseValue,
    DateTime MeasurementTime,
    int? BatteryVoltageMv,
    decimal? DeviceTemperatureC,
    decimal? WE1CurrentNa
);

public record BulkSyncRequestDto(
    int SensorId,
    List<GlucoseMeasurementDto> Measurements
);

public record GlucoseSummaryDto(
    decimal CurrentGlucose,
    decimal AverageGlucose,
    decimal LowestGlucose,
    decimal HighestGlucose,
    decimal TimeInRangePercentage,
    DateTime LastUpdated
);
