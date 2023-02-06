<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
</Query>

#load "Process_common.linq"

void Main()
{
	var timer = new Stopwatch();
	timer.Start();
	
	Console.WriteLine("*** Generating Baseline Scenario Risk Factors Adjustment ***\n");
	
	var baselineRange = new TimeRange { Min = 1950, Max = 2100 };
	var dataRootFolder = @"C:\Work\Data\Baseline";
	var baselineMaleFile = new FileInfo(Path.Combine(dataRootFolder,"MaleBaselineScenario.csv"));
	var baselineFemaleFile = new FileInfo(Path.Combine(dataRootFolder,"FemaleBaselineScenario.csv"));
		
	// Configure the processed output folders
	var baselineOutputFolder = new DirectoryInfo(Path.Combine(dataRootFolder, $"Output"));
	var baselineOutputFile = new FileInfo(Path.Combine(baselineOutputFolder.FullName, $"BaselineScenarioAdjustment.csv"));
	var baselineOutputDevFile = new FileInfo(baselineOutputFile.FullName.Replace(".csv","_Dev.csv"));
	
	// 1. Clear ouput folder before we start
	try
    {
		if (baselineOutputFolder.Exists) {
			baselineOutputFolder.Delete(true);
		}
		
		// 2. Create ouput folder;
		baselineOutputFolder.Create();
		
		// 3. Process baseline definition files
		var maleData = InitialiseBaselineRiskFactorAverages(baselineMaleFile, baselineRange);
		var femaleData = InitialiseBaselineRiskFactorAverages(baselineFemaleFile, baselineRange);
		
		Console.WriteLine();
		var baselineData = CreateBaselineDatasetPivot(baselineRange, maleData, femaleData);
		CreateBaselineDatasetFile(baselineOutputFile, baselineData);
		
		// 7. Create development datasets
		Console.WriteLine();
		var timeFilter = new TimeRange { Min = 2010, Max = 2030 };
		CreateDevelopmentDatasetFile(baselineOutputDevFile, baselineData, timeFilter);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}

	timer.Stop();
	Console.WriteLine("\nCompleted, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}

struct TimeRange{
	public int Min {get;set;}
	public int Max {get;set;}
}

class GenderValue
{
	public double Male {get;set;}
	
	public double Female {get;set;}

	public override string ToString()
	{
		return $"Male: {Male:N5}, Female: {Female:N5}";
	}
}

IReadOnlyDictionary<string, Dictionary<int, double>> InitialiseBaselineRiskFactorAverages(FileInfo source, TimeRange range)
{
	if (!source.Exists)
	{
		throw new FileNotFoundException($"Source file: {source} not found.");
	}
	
	Console.WriteLine("Processing definition file: {0}...", source.FullName);
	var output = new Dictionary<string, Dictionary<int, double>>();
	using (var reader = new StreamReader(source.FullName, Encoding.UTF8))
	{
		string line;
		if ((line = reader.ReadLine()) == null)
		{
			throw new InvalidDataException($"File {source.FullName} is empty.");
		}
		
		var riskFactorNames = SplitCsv(line);
		if (!riskFactorNames[0].Equals("Year", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException($"Invalid file header format: {line}, must start with [Year].");
		}
		
		string endLine;
		if ((line = reader.ReadLine()) == null || (endLine = reader.ReadLine()) == null)
		{
			throw new InvalidDataException($"File {source.FullName} must containt 3 lines.");
		}
		
		var startRiskFactors = SplitCsv(line);
		var endRiskFactors = SplitCsv(endLine);
		if (riskFactorNames.Length != startRiskFactors.Length ||riskFactorNames.Length != endRiskFactors.Length)
		{
			throw new InvalidDataException(
			$"Definition size mismatch: factors {riskFactorNames.Length}, start {startRiskFactors.Length }, end {endRiskFactors.Length}.");
		}
		
		// Parse the year range od the definition
		var startYear = int.Parse(startRiskFactors[0]);
		var endYear = int.Parse(endRiskFactors[0]);
		var yearDelta = endYear - startYear;
		if (yearDelta < 1)
		{
			throw new InvalidDataException(
			$"Invalid baseline definition, start time must be greater than end time.");
		}
		
		for (int index = 1; index < riskFactorNames.Length; index++)
		{
			var factor = riskFactorNames[index].Trim();
			double startValue = double.Parse(startRiskFactors[index]);
			double endValue = double.Parse(endRiskFactors[index]);
			
			// Using linear interpolation, but can be anything
			double slope = (endValue - startValue) / yearDelta;
			double intercept = endValue - slope * endYear;

			var column = new Dictionary<int, double>();
			for (int year = range.Min; year <= range.Max; year++)
			{
				double value = slope * year + intercept;
				column.Add(year, value);
			}
			
			output.Add(factor, column);
		}
	}
	
	return output;
}

IReadOnlyDictionary<int, Dictionary<string, GenderValue>> CreateBaselineDatasetPivot(
		TimeRange range,
		IReadOnlyDictionary<string, Dictionary<int, double>> maleData,
		IReadOnlyDictionary<string, Dictionary<int, double>> femaleData)
{
	Console.WriteLine("Creating baseline scenario adjustments dataset ...");
	var output = new Dictionary<int, Dictionary<string, GenderValue>>();
	
	// Pivot data to create a year => factors table
	for (int year = range.Min; year <= range.Max; year++)
	{
		var factorValues = new Dictionary<string, GenderValue>();
		foreach (var factor in maleData)
		{
			factorValues[factor.Key] = new GenderValue { Male = factor.Value[year]};
		}

		foreach (var factor in femaleData)
		{
			// This sould always already exist, otherwise need to handle nullables 
			factorValues[factor.Key].Female = factor.Value[year];
		}

		output.Add(year, factorValues);
	}
	
	return output;
}

void CreateBaselineDatasetFile(FileInfo outputFile, IReadOnlyDictionary<int, Dictionary<string, GenderValue>> data)
{
	if (outputFile.Exists)
	{
		throw new DuplicateNameException($"Output file: {outputFile.FullName} already exists.");
	}
	
	Console.WriteLine("Creating baseline CSV format file...: {0} ...", outputFile.FullName);
	using (var sw = new StreamWriter(outputFile.FullName, true, Encoding.UTF8))
	{
		sw.WriteLine("Time,RiskFactor,Male,Female");
		foreach (var year in data)
		{
			foreach (var factor in year.Value){
				sw.WriteLine("{0},{1},{2},{3}",year.Key, factor.Key, factor.Value.Male, factor.Value.Female);
			}
		}
	}
	
	var baselineOutputJsonFile = new FileInfo(outputFile.FullName.Replace(".csv", ".json"));
	Console.WriteLine("Creating baseline JSON format file: {0} ...", baselineOutputJsonFile.FullName);
	using (FileStream createStream = File.Create(baselineOutputJsonFile.FullName))
	{
		JsonSerializer.SerializeAsync(createStream, data).Wait();
	}
}

void CreateDevelopmentDatasetFile(FileInfo outputFile, IReadOnlyDictionary<int, Dictionary<string, GenderValue>> data, TimeRange timeFilter)
{
	if (outputFile.Exists)
	{
		throw new DuplicateNameException($"Output file: {outputFile.FullName} already exists.");
	}
	
	Console.WriteLine("Creating baseline development CSV format file...: {0} ...", outputFile.FullName);
	using (var sw = new StreamWriter(outputFile.FullName, true, Encoding.UTF8))
	{
		sw.WriteLine("Time,RiskFactor,Male,Female");
		foreach (var year in data)
		{
			if (year.Key < timeFilter.Min || year.Key > timeFilter.Max)
			{
				continue;	
			}
			
			foreach (var factor in year.Value)
			{
				sw.WriteLine("{0},{1},{2},{3}", year.Key, factor.Key, factor.Value.Male, factor.Value.Female);
			}
		}
	}
	
	// Generate Json format
	var devData = data.Where(s => timeFilter.Min <= s.Key && s.Key <= timeFilter.Max).ToDictionary(t => t.Key, t=> t.Value);
	var baselineOutputJsonFile = new FileInfo(outputFile.FullName.Replace(".csv", ".json"));
	Console.WriteLine("Creating baseline development JSON format file: {0} ...", baselineOutputJsonFile.FullName);
	using (FileStream createStream = File.Create(baselineOutputJsonFile.FullName))
	{
		JsonSerializer.SerializeAsync(createStream, devData).Wait();
	}
}