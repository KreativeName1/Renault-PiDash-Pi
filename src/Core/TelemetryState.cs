using System.Threading;

namespace PiDash.Core;

public sealed class TelemetryState
{
    private long _seq; // increment on each update batch

    // Example fields (expand as you go)
    public volatile int OilPressure_kPa;  // example
    public volatile int OilTemp_C_x10;
    public volatile int Vbat_mV;
    // gps data lat/lon, speed, heading, etc
    public volatile int Gps_longitude_x10_6;
    public volatile int Gps_latitude_x10_6;
    public volatile int Gps_speed_kph_x10;
    
    // 6-Axis accel/gyro, etc
    public volatile int AccelX_mg;
    public volatile int AccelY_mg;
    public volatile int AccelZ_mg;
    public volatile int GyroX_dps_x10;
    public volatile int GyroY_dps_x10;
    public volatile int GyroZ_dps_x10;
    
    public volatile int Roll_deg_x10;
    public volatile int Pitch_deg_x10;
    public volatile int Yaw_deg_x10; // not supplied by sensor, can be enabled if we add a magnetometer
    
    // Ambient light sensor for display brightness control
    public volatile int AmbientLight_lux;
    
    
    public long Sequence => Interlocked.Read(ref _seq);

    public void ApplySensorSample(in SensorSample s)
    {
        Gps_longitude_x10_6 = s.Gps_longitude_x10_6;
        Gps_latitude_x10_6 = s.Gps_latitude_x10_6;
        Gps_speed_kph_x10 = s.Gps_speed_kph_x10; 
        OilPressure_kPa = s.OilPressure_kPa;
        OilTemp_C_x10 = s.OilTemp_C_x10;
        Vbat_mV = s.Vbat_mV;
        AccelX_mg = s.AccelX_mg;
        AccelY_mg = s.AccelY_mg;
        AccelZ_mg = s.AccelZ_mg;
        GyroX_dps_x10 = s.GyroX_dps_x10;
        GyroY_dps_x10 = s.GyroY_dps_x10;
        GyroZ_dps_x10 = s.GyroZ_dps_x10;
        Roll_deg_x10 = s.Roll_deg_x10;
        Pitch_deg_x10 = s.Pitch_deg_x10;
        Yaw_deg_x10 = s.Yaw_deg_x10;
        AmbientLight_lux = s.AmbientLight_lux;

        Interlocked.Increment(ref _seq);
    }

    /// <summary>
    /// Convert current telemetry state to a SensorSample snapshot
    /// </summary>
    public SensorSample ToSample() => new(
        OilPressure_kPa,
        OilTemp_C_x10,
        Vbat_mV,
        Gps_longitude_x10_6,
        Gps_latitude_x10_6,
        Gps_speed_kph_x10,
        AccelX_mg,
        AccelY_mg,
        AccelZ_mg,
        GyroX_dps_x10,
        GyroY_dps_x10,
        GyroZ_dps_x10,
        Roll_deg_x10,
        Pitch_deg_x10,
        Yaw_deg_x10,
        AmbientLight_lux
    );
}

