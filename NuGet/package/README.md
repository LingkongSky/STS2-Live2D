# Prepared NuGet assets

This directory is the NuGet equivalent of JMC's `modPublish` directory. GitHub
Actions packages these committed files without compiling Live2D or downloading
game assemblies.

`ref/net9.0/Live2D.dll` must be a metadata-only reference assembly, never the
runtime DLL. After changing the public API or XML comments, refresh both files
from a verified local game installation:

```powershell
$env:STS2_DIR = "D:\Program Files\Steam\steamapps\common\Slay the Spire 2"
dotnet build .\Live2D.csproj -c Release -t:RefreshNuGetReference
dotnet pack .\NuGet\STS2.Live2D.Package.csproj -c Release -o .\artifacts
```

Review and commit the changed DLL and XML together with the source changes.
