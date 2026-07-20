using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

internal static partial class Live2DSettingsUi
{
    private static void ShowImportDialog(IModSettingsUiActionHost uiHost, Action rebuildModelList)
    {
        ShowNativeFileDialog(
            L("dialog.select_model", "Select a Live2D .model3.json File"),
            FileDialog.FileModeEnum.OpenFile,
            "*.model3.json",
            "Live2D Model",
            path =>
            {
                try
                {
                    var model = Live2DConfigStore.ImportModel(path);
                    Live2DHotkeyManager.Refresh();
                    Entry.Logger.Info($"[{Entry.ModId}] Imported model '{model.DisplayName}' ({model.Id}).");
                    rebuildModelList();
                    uiHost.RequestRefreshAfterDataModelBatchChange();
                }
                catch (Exception ex)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to import model '{path}': {ex}");
                }
            });
    }

    private static void ShowPackExportDialog()
    {
        ShowPackageDialog(
            L("dialog.export_pack", "Export Live2D Package"),
            FileDialog.FileModeEnum.SaveFile,
            $"Live2D-{DateTime.Now:yyyyMMdd-HHmm}.live2dpack",
            path =>
            {
                try
                {
                    Live2DPackService.ExportAll(path);
                    Entry.Logger.Info($"[{Entry.ModId}] Exported Live2D package: {path}");
                }
                catch (Exception ex)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to export package '{path}': {ex}");
                }
            });
    }

    private static void ShowGlobalExportDialog()
    {
        ShowPackageDialog(
            L("dialog.export_global_config", "Export Global Configuration"),
            FileDialog.FileModeEnum.SaveFile,
            $"Live2D-Global-{DateTime.Now:yyyyMMdd-HHmm}.live2dpack",
            path =>
            {
                try
                {
                    Live2DPackService.ExportGlobal(path);
                    Entry.Logger.Info($"[{Entry.ModId}] Exported global configuration package: {path}");
                }
                catch (Exception ex)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to export global configuration to '{path}': {ex}");
                }
            });
    }

    private static void ShowGlobalImportDialog(IModSettingsUiActionHost uiHost)
    {
        ShowPackageDialog(
            L("dialog.import_global_config", "Import Global Configuration"),
            FileDialog.FileModeEnum.OpenFile,
            null,
            path =>
            {
                try
                {
                    Live2DPackService.ImportGlobal(path);
                    uiHost.RequestRefresh();
                    Entry.Logger.Info($"[{Entry.ModId}] Imported global configuration package: {path}");
                }
                catch (Exception ex)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to import global configuration from '{path}': {ex}");
                }
            });
    }

    private static void ShowModelPackExportDialog(string modelId, string displayName)
    {
        ShowPackageDialog(
            F("dialog.export_model_pack", "Export Package — {0}", displayName),
            FileDialog.FileModeEnum.SaveFile,
            $"{SanitizeFileName(displayName)}-{DateTime.Now:yyyyMMdd-HHmm}.live2dpack",
            path =>
            {
                try
                {
                    Live2DPackService.ExportModel(path, modelId);
                    Entry.Logger.Info($"[{Entry.ModId}] Exported package for model {modelId}: {path}");
                }
                catch (Exception ex)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to export package for model {modelId} to '{path}': {ex}");
                }
            });
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return result.Length == 0 ? "Live2D-Model" : result;
    }

    private static void ShowPackImportDialog(
        IModSettingsUiActionHost uiHost,
        Action rebuildModelList)
    {
        ShowPackageDialog(
            L("dialog.import_keep", "Import Live2D Package"),
            FileDialog.FileModeEnum.OpenFile,
            null,
            path =>
            {
                try
                {
                    var summary = Live2DPackService.Import(path);
                    Entry.Logger.Info(
                        $"[{Entry.ModId}] Imported package '{path}': imported={summary.ImportedModels}, " +
                        $"duplicates={summary.SkippedDuplicates}.");
                    rebuildModelList();
                    uiHost.RequestRefreshAfterDataModelBatchChange();
                }
                catch (Exception ex)
                {
                    Entry.Logger.Error($"[{Entry.ModId}] Failed to import package '{path}': {ex}");
                }
            });
    }

    private static void ShowPackageDialog(
        string title,
        FileDialog.FileModeEnum mode,
        string? currentFile,
        Action<string> selected)
    {
        ShowNativeFileDialog(title, mode, "*.live2dpack", "Live2D Package", selected, currentFile);
    }

    private static void ShowNativeFileDialog(
        string title,
        FileDialog.FileModeEnum mode,
        string filter,
        string filterName,
        Action<string> selected,
        string? currentFile = null)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return;

        // 使用系统原生窗口，让标题、按钮和路径区域自动跟随操作系统语言。
        var dialog = new FileDialog
        {
            ModeOverridesTitle = false,
            UseNativeDialog = true,
            Title = title,
            FileMode = mode,
            Access = FileDialog.AccessEnum.Filesystem,
            Size = new Vector2I(900, 620),
        };
        if (!string.IsNullOrWhiteSpace(currentFile))
            dialog.CurrentFile = currentFile;
        dialog.AddFilter(filter, filterName);
        dialog.FileSelected += path =>
        {
            try
            {
                selected(path);
            }
            finally
            {
                dialog.QueueFree();
            }
        };
        dialog.Canceled += dialog.QueueFree;
        tree.Root.AddChild(dialog);
        dialog.PopupCentered();
    }

    private static void ModifyModel(string modelId, Action<Live2DModelConfig> mutation)
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings =>
        {
            var model = settings.Models.FirstOrDefault(value => value.Id == modelId);
            if (model != null)
                mutation(model);
        });
        Live2DConfigStore.SaveAndRefresh();
        Live2DHotkeyManager.Refresh();
    }

    private static void ModifyScene(
        string modelId,
        Live2DSceneKind scene,
        Action<SceneDisplayOverrides> mutation)
    {
        ModifyModel(modelId, model => mutation(scene switch
        {
            Live2DSceneKind.MainMenu => model.Overrides.MainMenu,
            Live2DSceneKind.InGame => model.Overrides.InGame,
            _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null),
        }));
    }

    private static void ModifyRendering(string modelId, Action<RenderingOverrides> mutation)
        => ModifyModel(modelId, model => mutation(model.Overrides.Rendering));

    private static void ModifyGlobal(Action<GlobalLive2DConfig> mutation)
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings => mutation(settings.Global));
        Live2DConfigStore.SaveAndRefresh();
    }

    private static void ModifyGlobalHotkey(Action<GlobalHotkeyConfig> mutation)
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings => mutation(settings.Global.Hotkeys));
        store.Save(Live2DConfigStore.SettingsKey);
        Live2DHotkeyManager.Refresh();
    }

    private static string NormalizeHotkey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return Live2DHotkeyManager.NormalizeBindingForStorage(value);
    }

    private static void ModifyGlobalScene(Live2DSceneKind scene, Action<SceneDisplayConfig> mutation)
    {
        ModifyGlobal(global => mutation(scene switch
        {
            Live2DSceneKind.MainMenu => global.MainMenu,
            Live2DSceneKind.InGame => global.InGame,
            _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null),
        }));
    }

    private static void UpdateActionBinding(
        string modelId,
        Live2DActionDescriptor action,
        Action<ActionBindingConfig> mutation)
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings =>
        {
            var model = settings.Models.FirstOrDefault(value => value.Id == modelId);
            if (model == null)
                return;

            var binding = model.ActionBindings.FirstOrDefault(value => Matches(value, action));
            if (binding == null)
            {
                binding = new ActionBindingConfig
                {
                    Kind = action.Kind,
                    MotionGroup = action.MotionGroup,
                    MotionIndex = action.MotionIndex,
                    ExpressionId = action.ExpressionId,
                };
                model.ActionBindings.Add(binding);
            }
            mutation(binding);
        });
        store.Save(Live2DConfigStore.SettingsKey);
        Live2DHotkeyManager.Refresh();
    }

    private static void RemoveModel(string modelId)
    {
        var store = RitsuLibFramework.GetDataStore(Entry.ModId);
        Live2DModelConfig? removedModel = null;
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey, settings =>
        {
            removedModel = settings.Models.FirstOrDefault(value => value.Id == modelId);
            if (removedModel == null)
                return;
            if (removedModel.IsExternalPackModel &&
                !settings.RemovedExternalModelIds.Contains(modelId, StringComparer.OrdinalIgnoreCase))
                settings.RemovedExternalModelIds.Add(modelId);
            settings.Models.RemoveAll(value => value.Id == modelId);
            for (var index = 0; index < settings.Models.Count; index++)
                settings.Models[index].DisplayOrder = index;
        });
        if (removedModel == null)
            return;
        store.Save(Live2DConfigStore.SettingsKey);
        Live2DRuntimeManager.RefreshAll();
        Live2DHotkeyManager.Refresh();
        if (removedModel.IsExternalPackModel)
        {
            Entry.Logger.Info(
                $"[{Entry.ModId}] Removed provider model {modelId} from the library without deleting provider-owned files.");
        }
        else
        {
            DeleteModelFilesAfterRuntimeRefresh(modelId);
        }
    }

    private static async void DeleteModelFilesAfterRuntimeRefresh(string modelId)
    {
        try
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Live2DModelRepository.DeleteFiles(modelId);
                    Entry.Logger.Info($"[{Entry.ModId}] Deleted model {modelId} and its managed files.");
                    return;
                }
                catch (IOException) when (attempt < 3)
                {
                    await Task.Delay(100 * attempt);
                }
                catch (UnauthorizedAccessException) when (attempt < 3)
                {
                    await Task.Delay(100 * attempt);
                }
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"[{Entry.ModId}] Model {modelId} was removed, but its files could not be cleaned up: {ex.Message}");
        }
    }
}

