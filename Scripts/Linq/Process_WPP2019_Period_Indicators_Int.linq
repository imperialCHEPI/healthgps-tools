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
	var indicatorsOutputFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "IndicatorsInt"));
	
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
		var upper_period_edge = 2100;
		var unit_multiplier = 1000.0f;
		var indicators_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2019_Period_Indicators_Medium.csv"));
		var country_data = SplitIndicatorsFileByCountry(countries, indicators_file, lower_period_edge, unit_multiplier);

		// 5. Interpolate between age-groups
		var country_full = InterpolateAgeGroupsLocal(country_data, lower_period_edge, upper_period_edge);

		// 4. Create country files, use test version to check algorithms. 
		// CreateTestCountryFiles(countries, country_data, indicatorsOutputFolder);
		WriteCountriesFileToCsv(countries, country_full, indicatorsOutputFolder);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}
}

IReadOnlyDictionary<int, SortedDictionary<int, IndicatorsRow>> InterpolateAgeGroupsLocal(
IReadOnlyDictionary<int, SortedDictionary<int, IndicatorsRow>> countries, int lower_period_edge, int upper_period_edge)
{
	var result = new Dictionary<int, SortedDictionary<int, IndicatorsRow>>();
	Console.WriteLine("\nInterpolating values between age-groups ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var country in countries)
	{
		if (!result.ContainsKey(country.Key))
		{
			result.Add(country.Key, new SortedDictionary<int, IndicatorsRow>()); // Country
		}
		
		if (country.Key == 250)
		{
			Console.WriteLine("France:");
		}

		var indexSentinel = country.Value.Count - 1;
		IndicatorsRow nextAgeGroup;
		for (int index = 0; index < country.Value.Count; index++)
		{
			var currentAgeGroup = country.Value.ElementAt(index).Value;
			if (index < indexSentinel)
			{
				nextAgeGroup = country.Value.ElementAt(index + 1).Value;
			}
			else
			{
				nextAgeGroup = currentAgeGroup;
			}
			
			var timeYearStart = currentAgeGroup.Period.Start;
			var timeYearEnd = currentAgeGroup.Period.End;
			var timeDiff = currentAgeGroup.Period.Span;
			
			var timeDeltaMultiplier = 1.0f / (timeYearEnd - timeYearStart + 1);
			for (var timeYear = timeYearStart; timeYear <= timeYearEnd; timeYear++)
			{
				var timeDelta = (timeYear-timeYearStart) * timeDeltaMultiplier;
				var births = currentAgeGroup.Births + (nextAgeGroup.Births - currentAgeGroup.Births) * timeDelta;
				var lexTotal = currentAgeGroup.LExTotal + (nextAgeGroup.LExTotal - currentAgeGroup.LExTotal) * timeDelta;
				var lexMale = currentAgeGroup.LExMale + (nextAgeGroup.LExMale - currentAgeGroup.LExMale) * timeDelta;
				var lexFemale = currentAgeGroup.LExFemale + (nextAgeGroup.LExFemale - currentAgeGroup.LExFemale) * timeDelta;
				var deaths = currentAgeGroup.Deaths + (nextAgeGroup.Deaths - currentAgeGroup.Deaths) * timeDelta;
				var deathsMale = currentAgeGroup.DeathsMale + (nextAgeGroup.DeathsMale - currentAgeGroup.DeathsMale) * timeDelta;
				var deathsFemale = currentAgeGroup.DeathsFemale + (nextAgeGroup.DeathsFemale - currentAgeGroup.DeathsFemale) * timeDelta;
				var migrations = currentAgeGroup.NetMigrations + (nextAgeGroup.NetMigrations - currentAgeGroup.NetMigrations) * timeDelta;
				var srb = currentAgeGroup.SRB + (nextAgeGroup.SRB - currentAgeGroup.SRB) * timeDelta;
				
				var countryYears = result[country.Key];
				countryYears.Add(timeYear, new IndicatorsRow
				{
					LocID = currentAgeGroup.LocID,
					Location = currentAgeGroup.Location,
					VarID = currentAgeGroup.VarID,
					Variant = currentAgeGroup.Variant,
					Time = currentAgeGroup.Time,
					MidPeriod = currentAgeGroup.MidPeriod,
					Period = currentAgeGroup.Period,
					TimeYear = timeYear,
					Births = births / currentAgeGroup.Period.RawSpan,
					LExTotal = lexTotal,
					LExMale = lexMale,
					LExFemale = lexFemale,
					Deaths = deaths / currentAgeGroup.Period.RawSpan,
					DeathsMale = deathsMale / currentAgeGroup.Period.RawSpan,
					DeathsFemale = deathsFemale / currentAgeGroup.Period.RawSpan,
					NetMigrations = migrations / currentAgeGroup.Period.RawSpan,
					SRB = srb,
				});
			}
		}
	}

	return result;
}


public static float[] SmoothDataInplaceLocal(int times, List<float> data)
{
	var working = new float[data.Count];
	for (int index = 0; index < data.Count; index++)
	{
		working[index] = data[index];
	}
	
	if (data.Count > 2)
	{
		const float divisor = 3.0f;
		for (var j = 0; j < times; j++)
		{
			var tmp = Array.ConvertAll(working, s => s);
			for (var i = 0; i < working.Length; i++)
			{
				if (i == 0)
				{
					working[i] = (2.0f * tmp[i] + tmp[i + 1]) / divisor;
				}
				else if (i == working.Length - 1)
				{
					working[i] = (tmp[i - 1] + tmp[i] * 2.0f) / divisor;
				}
				else
				{
					working[i] = (tmp[i - 1] + tmp[i] + tmp[i + 1]) / divisor;
				}
			}
		}
	}
	
	return working;
}

IReadOnlyDictionary<int, SortedDictionary<int, Indicator>> InterpolateAgeGroups(
IReadOnlyDictionary<int, SortedDictionary<int, Indicator>> countries, int lower_period_edge, int upper_period_edge)
{
	var result = new Dictionary<int, SortedDictionary<int, Indicator>>();
	Console.WriteLine("\nInterpolating values between age-groups ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var country in countries)
	{
		if (!result.ContainsKey(country.Key))
		{
			result.Add(country.Key, new SortedDictionary<int, Indicator>()); // Country
		}

		var indexSentinel = country.Value.Count - 1;
		Indicator nextAgeGrp;
		for (int index = 0; index < country.Value.Count; index++)
		{
			var currentAgeGrp = country.Value.ElementAt(index).Value;
			if (index < indexSentinel)
			{
				nextAgeGrp = country.Value.ElementAt(index + 1).Value;
			}
			else
			{
				nextAgeGrp = currentAgeGrp;
			}

			var period = Period.Parse(currentAgeGrp.Time, lower_period_edge);
			var timeYearEnd = period.End;
			var timeDiff = period.Span;
			if (timeYearEnd == upper_period_edge)
			{
				timeYearEnd++;
			}

			var ageMetrics = InterpolateAgeGroupMetric(currentAgeGrp, nextAgeGrp, period.Start, timeYearEnd);
			foreach (var yearPair in ageMetrics)
			{
				var metric = yearPair.Value;
				var countryYears = result[country.Key];
				countryYears.Add(yearPair.Key, new Indicator
				{
					LocID = currentAgeGrp.LocID,
					Location = currentAgeGrp.Location,
					VarID = currentAgeGrp.VarID,
					Variant = currentAgeGrp.Variant,
					Time = currentAgeGrp.Time,
					MidPeriod = currentAgeGrp.MidPeriod,
					TimeYear = yearPair.Key,
					Births = currentAgeGrp.Births,
					LExTotal = currentAgeGrp.LExTotal,
					LExMale = currentAgeGrp.LExMale,
					LExFemale = currentAgeGrp.LExFemale,
					Deaths = currentAgeGrp.Deaths,
					DeathsMale = currentAgeGrp.DeathsMale,
					DeathsFemale = currentAgeGrp.DeathsFemale,
					NetMigrations = currentAgeGrp.NetMigrations,
					SRB = currentAgeGrp.SRB,

					BirthsYear = metric.Births,
					LExTotalYear = metric.LExTotal,
					LExMaleYear = metric.LExMale,
					LExFemaleYear = metric.LExFemale,
					DeathsYear = metric.Deaths,
					DeathsMaleYear = metric.DeathsMale,
					DeathsFemaleYear = metric.DeathsFemale,
					NetMigrationsYear = metric.NetMigrations,
					SRBYear = metric.SRB,
				});
			}
		}
	}
	
	return result;
}

private static Dictionary<int, Metric> InterpolateAgeGroupMetric(Indicator current, Indicator next, int startYear, int endYear)
{
	var result = new Dictionary<int, Metric>();
	var timeDeltaMultiplier = 1.0f / (endYear - startYear);
	for (var timeYear = startYear; timeYear <= endYear; timeYear++)
	{
		var timeDelta = timeYear - startYear;
		var timeMetric = new Metric
		{
			Births = current.Births + (next.Births - current.Births) * timeDelta * timeDeltaMultiplier,
			LExTotal = current.LExTotal + (next.LExTotal - current.LExTotal) * timeDelta * timeDeltaMultiplier,
			LExMale = current.LExMale + (next.LExMale - current.LExMale) * timeDelta * timeDeltaMultiplier,
			LExFemale = current.LExFemale + (next.LExFemale - current.LExFemale) * timeDelta * timeDeltaMultiplier,
			Deaths = current.Deaths + (next.Deaths - current.Deaths) * timeDelta * timeDeltaMultiplier,
			DeathsMale = current.DeathsMale + (next.DeathsMale - current.DeathsMale) * timeDelta * timeDeltaMultiplier,
			DeathsFemale = current.DeathsFemale + (next.DeathsFemale - current.DeathsFemale) * timeDelta * timeDeltaMultiplier,
			NetMigrations = current.NetMigrations + (next.NetMigrations - current.NetMigrations) * timeDelta * timeDeltaMultiplier,
			SRB = current.SRB + (next.SRB - current.SRB) * timeDelta * timeDeltaMultiplier,
		};
		
		result.Add(timeYear, timeMetric);
	}
	
	return result;
}

public struct Period
{
	public int Start {get; set;}
	
	public int End {get; set;}
	
	public int Span => End-Start;
	public int SpanInc => Span + 1;
	public bool IsEdge {get; set;}
	public int RawStart => this.IsEdge ? Start : Start - 1;
	public int RawSpan => End-RawStart;
	public int RawSpanInc => RawSpan + 1;
	
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

public struct Metric
{
	public float Births { get; set; }
	public float LExTotal { get; set; }
	public float LExMale { get; set; }
	public float LExFemale { get; set; }
	public float Deaths { get; set; }
	public float DeathsMale { get; set; }
	public float DeathsFemale { get; set; }
	public float NetMigrations { get; set; }
	public float SRB { get; set; }
}

public struct Indicator
{
	public int LocID { get; set; }
	public string Location { get; set; }
	public int VarID { get; set; }
	public string Variant { get; set; }
	public string Time { get; set; }
	public int MidPeriod { get; set; }
	public int TimeYear { get; set; }
	
	public float Births { get; set; }
	public float LExTotal { get; set; }
	public float LExMale { get; set; }
	public float LExFemale { get; set; }
	public float Deaths { get; set; }
	public float DeathsMale { get; set; }
	public float DeathsFemale { get; set; }
	public float NetMigrations { get; set; }
	public float SRB { get; set; }

	public float BirthsYear { get; set; }
	public float LExTotalYear { get; set; }
	public float LExMaleYear { get; set; }
	public float LExFemaleYear { get; set; }
	public float DeathsYear { get; set;}
	public float DeathsMaleYear { get; set;}
	public float DeathsFemaleYear { get; set;}
	public float NetMigrationsYear { get; set;}
	public float SRBYear { get; set;}

	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{TimeYear},{Births},{LExTotal},{LExMale},{LExFemale}," +
			   $"{Deaths},{DeathsMale},{DeathsFemale},{NetMigrations},{SRB},{BirthsYear},{LExTotalYear},{LExMaleYear},{LExFemaleYear}," +
			   $"{DeathsYear},{DeathsMaleYear},{DeathsFemaleYear},{NetMigrationsYear},{SRBYear}";
	}

	public string ToCsv()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{TimeYear},{BirthsYear},{LExTotalYear},{LExMaleYear},{LExFemaleYear}," +
			   $"{DeathsYear},{DeathsMaleYear},{DeathsFemaleYear},{NetMigrationsYear},{SRBYear}";
	}

	public static Indicator Parse(string line, IReadOnlyDictionary<string, int> mapping, int lowerEdge, float unit_multiplier)
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

		if (parts.Length != 25)
		{
			throw new InvalidDataException($"Invalid input format: {line}, must have 25 fields, not {parts.Length} fields.");
		}

		try
		{
			// LocID,Location,VarID,Variant,Time,MidPeriod,TFR,NRR,CBR,Births,LEx,LExMale,LExFemale,IMR,Q5,CDR,Deaths,DeathsMale,DeathsFemale,CNMR,NetMigrations,GrowthRate,NatIncr,SRB,MAC	
			return new Indicator
			{
				LocID = int.Parse(parts[mapping["LocID"]].Trim()),
				Location = parts[mapping["Location"]].Trim(),
				VarID = int.Parse(parts[mapping["VarID"]]),
				Variant = parts[mapping["Variant"]].Trim(),
				Time = parts[mapping["Time"]].Trim(),
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
		return GetCsvFileHeader() + ",BirthsYear,LExYear,LExMaleYear,LExFemaleYear,DeathsYear,DeathsMaleYear,DeathsFemaleYear,NetMigrationsYear,SRBYear";
	}

	public static string GetCsvFileHeader()
	{
		return @"LocID,Location,VarID,Variant,Time,MidPeriod,TimeYear,Births,LEx,LExMale,LExFemale,Deaths,DeathsMale,DeathsFemale,NetMigrations,SRB";
	}

	public static IReadOnlyDictionary<string, int> CreateMapping(string line)
	{
		var mapping = new Dictionary<string, int>();
		string[] parts = null;
		if (line.Contains("\""))
		{
			parts = SplitCsv(line);
		}
		else
		{
			parts = line.Split(",");
		}

		if (parts.Length != 25)
		{
			throw new InvalidDataException($"Invalid input format: {line}, must have 25 fields, not {parts.Length} fields.");
		}

		var fields = new List<string>
		{
			"LocID","Location","VarID","Variant","Time","MidPeriod","Births",
			"LEx","LExMale","LExFemale", "Deaths","DeathsMale","DeathsFemale","NetMigrations","SRB"
		};

		foreach (var field in fields)
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

public struct IndicatorsRow
{
	public int LocID { get; set; }
	public string Location { get; set; }
	public int VarID { get; set; }
	public string Variant {get; set;}
	public string Time {get; set;}
	public Period Period {get; set;}
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
	
	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{TimeYear},{Births},{LExTotal},{LExMale},{LExFemale},{Deaths}," +
			   $"{DeathsMale},{DeathsFemale},{NetMigrations},{SRB}";
	}
	
	public string ToCsv()
	{
		return ToString();
		
		//return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{TimeYear},{BirthsYear},{LExTotal},{LExMale},{LExFemale}," +
		//	   $"{DeathsYear},{DeathsMaleYear},{DeathsFemaleYear},{NetMigrationsYear},{SRB}";
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

private static IReadOnlyDictionary<int, SortedDictionary<int, Indicator>> SplitIndicatorsFileByCountryV2(
IReadOnlyDictionary<int, Country> locations, FileInfo sourceFile, int lower_period_edge, float unit_multiplier)
{
	var summary = new Dictionary<int, SortedDictionary<int, Indicator>>();
	if (!sourceFile.Exists)
	{
		Console.WriteLine("Source file {0} not found.", sourceFile.FullName);
		return summary;
	}

	var ignoredLocations = new Dictionary<int, string>();
	Console.WriteLine("\nProcessing indicators file {0} ... \n", sourceFile.FullName);
	var timer = new Stopwatch();
	timer.Start();
	using (var reader = new StreamReader(sourceFile.FullName, Encoding.UTF8))
	{
		var line = string.Empty;
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", sourceFile.FullName);
			return summary;
		}

		var mapping = Indicator.CreateMapping(line);
		try
		{
			// Load dataset
			while ((line = reader.ReadLine()) != null)
			{
				var code_idx = line.IndexOf(",");
				var code_value = int.Parse(line.Substring(0, code_idx));
				if (!locations.ContainsKey(code_value))
				{
					var loc_index = line.IndexOf(",", code_idx + 1);
					var location = line.Substring(code_idx + 1, loc_index - code_idx - 1);
					ignoredLocations[code_value] = location;
					continue;
				}
				
				if (line.IndexOf(",,,,,,,,,") >= 0)
				{
					if (!ignoredLocations.ContainsKey(code_value))
					{
						var loc_index = line.IndexOf(",", code_idx + 1);
						var location = line.Substring(code_idx + 1, loc_index - code_idx - 1);
						ignoredLocations.Add(code_value, $"{location} - [{locations[code_value].Name}]");
					}

					continue; // Ignore line.
				}

				var row = Indicator.Parse(line, mapping, lower_period_edge, unit_multiplier);
				if (!summary.TryGetValue(row.LocID, out SortedDictionary<int, Indicator> country))
				{
					country = new SortedDictionary<int, Indicator>();
					summary.Add(row.LocID, country);
				}
				
				var period = Period.Parse(row.Time, lower_period_edge);
				country.Add(period.Start, row);
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
				
				country.Add(row.Period.Start, row);
				/*
				for (var year = row.Period.Start; year <= row.Period.End; year++)
				{
					row.TimeYear = year;
					country.Add(year, row);
				}
				*/
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

private static void WriteCountriesFileToCsv(IReadOnlyDictionary<int,Country> locations,
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


