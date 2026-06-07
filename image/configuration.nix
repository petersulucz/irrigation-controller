{ lib, ... }:

{
  imports = [
    ./irrigation-service.nix
  ];

  networking.hostName = "irrigation";
  networking.networkmanager.enable = true;

  boot.consoleLogLevel = lib.mkForce 0;
  boot.kernelParams = [ "quiet" ];

  services.openssh.enable = true;
  services.irrigation = {
    enable = true;
    apiToken = "dev-token";
    hardwareProvider = "RaspberryPiGpio";
    relayPolarity = "ActiveHigh";
  };

  services.displayManager = {
    enable = true;
    autoLogin = {
      enable = true;
      user = "irrigation";
    };
  };

  services.xserver = {
    enable = true;
    xkb.layout = "us";
    libinput.enable = true;
    windowManager.openbox.enable = true;
    displayManager.lightdm.enable = true;
    displayManager.defaultSession = "none+openbox";
  };

  hardware.enableRedistributableFirmware = true;
  system.stateVersion = "25.11";
}
