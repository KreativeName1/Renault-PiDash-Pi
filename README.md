# Renault PiDash


This is the Raspberry Pi application for the Renault PiDash project. It is primarily for the Renault 19, but can be used in other cars.
It runs on a Raspberry Pi Zero 2 W that is inside the custom secondary display unit. The PiDash application collects telemetry data from a custom CAN Bus system inside the car, sensors, and other sources, and renders it on the display. It also records telemetry to a local SQLite database for later analysis. It will also handle updates to the system and the other MCUs inside the display unit.


## Note
This is still in early developement. The hardware is currently in the prototyping phase on a breadboard, and the software is being developed in parallel. More details and documentation will be added in the future when it is more developed.

## Project Structure

```
Renault-PiDash-Pi/
├── README.md                      # This file
├── src/                           # .NET 10 PiDash application
│   ├── Program.cs
│   ├── PiDash.csproj
│   ├── appsettings.json
│   ├── Config/                    # Device configuration
│   ├── Core/                      # Core telemetry and utilities
│   ├── Features/                  # CAN, Display, Recording, Sensors
│   └── Hardware/                  # GPIO, SPI, Protocol definitions
└── deploy/                        # Deployment scripts and systemd config
    ├── scripts/
    │   ├── install.sh             # One-time Pi setup (runtime, user, SPI)
    │   ├── update_from_repo.sh    # On-Pi build & deploy (clone/pull + compile)
    │   ├── deployToPi.sh          # Dev: build & deploy from dev machine
    │   └── deployToPi.ps1         # Windows version of deployToPi.sh
    └── systemd/
        └── pidash.service         # systemd unit for PiDash service
```

## Requirements

### On Raspberry Pi
- Raspberry Pi OS (Bookworm or later recommended)
- Internet access (for installing .NET runtime and cloning repo)
- ~2 GB free storage (runtime + app + data)

### On Dev Machine (for remote deployment)
- .NET 10 SDK
- bash/PowerShell + SSH/SCP
- SSH access to the Pi

## Installation

### 1. Initial Setup on Pi

Run this once on the Raspberry Pi (as a regular user, not root):

```bash
sudo bash install.sh
```

**What it does:**
- Installs base packages (`ca-certificates`, `curl`, `rsync`, `sqlite3`)
- Installs .NET runtime into `/opt/dotnet`
- Enables SPI0 and SPI1 (3 chip selects) in boot config
- Creates `pidash` service user and data directory (`/var/lib/pidash`)
- Adds `pidash` to hardware groups (`spi`, `gpio`, `i2c`, `audio`)
- May require **reboot** if SPI config changes

```bash
# After reboot, verify SPI devices:
ls -l /dev/spidev*
```

### 2. Deploy & Compile on Pi

After `install.sh` completes, deploy the application. Two options:

#### Option A: From Dev Machine (Remote Build & Deploy)
```bash
./deploy/scripts/deployToPi.sh \
  --host <pi-ip-or-hostname> \
  --user <pi-user> \
  --project-dir /path/to/Renault-PiDash-Pi
```

This builds on your dev machine and uploads the compiled binaries to the Pi.

#### Option B: On Pi (Clone & Compile Locally)
```bash
sudo /opt/pidash/update_from_repo.sh \
  https://github.com/KreativeName1/Renault-PiDash-Pi.git \
  main
```

This clones the repo, compiles it on the Pi, and creates/enables the systemd service.

**Note:** The first time `update_from_repo.sh` is used, provide the Git repo URL. Subsequent runs will pull from the already-cloned repo.

### 3. Start the Service

```bash
sudo systemctl restart pidash
sudo journalctl -u pidash -f   # View logs
```

## Development

### Prerequisites
- .NET 10 SDK
- C# IDE (Rider, Visual Studio, VS Code with C# Dev Kit)

### Building Locally
```bash
cd src
dotnet build
dotnet run
```


### Hardware & Features
See the feature folders for details:
- **Can/** – CAN bus interface and message parsing
- **Display/** – Display driver and rendering
- **Hardware/** – GPIO, SPI device drivers, protocol buffers
- **Sensors/** – Sensor models and data collection
- **Recording/** – SQLite-based telemetry storage

## Deployment Notes

### Dev Workflow
1. Make code changes on dev machine
2. Push to GitHub or test locally with `dotnet run`
3. Deploy with `./deploy/scripts/deployToPi.sh` for quick iteration

### Production Workflow (when hardware exists)
1. Commit and push to `main` branch
2. On Pi, run:
   ```bash
   sudo /opt/pidash/update_from_repo.sh
   ```
3. Service auto-restarts and pulls latest code

## Troubleshooting

### Service won't start
```bash
sudo journalctl -u pidash -n 50   # Last 50 log lines
sudo systemctl status pidash       # Service status
```

### .NET runtime not found
```bash
dotnet --version                  # Check if /usr/local/bin/dotnet exists
/opt/dotnet/dotnet --version      # Try direct path
```

### SPI devices not visible
```bash
ls -l /dev/spidev*                # Should exist after reboot
grep -E "spi|dtoverlay" /boot/firmware/config.txt  # Check config
```

## Related Repositories
- [Renault-PiDash](https://github.com/KreativeName1/Renault-PiDash) – Main repo
- [Renault-PiDash-Hardware](https://github.com/KreativeName1/Renault-PiDash-Hardware) – Hardware schematics & CAD
- [Renault-PiDash-MCUs](https://github.com/KreativeName1/Renault-PiDash-MCUs) – Microcontroller firmware

## License
Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)
