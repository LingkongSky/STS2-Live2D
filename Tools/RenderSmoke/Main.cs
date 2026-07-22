using System.Reflection;
using Godot;
using Live2D.Api;
using Live2D.Scripts.Configuration;
using Live2D.Scripts.UI;

public partial class Main : Node2D
{
    private const string SuccessMarker = "LIVE2D_RENDER_SMOKE_OK";
    private Task? _dispatcherTest;
    private int _frames;
    private int _mainThreadId;
    private int _postedExceptionCount;

    public override void _Ready()
    {
        try
        {
            _mainThreadId = System.Environment.CurrentManagedThreadId;
            InstallDispatcher();
            TestDispatcherMainThreadFastPath();
            RenderingServer.SetDefaultClearColor(new Color(0.08f, 0.09f, 0.12f));
            TestApiValidation();
            TestBareDigitActionHotkey();
            TestConfigSanitization();
            TestMaskGeometry();
            TestShaderVariants();
            _dispatcherTest = Task.Run(TestDispatcherFromWorkerAsync);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void TestApiValidation()
    {
        var validate = typeof(Live2DModelUpdate).GetMethod(
            "Validate",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(Live2DModelUpdate).FullName, "Validate");
        validate.Invoke(new Live2DModelUpdate
        {
            BlendMode = Live2DBlendMode.Add,
            Filter = new Live2DFilterSettings { Gamma = 1f },
            Mask = new Live2DMaskSettings
            {
                Type = Live2DMaskType.Rectangle,
                Rect = new Rect2(-10f, -10f, 20f, 20f),
            },
        }, null);

        ExpectArgumentOutOfRange(validate, new Live2DModelUpdate
        {
            Filter = new Live2DFilterSettings { Gamma = 0f },
        });
        ExpectArgumentOutOfRange(validate, new Live2DModelUpdate
        {
            Mask = new Live2DMaskSettings
            {
                Type = Live2DMaskType.Rectangle,
                Rect = new Rect2(0f, 0f, 0f, 100f),
            },
        });
        ExpectArgumentOutOfRange(validate, new Live2DModelUpdate
        {
            BlendMode = (Live2DBlendMode)999,
        });
    }

    private static void TestBareDigitActionHotkey()
    {
        var captured = Live2DActionKeyBindingControl.BuildBinding(new InputEventKey
        {
            Pressed = true,
            Keycode = Key.Key1,
        });
        if (captured != Key.Key1.ToString())
            throw new InvalidOperationException(
                $"Bare digit action hotkey was not captured: captured='{captured}'.");
    }

    private static void ExpectArgumentOutOfRange(MethodInfo validate, Live2DModelUpdate update)
    {
        try
        {
            validate.Invoke(update, null);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is ArgumentOutOfRangeException)
        {
            return;
        }
        throw new InvalidOperationException("Invalid public rendering settings were accepted.");
    }

    private static void TestConfigSanitization()
    {
        var global = new GlobalLive2DConfig
        {
            Rendering = new RenderingConfig
            {
                MaskViewportSize = -50,
                BlendMode = (Live2DBlendMode)999,
                Filter = new FilterConfig
                {
                    Brightness = 20f,
                    Contrast = -2f,
                    Gamma = 0f,
                },
                Mask = new CanvasMaskConfig
                {
                    Type = (Live2DMaskType)999,
                    Width = -1f,
                    Height = 0f,
                    CornerRadius = -10f,
                    SegmentsPerCorner = 1,
                },
            },
        };
        var rendering = Live2DConfigResolver.Resolve(global, null).Rendering;
        if (rendering.MaskViewportSize != 0 ||
            rendering.BlendMode != Live2DBlendMode.Normal ||
            rendering.Filter.Brightness != 1f ||
            rendering.Filter.Contrast != 0f ||
            rendering.Filter.Gamma != 0.01f ||
            rendering.Mask.Type != Live2DMaskType.None ||
            rendering.Mask.Rect.Size != new Vector2(1000f, 1000f) ||
            rendering.Mask.CornerRadius != 0f ||
            rendering.Mask.SegmentsPerCorner != 2)
            throw new InvalidOperationException("Persisted rendering settings were not sanitized correctly.");
    }

    public override void _Process(double delta)
    {
        if (++_frames < 8 || _dispatcherTest is null || !_dispatcherTest.IsCompleted)
            return;
        try
        {
            _dispatcherTest.GetAwaiter().GetResult();
            var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument =>
                argument.StartsWith("--capture-path=", StringComparison.Ordinal));
            if (captureArgument is not null)
            {
                var capturePath = captureArgument["--capture-path=".Length..];
                var image = GetViewport().GetTexture().GetImage();
                if (image.IsEmpty())
                    throw new InvalidOperationException("Rendered viewport image is empty.");
                var error = image.SavePng(capturePath);
                if (error != Error.Ok)
                    throw new InvalidOperationException($"Unable to save render smoke image: {error}.");
                GD.Print($"LIVE2D_RENDER_SMOKE_IMAGE={capturePath}");
            }
            GD.Print(SuccessMarker);
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void InstallDispatcher()
    {
        var initialize = typeof(Live2DApi).GetMethod(
            "InitializeDispatcher",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(Live2DApi).FullName, "InitializeDispatcher");
        initialize.Invoke(null,
        [
            (Action<Exception>)(_ => Interlocked.Increment(ref _postedExceptionCount)),
        ]);
        if (!Live2DApi.IsDispatcherReady || !Live2DApi.IsMainThread)
            throw new InvalidOperationException("The public dispatcher did not initialize on the Godot main thread.");
    }

    private void TestDispatcherMainThreadFastPath()
    {
        var callbackThread = Live2DApi.InvokeAsync(
            () => System.Environment.CurrentManagedThreadId).GetAwaiter().GetResult();
        if (callbackThread != _mainThreadId)
            throw new InvalidOperationException("Main-thread InvokeAsync did not use its synchronous fast path.");
    }

    private async Task TestDispatcherFromWorkerAsync()
    {
        if (Live2DApi.IsMainThread)
            throw new InvalidOperationException("Dispatcher worker test did not start on a worker thread.");

        var callbackThread = await Live2DApi.InvokeAsync(
            () => System.Environment.CurrentManagedThreadId).ConfigureAwait(false);
        if (callbackThread != _mainThreadId)
            throw new InvalidOperationException(
                $"InvokeAsync ran on thread {callbackThread}; expected Godot main thread {_mainThreadId}.");

        await Live2DApi.InvokeAsync(() => GetTree().Paused = true).ConfigureAwait(false);
        await Live2DApi.InvokeAsync(() =>
        {
            if (!GetTree().Paused)
                throw new InvalidOperationException("SceneTree pause state was not applied.");
            GetTree().Paused = false;
        }).ConfigureAwait(false);

        await TestQueuedUpdateCoalescingAsync().ConfigureAwait(false);
        await TestQueuedValueCoalescingAsync().ConfigureAwait(false);
        await TestAvailabilityWaitersAsync().ConfigureAwait(false);

        var postCompletion = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Live2DApi.Post(() =>
            postCompletion.TrySetResult(System.Environment.CurrentManagedThreadId));
        if (await postCompletion.Task.ConfigureAwait(false) != _mainThreadId)
            throw new InvalidOperationException("Post did not run on the Godot main thread.");

        const string expectedMessage = "dispatcher exception probe";
        try
        {
            await Live2DApi.InvokeAsync(
                () => throw new InvalidOperationException(expectedMessage)).ConfigureAwait(false);
            throw new InvalidOperationException("InvokeAsync swallowed a callback exception.");
        }
        catch (InvalidOperationException exception) when (exception.Message == expectedMessage)
        {
        }

        var cancelledCallbackCount = 0;
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelledTask = Live2DApi.InvokeAsync(
                () => Interlocked.Increment(ref cancelledCallbackCount),
                cancellation.Token);
            cancellation.Cancel();
            try
            {
                await cancelledTask.ConfigureAwait(false);
                throw new InvalidOperationException("InvokeAsync did not observe queued cancellation.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        await Live2DApi.InvokeAsync(() => { }).ConfigureAwait(false);
        if (cancelledCallbackCount != 0)
            throw new InvalidOperationException("A cancelled queued callback was executed.");

        Live2DApi.Post(() => throw new InvalidOperationException("post exception probe"));
        await Live2DApi.InvokeAsync(() => { }).ConfigureAwait(false);
        if (Volatile.Read(ref _postedExceptionCount) != 1)
            throw new InvalidOperationException("Post did not report its callback exception.");
    }

    private static async Task TestQueuedUpdateCoalescingAsync()
    {
        var accumulatorType = typeof(Live2DApi).Assembly.GetType(
            "Live2D.Api.Live2DQueuedUpdateAccumulator",
            throwOnError: true)!;
        var constructor = accumulatorType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Action<Live2DModelUpdate>)],
            modifiers: null)
            ?? throw new MissingMethodException(accumulatorType.FullName, ".ctor");
        var queue = accumulatorType.GetMethod(
            "Queue",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(accumulatorType.FullName, "Queue");

        Live2DModelUpdate? applied = null;
        var applyCount = 0;
        var accumulator = constructor.Invoke(
        [
            (Action<Live2DModelUpdate>)(update =>
            {
                if (!Live2DApi.IsMainThread)
                    throw new InvalidOperationException("A queued model update ran outside the main thread.");
                applied = update;
                Interlocked.Increment(ref applyCount);
            }),
        ]);

        var first = new Live2DModelUpdate
        {
            Position = new Vector2(10f, 20f),
            Opacity = 0.35f,
        };
        await RunWhileMainThreadBlockedAsync("queued model update", () =>
        {
            queue.Invoke(accumulator, [first]);
            first.Position = new Vector2(999f, 999f);
            queue.Invoke(accumulator,
            [
                new Live2DModelUpdate
                {
                    Position = new Vector2(30f, 40f),
                    RotationDegrees = 12f,
                },
            ]);
            queue.Invoke(accumulator,
            [
                new Live2DModelUpdate
                {
                    Filter = new Live2DFilterSettings { Saturation = 0.5f },
                },
            ]);
        }).ConfigureAwait(false);
        if (Volatile.Read(ref applyCount) != 1 || applied is null)
            throw new InvalidOperationException("Queued model updates were not coalesced into one apply.");
        if (applied.Position != new Vector2(30f, 40f) ||
            applied.Opacity != 0.35f ||
            applied.RotationDegrees != 12f ||
            applied.Filter?.Saturation != 0.5f)
            throw new InvalidOperationException("Queued model update field merging produced an invalid result.");
    }

    private static async Task TestQueuedValueCoalescingAsync()
    {
        var accumulatorType = typeof(Live2DApi).Assembly.GetType(
            "Live2D.Api.Live2DQueuedValueAccumulator",
            throwOnError: true)!;
        var applyType = typeof(Action<IReadOnlyDictionary<string, float>>);
        var constructor = accumulatorType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [applyType],
            modifiers: null)
            ?? throw new MissingMethodException(accumulatorType.FullName, ".ctor");
        var queueSingle = accumulatorType.GetMethod(
            "Queue",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(string), typeof(float)],
            modifiers: null)
            ?? throw new MissingMethodException(accumulatorType.FullName, "Queue(string, float)");
        var queueBatch = accumulatorType.GetMethod(
            "Queue",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(IReadOnlyDictionary<string, float>)],
            modifiers: null)
            ?? throw new MissingMethodException(
                accumulatorType.FullName,
                "Queue(IReadOnlyDictionary<string, float>)");

        IReadOnlyDictionary<string, float>? applied = null;
        var applyCount = 0;
        var accumulator = constructor.Invoke(
        [
            (Action<IReadOnlyDictionary<string, float>>)(values =>
            {
                if (!Live2DApi.IsMainThread)
                    throw new InvalidOperationException("Queued Cubism values ran outside the main thread.");
                applied = values;
                Interlocked.Increment(ref applyCount);
            }),
        ]);

        var batch = new Dictionary<string, float>
        {
            ["ParamMouthOpenY"] = 0.6f,
            ["PARAMANGLEX"] = 3f,
        };
        await RunWhileMainThreadBlockedAsync("queued Cubism value", () =>
        {
            queueSingle.Invoke(accumulator, ["ParamAngleX", 1f]);
            queueSingle.Invoke(accumulator, ["paramanglex", 2f]);
            queueBatch.Invoke(accumulator, [batch]);
            batch["ParamMouthOpenY"] = 0.9f;
        }).ConfigureAwait(false);
        if (Volatile.Read(ref applyCount) != 1 || applied is null || applied.Count != 2)
            throw new InvalidOperationException("Queued Cubism values were not coalesced into one batch.");
        if (!applied.TryGetValue("paramanglex", out var angle) || angle != 3f ||
            !applied.TryGetValue("PARAMMOUTHOPENY", out var mouth) || mouth != 0.6f)
            throw new InvalidOperationException("Queued Cubism value merging produced an invalid result.");
    }

    private static async Task RunWhileMainThreadBlockedAsync(string name, Action action)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Live2DApi.Post(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException($"{name} test gate timed out.");
        });
        if (!entered.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException($"Godot main thread did not enter the {name} test gate.");

        try
        {
            action();
        }
        finally
        {
            release.Set();
        }

        await Live2DApi.InvokeAsync(() => { }).ConfigureAwait(false);
    }

    private static async Task TestAvailabilityWaitersAsync()
    {
        var openType = typeof(Live2DApi).Assembly.GetType(
            "Live2D.Api.Live2DAvailabilityState`1",
            throwOnError: true)!;
        var stateType = openType.MakeGenericType(typeof(string));
        var constructor = stateType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [typeof(string)],
            modifiers: null)
            ?? throw new MissingMethodException(stateType.FullName, ".ctor");
        var waitAsync = stateType.GetMethod(
            "WaitAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(stateType.FullName, "WaitAsync");
        var set = stateType.GetMethod(
            "Set",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(stateType.FullName, "Set");
        var state = constructor.Invoke(["stable-handle"]);

        var available = (Task<string>)waitAsync.Invoke(
            state,
            [true, CancellationToken.None])!;
        if (available.IsCompleted)
            throw new InvalidOperationException("Availability wait completed before the state changed.");
        set.Invoke(state, [true]);
        if (await available.ConfigureAwait(false) != "stable-handle")
            throw new InvalidOperationException("Availability wait returned an invalid stable handle.");

        var alreadyAvailable = (Task<string>)waitAsync.Invoke(
            state,
            [true, CancellationToken.None])!;
        if (!alreadyAvailable.IsCompletedSuccessfully)
            throw new InvalidOperationException("Already-available wait did not complete synchronously.");

        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = (Task<string>)waitAsync.Invoke(
                state,
                [false, cancellation.Token])!;
            cancellation.Cancel();
            try
            {
                await cancelled.ConfigureAwait(false);
                throw new InvalidOperationException("Availability wait did not observe cancellation.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        var unavailable = (Task<string>)waitAsync.Invoke(
            state,
            [false, CancellationToken.None])!;
        set.Invoke(state, [false]);
        if (await unavailable.ConfigureAwait(false) != "stable-handle")
            throw new InvalidOperationException("Unavailable wait returned an invalid stable handle.");
    }

    private static void TestMaskGeometry()
    {
        var pipeline = GetPipelineType();
        var buildMask = RequireMethod(pipeline, "BuildMaskPolygon");
        var rect = new Rect2(-120f, -80f, 240f, 160f);
        VerifyMask(buildMask, new Live2DMaskSettings
        {
            Type = Live2DMaskType.Rectangle, Rect = rect, SegmentsPerCorner = 8,
        }, 4, rect);
        VerifyMask(buildMask, new Live2DMaskSettings
        {
            Type = Live2DMaskType.Ellipse, Rect = rect, SegmentsPerCorner = 8,
        }, 32, rect);
        VerifyMask(buildMask, new Live2DMaskSettings
        {
            Type = Live2DMaskType.RoundedRectangle,
            Rect = rect,
            CornerRadius = 24f,
            SegmentsPerCorner = 8,
        }, 32, rect);
    }

    private static void VerifyMask(
        MethodInfo buildMask,
        Live2DMaskSettings settings,
        int expectedCount,
        Rect2 bounds)
    {
        var points = buildMask.Invoke(null, [settings]) as Vector2[]
            ?? throw new InvalidOperationException($"No polygon returned for {settings.Type}.");
        if (points.Length != expectedCount)
            throw new InvalidOperationException(
                $"{settings.Type} returned {points.Length} points; expected {expectedCount}.");

        var end = bounds.End;
        foreach (var point in points)
        {
            var inside = point.X >= bounds.Position.X - 0.001f &&
                         point.X <= end.X + 0.001f &&
                         point.Y >= bounds.Position.Y - 0.001f &&
                         point.Y <= end.Y + 0.001f;
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !inside)
                throw new InvalidOperationException(
                    $"{settings.Type} returned invalid/out-of-bounds point {point}.");
        }
    }

    private void TestShaderVariants()
    {
        var pipeline = GetPipelineType();
        var createMaterial = RequireMethod(pipeline, "CreateMaterial");
        var updateMaterial = RequireMethod(pipeline, "UpdateMaterial");
        var buildMask = RequireMethod(pipeline, "BuildMaskPolygon");

        var index = 0;
        foreach (var blendMode in Enum.GetValues<Live2DBlendMode>())
        {
            var material = createMaterial.Invoke(null, [blendMode]) as ShaderMaterial
                ?? throw new InvalidOperationException($"No material returned for {blendMode}.");
            updateMaterial.Invoke(null,
            [
                material,
                blendMode,
                new Live2DFilterSettings
                {
                    Tint = new Color(0.8f, 0.9f, 1f, 0.9f),
                    Brightness = 0.05f,
                    Contrast = 1.1f,
                    Saturation = 0.8f,
                    Grayscale = 0.1f,
                    HueShiftDegrees = 12f,
                    Invert = 0.05f,
                    Gamma = 1.05f,
                },
            ]);
            if (material.Shader is null || material.Shader.GetShaderUniformList().Count < 8)
                throw new InvalidOperationException($"Shader uniforms unavailable for {blendMode}.");

            var maskType = (index % 3) switch
            {
                0 => Live2DMaskType.Rectangle,
                1 => Live2DMaskType.Ellipse,
                _ => Live2DMaskType.RoundedRectangle,
            };
            var mask = new Live2DMaskSettings
            {
                Type = maskType,
                Rect = new Rect2(-42f, -62f, 84f, 124f),
                CornerRadius = 18f,
                SegmentsPerCorner = 12,
            };
            var maskNode = new Polygon2D
            {
                Position = new Vector2(70f + index * 110f, 180f),
                Polygon = (Vector2[])buildMask.Invoke(null, [mask])!,
                ClipChildren = CanvasItem.ClipChildrenMode.Only,
                Color = Colors.White,
            };
            AddChild(maskNode);
            var group = new CanvasGroup { Material = material };
            maskNode.AddChild(group);
            group.AddChild(new Polygon2D
            {
                Polygon =
                [
                    new Vector2(-40f, -60f),
                    new Vector2(40f, -60f),
                    new Vector2(40f, 60f),
                    new Vector2(-40f, 60f),
                ],
                Color = new Color(0.3f + index * 0.1f, 0.6f, 0.9f),
            });
            index++;
        }
    }

    private static Type GetPipelineType()
        => typeof(Live2DApi).Assembly.GetType(
            "Live2D.Scripts.Runtime.Live2DRenderPipeline",
            throwOnError: true)!;

    private static MethodInfo RequireMethod(Type type, string name)
        => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
           ?? throw new MissingMethodException(type.FullName, name);

    private void Fail(Exception exception)
    {
        GD.PushError(exception.ToString());
        SetProcess(false);
        GetTree().Quit(1);
    }
}
