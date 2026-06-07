{ config, lib, pkgs, ... }:

let
  cfg = config.services.irrigation;
  irrigationPackage = pkgs.callPackage ../package.nix { };
  serviceConfig = {
    Kestrel = {
      Endpoints = {
        Http = {
          Url = "http://127.0.0.1:5030";
        };
      };
    };
    Irrigation = {
      DataPath = cfg.rootDir;
      SetupComplete = true;
      MaximumManualDuration = "01:00:00";
      Security = {
        Token = cfg.apiToken;
      };
      Hardware = {
        Provider = cfg.hardwareProvider;
        RelayPolarity = cfg.relayPolarity;
        Zones = cfg.zones;
      };
    };
  };
  appsettings = pkgs.writeText "irrigation-appsettings.json" (builtins.toJSON serviceConfig);
in
{
  options.services.irrigation = {
    enable = lib.mkEnableOption "irrigation controller";

    rootDir = lib.mkOption {
      type = lib.types.str;
      default = "/data/irrigation";
      description = "Persistent irrigation data directory.";
    };

    apiToken = lib.mkOption {
      type = lib.types.str;
      default = "change-me";
      description = "Bearer token used by the kiosk app and LAN API clients.";
    };

    hardwareProvider = lib.mkOption {
      type = lib.types.enum [ "Simulated" "RaspberryPiGpio" ];
      default = "RaspberryPiGpio";
      description = "Relay controller backend.";
    };

    relayPolarity = lib.mkOption {
      type = lib.types.enum [ "ActiveHigh" "ActiveLow" ];
      default = "ActiveHigh";
      description = "Logical relay polarity for configured GPIO outputs.";
    };

    zones = lib.mkOption {
      type = lib.types.listOf lib.types.attrs;
      default = [
        { Order = 1; Name = "Front Lawn"; Pin = 4; Enabled = true; DefaultDuration = "00:05:00"; }
        { Order = 2; Name = "Back Lawn"; Pin = 27; Enabled = true; DefaultDuration = "00:05:00"; }
        { Order = 3; Name = "Garden Beds"; Pin = 22; Enabled = true; DefaultDuration = "00:05:00"; }
        { Order = 4; Name = "Side Yard"; Pin = 5; Enabled = true; DefaultDuration = "00:05:00"; }
      ];
      description = "Hardware-backed zone definitions. Pins are not editable from the UI.";
    };
  };

  config = lib.mkIf cfg.enable {
    users.users.irrigation = {
      isNormalUser = true;
      home = "/home/irrigation";
      extraGroups = [ "gpio" "networkmanager" ];
    };

    systemd.tmpfiles.rules = [
      "d ${cfg.rootDir} 0750 irrigation irrigation -"
    ];

    systemd.services.irrigation-backend = {
      description = "Irrigation backend";
      wantedBy = [ "multi-user.target" ];
      after = [ "network.target" ];
      serviceConfig = {
        ExecStart = "${irrigationPackage}/bin/Irrigation.Api";
        WorkingDirectory = cfg.rootDir;
        Restart = "on-failure";
        RestartSec = 5;
        BindReadOnlyPaths = [
          "${appsettings}:${cfg.rootDir}/appsettings.json"
        ];
      };
    };

    systemd.services.irrigation-frontend = {
      description = "Irrigation touchscreen UI";
      wantedBy = [ "graphical.target" ];
      after = [ "irrigation-backend.service" "display-manager.service" ];
      requires = [ "irrigation-backend.service" ];
      serviceConfig = {
        User = "irrigation";
        ExecStart = "${irrigationPackage}/bin/Irrigation.App";
        WorkingDirectory = cfg.rootDir;
        Restart = "on-failure";
        RestartSec = 2;
        Environment = [
          "IRRIGATION_API_URL=http://127.0.0.1:5030"
          "IRRIGATION_API_TOKEN=${cfg.apiToken}"
          "DISPLAY=:0"
          "XAUTHORITY=/home/irrigation/.Xauthority"
        ];
        BindReadOnlyPaths = [
          "${appsettings}:${cfg.rootDir}/appsettings.json"
        ];
      };
    };
  };
}
