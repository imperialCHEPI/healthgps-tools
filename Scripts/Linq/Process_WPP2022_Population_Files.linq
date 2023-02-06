<Query Kind="Program">
  <Namespace>System.Globalization</Namespace>
</Query>

#load "Process_common.linq"
#load "Process_WPP_common"

void Main()
{
	var timer = new Stopwatch();
	timer.Start();

	var data_root_folder = @"C:\Work\Data";
	var countries_file = Path.Combine(data_root_folder, "ISO3166-1-Countries.csv");
	Console.WriteLine("*** Loading ISO 3166-1 Countries. ***\n");

	var countries = LoadCountries(countries_file);
	Console.WriteLine($"There are {countries.Count} countries in file.");

	Console.WriteLine("\n*** Processing UN Population Database data. ***\n");
	var UN_DB_Folder = Path.Combine(data_root_folder, "UNDB2022");
	var populationOutputFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "population"));
	ListCsvFilesInFolder(UN_DB_Folder);

	try
	{
		// 1. Clear ouput folder before we start
		if (!populationOutputFolder.Exists)
		{
			populationOutputFolder.Create();
		}
		else
		{
			foreach (var file in populationOutputFolder.GetFiles("*.csv"))
			{
				file.Delete();
			}
		}

		// 2. Split historic file per country - population unit conversion from thousands
		var unit_multiplier = 1000.0f;
		var population_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2022_PopulationBySingleAgeSex_Medium_1950-2021.csv"));
		var country_data = SplitPopulationFileByCountry(countries, population_file, unit_multiplier, null);

		// 3. Split forecast file per country - population unit conversion from thousands
		population_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2022_PopulationBySingleAgeSex_Medium_2022-2100.csv"));
		country_data = SplitPopulationFileByCountry(countries, population_file, unit_multiplier, country_data);

		// 4. Create country files, use test version to create full datasets with scaled fields.
		var filenamePrefix = "P"; // PXXX.csv
		//CreateCountryRawFiles(countries, country_data, populationOutputFolder, filenamePrefix);
		CreateCountryFiles(countries, country_data, populationOutputFolder, filenamePrefix);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}

	timer.Stop();
	Console.WriteLine("\nScript total elapsed time: {0:N3} seconds.", timer.Elapsed.TotalSeconds);
}

public class PopulationRow : WPPRow
{
	public int LocID { get; set; }		// Key
	public string Location { get; set; }
	public int VarID { get; set; }
	public string Variant { get; set; }
	public int Time { get; set; }		// Key
	public int Age { get; set; }		// Key
	public float PopMale { get; set; }
	public float PopFemale { get; set; }
	public float PopTotal { get; set; }
	public string RawData { get; set; }
	
	public WPPRowKey ToRowKey()
	{
		return new WPPRowKey(this.LocID, this.Time, this.Age);
	}

	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{Age},{PopMale},{PopFemale},{PopTotal}";
	}

	public static string GetCsvFileHeader()
	{
		return "LocID,Location,VarID,Variant,Time,Age,PopMale,PopFemale,PopTotal";
	}

	public static IReadOnlyCollection<string> GetRequiredFields()
	{
		return new List<string>
		{
			"LocID", "Location", "VarID", "Variant", "Time", "AgeGrpStart", "PopMale", "PopFemale", "PopTotal"
		};
	}

	public static PopulationRow Parse(string line, IReadOnlyDictionary<string, int> mapping, float unit_multiplier)
	{
		string[] fields = null;
		if (line.Contains("\""))
		{
			fields = SplitCsv(line);
		}
		else
		{
			fields = line.Split(",");
		}

		if (fields.Length != 20)
		{
			throw new InvalidDataException($"Invalid input format: {line}, must have 20 fields, not {fields.Length} fields.");
		}

		try
		{
			var result = new PopulationRow
			{
				LocID = int.Parse(fields[mapping["LocID"]].Trim(), CultureInfo.InvariantCulture),
				Location = fields[mapping["Location"]].Trim(),
				VarID = int.Parse(fields[mapping["VarID"]], CultureInfo.InvariantCulture),
				Variant = fields[mapping["Variant"]].Trim(),
				Time = int.Parse(fields[mapping["Time"]].Trim(), CultureInfo.InvariantCulture),
				Age = int.Parse(fields[mapping["AgeGrpStart"]].Trim(), CultureInfo.InvariantCulture),
				PopMale = ParseAndScaleFloat(fields[mapping["PopMale"]].Trim(), unit_multiplier),
				PopFemale = ParseAndScaleFloat(fields[mapping["PopFemale"]].Trim(), unit_multiplier),
				PopTotal = ParseAndScaleFloat(fields[mapping["PopTotal"]].Trim(), unit_multiplier),
			};


			fields[mapping["PopMale"]] = result.PopMale.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["PopFemale"]] = result.PopFemale.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["PopTotal"]] = result.PopTotal.ToString("R", CultureInfo.InvariantCulture);
			result.RawData = string.Join(",", fields);
			return result;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid line format: {line}, {ex.Message}");
			throw;
		}
	}
}

public class PopulationDataset : WPPDataset<PopulationRow>
{
	public PopulationDataset(string csvFileHeader, string rawFileHeader, Dictionary<int, SortedDictionary<WPPRowKey, PopulationRow>> dataset)
	{
		if (string.IsNullOrWhiteSpace(csvFileHeader))
		{
			throw new ArgumentNullException(nameof(rawFileHeader), "The CSV file header parameter must not be null or empty.");
		}
		
		if (string.IsNullOrWhiteSpace(rawFileHeader))
		{
			throw new ArgumentNullException(nameof(rawFileHeader), "The raw file header parameter must not be null or empty.");
		}

		if (dataset == null)
		{
			throw new ArgumentNullException(nameof(dataset), "The dataset parameter must not be null.");
		}
		
		this.CsvFileHeader = csvFileHeader;
		this.RawFileHeader = rawFileHeader;
		this.Data = dataset;
	}
	
	public string CsvFileHeader { get; }
	
	public string RawFileHeader { get; }
	
	public Dictionary<int, SortedDictionary<WPPRowKey, PopulationRow>> Data { get; }
}

PopulationDataset SplitPopulationFileByCountry(IReadOnlyDictionary<int, Country> locations, 
FileInfo sourceFile, float unit_multiplier, PopulationDataset dataset)
{
	if (!sourceFile.Exists)
	{
		throw new FileNotFoundException($"Source file {sourceFile.FullName} not found.");
	}

	Console.WriteLine("\nProcessing mortality file {0} ... \n", sourceFile.FullName);
	var timer = new Stopwatch();
	timer.Start();

	var result = new Dictionary<int, SortedDictionary<WPPRowKey, PopulationRow>>();
	if (dataset != null)
	{
		result = dataset.Data;
	}

	var rawFileHeader = string.Empty;
	using (var reader = new StreamReader(sourceFile.FullName, Encoding.UTF8))
	{
		if ((rawFileHeader = reader.ReadLine()) == null)
		{
			throw new InvalidDataException($"File {sourceFile.FullName} is empty.");
		}

		var mapping = CreateCsvFieldsMapping(PopulationRow.GetRequiredFields(), rawFileHeader);
		var line = string.Empty;
		var code_idx_start = -1;
		var code_idx_end = -1;
		while ((line = reader.ReadLine()) != null)
		{
			code_idx_start = line.IndexOf(",") + 1;
			code_idx_end = line.IndexOf(",", code_idx_start);
			var code_value = int.Parse(line.Substring(code_idx_start, code_idx_end - code_idx_start));
			if (!locations.ContainsKey(code_value))
			{
				continue; // Ignore non country codes
			}

			var row = PopulationRow.Parse(line, mapping, unit_multiplier);
			if (!result.TryGetValue(code_value, out SortedDictionary<WPPRowKey, PopulationRow> country))
			{
				country = new SortedDictionary<WPPRowKey, PopulationRow>();
				result.Add(code_value, country);
				Console.WriteLine("{0,-5} Creating country: {1} ...", result.Count, locations[code_value].Name);
			}

			country.Add(row.ToRowKey(), row);
		}
	}

	timer.Stop();
	Console.WriteLine($"Completed {result.Count} countries, elapsed time: {timer.Elapsed.TotalSeconds} seconds.");
	return new PopulationDataset(PopulationRow.GetCsvFileHeader(), rawFileHeader, result);
}
