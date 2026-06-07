# Raspberry Pi Nix Image

Build workflow from a Linux machine with Nix flakes enabled:

```bash
nix develop
./nix-fetch-deps.sh
nix build .#packages.aarch64-linux.images.pi4
```

The image output will be under `result/sd-image/`.

The same image is built in GitHub Actions by `.github/workflows/build-pi-image.yml`.
The workflow uses GitHub's native `ubuntu-24.04-arm` ARM64 hosted runner, so the Pi
image build does not rely on QEMU emulation.
Push a version tag to create a release with the compressed Raspberry Pi image attached:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The workflow also supports manual dispatch. Manual runs upload the image as a workflow artifact;
tag runs additionally create/update the GitHub Release and attach:

- `irrigation-<tag>-pi4-aarch64.img.gz`
- `irrigation-<tag>-pi4-aarch64.img.gz.sha256`

The image defaults to `RaspberryPiGpio` hardware mode and uses the zone pin map in
`image/irrigation-service.nix`. GPIO pin mapping stays in service configuration and is not
editable from the touchscreen UI.

For a dry run without relays, set:

```nix
services.irrigation.hardwareProvider = "Simulated";
```
