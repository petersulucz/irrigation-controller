{ lib, modulesPath, pkgs, ... }:

{
  imports = [
    "${modulesPath}/installer/sd-card/sd-image-aarch64.nix"
    ./configuration.nix
  ];

  nixpkgs.hostPlatform = "aarch64-linux";

  boot = {
    # The raspberry-pi-4 nixos-hardware module owns the kernel selection.
    initrd.availableKernelModules = [ "xhci_pci" "usbhid" "usb_storage" ];
    loader = {
      grub.enable = false;
      generic-extlinux-compatible.enable = true;
      generic-extlinux-compatible.configurationLimit = 2;
    };
  };

  hardware.raspberry-pi = {
    firmware.uboot.enable = true;
    configtxt.settings.all.disable_splash = true;
  };

  sdImage = {
    compressImage = false;
    imageName = "irrigation-pi4-aarch64.img";
  };

  fileSystems."/" = {
    device = "/dev/disk/by-label/NIXOS_SD";
    fsType = "ext4";
    options = [ "noatime" ];
  };

  hardware.enableAllHardware = lib.mkForce false;
}
