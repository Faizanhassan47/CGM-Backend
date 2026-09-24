using System.ComponentModel.DataAnnotations;

namespace CGM.Api.Models.Dtos;

public record RegisterDeviceRequestDto(
    [Required] string DeviceName,
    [Required] string DeviceType, // Disposable / Reusable
    [Required] string DeviceModel, // G-AA / G-PA0 / G-PA1
    string? SerialNumber,
    string? BleDeviceName,
    string? FirmwareVersion,
    int? BatteryVoltageMv
);

public record UpdateDeviceStatusDto(
    string ConnectionStatus,
    int? BatteryVoltageMv,
    decimal? BatteryPercentage
);

public record StartSensorRequestDto(
    int DeviceId,
    string? SensorIdentifier,
    int WarmupMinutes = 60
);

public record DeviceInfoDto(
    int Id,
    string DeviceName,
    string DeviceType,
    string DeviceModel,
    string? SerialNumber,
    string? BleDeviceName,
    string? FirmwareVersion,
    int? BatteryVoltageMv,
    decimal? BatteryPercentage,
    string ConnectionStatus,
    DateTime? LastConnectedAt
);

public record SensorInfoDto(
    int Id,
    int DeviceId,
    string? SensorIdentifier,
    string Status,
    DateTime? StartedAt,
    DateTime? ActivatedAt,
    DateTime? LastReadingAt
);
