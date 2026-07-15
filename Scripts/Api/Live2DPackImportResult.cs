namespace Live2D.Api;

/// <summary>Result of importing a Live2D pack into the user's managed model library.</summary>
/// <param name="ImportedModels">Number of models added to the managed library.</param>
/// <param name="SkippedDuplicates">Number of models skipped as duplicate content.</param>
public sealed record Live2DPackImportResult(int ImportedModels, int SkippedDuplicates);
