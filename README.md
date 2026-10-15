# Crust to Crust

Clean source release of the 2D cave runner.

## Run

1. Install Godot **4.7.2 .NET** and the .NET 8 SDK (a newer SDK capable of targeting .NET 8 also works).
2. Import `crust-to-crust/project.godot`, wait for resource import, then build C# and press F5.

CLI build: `dotnet build crust-to-crust/Crust-to-Crust.csproj -c Release`.

A / D or arrows: move. Mouse: aim. Left click: toggle lantern. Esc: pause. Gamepad is supported.

This branch contains the runtime source and required assets. Developer notes, asset generators and smoke tests are retained on `main`. This archive is source code, not a prebuilt executable.

Font licenses are included in `crust-to-crust/Fonts/`.
