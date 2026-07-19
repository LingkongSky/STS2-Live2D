namespace Live2D.Api;

/// <summary>Kind of action declared by a model.</summary>
public enum Live2DActionType
{
    /// <summary>Cubism motion group and index.</summary>
    Motion,
    /// <summary>Cubism expression ID.</summary>
    Expression,
}

/// <summary>An action declared by a model inside a registered pack.</summary>
/// <param name="Type">Motion or expression.</param>
/// <param name="DisplayName">User-facing action name.</param>
/// <param name="MotionGroup">Motion group, or empty for an expression.</param>
/// <param name="MotionIndex">Motion index, or -1 for an expression.</param>
/// <param name="ExpressionId">Expression ID, or empty for a motion.</param>
public sealed record Live2DActionInfo(
    Live2DActionType Type,
    string DisplayName,
    string MotionGroup,
    int MotionIndex,
    string ExpressionId);

/// <summary>A read-only model entry exposed by a registered Live2D pack.</summary>
/// <param name="ModelKey">Stable key inside the Pack.</param>
/// <param name="DisplayName">User-facing model name.</param>
/// <param name="ContentHash">Pack-declared content identity.</param>
/// <param name="Actions">Actions declared by this model.</param>
public sealed record Live2DPackModelInfo(
    string ModelKey,
    string DisplayName,
    string ContentHash,
    IReadOnlyList<Live2DActionInfo> Actions);

/// <summary>
/// Handle for a provider-owned Live2D pack registered in the central model library.
/// <para>中文：其他 Mod 注册到统一模型库的只读资源 Pack 句柄。</para>
/// </summary>
public interface ILive2DPackHandle
{
    /// <summary>Mod ID that registered this pack.</summary>
    string OwnerModId { get; }
    /// <summary>Stable PackageId from manifest.json.</summary>
    string PackId { get; }
    /// <summary>Pack display name.</summary>
    string Name { get; }
    /// <summary>Whether this pack remains registered for the current session.</summary>
    bool IsRegistered { get; }
    /// <summary>Read-only models declared by this pack.</summary>
    IReadOnlyList<Live2DPackModelInfo> Models { get; }

    /// <summary>
    /// Unregisters the provider assets. User configuration remains in the model library.
    /// <para>中文：注销提供方资源；玩家在模型库中的配置会保留。</para>
    /// </summary>
    void Unregister();
}
