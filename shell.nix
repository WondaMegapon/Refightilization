{ pkgs ? import <nixpkgs> {} }:

(pkgs.buildFHSEnv {
  name = "rider-env";
  targetPkgs = pkgs: (with pkgs; [
    dotnetCorePackages.dotnet_8.sdk
    dotnetCorePackages.dotnet_8.aspnetcore
    powershell
  ]);
  multiPkgs = pkgs: (with pkgs; [
  ]);
  runScript = "nvim ./";
}).env

