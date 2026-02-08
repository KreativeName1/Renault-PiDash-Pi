using System.Device.Spi;
using PiDash.Config;

namespace PiDash.Hardware.Spi;

public sealed class SpiDeviceFactory
{
    public SpiDevice Create(DeviceMap.SpiEndpoint ep)
    {
        var settings = new SpiConnectionSettings(ep.BusId, ep.ChipSelect)
        {
            ClockFrequency = ep.Hz,
            Mode = ep.Mode switch
            {
                0 => SpiMode.Mode0,
                1 => SpiMode.Mode1,
                2 => SpiMode.Mode2,
                3 => SpiMode.Mode3,
                _ => SpiMode.Mode0
            }
        };
        return SpiDevice.Create(settings);
    }
}