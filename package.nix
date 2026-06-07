{
  buildDotnetModule,
  dotnetCorePackages,
  fontconfig,
  libGL,
  xorg,
}:

buildDotnetModule {
  pname = "irrigation";
  version = "0.1.0";

  src = ./.;
  projectFile = "src/build.proj";

  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.runtime_10_0;
  nugetDeps = ./src/deps.json;

  selfContainedBuild = true;

  runtimeDeps = [
    fontconfig
    libGL
    xorg.libX11
    xorg.libICE
    xorg.libSM
  ];
}
