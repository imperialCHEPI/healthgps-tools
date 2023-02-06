<Query Kind="Program">
  <Output>DataGrids</Output>
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
	var mortalityOutputFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "mortality"));
	ListCsvFilesInFolder(UN_DB_Folder);
	
	try
	{
		// 1. Clear ouput folder before we start
		if (mortalityOutputFolder.Exists)
		{
			mortalityOutputFolder.Delete(true);
		}

		// 2. Create ouput folder;
		mortalityOutputFolder.Create();

		// 3. Process file and split data per country - unit conversion from thousands
		var unit_multiplier = 1000.0f;

		var mortality_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2022_DeathsBySingleAgeSex_Medium_1950-2021.csv"));
		var country_data = SplitMortalityFileByCountry(countries, mortality_file, unit_multiplier, null);

		mortality_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2022_DeathsBySingleAgeSex_Medium_2022-2100.csv"));
		country_data = SplitMortalityFileByCountry(countries, mortality_file, unit_multiplier, country_data);

		// 4. Create country files, use test version to create full datasets with scaled fields.
		var filenamePrefix = "M"; // MXXX.csv
		//CreateCountryRawFiles(countries, country_data, mortalityOutputFolder, filenamePrefix);
		CreateCountryFiles(countries, country_data, mortalityOutputFolder, filenamePrefix);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}

	timer.Stop();
	Console.WriteLine("\nScript total elapsed time: {0:N3} seconds.", timer.Elapsed.TotalSeconds);
}

public class MortalityRow : WPPRow
{
	public int LocID { get; set; }      // Key
	public string Location { get; set; }
	public int VarID { get; set; }
	public string Variant { get; set; }
	public int Time { get; set; }  		// Key
	public int Age { get; set; }		// Key
	public float DeathMale { get; set; }
	public float DeathFemale { get; set; }
	public float DeathTotal { get; set; }
	public string RawData { get; set; }
	
	public WPPRowKey ToRowKey()
	{
		return new WPPRowKey(this.LocID, this.Time, this.Age);
	}

	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{Age},{DeathMale},{DeathFemale},{DeathTotal}";
	}

	public static string GetCsvFileHeader()
	{
		return "LocID,Location,VarID,Variant,Time,Age,DeathMale,DeathFemale,DeathTotal";
	}

	public static IReadOnlyCollection<string> GetRequiredFields()
	{
		return new List<string>
		{
			"LocID", "Location", "VarID", "Variant", "Time", "AgeGrpStart", "DeathMale", "DeathFemale", "DeathTotal"
		};
	}

	public static MortalityRow Parse(string line, IReadOnlyDictionary<string, int> mapping, float unit_multiplier)
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
			var result = new MortalityRow
			{
				LocID = int.Parse(fields[mapping["LocID"]].Trim(), CultureInfo.InvariantCulture),
				Location = fields[mapping["Location"]].Trim(),
				VarID = int.Parse(fields[mapping["VarID"]], CultureInfo.InvariantCulture),
				Variant = fields[mapping["Variant"]].Trim(),
				Time = int.Parse(fields[mapping["Time"]].Trim(), CultureInfo.InvariantCulture),
				Age = int.Parse(fields[mapping["AgeGrpStart"]].Trim(), CultureInfo.InvariantCulture),
				DeathMale = ParseAndScaleFloat(fields[mapping["DeathMale"]].Trim(), unit_multiplier),
				DeathFemale = ParseAndScaleFloat(fields[mapping["DeathFemale"]].Trim(), unit_multiplier),
				DeathTotal = ParseAndScaleFloat(fields[mapping["DeathTotal"]].Trim(), unit_multiplier),
			};


			fields[mapping["DeathMale"]] = result.DeathMale.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["DeathFemale"]] = result.DeathFemale.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["DeathTotal"]] = result.DeathTotal.ToString("R", CultureInfo.InvariantCulture);
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

public class MortalityDataset : WPPDataset<MortalityRow>
{
	public MortalityDataset(string csvFileHeader, string rawFileHeader, Dictionary<int, SortedDictionary<WPPRowKey, MortalityRow>> dataset)
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

	public string CsvFileHeader { get;}

	public string RawFileHeader { get;}

	public Dictionary<int, SortedDictionary<WPPRowKey, MortalityRow>> Data { get;}
}

private static MortalityDataset SplitMortalityFileByCountry(IReadOnlyDictionary<int, Country> locations,
FileInfo sourceFile, float unit_multiplier, MortalityDataset dataset)
{
	if (!sourceFile.Exists)
	{
		throw new FileNotFoundException($"Source file {sourceFile.FullName} not found.");
	}

	Console.WriteLine("\nProcessing mortality file {0} ... \n", sourceFile.FullName);
	var timer = new Stopwatch();
	timer.Start();

	var result = new Dictionary<int, SortedDictionary<WPPRowKey, MortalityRow>>();
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

		var mapping = CreateCsvFieldsMapping(MortalityRow.GetRequiredFields(), rawFileHeader);
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

			var row = MortalityRow.Parse(line, mapping, unit_multiplier);
			if (!result.TryGetValue(code_value, out SortedDictionary<WPPRowKey, MortalityRow> country))
			{
				country = new SortedDictionary<WPPRowKey, MortalityRow>();
				result.Add(code_value, country);
				Console.WriteLine("{0,-5} Creating country: {1} ...", result.Count, locations[code_value].Name);
			}

			country.Add(row.ToRowKey(), row);
		}
	}

	timer.Stop();
	Console.WriteLine($"Completed {result.Count} countries, elapsed time: {timer.Elapsed.TotalSeconds} seconds.");
	return new MortalityDataset(MortalityRow.GetCsvFileHeader(), rawFileHeader, result);
}
