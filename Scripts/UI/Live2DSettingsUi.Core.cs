using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.RuntimeInput;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

internal static partial class Live2DSettingsUi
{
    private const string ModelDetailPageId = "model_detail";
    private const float ModelDetailsTabsMinimumHeight = 680f;
    private static string? _selectedModelId;
    private static Action? _rebuildModelList;
    private static ModSettingsText Text(string key, string fallback) => Live2DLocalization.Text(key, fallback);
    private static string L(string key, string fallback) => Live2DLocalization.Get(key, fallback);
    private static string F(string key, string fallback, params object?[] args)
        => Live2DLocalization.Format(key, fallback, args);

    internal static string SceneName(Live2DSceneKind scene) => scene switch
    {
        Live2DSceneKind.MainMenu => L("scene.main_menu", "Main Menu"),
        Live2DSceneKind.InGame => L("scene.in_game", "In Game"),
        _ => scene.ToString(),
    };

    private static string AnchorName(AnchorPreset anchor) => anchor switch
    {
        AnchorPreset.TopLeft => L("anchor.top_left", "Top Left"),
        AnchorPreset.TopCenter => L("anchor.top_center", "Top Center"),
        AnchorPreset.TopRight => L("anchor.top_right", "Top Right"),
        AnchorPreset.CenterLeft => L("anchor.center_left", "Center Left"),
        AnchorPreset.Center => L("anchor.center", "Center"),
        AnchorPreset.CenterRight => L("anchor.center_right", "Center Right"),
        AnchorPreset.BottomLeft => L("anchor.bottom_left", "Bottom Left"),
        AnchorPreset.BottomCenter => L("anchor.bottom_center", "Bottom Center"),
        AnchorPreset.BottomRight => L("anchor.bottom_right", "Bottom Right"),
        _ => anchor.ToString(),
    };

    public static void Register()
    {
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithTitle(Text("models.title", "Model Management"))
            .WithModDisplayName(Text("page.display_name", "Live2D"))
            .WithDescription(Text("models.description", "Import, organize, and configure Live2D models."))
            .WithSortOrder(0)
            .AddSection("models", section => section
                .WithTitle(Text("models.library_title", "Model Library"))
                .WithDescription(Text("models.description", "Import and configure Live2D models."))
                .AddCustom("model_manager", Text("models.label", "Live2D Models"), CreateModelManager)));

        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .AsChildOf(Entry.ModId)
                .WithTitle(Text("global.title", "Global Configuration"))
                .WithDescription(Text("global.description", "Models inherit values they do not override."))
                .WithSortOrder(10)
                .AddSection("master", section => section
                    .WithTitle(Text("global.master_title", "Master Controls"))
                    .AddCustom("global_hotkeys", Text("global.hotkeys_title", "Global Hotkeys"),
                        _ => CreateGlobalHotkeys()))
                .AddSection("global_packages", section => section
                    .WithTitle(Text("global.packages_title", "Global Configuration Package"))
                    .AddCustom("global_package_manager", Text("global.packages_title", "Global Configuration Package"),
                        CreateGlobalPackageManager))
                .AddSection("main_menu", section => section
                    .WithTitle(Text("global.main_menu_title", "Main Menu Defaults"))
                    .WithDescription(Text("global.scene_description", "Defaults inherited by models without scene-specific overrides."))
                    .Collapsible()
                    .AddCustom("main_menu_editor", Text("scene.main_menu", "Main Menu"),
                        _ => CreateGlobalSceneEditor(Live2DSceneKind.MainMenu, Live2DConfigStore.Get().Global.MainMenu)))
                .AddSection("in_game", section => section
                    .WithTitle(Text("global.in_game_title", "In-Game Defaults"))
                    .WithDescription(Text("global.scene_description", "Defaults inherited by models without scene-specific overrides."))
                    .Collapsible(true)
                    .AddCustom("in_game_editor", Text("scene.in_game", "In Game"),
                        _ => CreateGlobalSceneEditor(Live2DSceneKind.InGame, Live2DConfigStore.Get().Global.InGame)))
                .AddSection("playback", section => section
                    .WithTitle(Text("global.playback_title", "Playback and Rendering"))
                    .WithDescription(Text("global.playback_description", "Shared animation, physics, and mask defaults."))
                    .Collapsible(true)
                    .AddCustom("playback_editor", Text("global.playback_title", "Playback and Rendering"),
                        _ => CreateGlobalPlaybackEditor())),
            "global");

        RitsuLibFramework.RegisterModSettings(
            Entry.ModId,
            page => page
                .AsChildOf(Entry.ModId)
                .WithSidebarVisibleOnlyWhenActive()
                .WithTitle(Text("model_details.title", "Model Details"))
                .WithDescription(Text("model_details.description", "Configure scenes and action hotkeys for the selected model."))
                .WithSortOrder(20)
                .AddSection("details", section => section
                    .WithTitle(Text("model_details.title", "Model Details"))
                    .AddCustom("model_details_editor", Text("model_details.title", "Model Details"), CreateSelectedModelDetails)),
            ModelDetailPageId);

        var modelsRegistered = ModSettingsRegistry.TryGetPage(Entry.ModId, Entry.ModId, out _);
        var globalRegistered = ModSettingsRegistry.TryGetPage(Entry.ModId, "global", out _);
        var detailRegistered = ModSettingsRegistry.TryGetPage(Entry.ModId, ModelDetailPageId, out _);
        if (modelsRegistered && globalRegistered && detailRegistered)
            Entry.Logger.Info($"[{Entry.ModId}] Registered model-management, global-configuration, and model-detail pages.");
        else
            Entry.Logger.Error($"[{Entry.ModId}] Settings page registration is incomplete: " +
                               $"models={modelsRegistered}, global={globalRegistered}, detail={detailRegistered}.");
    }

    private static Control CreateModelManager(IModSettingsUiActionHost uiHost)
    {
        var removedMissingModels = Live2DConfigStore.PruneMissingModels();
        if (removedMissingModels > 0)
        {
            Live2DRuntimeManager.RefreshAll();
            Live2DHotkeyManager.Refresh();
        }
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 14);
        var settings = Live2DConfigStore.Get();

        var summary = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var summaryText = new Label
        {
            Text = F("models.summary_status", "{0} models · {1} enabled",
                settings.Models.Count, settings.Models.Count(model => model.Enabled)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        summaryText.AddThemeFontSizeOverride("font_size", 20);
        summaryText.AddThemeColorOverride("font_color", new Color(0.55f, 0.8f, 1f));
        summary.AddChild(summaryText);
        root.AddChild(WrapCard(summary, new Color(0.22f, 0.55f, 0.9f)));

        var modelList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        modelList.AddThemeConstantOverride("separation", 14);
        Button restoreExternalButton = null!;
        Action rebuildModelList = null!;
        rebuildModelList = () =>
        {
            if (!GodotObject.IsInstanceValid(modelList))
                return;
            foreach (var child in modelList.GetChildren())
            {
                modelList.RemoveChild(child);
                child.QueueFree();
            }

            var latestSettings = Live2DConfigStore.Get();
            summaryText.Text = F("models.summary_status", "{0} models · {1} enabled",
                latestSettings.Models.Count, latestSettings.Models.Count(model => model.Enabled));
            restoreExternalButton.Disabled = latestSettings.RemovedExternalModelIds.Count == 0;
            restoreExternalButton.Text = F(
                "button.restore_external_models",
                "Restore Provider Models ({0})",
                latestSettings.RemovedExternalModelIds.Count);
            if (latestSettings.Models.Count == 0)
            {
                modelList.AddChild(CreateEmptyModelCard());
            }
            else
            {
                foreach (var model in latestSettings.Models.OrderBy(value => value.DisplayOrder))
                    modelList.AddChild(CreateModelRow(model, uiHost, rebuildModelList));
            }
            Entry.Logger.Info($"[{Entry.ModId}] Rebuilt model list in place: {latestSettings.Models.Count} model(s).");
        };
        _rebuildModelList = rebuildModelList;

        var modelToolbar = new HBoxContainer();
        modelToolbar.AddChild(CreateToolbarLabel(L("models.toolbar_models", "Models")));
        var addButton = new Button { Text = L("button.add_model", "+ Add Live2D Model") };
        addButton.Pressed += () => ShowImportDialog(uiHost, rebuildModelList);
        modelToolbar.AddChild(addButton);

        var openFolderButton = new Button { Text = L("button.open_folder", "Open Model Folder") };
        openFolderButton.Pressed += () => OS.ShellOpen(Live2DModelRepository.ModelsDirectory);
        modelToolbar.AddChild(openFolderButton);
        restoreExternalButton = new Button();
        restoreExternalButton.Pressed += () =>
        {
            var restored = Live2DConfigStore.RestoreRemovedExternalModels();
            if (restored <= 0)
                return;
            rebuildModelList();
            uiHost.RequestRefreshAfterDataModelBatchChange();
            Entry.Logger.Info($"[{Entry.ModId}] Restored {restored} removed provider model(s).");
        };
        modelToolbar.AddChild(restoreExternalButton);
        root.AddChild(WrapCard(modelToolbar));

        var packageToolbar = new HBoxContainer();
        packageToolbar.AddChild(CreateToolbarLabel(L("models.toolbar_packages", "Packages")));
        var importButton = new Button { Text = L("button.import_pack", "Import Package") };
        importButton.Pressed += () => ShowPackImportDialog(uiHost, rebuildModelList);
        importButton.TooltipText = L("tooltip.import_pack", "Import models while keeping local global defaults.");
        packageToolbar.AddChild(importButton);

        var exportButton = new Button { Text = L("button.export_pack", "Export Package") };
        exportButton.Pressed += ShowPackExportDialog;
        packageToolbar.AddChild(exportButton);
        root.AddChild(WrapCard(packageToolbar));
        root.AddChild(modelList);
        rebuildModelList();
        return root;
    }

    private static Control CreateEmptyModelCard() => WrapCard(new Label
    {
        Text = L("models.empty", "No models have been imported."),
        HorizontalAlignment = HorizontalAlignment.Center,
        CustomMinimumSize = new Vector2(0f, 72f),
        VerticalAlignment = VerticalAlignment.Center,
    });

    private static Label CreateToolbarLabel(string text) => new()
    {
        Text = text,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static PanelContainer WrapCard(Control content, Color? borderColor = null)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.065f, 0.085f, 0.92f),
            BorderColor = borderColor ?? new Color(0.3f, 0.34f, 0.42f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        };
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", style);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        margin.AddChild(content);
        panel.AddChild(margin);
        return panel;
    }

    private static Control CreateGlobalHotkeys()
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 16);

        row.AddChild(new Label
        {
            Text = L("global.toggle_visibility_hotkey", "Enable / Disable All Live2D"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var hotkey = new ModSettingsKeyBindingControl(
            Live2DConfigStore.Get().Global.Hotkeys.ToggleVisibility,
            allowModifierCombos: true,
            allowModifierOnly: false,
            distinguishModifierSides: false,
            onChanged: value => ModifyGlobalHotkey(target => target.ToggleVisibility = NormalizeHotkey(value)),
            allowActionBindings: false)
        {
            CustomMinimumSize = new Vector2(600f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            TooltipText = L("global.toggle_visibility_hotkey_tip", "Hide all Live2D models, or restore their configured visibility."),
        };
        HideKeyBindingHint(hotkey);
        row.AddChild(hotkey);
        return row;
    }

    private static Control CreateGlobalPackageManager(IModSettingsUiActionHost uiHost)
    {
        var toolbar = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        toolbar.AddThemeConstantOverride("separation", 12);

        toolbar.AddChild(CreateActionButton(
            L("button.import_global_config", "Import Global Configuration"),
            () => ShowGlobalImportDialog(uiHost)));
        toolbar.AddChild(CreateActionButton(
            L("button.export_global_config", "Export Global Configuration"),
            ShowGlobalExportDialog));
        return toolbar;
    }

    private static Button CreateActionButton(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(260f, 0f),
        };
        button.Pressed += pressed;
        return button;
    }

    private static void HideKeyBindingHint(ModSettingsKeyBindingControl control)
    {
        // RitsuLib 暂未提供隐藏录制说明的公开接口，只隐藏直属提示标签，不影响按键捕获逻辑。
        foreach (var hint in control.GetChildren().OfType<Label>())
            hint.Hide();
        control.CustomMinimumSize = new Vector2(control.CustomMinimumSize.X, 0f);
    }

}
