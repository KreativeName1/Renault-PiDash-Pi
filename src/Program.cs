using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiDash.Config;
using PiDash.Core;
using PiDash.Core.Diagnostics;
using PiDash.Features.Can;
using PiDash.Features.Display;
using PiDash.Features.Recording;
using PiDash.Features.Sensors;
using pidash.Hardware.Gpio;
using PiDash.Hardware.Spi;

namespace PiDash;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "PIDASH_");

        builder.Services.AddSingleton(sp =>
        {
            // Load DeviceMap.json (simple approach)
            var cfg = sp.GetRequiredService<IConfiguration>();
            var path = cfg.GetValue<string>("DeviceMapPath") ?? "Config/DeviceMap.json";
            return DeviceMap.Load(path);
        });

        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<TelemetryState>();
        builder.Services.AddSingleton<SpiDeviceFactory>();

        builder.Services.AddHostedService<SensorService>();
        builder.Services.AddHostedService<DisplayService>();
        builder.Services.AddHostedService<CanService>();
        builder.Services.AddHostedService<RecordingService>();

        builder.Services.AddHostedService<HealthService>();

        // Optional hardware services (disabled until implemented)
        // builder.Services.AddHostedService<ButtonsService>();
        // builder.Services.AddHostedService<ShutdownPinService>();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        await builder.Build().RunAsync();
    }
}