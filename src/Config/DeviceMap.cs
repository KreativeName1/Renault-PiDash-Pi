using System.Text.Json;

namespace PiDash.Config;

public sealed class DeviceMap
{
    public SpiMap Spi { get; init; } = new();
    public GpioMap Gpio { get; init; } = new();

    public static DeviceMap Load(string path)
    {
        var json = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<DeviceMap>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return map ?? throw new InvalidOperationException($"Failed to parse {path}");
    }

    public sealed class SpiMap
    {
        public SpiEndpoint SensorMcu { get; init; } = new();
        public SpiEndpoint CanMcu { get; init; } = new();
        public SpiEndpoint Display1 { get; init; } = new();
        public SpiEndpoint Display2 { get; init; } = new();
        public SpiEndpoint Display3 { get; init; } = new();
    }

    public sealed class GpioMap
    {
        public int ShutdownPin { get; init; }
        public int[] Buttons { get; init; } = Array.Empty<int>();
    }

    public sealed class SpiEndpoint
    {
        public int BusId { get; init; }
        public int ChipSelect { get; init; }
        public int Hz { get; init; }
        public int Mode { get; init; } // 0..3
    }
}