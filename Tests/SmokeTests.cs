using Live2D.Scripts.Configuration;
using Live2D.Scripts.Models;
using Live2D.Scripts.Packs;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

var workspace = FindWorkspace(AppContext.BaseDirectory);

var miSide = Live2DModelManifestParser.Parse(
    Path.Combine(workspace, "examples", "MiSide", "mita.model3.json"));
Assert(miSide.RelativeFiles.Count == 23, $"MiSide dependency count was {miSide.RelativeFiles.Count}, expected 23.");
Assert(miSide.Actions.Count == 18, $"MiSide action count was {miSide.Actions.Count}, expected 18.");
Assert(miSide.Actions.Count(action => action.Kind == Live2DActionKind.Expression) == 14,
    "MiSide expression count was not 14.");
Assert(miSide.Actions.Count(action => action.Kind == Live2DActionKind.Motion) == 4,
    "MiSide motion count was not 4.");

var global = new GlobalLive2DConfig();
Assert(global.MainMenu.Anchor == AnchorPreset.BottomRight, "Main-menu default anchor was not bottom-right.");
Assert(Math.Abs(global.MainMenu.OffsetX - (-499.4565f)) < 0.0001f,
    "Main-menu default horizontal offset did not match MiSide.");
Assert(Math.Abs(global.MainMenu.OffsetY - 616.9355f) < 0.0001f,
    "Main-menu default vertical offset did not match MiSide.");
Assert(Math.Abs(global.MainMenu.Scale - 0.5f) < 0.0001f,
    "Main-menu default scale did not match MiSide.");
var defaultModel = new Live2DModelConfig();
Assert(defaultModel.Enabled, "A newly created model was not enabled by default.");
defaultModel.Enabled = false;
var disabledModelJson = JsonSerializer.Serialize(defaultModel);
Assert(Regex.IsMatch(disabledModelJson, "\\\"Enabled\\\"\\s*:\\s*false"),
    "The per-model Enabled switch was not serialized.");
global.MainMenu.Scale = 0.42f;
global.MainMenu.Visible = false;
var overrides = new Live2DModelOverrides();
var inherited = Live2DConfigResolver.Resolve(global, overrides);
Assert(Math.Abs(inherited.MainMenu.Scale - 0.42f) < 0.0001f, "Model did not inherit global scale.");
Assert(!inherited.MainMenu.Visible, "Model did not inherit global visibility.");

overrides.MainMenu.Scale = 0.75f;
overrides.MainMenu.Visible = true;
var overridden = Live2DConfigResolver.Resolve(global, overrides);
Assert(Math.Abs(overridden.MainMenu.Scale - 0.75f) < 0.0001f, "Model scale override was ignored.");
Assert(overridden.MainMenu.Visible, "Model visibility override was ignored.");

overrides.MainMenu.Scale = null;
overrides.MainMenu.Visible = null;
var restored = Live2DConfigResolver.Resolve(global, overrides);
Assert(Math.Abs(restored.MainMenu.Scale - 0.42f) < 0.0001f, "Clearing override did not restore inheritance.");
Assert(!restored.MainMenu.Visible, "Clearing visibility override did not restore inheritance.");

Console.WriteLine($"PASS MiSide files={miSide.RelativeFiles.Count}, actions={miSide.Actions.Count}");
Console.WriteLine("PASS global inheritance -> override -> restored inheritance");
Console.WriteLine("PASS MiSide-equivalent responsive main-menu defaults");
Console.WriteLine("PASS per-model enabled default and serialization");

var normalizedSettings = new Live2DSettings
{
    RemovedExternalModelIds = ["extlib_example", "", "EXTLIB_EXAMPLE"],
};
Live2DConfigNormalizer.NormalizeInPlace(normalizedSettings);
Assert(normalizedSettings.RemovedExternalModelIds.SequenceEqual(["extlib_example"]),
    "Removed external model IDs were not cleaned and deduplicated.");
Console.WriteLine("PASS removed provider model identity normalization");

var staleModels = new Live2DSettings
{
    Models =
    [
        new Live2DModelConfig { Id = "present-a", DisplayOrder = 0 },
        new Live2DModelConfig { Id = "missing", DisplayOrder = 1 },
        new Live2DModelConfig { Id = "present-b", DisplayOrder = 2 },
    ],
};
var removedStaleModels = Live2DConfigNormalizer.RemoveUnavailableModels(
    staleModels,
    model => model.Id != "missing");
Assert(removedStaleModels.Count == 1 && removedStaleModels[0].Id == "missing",
    "Missing managed model configuration was not pruned.");
Assert(staleModels.Models.Select(model => model.DisplayOrder).SequenceEqual([0, 1]),
    "Display order was not compacted after pruning a missing model.");
Console.WriteLine("PASS missing managed model configuration pruning");

var localizationRoot = Path.Combine(workspace, "Live2D", "localization");
var localizations = new Dictionary<string, Dictionary<string, string>>();
foreach (var language in new[] { "eng", "zhs", "jpn" })
{
    var path = Path.Combine(localizationRoot, language + ".json");
    var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                 ?? throw new InvalidOperationException($"Unable to read localization file: {path}");
    Assert(values.Count > 0, $"Localization {language} is empty.");
    Assert(values.All(pair => !string.IsNullOrWhiteSpace(pair.Value)),
        $"Localization {language} contains a blank translation.");
    localizations[language] = values;
}

var referenceKeys = localizations["eng"].Keys.ToHashSet(StringComparer.Ordinal);
foreach (var (language, values) in localizations)
{
    var keys = values.Keys.ToHashSet(StringComparer.Ordinal);
    Assert(keys.SetEquals(referenceKeys),
        $"Localization key mismatch for {language}: missing=[{string.Join(',', referenceKeys.Except(keys))}], " +
        $"extra=[{string.Join(',', keys.Except(referenceKeys))}]");
    foreach (var key in referenceKeys)
    {
        var expectedPlaceholders = GetPlaceholders(localizations["eng"][key]);
        var actualPlaceholders = GetPlaceholders(values[key]);
        Assert(expectedPlaceholders.SetEquals(actualPlaceholders),
            $"Localization placeholder mismatch for {language}:{key}.");
    }
}
Console.WriteLine($"PASS eng/zhs/jpn localization parity ({referenceKeys.Count} keys)");

var testRoot = Path.Combine(Path.GetTempPath(), "Live2D-smoke-" + Guid.NewGuid().ToString("N"));
try
{
    var repository = Path.Combine(testRoot, "models");
    var miSideId = "miside-test";
    CopyDirectory(Path.Combine(workspace, "examples", "MiSide"), Path.Combine(repository, miSideId));

    var packSettings = new Live2DSettings
    {
        Models =
        [
            new Live2DModelConfig
            {
                Id = miSideId,
                DisplayName = "MiSide Test",
                ModelRelativePath = $"{miSideId}/mita.model3.json",
                ContentHash = miSide.ContentHash,
                AvailableActions = miSide.Actions.ToList(),
            },
        ],
    };
    packSettings.Global.MainMenu.Scale = 0.66f;
    packSettings.Global.Hotkeys.ToggleVisibility = "Ctrl+Shift+L";
    packSettings.Models[0].Enabled = false;
    packSettings.Models[0].Overrides.InGame.Scale = 0.88f;
    packSettings.Models[0].ActionBindings.Add(new ActionBindingConfig
    {
        Kind = Live2DActionKind.Motion,
        MotionGroup = "Hotkey",
        MotionIndex = 0,
        KeyBinding = "Ctrl+Key1",
        MainMenu = false,
        InGame = true,
    });

    var packagePath = Path.Combine(testRoot, "roundtrip.live2dpack");
    Live2DPackArchive.Write(packagePath, packSettings, repository, includeGlobalConfig: true, "Smoke Test");
    var staging = Path.Combine(testRoot, "staging");
    var roundtrip = Live2DPackArchive.ReadToStaging(packagePath, staging);
    Assert(roundtrip.Manifest.Models.Count == 1, "Package manifest model count did not round-trip.");
    Assert(roundtrip.Global != null && Math.Abs(roundtrip.Global.MainMenu.Scale - 0.66f) < 0.0001f,
        "Package global config did not round-trip.");
    Assert(roundtrip.Global?.Hotkeys.ToggleVisibility == "Ctrl+Shift+L",
        "Package global visibility hotkey did not round-trip.");
    Assert(roundtrip.Models[0].Overrides.InGame.Scale == 0.88f,
        "Package model override did not round-trip.");
    Assert(!roundtrip.Models[0].Enabled,
        "Package model enabled state did not round-trip.");
    Assert(roundtrip.Models[0].ActionBindings.Single().KeyBinding == "Ctrl+Key1",
        "Package action binding did not round-trip.");
    Assert(roundtrip.ExtractedEntryPaths.Values.All(File.Exists),
        "Package model entry files were not extracted.");
    Console.WriteLine("PASS .live2dpack round-trip with global config, overrides and hotkeys");

    var maliciousPath = Path.Combine(testRoot, "malicious.live2dpack");
    using (var archive = ZipFile.Open(maliciousPath, ZipArchiveMode.Create))
    {
        using var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open());
        writer.Write("escape");
    }
    AssertThrows<InvalidDataException>(
        () => Live2DPackArchive.ReadToStaging(maliciousPath, Path.Combine(testRoot, "malicious-staging")),
        "Package path traversal was not rejected.");
    Console.WriteLine("PASS .live2dpack path traversal rejection");
}
finally
{
    if (Directory.Exists(testRoot))
        Directory.Delete(testRoot, recursive: true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertThrows<T>(Action action, string message) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target);
    }
}

static string FindWorkspace(string startDirectory)
{
    for (var directory = new DirectoryInfo(startDirectory); directory != null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Live2D.csproj")))
            return directory.FullName;
    }
    throw new DirectoryNotFoundException("Unable to locate the Live2D workspace from the test output directory.");
}

static HashSet<string> GetPlaceholders(string value)
    => Regex.Matches(value, @"\{\d+(?::[^}]*)?\}")
        .Select(match => Regex.Match(match.Value, @"\d+").Value)
        .ToHashSet(StringComparer.Ordinal);
