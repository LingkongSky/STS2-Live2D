using Live2D.Api;
using Live2D.Scripts.Configuration;

namespace Live2D.Scripts.Packs;

internal sealed class RegisteredLive2DPackHandle : ILive2DPackHandle
{
    private readonly Live2DRegisteredPackRegistry.RegisteredPack _pack;

    internal RegisteredLive2DPackHandle(Live2DRegisteredPackRegistry.RegisteredPack pack)
    {
        _pack = pack;
        Models = Array.AsReadOnly(pack.Models.Values
            .Select(model => new Live2DPackModelInfo(
                model.ModelKey,
                model.DisplayName,
                model.ContentHash,
                Array.AsReadOnly(model.Config.AvailableActions.Select(action => new Live2DActionInfo(
                    action.Kind == Live2DActionKind.Motion
                        ? Live2DActionType.Motion
                        : Live2DActionType.Expression,
                    action.DisplayName,
                    action.MotionGroup,
                    action.MotionIndex,
                    action.ExpressionId)).ToArray())))
            .ToArray());
    }

    public string OwnerModId => _pack.Key.OwnerModId;
    public string PackId => _pack.Key.PackId;
    public string Name => _pack.Name;
    public bool IsRegistered { get; private set; } = true;
    public IReadOnlyList<Live2DPackModelInfo> Models { get; }

    public void Unregister() => Live2DRegisteredPackRegistry.Unregister(_pack);

    internal void MarkUnregistered() => IsRegistered = false;
}

