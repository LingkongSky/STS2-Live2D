namespace Live2D.Api;

/// <summary>
/// Provider-scoped lifecycle hooks for custom character behavior. Live2D retains
/// ownership of model configuration and scene instances.
/// </summary>
public interface ILive2DProviderLifecycleHook
{
    /// <summary>Called after provider assets are registered, before model instances are refreshed.</summary>
    void OnPackRegistered(ILive2DPackHandle pack) { }

    /// <summary>Called when a provider model is bound and can accept playback commands.</summary>
    void OnModelAvailable(ILive2DModelHandle model) { }

    /// <summary>Called after a provider model is unbound; cancel pending custom behavior here.</summary>
    void OnModelUnavailable(ILive2DModelHandle model) { }

    /// <summary>Called after provider assets are removed from the current session.</summary>
    void OnPackUnregistered(ILive2DPackHandle pack) { }
}
