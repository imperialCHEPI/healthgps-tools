<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
</Query>

void Main()
{
	var dataFolder = new DirectoryInfo(@"C:\Work\Data\IHME\RelativeRisk\Data");
	var sourceFolder = new DirectoryInfo(@"C:\Work\Data\IHME\RelativeRisk\Source");
	var outputFolder = new DirectoryInfo(@"C:\Work\Data\IHME\RelativeRisk\disease");
	
	var relativeRiskFiles = dataFolder.GetFiles("*.csv", SearchOption.TopDirectoryOnly);
	Console.WriteLine("Number of relative risk files: {0} found.", relativeRiskFiles.Length);

	var timer = new Stopwatch();
	var numberProcessed = 0;
	var invalidFiles = new List<string>();
	var duplicatedFiles = new List<string>();
	try
	{
		var knownDiseases = GetKnownDiseases();
		var relativeInfo = new List<RelativeRiskInfo>();
		if (sourceFolder.Exists)
		{
			sourceFolder.Delete(true);
		}
		
		if (outputFolder.Exists)
		{
			outputFolder.Delete(true);
		}
		
		sourceFolder.Create();
		outputFolder.Create();
		
		foreach (var fileInfo in relativeRiskFiles)
		{
			var fileName = Path.GetFileNameWithoutExtension(fileInfo.FullName);
			var fields = fileName.Split('_');
			
			var fileGender = Gender.Unknown;
			var fromDisease = string.Empty;
			var toDisease = string.Empty;

			if (fields.Length > 1)
			{
				try
				{	        
					fileGender = (Gender)Enum.Parse(typeof(Gender), fields[0].Trim());
				}
				catch (Exception ex)
				{
					invalidFiles.Add(fileInfo.Name);
					Console.WriteLine("Invalid file name: {0}, must start with Male or Female. {1}", fileInfo.Name, ex.Message);
					continue;
				}
				
				if (fields.Length > 2)
				{
					fromDisease = fields[1].Trim();
					toDisease = fields[2].Trim();
				}
				else
				{
					var map = fields[1].Trim();
					fromDisease = map;
					foreach (var pair in knownDiseases)
					{
						if (map.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
						{
							var split = pair.Key.Length;
							fromDisease = map.Substring(0, split);
							toDisease = map.Substring(split + 1).Trim();
							break;
						}
					}
				}
			}
			else
			{
				invalidFiles.Add(fileInfo.Name);
				Console.WriteLine("Invalid relative risk file name: {0}", fileInfo.Name);
				continue;
			}

			bool isMappingValid = true;
			if (!knownDiseases.ContainsKey(fromDisease))
			{
				Console.WriteLine("Unknown source disease: {0} in file: {1}", fromDisease, fileInfo.Name);
				isMappingValid = false;
			}

			if (!knownDiseases.ContainsKey(toDisease))
			{
				Console.WriteLine("Unknown target disease: {0} in file: {1}", toDisease, fileInfo.Name);
				isMappingValid = false;
			}

			if (!isMappingValid)
			{
				invalidFiles.Add(fileInfo.Name);
				continue;
			}

			try
			{
				// Copy file to output folder.
				var outFilename = $"{fileGender}_{fromDisease}_{toDisease}.csv";
				fileInfo.CopyTo(Path.Combine(sourceFolder.FullName, outFilename));
				
				numberProcessed++;
				relativeInfo.Add(new RelativeRiskInfo
				{
					Gender = fileGender,
					Source = fromDisease,
					Target = toDisease,
					File = fileInfo
				});
			}
			catch (IOException ex)
			{
				duplicatedFiles.Add(fileInfo.Name);
				Console.WriteLine("Failed to copy file: {0}, cause: {1}", fileInfo.Name, ex.Message);
			}
		}
		
		var diseaseFiles = new Dictionary<string,Dictionary<Gender, FileInfo>>(StringComparer.OrdinalIgnoreCase);
		foreach(var item in relativeInfo)
		{
			var key = $"{item.Source}_{item.Target}";
			if (!diseaseFiles.ContainsKey(key))
			{
				diseaseFiles[key] = new Dictionary<Gender, FileInfo>(2);
			}
			
			diseaseFiles[key].Add(item.Gender, item.File);
		}
		
		CreateRelativeRiskFiles(diseaseFiles, knownDiseases, outputFolder);
		var deletables = ValidateRelativeRiskFiles(outputFolder);
		deletables.Dump("Deletables");
	}
	catch (Exception ex)
	{
		Console.WriteLine("# Failed to relative risk, cause: {0}", ex.Message);
		throw;
	}

	Console.WriteLine("\n# of files processed: {0}, ignored: {1} and duplicated: {2} respectively",
	numberProcessed, invalidFiles.Count, duplicatedFiles.Count);
	duplicatedFiles.Dump("Duplicated");
	invalidFiles.Dump("Invalid File Names");
}

private static IReadOnlyDictionary<string,Dictionary<string, FileInfo>> CreateRelativeRiskFiles(
	IReadOnlyDictionary<string, Dictionary<Gender, FileInfo>> mappings,
	IReadOnlyDictionary<string, string> diseases, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nCreating disease relative risk files ...");
	var results = new Dictionary<string, Dictionary<string, FileInfo>>();
	var timer = new Stopwatch();
	timer.Restart();
	
	var undefinedCount = 0;
	var incompleteCount = 0;
	foreach (var item in mappings)
	{
		if (item.Value.Count != 2)
		{
			incompleteCount++;
			var missing = item.Value.ContainsKey(Gender.Male) ? Gender.Female : Gender.Male;
			Console.WriteLine("Incomplete relative risk mapping, missing: {0, -8} for {1}", missing, item.Key);
			continue;
		}

		var keys = item.Key.Split('_');
		var fromDisease = diseases[keys[0]];
		var toDisease = diseases[keys[1]];
		var filename = $"{fromDisease}_{toDisease}.csv";
		var outFileInfo = new FileInfo(Path.Combine(outputFolder.FullName, filename));
		
		var maleLines = File.ReadAllLines(item.Value[Gender.Male].FullName);
		var femeLines = File.ReadAllLines(item.Value[Gender.Female].FullName);
		var rows = Math.Min(maleLines.Length, femeLines.Length);
		if (maleLines.Length != femeLines.Length)
		{
			Console.WriteLine($"Male and female file mismatch: {maleLines.Length} vs. {femeLines.Length} for {item.Key}");
		}
		
		if (rows > 1)
		{
			var maleFields = maleLines[2].Split(",");
			var femeFfiels = femeLines[2].Split(",");
			
			if (maleFields.Length < 2 || femeFfiels.Length < 2)
			{
				undefinedCount++;
				Console.WriteLine($"Undefined relative risk values in file for: {item.Key}");
				continue;
			}
		}
		
		using (var sw = outFileInfo.CreateText())
		{
			sw.WriteLine("Age,Male,Female");
			for (int i = 1; i < rows; i++)
			{
				var ml = maleLines[i].Split(",");
				var fl = femeLines[i].Split(",");
				if (ml[0].Trim() != fl[0].Trim())
				{
					throw new InvalidDataException($"Male and female age mismatch: {ml[0]} vs. {fl[0]} for {item.Key}");
				}
				
				sw.WriteLine($"{ml[0].Trim()},{ml[1].Trim()},{fl[1].Trim()}");
			}
		}
		
		if (!results.ContainsKey(fromDisease))
		{
			results.Add(fromDisease, new Dictionary<string, FileInfo>());
		}
		
		results[fromDisease].Add(toDisease, outFileInfo);
	}
	
	timer.Stop();
	
	var totalFiles = results.Sum(s => s.Value.Count);
	Console.WriteLine("# of files created {0}, incomplete {1}, undefined {2}. Elapsed time: {3} seconds",
					  totalFiles, incompleteCount, undefinedCount, timer.Elapsed.TotalSeconds);
	return results;
}

private static IReadOnlyList<string> ValidateRelativeRiskFiles(DirectoryInfo outputFolder)
{
	Console.WriteLine("\nValidating disease relative risk files ...");
	var results = new List<string>();
	var timer = new Stopwatch();
	timer.Restart();
	var relativeRiskFiles = outputFolder.GetFiles("*.csv", SearchOption.TopDirectoryOnly);
	var invalidCount = 0;
	var defaultValueCount = 0;
	foreach(var info in relativeRiskFiles)
	{
		var lastAge = -1;
		var oneCount = 0;
		var rowsCount = 0;
		using(var sr = info.OpenText())
		{
			string line = sr.ReadLine();
			while ((line = sr.ReadLine()) != null)
			{
				rowsCount++;
				var fields = line.Split(',');
				if (fields.Length != 3)
				{
					invalidCount++;
					Console.WriteLine("Invalid number of columns: {0} vs. 3 in file: {1}", fields.Length, info.Name);
					break;
				}
				
				var age = int.Parse(fields[0]);
				var aged = double.Parse(fields[0]);
				var male = double.Parse(fields[1]);
				var feme = double.Parse(fields[2]);
				if ((aged - age) != 0.0)
				{
					invalidCount++;
					Console.WriteLine("Invalid decimal age: {0} vs. {1} in file: {2}", aged, age, info.Name);					
					break;
				}
				
				if (age - lastAge != 1)
				{
					invalidCount++;
					Console.WriteLine("Non-monotonic age: {0} <= {1} in file: {2}", age, lastAge, info.Name);
					break;
				}
			
				if (male == 1.0 && feme == 1.0)
				{
					oneCount++;
				}

				lastAge = age;
			}
			
			if (oneCount == rowsCount)
			{
				defaultValueCount++;
				Console.WriteLine("Default values only in file: {0}", info.Name);
				results.Add(info.FullName);
			}
		}
	}

	timer.Stop();
	var completeCount = relativeRiskFiles.Length - invalidCount;
	Console.WriteLine("# of files {0}, complete {1}, default value {2}, invalid {3}. Elapsed time: {4} seconds",
					  relativeRiskFiles.Length, completeCount, defaultValueCount, invalidCount, timer.Elapsed.TotalSeconds);
	return results;
}

private static bool RenameRelativeRiskFile(FileInfo fileInfo, ref string diseaseName)
{
	var knownErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{"Blindness due to Cataract", "Blindness due to Cataract Unsqueezed"},
		{"Vision Impairment due to cataract","Vision impairment due to cataract unsqueezed"},
		{"Vision Impairment","Vision impairment due to cataract unsqueezed"},
		{"Visual Impairment","Vision impairment due to cataract unsqueezed"},
		{"Gallbladder and Billiary Disease","Gallbladder and Biliary Diseases"},
		{"Myicardial Infarction","Myocardial Infarction"},
		{"Osteoarthritis hip", "Osteoarthritis of the hip"},
		{"Osteoarthirtis hip", "Osteoarthritis of the hip"},
		{"Osteaorthritis of the hip", "Osteoarthritis of the hip"},
		{"Osteoarthritis knee", "Osteoarthritis of the knee"},
		{"Osteoarthirtis knee", "Osteoarthritis of the knee"},
		{"Kindney cancer", "Kidney cancer"},
		{"Atopic Dertmatitis", "Atopic Dermatitis"},
		{"PancreasCancer", "Pancreas Cancer"},
		{"Gallbladder", "Gallbladder and biliary diseases"},
		{"Gallbladder and Biliary disease","Gallbladder and biliary diseases"},
		{"Major Ovary Cancer", "Ovary Cancer"},
		{"Major Pancreas Cancer", "Pancreas Cancer"},
		{"Major Stomach Cancer", "Stomach Cancer"},
		{"Major Thyroid Cancer", "Thyroid Cancer"},
		{"Major Parkinson Disease", "Parkinson Disease"},
		{"Chronic Obstructive Pulmonay Disease", "Chronic obstructive pulmonary disease"},
		{"Anxiety Disorders", "Anxiety Disorder"},
		{"Major Depressive Disorders", "Major Depressive Disorder"},
		{"Chrnonic Pancreatitis", "Chronic pancreatitis"},
		{"Major Depression Disorder", "Major depressive disorder"}
	};
	
	if (knownErrors.TryGetValue(diseaseName, out string replaceWith))
	{
		var filename = fileInfo.Name.Replace(diseaseName,replaceWith);
		Rename(fileInfo, filename);
		diseaseName = Path.GetFileNameWithoutExtension(filename);
		return true;
	}
	
	foreach(var pair in knownErrors)
	{
		if (pair.Key.Equals("Gallbladder",StringComparison.OrdinalIgnoreCase) ||
		    pair.Key.Equals("Vision Impairment",StringComparison.OrdinalIgnoreCase))
		{
			continue;
		}
		
		if (diseaseName.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
		{
			var filename = fileInfo.Name.Replace(pair.Key, pair.Value);
			try
			{
				Rename(fileInfo, filename);
			}
			catch (Exception ex)
			{
				throw;
			}
			
			diseaseName = Path.GetFileNameWithoutExtension(filename);
			return true;
		}
	}
	
	return false;
}

private static void Rename(FileInfo fileInfo, string newName)
{
	fileInfo.MoveTo(Path.Combine(fileInfo.Directory.FullName, newName));
}

IReadOnlyDictionary<string, string> GetKnownDiseases()
{
	var knownDiseases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	var doc = JsonDocument.Parse(File.ReadAllText(@"C:\Work\Data\IHME\diseases.json"));

	if (doc.RootElement.TryGetProperty("diseases", out JsonElement diseases))
	{
		foreach (var entry in diseases.EnumerateObject())
		{
			knownDiseases.Add(entry.Value.GetProperty("folder_name").GetString(), entry.Name);
		}
	}

	if (doc.RootElement.TryGetProperty("cancers", out JsonElement cancers))
	{
		foreach (var entry in cancers.EnumerateObject())
		{
			knownDiseases.Add(entry.Value.GetProperty("folder_name").GetString(), entry.Name);
		}
	}
	
	return knownDiseases;
}

// You can define other methods, fields, classes and namespaces here
public enum Gender { Unknown, Male, Female }

public sealed class RelativeRiskInfo
{
	public Gender Gender {get;set;}
	public string Source {get; set;}
	public string Target {get; set;}
	public FileInfo File {get;set;}
}

public sealed class Disease
{
	public Disease(string identifier, string display_name)
	{
		if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(display_name))
		{
			throw new ArgumentNullException("Disease id and name must not be null, empty or whitespace only.");
		}
				
		this.Id = identifier;
		this.Name = display_name;
	}
	
	public string Id {get;}
	public string Name {get;}
	public override string ToString() => $"{Id}: {Name}";
}

public class RelativeRisk
{
	public RelativeRisk(Disease fromDisease, Disease toDisease, FileInfo maleData, FileInfo femaleData)
	{
		if (fromDisease.Id.Equals(toDisease.Id, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("The source and target diseases must not be the same."); 	
		}
		
		this.Disease = fromDisease;
		this.ToDisease = toDisease;
		this.MaleFile = maleData;
		this.FemaleFile = femaleData;
	}
	
	public Disease Disease {get; }
	public Disease ToDisease {get; }
	public FileInfo MaleFile {get; }
	public FileInfo FemaleFile {get; }
}