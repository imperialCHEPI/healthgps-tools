<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
</Query>

#load "Process_common.linq"

void Main()
{
	Console.WriteLine("Process Energy Balance Model folder.");

	var countryName = "Slovenia";

	var dataRootFolder = @"C:\Work\Data";
	var countriesFileName = Path.Combine(dataRootFolder, "ISO3166-1-Countries.csv");
	Console.WriteLine("*** Loading ISO 3166-1 Countries. ***\n");
	var countries = LoadCountries(countriesFileName);
	
	// Validate country
	var countryInfo = countries.FirstOrDefault(s => s.Value.Name.StartsWith(countryName, StringComparison.OrdinalIgnoreCase));
	if (countryInfo.Key < 1)
	{
		throw new InvalidOperationException($"Country name: {countryName} not found.");
	}
	
	Console.WriteLine("Country: {0}", countryInfo.Value.ToString());

	var modelRootFolder = Path.Combine(dataRootFolder, "Models");
	var countryFolder = new DirectoryInfo(Path.Combine(modelRootFolder, countryName));
	if (!countryFolder.Exists)
	{
		throw new DirectoryNotFoundException($"Country data folder: {countryFolder.FullName} not found.");
	}
	
	// 1. Process country folderS
	UpdateDataFileAndMoveAdjustmentFiles(countryFolder);
	var functions = ProcessCountryModelFolder(countryFolder);
	
	// 2. Create mappings
	var mappingFileName = Path.Combine(modelRootFolder, "StaticMappingFile.csv");
	var mappings = CreateListOfFactors(mappingFileName, functions);
	mappings.Dump();
	
	// 3. Process country data file
	var lowerQuantile = 0.01;
	var upperQuantile = 0.99;
	var countryDataFileName = Path.Combine(countryFolder.FullName, $"{countryName}.DataFile.csv");
	var boundaries = CalculateFactorBoundaries(countryDataFileName, mappings, lowerQuantile, upperQuantile);

	// 4. Write model to file
	var maximumAge = 100;
	var boundaryPercentage = 0.05;
	var countryModel = CreateCountryModel(countryInfo.Value, mappings, boundaries, functions, maximumAge, boundaryPercentage);
	var outputFile = Path.Combine(countryFolder.FullName, $"{countryName}.EBHLM.json");
	using (var writer = new FileStream(outputFile, FileMode.Create))
	{
		JsonSerializer.SerializeAsync(writer, countryModel).Wait();
	}
	
	// 4. Write model config section to file
	outputFile = Path.Combine(countryFolder.FullName, $"{countryName}.EBHLM_Config.json");
	CreateRiskFactorsJson(outputFile, countryModel.RiskFactors);
}

public void CreateRiskFactorsJson(string outputFile, IReadOnlyCollection<RiskFactor> riskFactors)
{
	using var stream = new FileStream(outputFile, FileMode.CreateNew);
	using var writer = new Utf8JsonWriter(stream);
	writer.WriteStartObject();
	writer.WriteStartArray("risk_factors");
	foreach (var item in riskFactors)
	{
		writer.WriteStartObject();
		writer.WriteString("name", item.Name);
		writer.WriteNumber("level", item.Level);
		writer.WriteString("proxy", item.Proxy);
		writer.WriteStartArray("range");
		writer.WriteNumberValue(item.Range.First());
		writer.WriteNumberValue(item.Range.Last());
		writer.WriteEndArray();
		writer.WriteEndObject();
	}
	
	writer.WriteEndArray();
	writer.WriteEndObject();
	writer.Flush();
}

public class CountryModel
{
	public CountryModel(Country countryInfo, double percentage, List<RiskFactor> riskFactors, List<DeltaCoefficient> variables,
						SortedDictionary<string, Dictionary<string, List<FactorDynamicEquation>>> equations)
	{
		this.Country = countryInfo;
		this.BoundaryPercentage = percentage;
		this.RiskFactors = riskFactors;
		this.Variables = variables;
		this.Equations = equations;
	}
	
	public Country Country {get;}
	public double BoundaryPercentage {get;}
	public List<RiskFactor> RiskFactors {get;}
	public List<DeltaCoefficient> Variables {get;}
	public SortedDictionary<string, Dictionary<string, List<FactorDynamicEquation>>> Equations {get;}
}

public struct RiskFactor
{
	public string Name {get; set;}
	public int Level {get; set;}
	public string Proxy {get; set;}
	public double[] Range {get;set;}
}

public struct DeltaCoefficient
{
	public string Name { get; set; }
	public int Level { get; set; }
	public string Factor { get; set; }
}

public class FactorRange
{
	public FactorRange(double lower, double upper)
	{
		if (upper < lower)
		{
			throw new InvalidDataException("The upper boundary must not be less than the lower.");
		}
		
		this.Minimum = lower;
		this.Maximum = upper;
	}
	
	public double Minimum {get;}
	public double Maximum {get;}
	
	public override string ToString()
	{
		return string.Format("[{0} - {1}]", this.Minimum, this.Maximum);
	}

	public double[] ToArray()
	{
		return new double[] {Minimum, Maximum};
	}
}

CountryModel CreateCountryModel(Country countryInfo, Dictionary<string, int> mappings,
Dictionary<string, FactorRange> boundaries, Dictionary<string, FactorDynamicEquation> functions, int maxAge, double percentage)
{
	// Create risk factors
	var modelMapping = new List<RiskFactor>();
	var otherMapping = new List<DeltaCoefficient>();
	foreach (var item in mappings)
	{
		var factor = new RiskFactor()
		{
			Name = item.Key,
			Level = item.Value,
			Proxy = string.Empty,
			Range = new double[0],
		};

		if (factor.Level == 0)
		{
			if (factor.Name.StartsWith("Age"))
			{
				if (factor.Name.Length == 3)
				{
					factor.Proxy = factor.Name.ToLower();
				}
			}
			else if (factor.Name.Equals("Year1", StringComparison.OrdinalIgnoreCase))
			{
				factor.Name = "Year";
				factor.Proxy = factor.Name.ToLower();
			}
			else
			{
				factor.Proxy = factor.Name.ToLower();
			}
		}

		if (boundaries.ContainsKey(item.Key))
		{
			factor.Range = boundaries[item.Key].ToArray();
		}

		if (factor.Range.Length > 0)
		{
			modelMapping.Add(factor);
		}
		else
		{
			if (string.IsNullOrWhiteSpace(factor.Proxy) && factor.Name.StartsWith("d"))
			{
				factor.Proxy = factor.Name.Substring(1).ToLower();
			}

			otherMapping.Add(new DeltaCoefficient {
			 Name = factor.Name,
			 Factor = factor.Proxy,
			 Level = factor.Level,
			});
		}
	}
	
	// Create model function
	var modelFunctions = new SortedDictionary<string, Dictionary<string, List<FactorDynamicEquation>>>(); 
	foreach (var entry in functions)
	{
		var fields = entry.Key.Split(".");
		var ageKey = "0-19";
		if (fields.Last().StartsWith("Over"))
		{
			ageKey = $"20-{maxAge}";
		}

		var genderKey = fields[1];
		if (!modelFunctions.ContainsKey(ageKey))
		{
			modelFunctions.Add(ageKey, new Dictionary<string, List<FactorDynamicEquation>>());
		}
		
		if (!modelFunctions[ageKey].ContainsKey(genderKey))
		{
			modelFunctions[ageKey].Add(genderKey, new List<FactorDynamicEquation>());
		}
		
		modelFunctions[ageKey][genderKey].Add(entry.Value);
	}

	return new CountryModel(countryInfo, percentage, modelMapping, otherMapping, modelFunctions);
}

Dictionary<string, FactorRange> CalculateFactorBoundaries(string countryDataFileName,
Dictionary<string,int> mappings, double lowerQuantile, double upperQuantile)
{
	var result = new Dictionary<string, FactorRange>();
	if (!File.Exists(countryDataFileName))
	{
		throw new FileNotFoundException($"Country data file: {countryDataFileName} not found.");	
	}
	
	using (var reader = new StreamReader(countryDataFileName))
	{
		// Process file header
		var line = reader.ReadLine();
		var fileColumns = line.Split(",").Select(l => l.Replace("\"", "").Trim()).ToList();
		
		var columnsIndex = new Dictionary<string, int>();
		var columnsData = new Dictionary<string, List<double>>();
		foreach (var factor in mappings)
		{
			var factorIndex = fileColumns.FindIndex(s => s.Equals(factor.Key, StringComparison.OrdinalIgnoreCase));
			if (factorIndex >= 0)
			{
				columnsIndex.Add(factor.Key, factorIndex);
				columnsData.Add(factor.Key, new List<double>());
			}
		}
		
		while ((line = reader.ReadLine()) != null)
		{
			var row = line.Split(",");
			foreach(var column in columnsIndex)
			{
				if (double.TryParse(row[column.Value], out double value))
				{
					columnsData[column.Key].Add(value);
				}
			}
		}
		
		// Calculate range
		foreach (var column in columnsData)
		{
			column.Value.Sort();
			var lowerIndex = (int)Math.Round(lowerQuantile * column.Value.Count, 0);
			var upperIndex = (int)Math.Round(upperQuantile * column.Value.Count, 0);
			result.Add(column.Key, new FactorRange(column.Value[lowerIndex], column.Value[upperIndex]));
		}	
	}
	
	return result;
}

Dictionary<string, int> CreateListOfFactors(string mappingFileName, Dictionary<string, FactorDynamicEquation> functions)
{
	if (!File.Exists(mappingFileName))
	{
		throw new FileNotFoundException($"Models mapping file: {mappingFileName} not found.");
	}
	
	var mappings = new Dictionary<string, int>();
	using (var reader = new StreamReader(mappingFileName))
	{
		var line = reader.ReadLine();
		var fields = line.Split(",").Select(l => l.Replace("\"", "")).ToArray();
		if (fields.Length != 2)
		{
			throw new InvalidDataException($"Invalid mapping file format: {line}, must have 2 columns.");
		}
		
		while ((line = reader.ReadLine()) != null)
		{
			fields = line.Split(",");
			mappings.Add(fields[0].Trim(), int.Parse(fields[1].Trim()));
		}
	}

	// Add non-mapped factors
	foreach (var model in functions)
	{
		var factorLevel = -1;
		if (!mappings.ContainsKey(model.Value.Name))
		{
			mappings.Add(model.Value.Name, factorLevel);
		}
		else
		{
			factorLevel = mappings[model.Value.Name];
		}
		
		foreach(var factor in model.Value.Coefficients)
		{
			if (!mappings.ContainsKey(factor.Key) && !factor.Key.Contains("Intercept"))
			{
				mappings.Add(factor.Key, factorLevel);
			}
		}
	}
	
	return mappings;
}

public class FactorDynamicEquation
{
	public FactorDynamicEquation()
	{
		Coefficients = new Dictionary<string, double>();
	}
	
	public string Name {get; set;}
	
	public IDictionary<string, double> Coefficients {get; set;}
	
	public double ResidualsStandardDeviation { get; set;}
}

public static double StandardDeviationCompact(IEnumerable<double> values)
{
	double avg = values.Average();
	return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
}

public static double StandardDeviationLocal(IEnumerable<double> values)
{
	var sum1 = 0.0;
	var sum2 = 0.0;
	int count = 0;
	foreach (var residual in values)
	{
		sum1 += residual;
		sum2 += residual * residual;
		count++;
	}

	var m1 = sum1 / count;
	var m2 = sum2 / count;
	return Math.Sqrt(m2 - m1 * m1);
}

private Dictionary<string, FactorDynamicEquation> ProcessCountryModelFolder(DirectoryInfo countryFolder)
{
	var modelsFolder = Path.Combine(countryFolder.FullName, "Models");
	if (!Directory.Exists(modelsFolder))
	{
		throw new DirectoryNotFoundException($"Country models folder: {modelsFolder} not found.");
	}
	
	var results = new Dictionary<string, FactorDynamicEquation>();
	var modelFiles = Directory.GetFiles(modelsFolder, "*.csv");
	Console.WriteLine("Folder: {0}, contains {1} model files.", modelsFolder, modelFiles.Length);
	var modelKey = string.Empty;
	foreach(string fileName in modelFiles)
	{
		var modelFileShortName = Path.GetFileName(fileName);
		var modelKeyIndex = modelFileShortName.IndexOf(".coefficient", StringComparison.OrdinalIgnoreCase);
		if (modelKeyIndex > 0)
		{
			Console.WriteLine("Processing model {0}", modelFileShortName);

			// Process 	coefficients file
			modelKey = modelFileShortName.Substring(0, modelKeyIndex);
			if (!results.ContainsKey(modelKey))
			{
				var modelName = modelFileShortName.Substring(0, modelFileShortName.IndexOf("."));
				results.Add(modelKey, new FactorDynamicEquation() {Name = modelName});
			}
			
			results[modelKey].Coefficients = ParseCoefficientsFile(fileName);
			continue;
		}
		
		modelKeyIndex = modelFileShortName.IndexOf(".residuals", StringComparison.OrdinalIgnoreCase);
		if (modelKeyIndex > 0)
		{
			Console.WriteLine("Processing model {0}", modelFileShortName);
			
			// Process 	residuals file
			modelKey = modelFileShortName.Substring(0, modelKeyIndex);
			if (!results.ContainsKey(modelKey))
			{
				var modelName = modelFileShortName.Substring(0, modelFileShortName.IndexOf("."));
				results.Add(modelKey, new FactorDynamicEquation() {Name = modelName});
			}
			
			var residuals = ParseResidualFile(fileName);
			results[modelKey].ResidualsStandardDeviation = StandardDeviationLocal(residuals);
			continue;
		}
		
		Console.WriteLine("Invalid model file in folder: {0}", modelFileShortName); 
	}
	
	return results;
}

IDictionary<string, double> ParseCoefficientsFile(string fileName)
{
	var results = new Dictionary<string, double>();
	using(var reader = new StreamReader(fileName))
	{
		// Process file header
		var line = reader.ReadLine();
		var fields = line.Split(",").Select(l => l.Replace("\"", "")).ToArray();
		var valueIndex = Array.FindIndex(fields, s => s.Equals("x", StringComparison.OrdinalIgnoreCase));
		if (valueIndex < 0 || fields.Length != 2)
		{
			throw new InvalidDataException($"Invalid coefficients file header format: {line}, must have 2 columns, one is 'x' column.");
		}

		var nameIndex = valueIndex == 0 ? 1 : 0;
		var interceptName = "Intercept";
		while ((line = reader.ReadLine()) != null)
		{
			fields = line.Split(",");
			var coeffName = fields[nameIndex].Replace("\"", "").Trim();
			if (coeffName.Contains(interceptName, StringComparison.OrdinalIgnoreCase) && coeffName.Length != interceptName.Length)
			{
				coeffName = interceptName;
			}
			
			results.Add(coeffName, double.Parse(fields[valueIndex].Trim()));
		}

		results.TrimExcess();
	}
	
	return results;
}

private IList<double> ParseResidualFile(string fileName)
{
	var results = new List<double>();
	using(var reader = new StreamReader(fileName))
	{
		// Process file header
		var line = reader.ReadLine();
		var fields = line.Split(",").Select(l => l.Replace("\"", "")).ToArray();
		var valueIndex = Array.FindIndex(fields, s => s.Equals("x", StringComparison.OrdinalIgnoreCase));
		if (valueIndex < 0)
		{
			throw new InvalidDataException($"Invalid residual file header format: {line}, must have a 'x' column.");
		}
		
		while ((line = reader.ReadLine()) != null)
		{
			fields = line.Split(",");
			results.Add(double.Parse(fields[valueIndex].Trim()));
		}
		
		results.TrimExcess();
	}
	
	return results;
}

private void UpdateDataFileAndMoveAdjustmentFiles(DirectoryInfo countryFolder)
{
	var modelsFolder = Path.Combine(countryFolder.FullName, "Models");
	if (!Directory.Exists(modelsFolder))
	{
		throw new DirectoryNotFoundException($"Country models folder: {modelsFolder} not found.");
	}

	var countryName = countryFolder.Name;
	var countryDataFile = Path.Combine(countryFolder.FullName, $"{countryName}.DataFile.csv");
	if (!File.Exists(countryDataFile))
	{
		throw new DirectoryNotFoundException($"Country data file: {countryDataFile} not found.");
	}
	
	using (var fs = File.Open(countryDataFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
	{
		using (var sr = new StreamReader(fs))
		{
			var header = sr.ReadLine();
			if (header.Contains("Year1", StringComparison.OrdinalIgnoreCase))
			{
				header = header.Replace("\"", "", StringComparison.OrdinalIgnoreCase);
				header = header.Replace("Year1", "Year", StringComparison.OrdinalIgnoreCase);
				var fileData = sr.ReadToEnd();
				
				// Delete file content
				fs.SetLength(0);
				fs.Flush();
				using (var sw = new StreamWriter(fs))
				{
					sw.WriteLine(header);
					sw.Write(fileData);
				}
			}
		}
	}
	
	var initialFiles = Directory.GetFiles(modelsFolder, "InitialModelAdjustment*.csv");
	if (initialFiles.Length > 0)
	{
		var fileStartName = $"{countryName}.Initial.Adjustment";
		foreach (var sourceFile in initialFiles)
		{
			var fileName = Path.GetFileName(sourceFile);
			var genderIndex = fileName.IndexOf(".");
			fileName = $"{fileStartName}{fileName.Substring(genderIndex)}";
			var destinationFile = Path.Combine(countryFolder.FullName, fileName);
			File.Move(sourceFile, destinationFile);
		}
	}
	
	var updateFiles = Directory.GetFiles(modelsFolder, "ModelAdjustment*.csv");
	if (updateFiles.Length > 0)
	{
		var fileStartName = $"{countryName}.Update.Adjustment";
		foreach (var sourceFile in updateFiles)
		{
			var fileName = Path.GetFileName(sourceFile);
			var genderIndex = fileName.IndexOf(".");
			fileName = $"{fileStartName}{fileName.Substring(genderIndex)}";
			var destinationFile = Path.Combine(countryFolder.FullName, fileName);
			File.Move(sourceFile, destinationFile);
		}
	}

	var factorMeanFiles = Directory.GetFiles(modelsFolder, "FactorMeans*.csv");
	if (factorMeanFiles.Length > 0)
	{
		var fileStartName = $"{countryName}.FactorsMean";
		foreach (var sourceFile in factorMeanFiles)
		{
			var fileName = Path.GetFileName(sourceFile);
			var genderIndex = fileName.IndexOf(".");
			fileName = $"{fileStartName}{fileName.Substring(genderIndex)}";
			var destinationFile = Path.Combine(countryFolder.FullName, fileName);
			File.Move(sourceFile, destinationFile);
		}
	}
}

