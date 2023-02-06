<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
</Query>

#load ".\Machar.cs"

void Main()
{
	var dataFolder = new DirectoryInfo(@"C:\temp\healthgps.net\Data\Diseases");
	var sourceFolder = new DirectoryInfo(@"C:\Work\Data\IHME\RelativeRisk\sources");
	var outputFolder = new DirectoryInfo(@"C:\Work\Data\IHME\RelativeRisk\diseases");
	var diseaseRelativeRiskFolderName = "RelativeRisk";
	var factorRelativeRiskFolderName = "RiskFactorsRelativeRisk";
	var riskFactorName = "bmi";
	var defaultRiskValue = 1.0;

	Console.WriteLine("Machine Parameters: Radix = {0}, machine precision = {1}, numerical precision = {2}.\n",
				  MathHelper.Radix, MathHelper.MachinePrecision, MathHelper.DefaultNumericalPrecision);

	var diseaseFolders = dataFolder.GetDirectories();
	Console.WriteLine("Number of potential disease folders: {0} found.", diseaseFolders.Length);
	
	var timer = new Stopwatch();
	var numberOfDiseases = 0;
	var numberOfDiseaseProcessedFiles = 0;
	var numberOfFactorsProcessedFiles = 0;
	var invalidFolders = new List<string>();
	var invalidFiles = new List<string>();
	var duplicatedFiles = new List<string>();
	try
	{
		var knownDiseases = GetKnownDiseases();
		var diseaseRelativeInfo = new List<DiseaseRelativeRiskInfo>();
		var factorRelativeInfo = new List<FactorRelativeRiskInfo>();

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

		foreach (var diseaseInfo in diseaseFolders)
		{
			if (!knownDiseases.TryGetValue(diseaseInfo.Name, out string diseaseKey))
			{
				invalidFolders.Add(diseaseInfo.Name);
				Console.WriteLine("Unknown disease folder: {0}", diseaseInfo.FullName);
				continue;
			}

			numberOfDiseases++;
			numberOfDiseaseProcessedFiles += ProcessDiseaseRelativeRiskFiles(sourceFolder, diseaseInfo, diseaseRelativeInfo,
			diseaseRelativeRiskFolderName, knownDiseases, invalidFolders, invalidFiles, duplicatedFiles);

			numberOfFactorsProcessedFiles += ProcessFactorRelativeRiskFiles(sourceFolder, diseaseInfo, factorRelativeInfo,
			factorRelativeRiskFolderName, knownDiseases, riskFactorName, invalidFolders, invalidFiles, duplicatedFiles);
		}

		CreateDiseaseRelativeRiskFiles(diseaseRelativeInfo, knownDiseases, outputFolder);
		
		CreateRiskFactorRelativeRiskFiles(factorRelativeInfo, knownDiseases, outputFolder);
		
		var diseaseDeletables = ValidateDiseaseRelativeRiskFiles(outputFolder, defaultRiskValue);
		if (diseaseDeletables.Count > 0)
		{
			diseaseDeletables.Dump("Deletables");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine("# Failed to relative risk, cause: {0}", ex.Message);
		throw;
	}


	Console.WriteLine("\n# of disease processed: {0}, ignored: {1}, processed file: D{2} F{3}, invalid files: {4}, duplicated files: {5}.",
	numberOfDiseases, invalidFolders.Count, numberOfDiseaseProcessedFiles, numberOfFactorsProcessedFiles, invalidFiles.Count, duplicatedFiles.Count);
	if (invalidFolders.Count > 0) invalidFolders.Dump("Invalid Folders");	
	if (duplicatedFiles.Count > 0) duplicatedFiles.Dump("Duplicated files");	
	if (invalidFiles.Count > 0) invalidFiles.Dump("Invalid File Names");
}

public enum Gender { Unknown, Male, Female }

public sealed class DiseaseRelativeRiskInfo
{
	public Gender Gender { get; set; }
	public string Source { get; set; }
	public string Target { get; set; }
	public FileInfo File { get; set; }
}

public sealed class FactorRelativeRiskInfo
{
	public Gender Gender { get; set; }
	public string Source { get; set; }
	public string Factor { get; set; }
	public FileInfo File { get; set; }
}


private static int ProcessDiseaseRelativeRiskFiles(
	DirectoryInfo sourceFolder, DirectoryInfo diseaseInfo, List<DiseaseRelativeRiskInfo> results,
	string relativeRiskFolderName, IReadOnlyDictionary<string, string> knownDiseases, List<string> invalidFolders,
	List<string> invalidFiles, List<string> duplicatedFiles)
{
	var numberOfProcessedFiles = 0;
	var diseaseRelativeRiskFolder = diseaseInfo.GetDirectories(relativeRiskFolderName).FirstOrDefault();
	if (!diseaseRelativeRiskFolder.Exists)
	{
		invalidFolders.Add(diseaseInfo.Name);
		Console.WriteLine("Disease {0} missing relative risk forder.", diseaseInfo.FullName);
		return numberOfProcessedFiles;
	}

	var relativeRiskFiles = diseaseRelativeRiskFolder.GetFiles("*.csv", SearchOption.TopDirectoryOnly);
	Console.WriteLine("Processing {0}: disease relative risk files found: {1}.", diseaseInfo.Name, relativeRiskFiles.Length);
	foreach (var fileInfo in relativeRiskFiles)
	{
		var fileName = Path.GetFileNameWithoutExtension(fileInfo.FullName);
		var fields = fileName.Split('_');

		var fileGender = Gender.Unknown;
		var causeDisease = string.Empty;
		var effectDisease = string.Empty;
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
				causeDisease = fields[1].Trim();
				effectDisease = fields[2].Trim();
			}
			else
			{
				var map = fields[1].Trim();
				causeDisease = map;
				foreach (var pair in knownDiseases)
				{
					if (map.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
					{
						var split = pair.Key.Length;
						causeDisease = map.Substring(0, split);
						effectDisease = map.Substring(split + 1).Trim();
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
		if (!knownDiseases.ContainsKey(causeDisease))
		{
			Console.WriteLine("Unknown source disease: {0} in file: {1}", causeDisease, fileInfo.Name);
			isMappingValid = false;
		}

		if (!knownDiseases.ContainsKey(effectDisease))
		{
			Console.WriteLine("Unknown target disease: {0} in file: {1}", effectDisease, fileInfo.Name);
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
			var outFilename = $"{fileGender}_{causeDisease}_{effectDisease}.csv";
			fileInfo.CopyTo(Path.Combine(sourceFolder.FullName, outFilename));

			numberOfProcessedFiles++;
			results.Add(new DiseaseRelativeRiskInfo
			{
				Gender = fileGender,
				Source = causeDisease,
				Target = effectDisease,
				File = fileInfo
			});
		}
		catch (IOException ex)
		{
			duplicatedFiles.Add(fileInfo.Name);
			Console.WriteLine("Failed to copy file: {0}, cause: {1}", fileInfo.Name, ex.Message);
		}
	}

	return numberOfProcessedFiles;
}

private static int ProcessFactorRelativeRiskFiles(
	DirectoryInfo sourceFolder, DirectoryInfo diseaseInfo, List<FactorRelativeRiskInfo> results,
	string factorRelativeRiskFolderName, IReadOnlyDictionary<string, string> knownDiseases, string riskFactorName,
	List<string> invalidFolders, List<string> invalidFiles, List<string> duplicatedFiles)
{
	var numberOfProcessedFiles = 0;
	var factorRelativeRiskFolder = diseaseInfo.GetDirectories(factorRelativeRiskFolderName).FirstOrDefault();
	if (factorRelativeRiskFolder == null || !factorRelativeRiskFolder.Exists)
	{
		invalidFolders.Add(diseaseInfo.Name);
		Console.WriteLine("Disease {0} missing factor relative risk forder.", diseaseInfo.FullName);
		return numberOfProcessedFiles;
	}

	var relativeRiskFiles = factorRelativeRiskFolder.GetFiles("*.csv", SearchOption.TopDirectoryOnly);
	Console.WriteLine("Processing {0}: factor relative risk files found:{1}.", diseaseInfo.Name, relativeRiskFiles.Length);
	foreach (var fileInfo in relativeRiskFiles)
	{
		var fileName = Path.GetFileNameWithoutExtension(fileInfo.FullName);
		var fields = fileName.Split('_');

		var fileGender = Gender.Unknown;
		var causeDisease = string.Empty;
		var factorName = string.Empty;
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
				causeDisease = fields[1].Trim();
				factorName = fields[2].Trim();
			}

			bool isMappingValid = true;
			if (!knownDiseases.ContainsKey(causeDisease))
			{
				Console.WriteLine("Unknown source disease: {0} in file: {1}", causeDisease, fileInfo.Name);
				isMappingValid = false;
			}

			if (!riskFactorName.Equals(factorName, StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Unknown risk factor: {0} in file: {1}", factorName, fileInfo.Name);
				isMappingValid = false;
			}

			if (!isMappingValid)
			{
				invalidFiles.Add(fileInfo.Name);
				continue;
			}
		}
		else
		{
			invalidFiles.Add(fileInfo.Name);
			Console.WriteLine("Invalid factor relative risk file name: {0}", fileInfo.Name);
			continue;
		}

		try
		{
			// Copy file to output folder.
			var outFilename = $"{fileGender}_{causeDisease}_{factorName}.frr";
			fileInfo.CopyTo(Path.Combine(sourceFolder.FullName, outFilename));

			numberOfProcessedFiles++;
			results.Add(new FactorRelativeRiskInfo
			{
				Gender = fileGender,
				Source = causeDisease,
				Factor = factorName,
				File = fileInfo
			});
		}
		catch (IOException ex)
		{
			duplicatedFiles.Add(fileInfo.Name);
			Console.WriteLine("Failed to copy file: {0}, cause: {1}", fileInfo.Name, ex.Message);
		}
	}
	
	return numberOfProcessedFiles;
}

private static void CreateDiseaseRelativeRiskFiles(List<DiseaseRelativeRiskInfo> diseaseRelativeInfo,
	IReadOnlyDictionary<string, string> diseases, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nCreating disease relative risk files ...");

	var timer = new Stopwatch();
	timer.Restart();
	var diseaseFiles = new Dictionary<string, Dictionary<Gender, FileInfo>>(StringComparer.OrdinalIgnoreCase);
	foreach (var item in diseaseRelativeInfo)
	{
		var key = $"{item.Source}_{item.Target}";
		if (!diseaseFiles.ContainsKey(key))
		{
			diseaseFiles[key] = new Dictionary<Gender, FileInfo>(2);
		}

		diseaseFiles[key].Add(item.Gender, item.File);
	}
	
	var results = new Dictionary<string, Dictionary<string, FileInfo>>();
	var undefinedCount = 0;
	var incompleteCount = 0;
	foreach (var item in diseaseFiles)
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
		var diseaseOutputFolder = Path.Combine(outputFolder.FullName, fromDisease, "relative_risk", "disease");
		if (!Directory.Exists(diseaseOutputFolder))
		{
			Directory.CreateDirectory(diseaseOutputFolder);
		}
		
		var filename = $"{fromDisease}_{toDisease}.csv";
		var outFileInfo = new FileInfo(Path.Combine(diseaseOutputFolder, filename));
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
}

private static void CreateRiskFactorRelativeRiskFiles(List<FactorRelativeRiskInfo> factorRelativeInfo,
	IReadOnlyDictionary<string, string> diseases, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nCreating risk factors relative risk files ...");

	var timer = new Stopwatch();
	timer.Restart();
	var riskFactorFiles = new Dictionary<string, Dictionary<Gender, FileInfo>>(StringComparer.OrdinalIgnoreCase);
	foreach (var item in factorRelativeInfo)
	{
		var key = $"{item.Source}_{item.Factor}";
		if (!riskFactorFiles.ContainsKey(key))
		{
			riskFactorFiles[key] = new Dictionary<Gender, FileInfo>(2);
		}

		riskFactorFiles[key].Add(item.Gender, item.File);
	}

	var results = new Dictionary<string, Dictionary<string, FileInfo>>();
	var undefinedCount = 0;
	var incompleteCount = 0;
	foreach (var item in riskFactorFiles)
	{
		if (item.Value.Count != 2)
		{
			incompleteCount++;
			var missing = item.Value.ContainsKey(Gender.Male) ? Gender.Female : Gender.Male;
			Console.WriteLine("Incomplete relative risk mapping, missing: {0, -8} for {1}", missing, item.Key);
			continue;
		}

		var keys = item.Key.Split('_');
		var disease = diseases[keys[0]];
		var riskFactor = keys[1].ToLower();
		var riskOutputFolder = Path.Combine(outputFolder.FullName, disease, "relative_risk", "risk_factor");
		if (!Directory.Exists(riskOutputFolder))
		{
			Directory.CreateDirectory(riskOutputFolder);
		}

		foreach (var file in item.Value)
		{
			var gender = file.Key.ToString().ToLower();
			var filename = $"{gender}_{disease}_{riskFactor}.csv";
			var outFileInfo = new FileInfo(Path.Combine(riskOutputFolder, filename));
			File.Copy(file.Value.FullName, outFileInfo.FullName);

			if (!results.ContainsKey(disease))
			{
				results.Add(disease, new Dictionary<string, FileInfo>());
			}

			results[disease].Add(gender, outFileInfo);
		}
	}

	timer.Stop();

	var totalFiles = results.Sum(s => s.Value.Count);
	Console.WriteLine("# of files created {0}, incomplete {1}, undefined {2}. Elapsed time: {3} seconds",
					  totalFiles, incompleteCount, undefinedCount, timer.Elapsed.TotalSeconds);
}

private static IReadOnlyList<string> ValidateDiseaseRelativeRiskFiles(DirectoryInfo outputFolder, double defaultRiskValue)
{
	Console.WriteLine("\nValidating disease relative risk files ...");
	var results = new List<string>();
	var timer = new Stopwatch();
	timer.Restart();
	var diseaseFolders = outputFolder.GetDirectories();
	var invalidCount = 0;
	var defaultValueCount = 0;
	var totalFileCount = 0;
	foreach (var item in diseaseFolders)
	{
		var diseaseFolder = new DirectoryInfo(Path.Combine(item.FullName, "relative_risk", "disease"));
		var relativeRiskFiles = diseaseFolder.GetFiles("*.csv", SearchOption.TopDirectoryOnly);
		totalFileCount += relativeRiskFiles.Length;
		foreach (var info in relativeRiskFiles)
		{
			var lastAge = -1;
			var oneCount = 0;
			var rowsCount = 0;
			using (var sr = info.OpenText())
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

					if (MathHelper.AreEqual(male, defaultRiskValue) && MathHelper.AreEqual(feme, defaultRiskValue))
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
	}

	timer.Stop();
	var completeCount = totalFileCount - invalidCount;
	var realContentFiles = completeCount - defaultValueCount;
	var defaulPercentage = defaultValueCount / (double)completeCount;
	Console.WriteLine("# of files {0}, complete {1}, default value {2} ({3:P2}), invalid {4}. Elapsed time: {5} seconds",
					  totalFileCount, completeCount, defaultValueCount, defaulPercentage , invalidCount, timer.Elapsed.TotalSeconds);
	return results;
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
