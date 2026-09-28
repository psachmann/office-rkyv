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
              gh
              just
              just-lsp
              nixd
              dotnet
              omnisharp-roslyn
              netcoredbg
              buf
              protobuf
              protolint
              api-linter
              nss.tools
              openssl
              icu
              # For installing Playwright's own browser binaries: after
              # building tests/OfficeRkyv.FunctionalTests, run
              # `pwsh bin/Debug/netX/playwright.ps1 install` once (needs
              # nix-ld configured on the host to run the downloaded browsers,
              # since they aren't built by nixpkgs).
              powershell
              openspec.packages.${system}.default
            ];
            DOTNET_ROOT = "${dotnet}/share/dotnet";
            # .NET (and thus Aspire) loads ICU dynamically; nixpkgs has no
            # global lib path, so expose it explicitly.
            LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath [ pkgs.icu ];
            # Let OpenSSL-based clients (.NET, Aspire's DCP, curl, ...) trust
            # the ASP.NET Core dev cert that `dotnet dev-certs https --trust`
            # drops into ~/.aspnet/dev-certs/trust.
            shellHook = ''
              export SSL_CERT_DIR="''${SSL_CERT_DIR:+$SSL_CERT_DIR:}/etc/ssl/certs:$HOME/.aspnet/dev-certs/trust"
            '';
          };
      }
    );
}
