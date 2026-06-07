{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    nixos-hardware.url = "github:NixOS/nixos-hardware";
  };

  outputs = { self, nixpkgs, flake-utils, nixos-hardware }:
    let
      pi4System = nixpkgs.lib.nixosSystem {
        system = "aarch64-linux";
        modules = [
          nixos-hardware.nixosModules.raspberry-pi-4
          ./image/imager-pi4.nix
        ];
      };
    in
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs { inherit system; };
        service = pkgs.callPackage ./package.nix { };
      in
      {
        packages = {
          inherit service;
          default = service;
        };

        devShells.default = pkgs.mkShell {
          packages = [
            pkgs.dotnetCorePackages.sdk_10_0
            pkgs.nuget-to-json
          ];
        };
      }
    ) // {
      nixosConfigurations.pi4 = pi4System;
      packages.aarch64-linux.images.pi4 = pi4System.config.system.build.sdImage;
    };
}
