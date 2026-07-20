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
    private static Control CreateModelRow(
        Live2DModelConfig model,
        IModSettingsUiActionHost uiHost,
        Action onDeleted)
    {
        var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeConstantOverride("separation", 10);
        var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var enabled = new CheckBox
        {
            Text = L("field.enabled", "Enabled"),
            ButtonPressed = model.Enabled,
            CustomMinimumSize = new Vector2(110f, 0f),
            TooltipText = L("field.enabled_tip", "Disabled models do not render, run, accept input, or register hotkeys."),
        };
        titleRow.AddChild(enabled);
        var name = new Label
        {
            Text = F("model.summary", "{0} · Actions/Expressions {1}", model.DisplayName, model.AvailableActions.Count),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        name.AddThemeFontSizeOverride("font_size", 20);
        name.Modulate = model.Enabled ? Colors.White : new Color(1f, 1f, 1f, 0.5f);
        titleRow.AddChild(name);

        if (model.IsExternalPackModel)
        {
            var provider = new Label
            {
                Text = F("model.external_provider", "Provided by {0}", model.ExternalOwnerModId),
                TooltipText = $"{model.ExternalOwnerModId}/{model.ExternalPackId}/{model.ExternalModelKey}",
            };
            provider.AddThemeColorOverride("font_color", new Color(0.55f, 0.8f, 1f));
            titleRow.AddChild(provider);
        }

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
        if (!model.IsExternalPackModel)
        {
            var exportModel = new Button
            {
                Text = L("button.export_model_pack", "Export Package"),
                CustomMinimumSize = new Vector2(140f, 0f),
            };
            exportModel.Pressed += () => ShowModelPackExportDialog(model.Id, model.DisplayName);
            titleRow.AddChild(exportModel);
        }
        card.AddChild(titleRow);

        var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

        var previewEditor = new Button { Text = L("button.preview_adjust", "Preview & Adjust") };
        if (!model.Enabled)
        {
            previewEditor.Disabled = true;
            previewEditor.TooltipText = L("model.disabled", "Enable this model before opening its preview.");
        }
        else if (model.IsExternalPackModel &&
            !Live2DRegisteredPackRegistry.TryGetLibraryModelAsset(model, out _))
        {
            previewEditor.Disabled = true;
            previewEditor.TooltipText = L("model.external_unavailable", "The provider Mod is not loaded.");
        }
        previewEditor.Pressed += () => Live2DPreviewEditor.Show(model.Id, uiHost);
        actions.AddChild(previewEditor);

        enabled.Toggled += active =>
        {
            try
            {
                enabled.Disabled = true;
                ModifyModel(model.Id, target => target.Enabled = active);
                model.Enabled = active;
                name.Modulate = active ? Colors.White : new Color(1f, 1f, 1f, 0.5f);
                previewEditor.Disabled = !active ||
                    (model.IsExternalPackModel &&
                     !Live2DRegisteredPackRegistry.TryGetLibraryModelAsset(model, out _));
                previewEditor.TooltipText = !active
                    ? L("model.disabled", "Enable this model before opening its preview.")
                    : previewEditor.Disabled
                        ? L("model.external_unavailable", "The provider Mod is not loaded.")
                        : "";
            }
            catch (Exception ex)
            {
                enabled.SetPressedNoSignal(!active);
                Entry.Logger.Error($"[{Entry.ModId}] Failed to set model {model.Id} enabled={active}: {ex}");
            }
            finally
            {
                enabled.Disabled = false;
            }
        };

        PanelContainer? modelCard = null;
        var delete = new Button { Text = L("button.delete", "Delete") };
        delete.Pressed += () =>
        {
            delete.Disabled = true;
            ShowDeleteModelConfirmation(model, () =>
            {
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
            }, () => delete.Disabled = false);
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
        preview.Disabled = !model.Enabled;
        if (!model.Enabled)
            preview.TooltipText = L("model.disabled", "Enable this model before opening its preview.");
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
        tabs.AddChild(CreateModelTab(
            L("rendering.tab", "Rendering"),
            CreateModelRenderingEditor(model.Id, model.Overrides.Rendering, settings.Global.Rendering),
            scroll: true));
        tabs.AddChild(CreateModelTab(L("actions.tab", "Hotkeys"), CreateActionEditor(model), scroll: true));
        details.AddChild(tabs);
        return details;
    }

    private static void ShowDeleteModelConfirmation(
        Live2DModelConfig model,
        Action confirmed,
        Action canceled)
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            canceled();
            Entry.Logger.Error($"[{Entry.ModId}] Cannot show the model deletion confirmation without a SceneTree.");
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = L("dialog.delete_model_title", "Delete Model?"),
            DialogText = model.IsExternalPackModel
                ? F("dialog.delete_external_model", "Remove ‘{0}’ from the library? Provider-owned files will not be deleted. You can restore the model later.", model.DisplayName)
                : F("dialog.delete_local_model", "Permanently delete ‘{0}’, its settings, and its managed files? This cannot be undone.", model.DisplayName),
            OkButtonText = L("button.delete", "Delete"),
            CancelButtonText = L("button.cancel", "Cancel"),
            MinSize = new Vector2I(520, 160),
            Exclusive = true,
        };
        var completed = false;
        dialog.Confirmed += () =>
        {
            if (completed)
                return;
            completed = true;
            try
            {
                confirmed();
            }
            finally
            {
                dialog.QueueFree();
            }
        };
        dialog.Canceled += () =>
        {
            if (completed)
                return;
            completed = true;
            canceled();
            dialog.QueueFree();
        };
        tree.Root.AddChild(dialog);
        dialog.PopupCentered();
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

}

