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
	var countries_file = Path.Combine(data_root_folder,"ISO3166-1-Countries.csv");
	Console.WriteLine("*** Loading ISO 3166-1 Countries. ***\n");
	
	var countries = LoadCountries(countries_file);
	Console.WriteLine($"There are {countries.Count} countries in file.");
	
	Console.WriteLine("\n*** Processing UN Population Database data. ***\n");
	var UN_DB_Folder = Path.Combine(data_root_folder,"UNDB2022");
	var indicatorsOutputFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "indicators"));
	ListCsvFilesInFolder(UN_DB_Folder);
	
	try
	{
		// 1. Clear ouput folder before we start
		if (indicatorsOutputFolder.Exists) {
			indicatorsOutputFolder.Delete(true);
		}
		
		// 2. Create ouput folder;
		indicatorsOutputFolder.Create();
		
		// 3. Process file and split data per country - unit conversion from thousands
		var sexRatioUnitMultiplier = 1.0f/100.0f;
		var populationUnitMultiplier = 1000.0f;
		var indicatorsFilename = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2022_Demographic_Indicators_Medium.csv"));
		var country_data = SplitIndicatorsFileByCountry(countries, indicatorsFilename, sexRatioUnitMultiplier, populationUnitMultiplier);
		
		// 4. Create country files, use test version to create full datasets with scaled fields.
		var filenamePrefix = "Pi"; // PiXXX.csv
		//CreateCountryRawFiles(countries, country_data, indicatorsOutputFolder, filenamePrefix);
		CreateCountryFiles(countries, country_data, indicatorsOutputFolder, filenamePrefix);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}

	timer.Stop();
	Console.WriteLine("\nScript total elapsed time: {0:N3} seconds.", timer.Elapsed.TotalSeconds);
}

public struct IndicatorRow : WPPRow
{
	public int LocID { get; set; }		// Key
	public string Location {get; set;}
	public int VarID {get; set;}
	public string Variant {get; set;}
	public int Time {get; set;}			// Key
	public float Births { get; set; }
	public float SRB { get; set; }
	public float LEx {get; set;}
	public float LExMale {get; set;}
	public float LExFemale {get; set;}
	public float Deaths {get; set;}
	public float DeathsMale {get; set;}
	public float DeathsFemale {get; set;}
	public float NetMigrations {get; set;}
	public string RawData  {get; set;}

	public WPPRowKey ToRowKey()
	{
		return new WPPRowKey(this.LocID, this.Time);
	}

	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{Births},{SRB},{LEx},{LExMale},{LExFemale},{Deaths},{DeathsMale},{DeathsFemale},{NetMigrations}";
	}
	
	public static string GetCsvFileHeader()
	{
		return "LocID,Location,VarID,Variant,Time,Births,SRB,LEx,LExMale,LExFemale,Deaths,DeathsMale,DeathsFemale,NetMigrations";
	}
	
	public static IReadOnlyCollection<string> GetRequiredFields()
	{
		return new List<string>
		{
			"LocID","Location","VarID","Variant","Time","Births","SRB","LEx","LExMale","LExFemale",
			"Deaths","DeathsMale","DeathsFemale","NetMigrations"
		};
	}

	public static IndicatorRow Parse(string line, IReadOnlyDictionary<string,int> mapping, float sexRatioUnitMultiplier, float populationUnitMultiplier)
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
		
		if (fields.Length != 67) {
			throw new InvalidDataException($"Invalid input format: {line}, must have 25 fields, not {fields.Length} fields.");
		}
		
		try
		{
			var result = new IndicatorRow{
				LocID = int.Parse(fields[mapping["LocID"]].Trim(), CultureInfo.InvariantCulture),
				Location = fields[mapping["Location"]].Trim(),
				VarID = int.Parse(fields[mapping["VarID"]], CultureInfo.InvariantCulture),
				Variant = fields[mapping["Variant"]].Trim(),
				Time = int.Parse(fields[mapping["Time"]].Trim(), CultureInfo.InvariantCulture),
				Births = ParseAndScaleFloat(fields[mapping["Births"]].Trim(), populationUnitMultiplier),
				SRB = ParseAndScaleFloat(fields[mapping["SRB"]].Trim(), sexRatioUnitMultiplier, 4),
				LEx = float.Parse(fields[mapping["LEx"]].Trim(), CultureInfo.InvariantCulture),
				LExMale = float.Parse(fields[mapping["LExMale"]].Trim(), CultureInfo.InvariantCulture),
				LExFemale = float.Parse(fields[mapping["LExFemale"]].Trim(), CultureInfo.InvariantCulture),
				Deaths = ParseAndScaleFloat(fields[mapping["Deaths"]].Trim(), populationUnitMultiplier),
				DeathsMale = ParseAndScaleFloat(fields[mapping["DeathsMale"]].Trim(), populationUnitMultiplier),
				DeathsFemale = ParseAndScaleFloat(fields[mapping["DeathsFemale"]].Trim(), populationUnitMultiplier),
				NetMigrations = ParseAndScaleFloat(fields[mapping["NetMigrations"]].Trim(), populationUnitMultiplier)
			};
			
			fields[mapping["Births"]] = result.Births.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["SRB"]] = result.SRB.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["Deaths"]] = result.Deaths.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["DeathsMale"]] = result.DeathsMale.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["DeathsFemale"]] = result.DeathsFemale.ToString("R", CultureInfo.InvariantCulture);
			fields[mapping["NetMigrations"]] = result.NetMigrations.ToString("R", CultureInfo.InvariantCulture);
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

public class IndicatorDataset : WPPDataset<IndicatorRow>
{
	public IndicatorDataset(string csvFileHeader, string rawFileHeader, Dictionary<int, SortedDictionary<WPPRowKey, IndicatorRow>> dataset)
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

	public Dictionary<int, SortedDictionary<WPPRowKey, IndicatorRow>> Data { get; }
}

private static IndicatorDataset SplitIndicatorsFileByCountry(IReadOnlyDictionary<int, Country> locations,
FileInfo sourceFile, float sexRatioUnitMultiplier, float populationUnitMultiplier)
{
	if (!sourceFile.Exists)
	{
		throw new FileNotFoundException("Source file {0} not found.", sourceFile.FullName);
	}
	
	var result = new Dictionary<int, SortedDictionary<WPPRowKey, IndicatorRow>>();
	var rawFileHeader = string.Empty;
	Console.WriteLine("\nProcessing indicators file {0} ... \n", sourceFile.FullName);
	var timer = new Stopwatch();
	timer.Start();
	using(var reader = new StreamReader(sourceFile.FullName, Encoding.UTF8))
	{
		if ((rawFileHeader = reader.ReadLine()) == null)
		{
			throw new InvalidDataException($"File {sourceFile.FullName} is empty.");		
		}
		
		var mapping = CreateCsvFieldsMapping(IndicatorRow.GetRequiredFields(), rawFileHeader);
		try
		{
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
				else if (line.IndexOf(",,,,,,,,,") >= 0)
				{
					continue; // Ignore line.
				}
				
				var row = IndicatorRow.Parse(line, mapping, sexRatioUnitMultiplier, populationUnitMultiplier);
				if (!result.TryGetValue(row.LocID, out SortedDictionary<WPPRowKey, IndicatorRow> country))
				{
					country = new SortedDictionary<WPPRowKey, IndicatorRow>();
					result.Add(row.LocID, country);
					Console.WriteLine("{0,-5} Creating country: {1} ...", result.Count, locations[code_value].Name);
				}
				
				country.Add(row.ToRowKey(), row);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error parsing file: {0}, cause: {1}", sourceFile.FullName, ex.Message);
			Console.WriteLine(ex);
		}
	}
		
	timer.Stop();
	Console.WriteLine("Created: {0} locations, elapsed time: {1} seconds.", result.Count, timer.Elapsed.TotalSeconds);
	
	return new IndicatorDataset(IndicatorRow.GetCsvFileHeader(), rawFileHeader, result);
}
