/// <summary>
/// Sensor data sample. To add/remove fields:
/// 1. Modify this record's parameters
/// 2. Update SENSOR_FIELD_COUNT and adjust byte offsets in SensorSampleExtensions
/// 3. Update TelemetryState.ApplySensorSample if needed
/// </summary>
public readonly record struct SensorSample(
    int OilPressure_kPa,
    int OilTemp_C_x10,
    int Vbat_mV,
    int Gps_longitude_x10_6,
    int Gps_latitude_x10_6,
    int Gps_speed_kph_x10,
    int AccelX_mg,
    int AccelY_mg,
    int AccelZ_mg,
    int GyroX_dps_x10,
    int GyroY_dps_x10,
    int GyroZ_dps_x10,
    int Roll_deg_x10,
    int Pitch_deg_x10,
    int Yaw_deg_x10,
    int AmbientLight_lux
);

/// <summary>
/// Extension methods for SensorSample serialization/deserialization
/// </summary>
public static class SensorSampleExtensions
{
    public const int SENSOR_FIELD_COUNT = 16;
    public const int SENSOR_SAMPLE_BYTES = SENSOR_FIELD_COUNT * sizeof(int);

    /// <summary>
    /// Parse a SensorSample from a byte span received via SPI
    /// </summary>
    public static SensorSample FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < SENSOR_SAMPLE_BYTES)
            throw new ArgumentException($"Data must be at least {SENSOR_SAMPLE_BYTES} bytes");

        return new SensorSample(
            OilPressure_kPa: BitConverter.ToInt32(data.Slice(0, 4)),
            OilTemp_C_x10: BitConverter.ToInt32(data.Slice(4, 4)),
            Vbat_mV: BitConverter.ToInt32(data.Slice(8, 4)),
            Gps_longitude_x10_6: BitConverter.ToInt32(data.Slice(12, 4)),
            Gps_latitude_x10_6: BitConverter.ToInt32(data.Slice(16, 4)),
            Gps_speed_kph_x10: BitConverter.ToInt32(data.Slice(20, 4)),
            AccelX_mg: BitConverter.ToInt32(data.Slice(24, 4)),
            AccelY_mg: BitConverter.ToInt32(data.Slice(28, 4)),
            AccelZ_mg: BitConverter.ToInt32(data.Slice(32, 4)),
            GyroX_dps_x10: BitConverter.ToInt32(data.Slice(36, 4)),
            GyroY_dps_x10: BitConverter.ToInt32(data.Slice(40, 4)),
            GyroZ_dps_x10: BitConverter.ToInt32(data.Slice(44, 4)),
            Roll_deg_x10: BitConverter.ToInt32(data.Slice(48, 4)),
            Pitch_deg_x10: BitConverter.ToInt32(data.Slice(52, 4)),
            Yaw_deg_x10: BitConverter.ToInt32(data.Slice(56, 4)),
            AmbientLight_lux: BitConverter.ToInt32(data.Slice(60, 4))
        );
    }

    /// <summary>
    /// Write a SensorSample to a byte span for sending via SPI
    /// </summary>
    public static void ToBytes(this SensorSample sample, Span<byte> destination)
    {
        if (destination.Length < SENSOR_SAMPLE_BYTES)
            throw new ArgumentException($"Destination must be at least {SENSOR_SAMPLE_BYTES} bytes");

        BitConverter.GetBytes(sample.OilPressure_kPa).CopyTo(destination.Slice(0, 4));
        BitConverter.GetBytes(sample.OilTemp_C_x10).CopyTo(destination.Slice(4, 4));
        BitConverter.GetBytes(sample.Vbat_mV).CopyTo(destination.Slice(8, 4));
        BitConverter.GetBytes(sample.Gps_longitude_x10_6).CopyTo(destination.Slice(12, 4));
        BitConverter.GetBytes(sample.Gps_latitude_x10_6).CopyTo(destination.Slice(16, 4));
        BitConverter.GetBytes(sample.Gps_speed_kph_x10).CopyTo(destination.Slice(20, 4));
        BitConverter.GetBytes(sample.AccelX_mg).CopyTo(destination.Slice(24, 4));
        BitConverter.GetBytes(sample.AccelY_mg).CopyTo(destination.Slice(28, 4));
        BitConverter.GetBytes(sample.AccelZ_mg).CopyTo(destination.Slice(32, 4));
        BitConverter.GetBytes(sample.GyroX_dps_x10).CopyTo(destination.Slice(36, 4));
        BitConverter.GetBytes(sample.GyroY_dps_x10).CopyTo(destination.Slice(40, 4));
        BitConverter.GetBytes(sample.GyroZ_dps_x10).CopyTo(destination.Slice(44, 4));
        BitConverter.GetBytes(sample.Roll_deg_x10).CopyTo(destination.Slice(48, 4));
        BitConverter.GetBytes(sample.Pitch_deg_x10).CopyTo(destination.Slice(52, 4));
        BitConverter.GetBytes(sample.Yaw_deg_x10).CopyTo(destination.Slice(56, 4));
        BitConverter.GetBytes(sample.AmbientLight_lux).CopyTo(destination.Slice(60, 4));
    }
}
