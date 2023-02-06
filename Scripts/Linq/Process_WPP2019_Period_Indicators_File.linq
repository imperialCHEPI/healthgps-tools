<Query Kind="Program" />

#load "Process_common.linq"

void Main()
{
	var data_root_folder = @"C:\Work\Data";
	var countries_file = Path.Combine(data_root_folder,"ISO3166-1-Countries.csv");
	Console.WriteLine("*** Loading ISO 3166-1 Countries. ***\n");
	
	var countries = LoadCountries(countries_file);
	Console.WriteLine("There are {0} countries in file:", countries.Count);
	
	Console.WriteLine("\n*** Processing UN Population Database data. ***\n");
	var UN_DB_Folder = Path.Combine(data_root_folder,"UNDB");
	var indicatorsOutputFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "Indicators"));
	
	string[] files = Directory.GetFiles(UN_DB_Folder, "*.csv");
	Console.WriteLine("The number of *.csv files in {0} is {1}.", UN_DB_Folder,  files.Length);
    foreach (string dir in files)
    {
        Console.WriteLine(dir);
    }
	
	try
	{
		// 1. Clear ouput folder before we start
		if (indicatorsOutputFolder.Exists) {
			indicatorsOutputFolder.Delete(true);
		}
		
		// 2. Create ouput folder;
		indicatorsOutputFolder.Create();
		
		// 3. Process file and split data per country - unit conversion from thousands
		var lower_period_edge = 1950;
		var unit_multiplier = 1000.0f;
		var indicators_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2019_Period_Indicators_Medium.csv"));
		var country_data = SplitIndicatorsFileByCountry(countries, indicators_file, lower_period_edge, unit_multiplier);
		
		// 4. Create country files, use test version to check algorithms. 
		// CreateTestCountryFiles(countries, country_data, indicatorsOutputFolder);
		CreateCountryFiles(countries, country_data, indicatorsOutputFolder);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}
}

public struct Period
{
	public int Start {get; set;}
	
	public int End {get; set;}
	
	public int Span => End-Start;
	
	public bool IsEdge {get; set;}
	
	public int RawStart => this.IsEdge ? Start : Start - 1;
	
	public int RawSpan => End-RawStart;
	
	public override string ToString()
	{
		return $"{Start}-{End}";
	}

	
	public string ToRawString()
	{
		return $"{RawStart}-{End}";
	}
	
	public static Period Parse(string period, int lowerEdge)
	{
		var index = period.IndexOf("-", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			var end_index = index + 1;
			var info = new Period
			{
				Start = int.Parse(period.Substring(0, index)),
				End = int.Parse(period.Substring(end_index, period.Length - end_index)),
				IsEdge = false,
			};

			if (info.Start == lowerEdge){
				info.IsEdge = true;
			} else {
				info.Start++;
			}
			
			return info;
		}
		
		throw new InvalidCastException($"Invalid time period format: {period}.");
	}
}

public struct IndicatorsRow
{
	public int LocID {get; set;}
	public string Location {get; set;}
	public int VarID {get; set;}
	public string Variant {get; set;}
	public string Time {get; set;}
	public Period Period {get; private set;}
	public int MidPeriod {get; set;}
	public int TimeYear {get; set;}
	public float Births {get; set;}
	public float LExTotal {get; set;}
	public float LExMale {get; set;}
	public float LExFemale {get; set;}
	public float Deaths {get; set;}
	public float DeathsMale {get; set;}
	public float DeathsFemale {get; set;}
	public float NetMigrations {get; set;}
	public float SRB {get; set;}
	
	public float BirthsYear => this.Births / this.Period.RawSpan;
	public float DeathsYear => this.Deaths / this.Period.RawSpan;
	public float DeathsMaleYear => this.DeathsMale / this.Period.RawSpan;
	public float DeathsFemaleYear => this.DeathsFemale / this.Period.RawSpan;
	public float NetMigrationsYear => this.NetMigrations / this.Period.RawSpan;
	
	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{TimeYear},{Births},{LExTotal},{LExMale},{LExFemale},{Deaths}," +
			   $"{DeathsMale},{DeathsFemale},{NetMigrations},{SRB},{BirthsYear},{DeathsYear},{DeathsMaleYear},{DeathsFemaleYear},{NetMigrationsYear}";
	}
	
	public string ToCsv()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{TimeYear},{BirthsYear},{LExTotal},{LExMale},{LExFemale}," +
			   $"{DeathsYear},{DeathsMaleYear},{DeathsFemaleYear},{NetMigrationsYear},{SRB}";
	}
	
	public static IndicatorsRow Parse(string line, IReadOnlyDictionary<string,int> mapping, int lowerEdge, float unit_multiplier)
	{
		string[] parts = null;
		if (line.Contains("\""))
		{
			parts = SplitCsv(line);
		}
		else
		{
			parts = line.Split(",");
		}
		
		if (parts.Length != 25) {
			throw new InvalidDataException($"Invalid input format: {line}, must have 25 fields, not {parts.Length} fields.");
		}
		
		try
		{
			// LocID,Location,VarID,Variant,Time,MidPeriod,TFR,NRR,CBR,Births,LEx,LExMale,LExFemale,IMR,Q5,CDR,Deaths,DeathsMale,DeathsFemale,CNMR,NetMigrations,GrowthRate,NatIncr,SRB,MAC	
			return new IndicatorsRow{
				LocID = int.Parse(parts[mapping["LocID"]].Trim()),
				Location = parts[mapping["Location"]].Trim(),
				VarID = int.Parse(parts[mapping["VarID"]]),
				Variant = parts[mapping["Variant"]].Trim(),
				Time = parts[mapping["Time"]].Trim(),
				Period = Period.Parse(parts[mapping["Time"]].Trim(), lowerEdge),
				MidPeriod = int.Parse(parts[mapping["MidPeriod"]].Trim()),
				Births = float.Parse(parts[mapping["Births"]].Trim()) * unit_multiplier,
				LExTotal = float.Parse(parts[mapping["LEx"]].Trim()),
				LExMale = float.Parse(parts[mapping["LExMale"]].Trim()),
				LExFemale = float.Parse(parts[mapping["LExFemale"]].Trim()),
				Deaths = float.Parse(parts[mapping["Deaths"]].Trim()) * unit_multiplier,
				DeathsMale = float.Parse(parts[mapping["DeathsMale"]].Trim()) * unit_multiplier,
				DeathsFemale = float.Parse(parts[mapping["DeathsFemale"]].Trim()) * unit_multiplier,
				NetMigrations = float.Parse(parts[mapping["NetMigrations"]].Trim()) * unit_multiplier,
				SRB = float.Parse(parts[mapping["SRB"]].Trim()),
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid line format: {line}, {ex.Message}");
			throw;
		}
	}

	public static string GetTestFileHeader()
	{
		return GetCsvFileHeader() + ",BirthsYear,DeathsYear,DeathsMaleYear,DeathsFemaleYear,NetMigrationsYear";
	}
	
	public static string GetCsvFileHeader()
	{
		return @"LocID,Location,VarID,Variant,Time,MidPeriod,TimeYear,Births,LEx,LExMale,LExFemale,Deaths,DeathsMale,DeathsFemale,NetMigrations,SRB";
	}
	
	public static IReadOnlyDictionary<string,int> CreateMapping(string line)
	{
		var mapping = new Dictionary<string,int>();
		string[] parts = null;
		if (line.Contains("\""))
		{
			parts = SplitCsv(line);
		}
		else
		{
			parts = line.Split(",");
		}
		
		if (parts.Length != 25) {
			throw new InvalidDataException($"Invalid input format: {line}, must have 25 fields, not {parts.Length} fields.");
		}
		
		var fields = new List<string> 
		{
			"LocID","Location","VarID","Variant","Time","MidPeriod","Births", 
			"LEx","LExMale","LExFemale", "Deaths","DeathsMale","DeathsFemale","NetMigrations","SRB"
		};
		
		foreach(var field in fields)
		{
			var index = Array.IndexOf(parts, field);
			if (index < 0)
			{
				throw new InvalidDataException($"Invalid file header format: {line}.");
			}
			
			mapping.Add(field, index);
		}
		
		return mapping;
	}
}

private static IReadOnlyDictionary<int, SortedDictionary<int, IndicatorsRow>> SplitIndicatorsFileByCountry(
IReadOnlyDictionary<int, Country> locations, FileInfo sourceFile, int lower_period_edge, float unit_multiplier)
{
	var summary = new Dictionary<int, SortedDictionary<int, IndicatorsRow>>();
	if (!sourceFile.Exists) {
		Console.WriteLine("Source file {0} not found.", sourceFile.FullName); 
		return summary;
	}

	var ignoredLocations = new Dictionary<int,string>();
	Console.WriteLine("\nProcessing indicators file {0} ... \n", sourceFile.FullName);
	var timer = new Stopwatch();
	timer.Start();
	using(var reader = new StreamReader(sourceFile.FullName, Encoding.UTF8))
	{
		var line = string.Empty;	
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", sourceFile.FullName);		
			return summary;
		}
		
		var mapping = IndicatorsRow.CreateMapping(line);
		try
		{
			while ((line = reader.ReadLine()) != null)
			{
				var code_idx = line.IndexOf(",");
				var code_value = int.Parse(line.Substring(0, code_idx));
				if (!locations.ContainsKey(code_value))
				{
					var loc_index = line.IndexOf(",", code_idx+1);
					var location = line.Substring(code_idx+1, loc_index - code_idx -1);
					ignoredLocations[code_value] = location;
					continue;
				}
				
				if (line.IndexOf(",,,,,,,,,") >= 0)
				{
					if (!ignoredLocations.ContainsKey(code_value))
					{
						var loc_index = line.IndexOf(",", code_idx+1);
						var location = line.Substring(code_idx+1, loc_index - code_idx - 1);
						ignoredLocations.Add(code_value, $"{location} - [{locations[code_value].Name}]");
					}
					
					continue; // Ignore line.
				}
				
				var row = IndicatorsRow.Parse(line, mapping, lower_period_edge, unit_multiplier);
				if (!summary.TryGetValue(row.LocID, out SortedDictionary<int, IndicatorsRow> country))
				{
					country = new SortedDictionary<int, IndicatorsRow>();
					summary.Add(row.LocID, country);
				}
				
				for (var year = row.Period.Start; year <= row.Period.End; year++)
				{
					row.TimeYear = year;
					country.Add(year, row);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error parsing file: {0}, cause: {1}", sourceFile.FullName, ex.Message);
			Console.WriteLine(ex);
		}
	}
		
	timer.Stop();
	Console.WriteLine("Created: {0} locations, elapsed time: {1} seconds.\n", summary.Count, timer.Elapsed.TotalSeconds);
	Console.WriteLine($"#{ignoredLocations.Count} locations ignored due to regional or missing data.");
	ignoredLocations.Dump();
	
	return summary;
}

private static void CreateTestCountryFiles(IReadOnlyDictionary<int,Country> locations,
IReadOnlyDictionary<int, SortedDictionary<int, IndicatorsRow>> countries, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nWrite processed data to output files ...");
	var timer = new Stopwatch();
	timer.Start();
	
	foreach (var entry in countries)
	{
		var location_file = Path.Combine(outputFolder.FullName, $"Pi{locations[entry.Key].Code}.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(IndicatorsRow.GetTestFileHeader());
			foreach(var year in entry.Value)
			{
				sw.WriteLine(year.Value.ToString());
			}
		}
	}
	
	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}

private static void CreateCountryFiles(IReadOnlyDictionary<int,Country> locations,
IReadOnlyDictionary<int, SortedDictionary<int, IndicatorsRow>> countries, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nWrite processed data to output files ...");
	var timer = new Stopwatch();
	timer.Start();
	
	foreach (var entry in countries)
	{
		var location_file = Path.Combine(outputFolder.FullName, $"Pi{locations[entry.Key].Code}.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(IndicatorsRow.GetCsvFileHeader());
			foreach(var year in entry.Value)
			{
				sw.WriteLine(year.Value.ToCsv());
			}
		}
	}
	
	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}


