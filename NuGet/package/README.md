# Prepared NuGet assets

This directory is the NuGet equivalent of JMC's `modPublish` directory. GitHub
Actions packages these committed files without compiling Live2D or downloading
game assemblies.

`ref/net9.0/Live2D.dll` must be a metadata-only reference assembly, never the
runtime DLL. After changing the public API or XML comments, refresh both files
from a verified local game installation:

```powershell
.\Tools\update-nuget-reference.ps1 `
  -Sts2Dir "D:\Program Files\Steam\steamapps\common\Slay the Spire 2"
```

Review and commit the changed DLL and XML together with the source changes.
