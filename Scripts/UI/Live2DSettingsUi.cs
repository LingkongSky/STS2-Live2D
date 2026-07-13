using Godot;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using Live2D.Scripts.Runtime;
using STS2RitsuLib;
using STS2RitsuLib.RuntimeInput;
using STS2RitsuLib.Settings;

namespace Live2D.Scripts.UI;

public static class Live2DSettingsUi
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
            Text = F("models.summary", "{0} models", settings.Models.Count),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        summaryText.AddThemeFontSizeOverride("font_size", 20);
        summaryText.AddThemeColorOverride("font_color", new Color(0.55f, 0.8f, 1f));
        summary.AddChild(summaryText);
        root.AddChild(WrapCard(summary, new Color(0.22f, 0.55f, 0.9f)));

        var modelList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        modelList.AddThemeConstantOverride("separation", 14);
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
            summaryText.Text = F("models.summary", "{0} models", latestSettings.Models.Count);
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

    private static Control CreateGlobalPlaybackEditor()
    {
        var global = Live2DConfigStore.Get().Global;
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddGlobalFloat(grid, L("field.speed", "Playback Speed"), global.Playback.Speed, 0, 4, 0.05,
            value => ModifyGlobal(target => target.Playback.Speed = value));
        AddGlobalBool(grid, L("field.physics", "Physics"), global.Playback.EnablePhysics,
            value => ModifyGlobal(target => target.Playback.EnablePhysics = value));
        AddGlobalBool(grid, L("field.pose", "Pose Processing"), global.Playback.EnablePose,
            value => ModifyGlobal(target => target.Playback.EnablePose = value));
        AddGlobalBool(grid, L("field.auto_idle", "Auto-play Idle"), global.Playback.AutoPlayIdle,
            value => ModifyGlobal(target => target.Playback.AutoPlayIdle = value));
        AddGlobalFloat(grid, L("field.cooldown", "Action Cooldown (seconds)"), global.Playback.ActionCooldownSeconds, 0, 10, 0.05,
            value => ModifyGlobal(target => target.Playback.ActionCooldownSeconds = value));
        AddGlobalInt(grid, L("field.mask_size", "Mask Viewport Size (0 = Auto)"), global.Rendering.MaskViewportSize, 0, 4096,
            value => ModifyGlobal(target => target.Rendering.MaskViewportSize = value));
        root.AddChild(grid);
        return root;
    }

    private static Control CreateGlobalSceneEditor(Live2DSceneKind scene, SceneDisplayConfig config)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddGlobalBool(grid, L("field.visible", "Visible"), config.Visible,
            value => ModifyGlobalScene(scene, target => target.Visible = value));
        AddGlobalAnchor(grid, config.Anchor,
            value => ModifyGlobalScene(scene, target => target.Anchor = value));
        AddGlobalFloat(grid, L("field.offset_x", "Horizontal Offset"), config.OffsetX, -4000, 4000, 1,
            value => ModifyGlobalScene(scene, target => target.OffsetX = value));
        AddGlobalFloat(grid, L("field.offset_y", "Vertical Offset"), config.OffsetY, -4000, 4000, 1,
            value => ModifyGlobalScene(scene, target => target.OffsetY = value));
        AddGlobalFloat(grid, L("field.scale", "Scale"), config.Scale, 0.01, 4, 0.01,
            value => ModifyGlobalScene(scene, target => target.Scale = value));
        AddGlobalFloat(grid, L("field.rotation", "Rotation"), config.RotationDegrees, -180, 180, 1,
            value => ModifyGlobalScene(scene, target => target.RotationDegrees = value));
        AddGlobalFloat(grid, L("field.opacity", "Opacity"), config.Opacity, 0, 1, 0.01,
            value => ModifyGlobalScene(scene, target => target.Opacity = value));
        AddGlobalInt(grid, L("field.layer", "Display Layer"), config.Layer, -100, 100,
            value => ModifyGlobalScene(scene, target => target.Layer = value));
        AddGlobalBool(grid, L("field.mouse", "Mouse Interaction"), config.MouseInteraction,
            value => ModifyGlobalScene(scene, target => target.MouseInteraction = value));
        box.AddChild(grid);
        return box;
    }

    private static void AddGlobalBool(GridContainer grid, string label, bool value, Action<bool> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new CheckBox
        {
            Text = value ? L("state.on", "On") : L("state.off", "Off"),
            ButtonPressed = value,
        };
        input.Toggled += active =>
        {
            input.Text = active ? L("state.on", "On") : L("state.off", "Off");
            changed(active);
        };
        grid.AddChild(input);
    }

    private static void AddGlobalAnchor(GridContainer grid, AnchorPreset value, Action<AnchorPreset> changed)
    {
        grid.AddChild(new Label { Text = L("field.anchor", "Anchor") });
        var input = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var anchor in Enum.GetValues<AnchorPreset>())
            input.AddItem(AnchorName(anchor));
        input.Selected = (int)value;
        input.ItemSelected += index => changed((AnchorPreset)index);
        grid.AddChild(input);
    }

    private static void AddGlobalFloat(
        GridContainer grid,
        string label,
        float value,
        double min,
        double max,
        double step,
        Action<float> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += newValue => changed((float)newValue);
        grid.AddChild(input);
    }

    private static void AddGlobalInt(
        GridContainer grid,
        string label,
        int value,
        int min,
        int max,
        Action<int> changed)
    {
        grid.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.ValueChanged += newValue => changed((int)newValue);
        grid.AddChild(input);
    }

    private static Control CreateModelRow(
        Live2DModelConfig model,
        IModSettingsUiActionHost uiHost,
        Action onDeleted)
    {
        var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeConstantOverride("separation", 10);
        var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var name = new Label
        {
            Text = F("model.summary", "{0} · Actions/Expressions {1}", model.DisplayName, model.AvailableActions.Count),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        name.AddThemeFontSizeOverride("font_size", 20);
        titleRow.AddChild(name);

        var configure = new Button
        {
            Text = L("button.configure", "Configure"),
            CustomMinimumSize = new Vector2(120f, 0f),
        };
        configure.Pressed += () =>
        {
            _selectedModelId = model.Id;
            NavigateToPage(configure, ModelDetailPageId);
        };
        titleRow.AddChild(configure);
        var exportModel = new Button
        {
            Text = L("button.export_model_pack", "Export Package"),
            CustomMinimumSize = new Vector2(140f, 0f),
        };
        exportModel.Pressed += () => ShowModelPackExportDialog(model.Id, model.DisplayName);
        titleRow.AddChild(exportModel);
        card.AddChild(titleRow);

        var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

        var previewEditor = new Button { Text = L("button.preview_adjust", "Preview & Adjust") };
        previewEditor.Pressed += () => Live2DPreviewEditor.Show(model.Id, uiHost);
        actions.AddChild(previewEditor);

        var delete = new Button { Text = L("button.delete", "Delete") };
        PanelContainer? modelCard = null;
        delete.Pressed += () =>
        {
            delete.Disabled = true;
            try
            {
                if (_selectedModelId == model.Id)
                    _selectedModelId = null;
                RemoveModel(model.Id);
                modelCard?.Hide();
                modelCard?.QueueFree();
                onDeleted();
                uiHost.RequestRefreshAfterDataModelBatchChange();
            }
            catch (Exception ex)
            {
                delete.Disabled = false;
                Entry.Logger.Error($"[{Entry.ModId}] Failed to delete model {model.Id}: {ex}");
            }
        };
        actions.AddChild(delete);
        card.AddChild(actions);
        modelCard = WrapCard(card);
        return modelCard;
    }

    private static Control CreateSelectedModelDetails(IModSettingsUiActionHost uiHost)
    {
        var model = Live2DConfigStore.Get().Models.FirstOrDefault(value => value.Id == _selectedModelId);
        if (model == null)
            return new Label
            {
                Text = L("model_details.none", "Select a model from Model Management first."),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(0f, 240f),
            };

        // 详情页、标签容器和滚动区必须逐层允许扩展，快捷键列表才能占用页面剩余高度。
        var details = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        details.AddThemeConstantOverride("separation", 12);
        var header = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var identity = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        identity.AddChild(new Label { Text = L("field.model_name", "Model Name") });
        var renameRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var nameInput = new LineEdit
        {
            Text = model.DisplayName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        renameRow.AddChild(nameInput);
        var rename = new Button { Text = L("button.rename", "Rename") };
        Action commitRename = () =>
        {
            var newName = nameInput.Text.Trim();
            if (newName.Length == 0)
            {
                nameInput.Text = model.DisplayName;
                return;
            }
            if (newName == model.DisplayName)
                return;
            ModifyModel(model.Id, target => target.DisplayName = newName);
            model.DisplayName = newName;
            nameInput.Text = newName;
            _rebuildModelList?.Invoke();
            Entry.Logger.Info($"[{Entry.ModId}] Renamed model {model.Id} to '{newName}'.");
        };
        rename.Pressed += commitRename;
        nameInput.TextSubmitted += _ => commitRename();
        renameRow.AddChild(rename);
        identity.AddChild(renameRow);
        identity.AddChild(new Label
        {
            Text = F("model.path", "Resource: {0}", model.ModelRelativePath),
            Modulate = new Color(1f, 1f, 1f, 0.65f),
        });
        header.AddChild(identity);
        var preview = new Button { Text = L("button.preview_adjust", "Preview & Adjust") };
        preview.Pressed += () => Live2DPreviewEditor.Show(model.Id, uiHost);
        header.AddChild(preview);
        details.AddChild(WrapCard(header, new Color(0.72f, 0.55f, 0.25f)));

        var settings = Live2DConfigStore.Get();
        var tabs = new TabContainer
        {
            // RitsuLib 的页面栈按最小高度布局，需在这里为详情列表预留完整的纵向空间。
            CustomMinimumSize = new Vector2(0f, ModelDetailsTabsMinimumHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        tabs.AddChild(CreateModelTab(
            SceneName(Live2DSceneKind.MainMenu),
            CreateSceneEditor(model.Id, Live2DSceneKind.MainMenu, model.Overrides.MainMenu, settings.Global.MainMenu)));
        tabs.AddChild(CreateModelTab(
            SceneName(Live2DSceneKind.InGame),
            CreateSceneEditor(model.Id, Live2DSceneKind.InGame, model.Overrides.InGame, settings.Global.InGame)));
        tabs.AddChild(CreateModelTab(L("actions.tab", "Hotkeys"), CreateActionEditor(model), scroll: true));
        details.AddChild(tabs);
        return details;
    }

    private static void NavigateToPage(Control source, string pageId)
    {
        for (Node? current = source; current != null; current = current.GetParent())
        {
            if (current is not RitsuModSettingsSubmenu submenu)
                continue;
            submenu.NavigateToPage(pageId);
            return;
        }
        Entry.Logger.Error($"[{Entry.ModId}] Unable to find the RitsuLib settings host for page navigation.");
    }

    private static Control CreateModelTab(string title, Control content, bool scroll = false)
    {
        Control host;
        if (scroll)
        {
            var container = new ScrollContainer
            {
                Name = title,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            container.AddChild(content);
            host = container;
        }
        else
        {
            var margin = new MarginContainer
            {
                Name = title,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
                margin.AddThemeConstantOverride(side, 12);
            margin.AddChild(content);
            host = margin;
        }
        return host;
    }

    private static Control CreateActionEditor(Live2DModelConfig model)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        if (model.AvailableActions.Count == 0)
        {
            root.AddChild(new Label { Text = L("actions.empty", "This model declares no actions or expressions.") });
            return root;
        }

        var duplicateBindings = model.ActionBindings
            .Where(value => !string.IsNullOrWhiteSpace(value.KeyBinding))
            .GroupBy(value => value.KeyBinding, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in model.AvailableActions)
        {
            var binding = FindBinding(model, action);
            var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddChild(new Label
            {
                Text = action.Kind == Live2DActionKind.Expression
                    ? F("action.expression", "Expression: {0}", action.ExpressionId)
                    : F("action.motion", "Motion: {0} / {1}", action.MotionGroup, action.MotionIndex),
            });

            var keyControl = new ModSettingsKeyBindingControl(
                binding?.KeyBinding ?? "",
                allowModifierCombos: true,
                allowModifierOnly: false,
                distinguishModifierSides: false,
                onChanged: value => UpdateActionBinding(model.Id, action,
                    target => target.KeyBinding = NormalizeHotkey(value)),
                allowActionBindings: false);
            HideKeyBindingHint(keyControl);

            var options = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            options.AddChild(CreateBindingToggle(SceneName(Live2DSceneKind.MainMenu), binding?.MainMenu ?? true,
                value => UpdateActionBinding(model.Id, action, target => target.MainMenu = value)));
            options.AddChild(CreateBindingToggle(SceneName(Live2DSceneKind.InGame), binding?.InGame ?? true,
                value => UpdateActionBinding(model.Id, action, target => target.InGame = value)));
            if (action.Kind == Live2DActionKind.Motion)
            {
                options.AddChild(CreateBindingToggle(L("field.loop", "Loop"), binding?.Loop ?? false,
                    value => UpdateActionBinding(model.Id, action, target => target.Loop = value)));
            }

            var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            actionRow.AddThemeConstantOverride("separation", 16);
            actionRow.AddChild(options);
            actionRow.AddChild(keyControl);
            card.AddChild(actionRow);

            if (binding != null && duplicateBindings.Contains(binding.KeyBinding))
                card.AddChild(new Label
                {
                    Text = L("action.conflict", "⚠ Duplicate hotkey; multiple actions will play."),
                });

            root.AddChild(WrapCard(card));
        }
        return root;
    }

    private static CheckBox CreateBindingToggle(string text, bool initial, Action<bool> changed)
    {
        var toggle = new CheckBox { Text = text, ButtonPressed = initial };
        toggle.Toggled += value => changed(value);
        return toggle;
    }

    private static ActionBindingConfig? FindBinding(Live2DModelConfig model, Live2DActionDescriptor action)
        => model.ActionBindings.FirstOrDefault(binding => Matches(binding, action));

    private static bool Matches(ActionBindingConfig binding, Live2DActionDescriptor action)
        => binding.Kind == action.Kind
           && (action.Kind == Live2DActionKind.Expression
               ? binding.ExpressionId == action.ExpressionId
               : binding.MotionGroup == action.MotionGroup && binding.MotionIndex == action.MotionIndex);

    private static Control CreateSceneEditor(
        string modelId,
        Live2DSceneKind scene,
        SceneDisplayOverrides overrides,
        SceneDisplayConfig global)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var grid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        box.AddChild(grid);

        AddTriStateBool(grid, L("field.visible", "Visible"), overrides.Visible, global.Visible,
            value => ModifyScene(modelId, scene, target => target.Visible = value));
        AddAnchor(grid, overrides.Anchor, global.Anchor,
            value => ModifyScene(modelId, scene, target => target.Anchor = value));
        AddNullableFloat(grid, L("field.offset_x", "Horizontal Offset"), overrides.OffsetX, global.OffsetX, -4000, 4000, 1,
            value => ModifyScene(modelId, scene, target => target.OffsetX = value));
        AddNullableFloat(grid, L("field.offset_y", "Vertical Offset"), overrides.OffsetY, global.OffsetY, -4000, 4000, 1,
            value => ModifyScene(modelId, scene, target => target.OffsetY = value));
        AddNullableFloat(grid, L("field.scale", "Scale"), overrides.Scale, global.Scale, 0.01, 4, 0.01,
            value => ModifyScene(modelId, scene, target => target.Scale = value));
        AddNullableFloat(grid, L("field.rotation", "Rotation"), overrides.RotationDegrees, global.RotationDegrees, -180, 180, 1,
            value => ModifyScene(modelId, scene, target => target.RotationDegrees = value));
        AddNullableFloat(grid, L("field.opacity", "Opacity"), overrides.Opacity, global.Opacity, 0, 1, 0.01,
            value => ModifyScene(modelId, scene, target => target.Opacity = value));
        AddNullableInt(grid, L("field.layer", "Display Layer"), overrides.Layer, global.Layer, -100, 100,
            value => ModifyScene(modelId, scene, target => target.Layer = value));
        AddTriStateBool(grid, L("field.mouse", "Mouse Interaction"), overrides.MouseInteraction, global.MouseInteraction,
            value => ModifyScene(modelId, scene, target => target.MouseInteraction = value));
        return box;
    }

    private static void AddTriStateBool(
        GridContainer grid,
        string label,
        bool? overrideValue,
        bool globalValue,
        Action<bool?> changed)
    {
        grid.AddChild(new Label { Text = label });
        var choice = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        choice.AddItem(F("state.inherit_bool", "Inherit Global ({0})",
            globalValue ? L("state.on", "On") : L("state.off", "Off")));
        choice.AddItem(L("state.custom_on", "Custom: On"));
        choice.AddItem(L("state.custom_off", "Custom: Off"));
        choice.Selected = overrideValue switch { true => 1, false => 2, _ => 0 };
        choice.ItemSelected += index => changed(index switch { 1 => true, 2 => false, _ => null });
        grid.AddChild(choice);
    }

    private static void AddAnchor(
        GridContainer grid,
        AnchorPreset? overrideValue,
        AnchorPreset globalValue,
        Action<AnchorPreset?> changed)
    {
        grid.AddChild(new Label { Text = L("field.anchor", "Anchor") });
        var choice = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        choice.AddItem(F("state.inherit_anchor", "Inherit Global ({0})", AnchorName(globalValue)));
        foreach (var value in Enum.GetValues<AnchorPreset>())
            choice.AddItem(AnchorName(value));
        choice.Selected = overrideValue.HasValue ? (int)overrideValue.Value + 1 : 0;
        choice.ItemSelected += index => changed(index == 0 ? null : (AnchorPreset?)(index - 1));
        grid.AddChild(choice);
    }

    private static void AddNullableFloat(
        GridContainer grid,
        string label,
        float? overrideValue,
        float globalValue,
        double min,
        double max,
        double step,
        Action<float?> changed)
    {
        var custom = new CheckBox { Text = label, ButtonPressed = overrideValue.HasValue };
        grid.AddChild(custom);
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = overrideValue ?? globalValue,
            Editable = overrideValue.HasValue,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = overrideValue.HasValue
                ? L("state.custom_value", "Model-specific value")
                : F("state.inherited_value", "Inherited global value: {0}", globalValue),
        };
        custom.Text = overrideValue.HasValue
            ? F("state.label_custom", "{0} (Custom)", label)
            : F("state.label_inherit", "{0} (Inherited)", label);
        custom.Toggled += active =>
        {
            input.Editable = active;
            custom.Text = active
                ? F("state.label_custom", "{0} (Custom)", label)
                : F("state.label_inherit", "{0} (Inherited)", label);
            changed(active ? (float)input.Value : null);
        };
        input.ValueChanged += value =>
        {
            if (custom.ButtonPressed)
                changed((float)value);
        };
        grid.AddChild(input);
    }

    private static void AddNullableInt(
        GridContainer grid,
        string label,
        int? overrideValue,
        int globalValue,
        int min,
        int max,
        Action<int?> changed)
    {
        var custom = new CheckBox { Text = label, ButtonPressed = overrideValue.HasValue };
        grid.AddChild(custom);
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = overrideValue ?? globalValue,
            Editable = overrideValue.HasValue,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        custom.Text = overrideValue.HasValue
            ? F("state.label_custom", "{0} (Custom)", label)
            : F("state.label_inherit", "{0} (Inherited)", label);
        custom.Toggled += active =>
        {
            input.Editable = active;
            custom.Text = active
                ? F("state.label_custom", "{0} (Custom)", label)
                : F("state.label_inherit", "{0} (Inherited)", label);
            changed(active ? (int)input.Value : null);
        };
        input.ValueChanged += value =>
        {
            if (custom.ButtonPressed)
                changed((int)value);
        };
        grid.AddChild(input);
    }

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
        return RuntimeHotkeyService.TryNormalizeBinding(value, out var normalized) ? normalized : value.Trim();
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
        store.Modify<Live2DSettings>(Live2DConfigStore.SettingsKey,
            settings => settings.Models.RemoveAll(value => value.Id == modelId));
        store.Save(Live2DConfigStore.SettingsKey);
        Live2DRuntimeManager.RefreshAll();
        Live2DHotkeyManager.Refresh();
        DeleteModelFilesAfterRuntimeRefresh(modelId);
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
