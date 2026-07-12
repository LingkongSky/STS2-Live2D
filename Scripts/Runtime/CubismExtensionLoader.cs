using System.Reflection;
using Godot;

namespace Live2D.Scripts.Runtime;

public static class CubismExtensionLoader
{
    private static readonly string[] ShaderResourcePaths =
    [
        "res://addons/gd_cubism/res/shader/2d_cubism_mask.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_mask_add.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_mask_add_inv.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_mask_mix.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_mask_mix_inv.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_mask_mul.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_mask_mul_inv.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_norm_add.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_norm_mix.gdshader",
        "res://addons/gd_cubism/res/shader/2d_cubism_norm_mul.gdshader"
    ];

    public static bool EnsureLoaded()
    {
        var extensionPath = ResolveExtensionPath();
        if (!File.Exists(extensionPath))
        {
            Entry.Logger.Error($"[{Entry.ModId}] Missing gd_cubism extension manifest: {extensionPath}");
            return false;
        }

        if (!GDExtensionManager.IsExtensionLoaded(extensionPath))
        {
            var status = GDExtensionManager.LoadExtension(extensionPath);
            Entry.Logger.Info($"[{Entry.ModId}] gd_cubism load status: {status} ({extensionPath})");
            if (status != GDExtensionManager.LoadStatus.Ok)
                return false;
        }

        return ValidateShaderResources();
    }

    private static bool ValidateShaderResources()
    {
        var loaded = 0;
        foreach (var path in ShaderResourcePaths)
        {
            if (!ResourceLoader.Exists(path))
            {
                Entry.Logger.Error($"[{Entry.ModId}] Missing Cubism shader resource: {path}");
                continue;
            }

            try
            {
                if (ResourceLoader.Load<Shader>(path) is null)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to load Cubism shader resource: {path}");
                    continue;
                }

                loaded++;
            }
            catch (Exception exception)
            {
                Entry.Logger.Error($"[{Entry.ModId}] Failed to load Cubism shader resource '{path}': {exception.Message}");
            }
        }

        Entry.Logger.Info($"[{Entry.ModId}] Cubism shader resources ready: {loaded}/{ShaderResourcePaths.Length}.");
        return loaded == ShaderResourcePaths.Length;
    }

    private static string ResolveExtensionPath()
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                ?? AppContext.BaseDirectory;
        var deployed = Path.Combine(assemblyDirectory, "addons", "gd_cubism", "gd_cubism.gdextension");
        if (File.Exists(deployed))
            return Path.GetFullPath(deployed);

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "addons", "gd_cubism", "gd_cubism.gdextension"));
    }
}
