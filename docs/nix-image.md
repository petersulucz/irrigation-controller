# Raspberry Pi Nix Image

Build workflow from a Linux machine with Nix flakes enabled:

```bash
nix develop
./nix-fetch-deps.sh
nix build .#packages.aarch64-linux.images.pi4
```

The image output will be under `result/sd-image/`.

The image defaults to `RaspberryPiGpio` hardware mode and uses the zone pin map in
`image/irrigation-service.nix`. GPIO pin mapping stays in service configuration and is not
editable from the touchscreen UI.

For a dry run without relays, set:

```nix
services.irrigation.hardwareProvider = "Simulated";
```
