{
  description = "DotNet dependencies for the project";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    { nixpkgs, flake-utils, ... }:
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
            ];
            DOTNET_ROOT = "${dotnet}/share/dotnet";
          };
      }
    );
}
