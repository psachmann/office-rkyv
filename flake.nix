{
  description = "DotNet dependencies for the project";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    openspec.url = "github:Fission-AI/OpenSpec";
  };

  outputs =
    { nixpkgs, flake-utils, openspec, ... }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs {
          inherit system;
        };
      in
      {
        devShells.default =
          let
            dotnet = pkgs.dotnetCorePackages.sdk_10_0;
          in
          pkgs.mkShell {
            packages = with pkgs; [
              nixd
              dotnet
              omnisharp-roslyn
              netcoredbg
              buf
              protobuf
              protolint
              api-linter
              nss.tools
              openspec.packages.${system}.default
            ];
            DOTNET_ROOT = "${dotnet}/share/dotnet";
          };
      }
    );
}
