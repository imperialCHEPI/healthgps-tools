<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
</Query>

#load "Process_common.linq"
#load "Process_IHMEShared.linq"

void Main()
{
	Console.WriteLine("Processing disability weights ...");
	
	var doc = JsonDocument.Parse(File.ReadAllText(@"C:\Work\Data\IHME\diseases.json"));
	ValidateSchemaVersion(doc);

	var disability_weights_filename = new FileInfo(@"C:\temp\healthgps.net\Data\DisabilityWeights.csv");
	if (!disability_weights_filename.Exists)
	{
		throw new FileNotFoundException($"Disability weights file: {disability_weights_filename}, not found.");
	}
	
	var sep = ',';
	var timer = new Stopwatch();
	timer.Start();
	
	var diseases = GetKnownDiseases();
	try
	{
		var data = new Dictionary<string, double>(StringComparer.InvariantCulture);
		using (var sr = disability_weights_filename.OpenText())
		{
			var line = sr.ReadLine();
			var fields = line.Split(sep);
			if (fields.Length != 2)
			{
				throw new InvalidDataException($"The number of fields must be 2 vs. {fields.Length} in {line}");
			}

			while ((line = sr.ReadLine()) != null)
			{
				fields = line.Split(sep);
				if (fields.Length != 2)
				{
					Console.WriteLine($"Invalid entry in row: {line}");
					continue;
				}

				var key = fields[0].Trim();
				if (!diseases.TryGetValue(key, out DiseaseInfo info))
				{
					Console.WriteLine($"Unknown disease: {key} in row: {line}");
					continue;
				}

				if (double.TryParse(fields[1].Trim(), out double weight))
				{
					data.Add(diseases[key].Identifier, weight);
				}
				else
				{
					Console.WriteLine($"Invalid weight value: {fields[1]} for {key}");
				}
			}
		}
		
		Console.WriteLine("\ndisability_weights.csv file content:");
		Console.WriteLine("--------------------------------------");
		Console.WriteLine("disease,weight");
		foreach (var entry in data)
		{
			Console.WriteLine($"{entry.Key},{entry.Value}");
		}

		Console.WriteLine("--------------------------------------");
		data.Dump("Dataset");

		timer.Stop();
		Console.WriteLine("# Completed {0} diseases in {1} seconds.\n", data.Count, timer.Elapsed.TotalSeconds);
	}
	catch (Exception ex)
	{
		Console.WriteLine("# Failed to process: {0}, cause: {1}", disability_weights_filename.FullName, ex.Message);
		throw; // Stop processing, remove to continue to next disease.
	}
}

public class Disease
{
	[JsonPropertyName("id")]
	public string Id { get; set; }

	[JsonPropertyName("overwrite")]
	public bool Overwrite { get; set; }

	[JsonPropertyName("folder_name")]
	public string Name { get; set; }

	[JsonPropertyName("input_folders")]
	public Dictionary<string, string> InputFolders { get; set; }
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

