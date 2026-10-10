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
        dotnet = pkgs.dotnetCorePackages.sdk_10_0;

        # The dev shell, plus `runtimeLibs`: native libraries that .NET loads by name at run time, which
        # nixpkgs has no global path for, so they are put on LD_LIBRARY_PATH explicitly. And
        # `schemaPackages`: packages whose GSettings schemas the app needs at run time. GTK aborts the
        # whole process (it is not an exception) when a dialog asks for a schema that nobody can find,
        # and on NixOS nothing finds them unless XDG_DATA_DIRS says where they are.
        mkDevShell =
          runtimeLibs: schemaPackages:
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
            # .NET (and thus Aspire) loads ICU dynamically.
            LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath ([ pkgs.icu ] ++ runtimeLibs);
            # Let OpenSSL-based clients (.NET, Aspire's DCP, curl, ...) trust
            # the ASP.NET Core dev cert that `dotnet dev-certs https --trust`
            # drops into ~/.aspnet/dev-certs/trust.
            shellHook =
              ''
                export SSL_CERT_DIR="''${SSL_CERT_DIR:+$SSL_CERT_DIR:}/etc/ssl/certs:$HOME/.aspnet/dev-certs/trust"
              ''
              + pkgs.lib.optionalString (schemaPackages != [ ]) ''
                export XDG_DATA_DIRS="${
                  pkgs.lib.concatMapStringsSep ":" (p: "${p}/share/gsettings-schemas/${p.name}") schemaPackages
                }''${XDG_DATA_DIRS:+:$XDG_DATA_DIRS}"
              '';
          };
      in
      {
        devShells.default = mkDevShell [ ] [ ];

        # For running the desktop app (src/OfficeRkyv.Desktop): `nix develop .#desktop`, or `just desktop`.
        # Building it needs none of this, only running it does. GirCore loads these by soname, so each
        # must be listed: without GLib the app exits at once with code 0 and prints nothing, although
        # GTK depends on it. The schemas are for GTK's file dialog (`DesktopFileSaver`), which reads
        # `org.gtk.gtk4.Settings.FileChooser`: without them opening it aborts the process.
        devShells.desktop = mkDevShell (
          with pkgs;
          [
            gtk4
            webkitgtk_6_0
            glib
          ]
        ) (
          with pkgs;
          [
            gtk4
            gsettings-desktop-schemas
          ]
        );
      }
    );
}
