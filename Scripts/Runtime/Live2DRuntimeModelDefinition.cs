using Live2D.Api;
using Live2D.Scripts.Configuration;

namespace Live2D.Scripts.Runtime;

internal sealed record Live2DRuntimeModelIdentity(
    string RuntimeId,
    string OwnerModId,
    string? PackId,
    string ModelKey,
    string InstanceId,
    Live2DScene Scene);

internal sealed record Live2DRuntimeModelDefinition(
    Live2DRuntimeModelIdentity Identity,
    Live2DModelConfig Config,
    string AssetPath);
