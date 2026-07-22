# Pack Format

`.live2dpack` is the Live2D package extension. Its content is a ZIP archive with `FormatVersion = 1`; other filename extensions are not Live2D packages.

## Layout

```text
manifest.json
settings/
  models.json
  global.json                 # required when IncludesGlobalConfig=true
models/
  <OriginalId>/
    <model>.model3.json
    <model>.moc3
    textures/...
    motions/...
    expressions/...
    other referenced files...
```

Each model and all of its dependencies must remain under its own `models/<OriginalId>/` prefix.

## manifest.json

```json
{
  "FormatVersion": 1,
  "PackageId": "example.characters",
  "Name": "Example Characters",
  "Author": "ExampleMod",
  "CreatedAt": "2026-07-15T00:00:00+08:00",
  "MinimumModVersion": "0.6.1",
  "SettingsSchemaVersion": 6,
  "IncludesGlobalConfig": false,
  "Models": [{
    "OriginalId": "character-source-id",
    "ModelKey": "character-main",
    "DisplayName": "Character",
    "EntryPath": "models/character-source-id/character.model3.json",
    "ContentHash": "sha256-or-stable-content-id"
  }]
}
```

- `PackageId` and `ModelKey` are stable, non-empty identifiers of at most 128 characters without control characters.
- `SettingsSchemaVersion` must be `6`.
- `EntryPath` must end in `.model3.json` inside the matching resource directory.
- `ContentHash` should be a stable SHA-256 of the complete model content.

## settings files

`settings/models.json` is an array of model configuration records. It must provide model identity, relative entry path, content hash, override
objects, action metadata, and an action-binding array. `settings/global.json` uses the [Global configuration](./configuration) structure and is
present only when `IncludesGlobalConfig=true`.

`RegisterPack` does not import global settings; persistent import may import them.

## Security limits

- At most 10,000 ZIP entries.
- At most 512 MiB per expanded entry and 2 GiB total.
- Maximum compression ratio 1000:1.
- At most 16 MiB for one JSON metadata entry.
- No absolute paths, `.`, `..`, traversal, symlinks, or duplicate paths.
- Exactly one `manifest.json`.
- Every declared entry and dependency must exist.

Build Packs during Mod packaging. Do not assemble untrusted ZIP data at runtime.

## Registration identity

Registering identical content under the same `OwnerModId + PackageId` returns the existing handle. Different content under the same identity is
rejected. Runtime instances use `OwnerModId + PackageId + Scene + InstanceId`.
