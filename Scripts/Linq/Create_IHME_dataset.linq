<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Nodes</Namespace>
</Query>

void Main()
{
	var timer = new Stopwatch();
	timer.Restart();
	var outputFolder = new DirectoryInfo(@"C:\Work\Data\IHME\dataset");
	var cancerDataFolder = new DirectoryInfo(@"C:\Work\Data\IHME\Cancers\Output2019");
	var diseaseDataFolder = new DirectoryInfo(@"C:\Work\Data\IHME\Diseases\Output2019");
	var relativeRiskFolder = new DirectoryInfo(@"C:\Work\Data\IHME\RelativeRisk\diseases");

	var knownDiseases = GetKnownDiseases();
	var registry = new List<DiseaseInfo>(knownDiseases.Count);
	try
	{
		if (outputFolder.Exists)
		{
			outputFolder.Delete(true);
		}

		outputFolder.Create();
		foreach (var disease in knownDiseases)
		{
			Console.WriteLine("Processing disease: {0}", disease.Value);
			var diseaseFolder = new DirectoryInfo(Path.Combine(outputFolder.FullName, disease.Value.Identifier));
			if (!diseaseFolder.Exists){
				diseaseFolder.Create();
			}
			
			if (disease.Value.Group == DiseaseGroup.Other)
			{
				var diseaseFolders = diseaseDataFolder.GetDirectories(disease.Key, SearchOption.TopDirectoryOnly);
				if (diseaseFolders.Length < 1)
				{
					Console.WriteLine("Disease: {0} definition data not found.", disease.Key);
					continue;
				}
				
				CopyDirectory(diseaseFolders.First(), diseaseFolder, false);
			}
			else if (disease.Value.Group == DiseaseGroup.Cancer)
			{
				var cancerFolders = cancerDataFolder.GetDirectories(disease.Key, SearchOption.TopDirectoryOnly);
				if (cancerFolders.Length < 1)
				{
					Console.WriteLine("Cancer: {0} definition data not found.", disease.Key);
					continue;
				}

				CopyDirectory(cancerFolders.First(), diseaseFolder, true);
			}
			else
			{
				Console.WriteLine("Invalid disease group: {0}", disease.Value.Group);
				continue;
			}
			
			var relativeFolders = relativeRiskFolder.GetDirectories(disease.Value.Identifier, SearchOption.TopDirectoryOnly);
			if (relativeFolders.Length < 1)
			{
				Console.WriteLine("Disease: {0} relative risk data not found.", disease.Key);
				continue;
			}
			
			CopyDirectory(relativeFolders.First(), diseaseFolder, true);
			registry.Add(disease.Value);
		}

		CreateRegistryMetadata(outputFolder, registry);
	}
	catch (Exception ex)
	{
		Console.WriteLine("# Failed to relative risk, cause: {0}", ex.Message);
		throw;
	}

	timer.Stop();
	var incompleteCount = knownDiseases.Count - registry.Count;
	Console.WriteLine("\n# of diseases {0}, complete {1}, incomplete {2}. Elapsed time: {3} seconds",
					  knownDiseases.Count, registry.Count, incompleteCount, timer.Elapsed.TotalSeconds);
}
                                                            
public enum DiseaseGroup { Other, Cancer }

public class DiseaseInfo
{
	public DiseaseInfo(string key, string fullName, DiseaseGroup category)
	{
		this.Identifier = key;
		this.Name = fullName;
		this.Group = category;
	}

	public string Identifier { get; }
	public string Name { get; }
	public DiseaseGroup Group { get; }

	public override string ToString() => $"{Group}, {Identifier}, {Name}";
}

IReadOnlyDictionary<string, DiseaseInfo> GetKnownDiseases()
{
	var knownDiseases = new Dictionary<string, DiseaseInfo>(StringComparer.OrdinalIgnoreCase);
	var doc = JsonDocument.Parse(File.ReadAllText(@"C:\Work\Data\IHME\diseases.json"));

	if (doc.RootElement.TryGetProperty("diseases", out JsonElement diseases))
	{
		foreach (var entry in diseases.EnumerateObject())
		{
			var fullName = entry.Value.GetProperty("folder_name").GetString();
			knownDiseases.Add(fullName, new DiseaseInfo(entry.Name, fullName, DiseaseGroup.Other));
		}
	}

	if (doc.RootElement.TryGetProperty("cancers", out JsonElement cancers))
	{
		foreach (var entry in cancers.EnumerateObject())
		{
			var fullName = entry.Value.GetProperty("folder_name").GetString();
			knownDiseases.Add(fullName, new DiseaseInfo(entry.Name, fullName, DiseaseGroup.Cancer));
		}
	}

	return knownDiseases;
}

private static void CopyDirectory(DirectoryInfo sourceDir, DirectoryInfo destinationDir, bool recursive)
{
	if (!sourceDir.Exists)
	{
		throw new DirectoryNotFoundException($"Source directory not found: {sourceDir.FullName}");
	}
	
	// Cache directories before we start copying
	var dirs = sourceDir.GetDirectories();

	// Create destination folder, if not exists.
	destinationDir.Create();

	// Get the files in the source directory and copy to the destination directory
	foreach (FileInfo file in sourceDir.GetFiles())
	{
		string targetFilePath = Path.Combine(destinationDir.FullName, file.Name);
		file.CopyTo(targetFilePath);
	}

	// If recursive and copying subdirectories, recursively call this method
	if (recursive)
	{
		foreach (var subDir in dirs)
		{
			var newDestinationDir = new DirectoryInfo(Path.Combine(destinationDir.FullName, subDir.Name));
			CopyDirectory(subDir, newDestinationDir, recursive);
		}
	}
}

private static void CreateRegistryMetadata(DirectoryInfo outputFolder, IEnumerable<DiseaseInfo> diseases)
{
	var filename = Path.Combine(outputFolder.FullName, "Metadata.json");
	Console.WriteLine("\nCreating registry metadata with {0} diseases: {1}", diseases.Count(), filename);
	
	var options = new JsonWriterOptions { Indented = true};
	using var stream = new MemoryStream();
	using var writer = new Utf8JsonWriter(stream, options);
	
	writer.WriteStartObject();
	
	// Input configuration file
	writer.WriteStartObject("input_file");
	writer.WriteStartObject("running");
	writer.WriteStartArray("diseases");
	foreach (var item in diseases)
	{
		writer.WriteStringValue(item.Identifier);	
	}

	writer.WriteEndArray();
	writer.WriteEndObject();
	writer.WriteEndObject();

	// Backend configuration file
	writer.WriteStartObject("index_file");
	writer.WriteStartObject("diseases");
	writer.WriteStartArray("registry");
	foreach (var item in diseases)
	{
		writer.WriteStartObject();
		writer.WriteString("group", item.Group.ToString().ToLower());
		writer.WriteString("id", item.Identifier);
		writer.WriteString("name", item.Name);
		writer.WriteEndObject();
	}

	writer.WriteEndArray();
	writer.WriteEndObject();
	writer.WriteEndObject();

	writer.WriteEndObject();
	writer.Flush();
	
	using var sw = new StreamWriter(filename, false, Encoding.UTF8);
	sw.Write(Encoding.UTF8.GetString(stream.ToArray()));
	sw.Flush();
}