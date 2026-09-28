# List all available recipes
default:
  just --list

# One-time setup of the dev environment; run it inside the Nix dev shell.
# `dotnet build` also generates playwright.ps1 in the functional tests'
# output folder, which the last step needs.
# Restore .NET tools, build, and download Playwright's browsers
setup:
  dotnet tool restore
  dotnet build
  pwsh ./tests/OfficeRkyv.FunctionalTests/bin/Debug/net10.0/playwright.ps1 install

# Start the whole app (API, web UI, dependencies) via the Aspire AppHost
dev:
  dotnet aspire run

# Format all C# sources with CSharpier
format:
  dotnet csharpier format .

# Build the solution in the given configuration (e.g. Debug, Release)
build $config="Debug":
  dotnet build --configuration {{config}}

# Run all tests in the given configuration (e.g. Debug, Release)
test $config="Debug":
  dotnet test --configuration {{config}}
