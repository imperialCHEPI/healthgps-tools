<Query Kind="Program">
  <Output>DataGrids</Output>
</Query>

#load "Process_common.linq"

void Main()
{
	var data_root_folder = @"C:\Work\Data";
	var countries_file = Path.Combine(data_root_folder, "ISO3166-1-Countries.csv");
	Console.WriteLine("*** Loading ISO 3166-1 Countries. ***\n");

	var countries = LoadCountries(countries_file);
	Console.WriteLine("There are {0} countries in file:", countries.Count);

	Console.WriteLine("\n*** Processing UN Population Database data. ***\n");
	var UN_DB_Folder = Path.Combine(data_root_folder, "UNDB");
	var mortalityOutputFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "Mortality"));

	string[] files = Directory.GetFiles(UN_DB_Folder, "*.csv");
	Console.WriteLine("The number of *.csv files in {0} is {1}.", UN_DB_Folder, files.Length);
	foreach (string dir in files)
	{
		Console.WriteLine(dir);
	}

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
		var max_age = 100;
		var max_period = 2100;
		var unit_multiplier = 1000.0f;
		var mortality_file = new FileInfo(Path.Combine(UN_DB_Folder, "WPP2019_Mortality_by_Age.csv"));
		var country_data = SplitMortalityFileByCountry(countries, mortality_file, max_period, max_age, unit_multiplier);
		
		// 4. Create year-age mortality 
		var country_age = CreateCountryYearAgeMortality(country_data);

		// 4. Apply smoothing to mortality rates - avoid sudden mortality jumps between adjacent Age-Groups
		var smoothingParameters = 50;
		CreateYearSmoothMortality(country_age, smoothingParameters);

		// 5. Interpolate bettwen age-groups
		var country_files = InterpolateAgeGroups(country_age, max_period);

		// 6. Create country files, use test version to check algorithms. 
		//CreateTestCountryFiles(countries, country_files, mortalityOutputFolder);
		CreateCountryFiles(countries, country_files, mortalityOutputFolder);

		// 7. Create development datasets for France, UK and Portugal
		var filter = new DatasetFilter
		{
			Locations = new List<int> { 250, 620, 826 },
			TimeRange = new Interval(2010, 2030),
		};

		CreateDevelopmentDatasets(countries, country_files, mortalityOutputFolder, filter);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}
}

public struct DatasetFilter
{
	public List<int> Locations;

	public Interval TimeRange;
}

// You can define other methods, fields, classes and namespaces here
public class Metric
{
	public float Male { get; set; }
	public float Female { get; set; }
	public float Total { get; set; }

	public Metric Clone()
	{
		return new Metric { Male = this.Male, Female = this.Female, Total = this.Total };
	}

	public override string ToString()
	{
		return $"{Male},{Female},{Total}";
	}
}

public class RowKey : IEquatable<RowKey>, IComparable<RowKey>
{
	public int LocID { get; set; }      // Key
	public string Location { get; set; }
	public string Variant { get; set; }
	public Interval Time { get; set; }  // Key
	public int GenderId { get; set; }
	public string Gender { get; set; }

	public int CompareTo(RowKey other)
	{
		if (other == null)
			return 1;

		var result = LocID.CompareTo(other.LocID);
		if (result == 0)
		{
			return Time.CompareTo(other.Time);
		}

		return result;
	}

	public bool Equals(RowKey other)
	{
		return this.LocID.Equals(other.LocID) && this.Time.Equals(other.Time);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
			return false;

		RowKey other = obj as RowKey;
		if (other == null)
			return false;
		else
			return Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(this.LocID.GetHashCode(), this.Time.GetHashCode());
	}

	public static bool operator ==(RowKey left, RowKey right)
	{
		if (((object)left) == null || ((object)right) == null)
			return object.Equals(left, right);

		return left.Equals(right);
	}

	public static bool operator !=(RowKey left, RowKey right)
	{
		return !(left == right);
	}

	public static RowKey Parse(string[] fields, IReadOnlyDictionary<string, int> mapping, int max_period)
	{
		if (fields.Length < 30)
		{
			throw new InvalidDataException($"Invalid fields count: expected 30, but given {fields.Length} fields.");
		}

		try
		{
			return new RowKey
			{
				LocID = int.Parse(fields[mapping["LocID"]].Trim()),
				Location = fields[mapping["Location"]].Trim(),
				Variant = fields[mapping["Variant"]].Trim(),
				Time = Interval.Parse(fields[mapping["Period"]].Trim(), max_period),
				GenderId = int.Parse(fields[mapping["SexID"]].Trim()),
				Gender = fields[mapping["Sex"]].Trim(),
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid row data format: {string.Join(",", fields)}, {ex.Message}");
			throw;
		}
	}

	public static Dictionary<string, int> CreateMapping(string[] fields)
	{
		if (fields.Length < 30)
		{
			throw new InvalidDataException($"Invalid fields count: expected 30, but given {fields.Length} fields.");
		}

		var mapping = new Dictionary<string, int>();
		var requiredFields = new List<string> { "LocID", "Location", "Variant", "SexID", "Sex", "Period" };
		foreach (var field in requiredFields)
		{
			var index = Array.IndexOf(fields, field);
			if (index < 0)
			{
				throw new InvalidDataException($"Invalid file header format: {string.Join(",", fields)}.");
			}

			mapping.Add(field, index);
		}

		return mapping;
	}
}

public class FileRow
{
	public int LocID { get; set; }      // Key
	public string Location { get; set; }
	public string Variant { get; set; }
	public Interval Time { get; set; }  // Key
	public int TimeYear { get; set; }
	public Interval AgeGroup { get; set; }
	public int Age { get; set; }
	public Metric OriginalDeaths { get; set; }
	public Metric YearAgeDeaths { get; set; }
	public Metric SmoothDeaths { get; set; }
	public Metric IntSmoothDeaths { get; set; }

	public override string ToString()
	{
		return $"{LocID},{Location},{Variant},{Time},{TimeYear},{AgeGroup},{Age},{OriginalDeaths},{YearAgeDeaths},{SmoothDeaths},{IntSmoothDeaths}";
	}

	public string ToCsv()
	{
		return $"{LocID},{Location},{Variant},{Time},{TimeYear},{AgeGroup},{Age},{IntSmoothDeaths}";
	}

	public static string GetTestFileHeader()
	{
		return GetCsvFileHeader() + ",AgeMale,AgeFemale,AgeTotal,SmoothMale,SmoothFemale,SmoothTotal,IntSmoothMale,IntSmoothFemale,IntSmoothTotal";
	}

	public static string GetCsvFileHeader()
	{
		return "LocID,Location,Variant,Time,TimeYear,AgeGrp,Age,DeathsMale,DeathsFemale,DeathsTotal";
	}
}

private static List<Interval> CreateAgeGroups(string[] fields, int ageGrpStartIndex, int max_period)
{
	var result = new List<Interval>();
	if (fields.Length < 30)
	{
		throw new InvalidDataException($"Invalid fields count: expected 30, but given {fields.Length} fields.");
	}

	for (var index = ageGrpStartIndex; index < fields.Length; index++)
	{
		result.Add(Interval.Parse(fields[index], max_period));
	}

	return result;
}

private static SortedDictionary<RowKey, SortedDictionary<Interval, Metric>> SplitMortalityFileByCountry(
IReadOnlyDictionary<int, Country> locations, FileInfo sourceFile, int max_period, int max_age, float unit_multiplier)
{
	var result = new SortedDictionary<RowKey, SortedDictionary<Interval, Metric>>();
	if (!sourceFile.Exists)
	{
		Console.WriteLine("Source file {0} not found.", sourceFile.FullName);
		return result;
	}

	Console.WriteLine("\nProcessing mortality file {0} ... \n", sourceFile.FullName);
	var timer = new Stopwatch();
	timer.Start();

	var ignoredLocations = new Dictionary<int, string>();
	using (var reader = new StreamReader(sourceFile.FullName, Encoding.UTF8))
	{
		var line = string.Empty;
		string[] rowFields = null;
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", sourceFile.FullName);
			return result;
		}

		if (line.Contains("\""))
		{
			rowFields = SplitCsv(line);
		}
		else
		{
			rowFields = line.Split(",");
		}

		var mapping = RowKey.CreateMapping(rowFields);
		var ageGrpStartIndex = mapping["Period"] + 1;
		var ageGroups = CreateAgeGroups(rowFields, ageGrpStartIndex, max_age);

		RowKey row;
		while ((line = reader.ReadLine()) != null)
		{
			if (line.Contains("\""))
			{
				rowFields = SplitCsv(line);
			}
			else
			{
				rowFields = line.Split(",");
			}

			row = RowKey.Parse(rowFields, mapping, max_period);
			if (!locations.ContainsKey(row.LocID))
			{
				ignoredLocations[row.LocID] = row.Location;
				continue;
			}

			if (!result.TryGetValue(row, out SortedDictionary<Interval, Metric> location))
			{
				location = new SortedDictionary<Interval, Metric>();
				result.Add(row, location);
			}

			for (var index = ageGrpStartIndex; index < rowFields.Length; index++)
			{
				var ageGrp = ageGroups[index - ageGrpStartIndex];
				var numberOfDeaths = float.Parse(rowFields[index]) * unit_multiplier;
				if (!location.TryGetValue(ageGrp, out Metric metric))
				{
					metric = new Metric();
					location.Add(ageGrp, metric);
				}

				if (row.GenderId == 1)
				{
					metric.Male = numberOfDeaths;
				}
				else if (row.GenderId == 2)
				{
					metric.Female = numberOfDeaths;
				}
				else if (row.GenderId == 3)
				{
					metric.Total = numberOfDeaths;
				}
				else
				{
					throw new InvalidDataException($"Invalid gender identifier = {row.GenderId} in line {line}.");
				}
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed {0} countries, elapsed time: {1} seconds.\n", result.Count, timer.Elapsed.TotalSeconds);

	Console.WriteLine($"#{ignoredLocations.Count} locations ignored due to regional or missing data.");
	ignoredLocations.Dump();
	return result;
}

private static Dictionary<int, SortedDictionary<Interval, SortedDictionary<int, FileRow>>> CreateCountryYearAgeMortality(
SortedDictionary<RowKey, SortedDictionary<Interval, Metric>> countries)
{
	Console.WriteLine("\nCreating countries year age mortality...");
	var timer = new Stopwatch();
	timer.Start();
	
	var result = new Dictionary<int, SortedDictionary<Interval, SortedDictionary<int, FileRow>>>();
	foreach (var country in countries)
	{
		var rowInfo = country.Key;
		if (!result.ContainsKey(rowInfo.LocID))
		{
			result.Add(rowInfo.LocID, new SortedDictionary<Interval, SortedDictionary<int, FileRow>>()); // Country
		}
		
		var timeDiff = rowInfo.Time.Span;
		foreach (var ageGrp in country.Value)
		{
			var ageInfo = ageGrp.Key;
			var ageGrpDiff = ageInfo.IsEdge ? ageInfo.Span : ageInfo.Span + 1;
			var totalDiff = timeDiff * ageGrpDiff;
			if (!result[rowInfo.LocID].TryGetValue(rowInfo.Time, out SortedDictionary<int, FileRow> countryYears))
			{
				countryYears = new SortedDictionary<int, FileRow>();
				result[rowInfo.LocID].Add(rowInfo.Time, countryYears);
			}

			// Fill age group
			var metric = ageGrp.Value;
			for (var age = ageInfo.Start; age <= ageInfo.End; age++)
			{
				countryYears.Add(age, new FileRow
				{
					LocID = rowInfo.LocID,
					Location = rowInfo.Location,
					Variant = rowInfo.Variant,
					Time = rowInfo.Time,
					TimeYear = default,
					AgeGroup = ageGrp.Key,
					Age = age,
					OriginalDeaths = metric,
					YearAgeDeaths = new Metric
					{
						Male = metric.Male / totalDiff,
						Female = metric.Female / totalDiff,
						Total = metric.Total / totalDiff
					},
					SmoothDeaths = new(),
					IntSmoothDeaths = new(),
				});
			}
		}
	}
	
	timer.Stop();
	Console.WriteLine("Completed {0} countries, elapsed time: {1} seconds.", result.Count, timer.Elapsed.TotalSeconds);
	return result;
}


private static void CreateYearSmoothMortality(
IReadOnlyDictionary<int, SortedDictionary<Interval, SortedDictionary<int, FileRow>>> countries, int smoothingParameters)
{
	Console.WriteLine("\nSmoothing mortality values for each time year ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var country in countries)
	{
		foreach (var year in country.Value)
		{
			var mortality = new List<Metric>(year.Value.Count);
			foreach (var age in year.Value)
			{
				mortality.Add(age.Value.YearAgeDeaths);
			}

			var smooth_mortality = SmoothMortality(mortality, smoothingParameters);
			var index = 0;
			foreach (var age in year.Value)
			{
				age.Value.SmoothDeaths = smooth_mortality[index];
				index++;
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}

IReadOnlyDictionary<int, SortedDictionary<int, SortedDictionary<int, FileRow>>> InterpolateAgeGroups(
IReadOnlyDictionary<int, SortedDictionary<Interval, SortedDictionary<int, FileRow>>> countries, int max_period)
{
	var result = new Dictionary<int, SortedDictionary<int, SortedDictionary<int, FileRow>>>();
	Console.WriteLine("\nInterpolating mortality values between age-groups ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var country in countries)
	{
		if (!result.ContainsKey(country.Key))
		{
			result.Add(country.Key, new SortedDictionary<int, SortedDictionary<int, FileRow>>()); // Country
		}

		var indexSentinel = country.Value.Count - 1;
		KeyValuePair<Interval, SortedDictionary<int, FileRow>> nextAgeGrp;
		for (int index = 0; index < country.Value.Count; index++)
		{
			var currentAgeGrp = country.Value.ElementAt(index);
			if (index < indexSentinel)
			{
				nextAgeGrp = country.Value.ElementAt(index + 1);
			}
			else
			{
				nextAgeGrp = currentAgeGrp;
			}

			var ageGrpInfo = currentAgeGrp.Key;
			var timeYearEnd = ageGrpInfo.End;
			var timeDiff = ageGrpInfo.Span;
			if (timeYearEnd == max_period)
			{
				timeYearEnd++;
			}

			foreach (var age in currentAgeGrp.Value)
			{
				var currentRow = age.Value;
				var nextRow = nextAgeGrp.Value[age.Key];

				var ageMetrics = InterpolateAgeGroupMetric(currentRow.SmoothDeaths, nextRow.SmoothDeaths, ageGrpInfo.Start, timeYearEnd);

				foreach (var yearPair in ageMetrics)
				{
					if (!result[country.Key].TryGetValue(yearPair.Key, out SortedDictionary<int, FileRow> countryYears))
					{
						countryYears = new SortedDictionary<int, FileRow>();
						result[country.Key].Add(yearPair.Key, countryYears);
					}

					countryYears.Add(age.Key, new FileRow
					{
						LocID = currentRow.LocID,
						Location = currentRow.Location,
						Variant = currentRow.Variant,
						Time = currentRow.Time,
						TimeYear = yearPair.Key,
						AgeGroup = currentRow.AgeGroup,
						Age = currentRow.Age,
						OriginalDeaths = currentRow.OriginalDeaths,
						YearAgeDeaths = currentRow.YearAgeDeaths,
						SmoothDeaths = currentRow.SmoothDeaths,
						IntSmoothDeaths = yearPair.Value,
					});
				}
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);

	return result;
}

private static Dictionary<int, Metric> InterpolateAgeGroupMetric(Metric current, Metric next, int startYear, int endYear)
{
	var result = new Dictionary<int, Metric>();
	var yearDeltaMultiplier = 1.0f / (endYear - startYear);
	for (var timeYear = startYear; timeYear < endYear; timeYear++)
	{
		result.Add(timeYear, new Metric
		{
			Male = current.Male + (next.Male - current.Male) * (timeYear - startYear) * yearDeltaMultiplier,
			Female = current.Female + (next.Female - current.Female) * (timeYear - startYear) * yearDeltaMultiplier,
			Total = current.Total + (next.Total - current.Total) * (timeYear - startYear) * yearDeltaMultiplier,
		});
	}

	return result;
}

private static Metric[] SmoothMortality(List<Metric> source, int trials)
{
	// Must deep copy metric
	var mortality = new Metric[source.Count];
	for (int i = 0; i < source.Count; i++)
	{
		mortality[i] = source[i].Clone();
	}

	for (int trial = 0; trial < trials; trial++)
	{
		// Must deep copy metric
		var tmp = new Metric[mortality.Length];
		for (int i = 0; i < mortality.Length; i++)
		{
			tmp[i] = mortality[i].Clone();
		}

		for (int i = 0; i < mortality.Length; i++)
		{
			if (i == 0)
			{
				mortality[i].Male = (2 * tmp[i].Male + tmp[i + 1].Male) / 3.0f;
				mortality[i].Female = (2 * tmp[i].Female + tmp[i + 1].Female) / 3.0f;
				mortality[i].Total = (2 * tmp[i].Total + tmp[i + 1].Total) / 3.0f;
			}
			else if (i == tmp.Length - 1)
			{
				mortality[i].Male = (tmp[i - 1].Male + tmp[i].Male * 2) / 3.0f;
				mortality[i].Female = (tmp[i - 1].Female + tmp[i].Female * 2) / 3.0f;
				mortality[i].Total = (tmp[i - 1].Total + tmp[i].Total * 2) / 3.0f;
			}
			else
			{
				mortality[i].Male = (tmp[i - 1].Male + tmp[i].Male + tmp[i + 1].Male) / 3.0f;
				mortality[i].Female = (tmp[i - 1].Female + tmp[i].Female + tmp[i + 1].Female) / 3.0f;
				mortality[i].Total = (tmp[i - 1].Total + tmp[i].Total + tmp[i + 1].Total) / 3.0f;
			}
		}
	}

	return mortality;
}

private static void CreateTestCountryFiles(IReadOnlyDictionary<int, Country> locations,
IReadOnlyDictionary<int, SortedDictionary<int, SortedDictionary<int, FileRow>>> countries, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nWrite processed data to output files ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var entry in countries)
	{
		var location_file = Path.Combine(outputFolder.FullName, $"M{locations[entry.Key].Code}.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(FileRow.GetTestFileHeader());
			foreach (var year in entry.Value)
			{
				foreach (var age in year.Value)
				{
					sw.WriteLine(age.Value.ToString());
				}
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}

private static void CreateCountryFiles(IReadOnlyDictionary<int, Country> locations,
IReadOnlyDictionary<int, SortedDictionary<int, SortedDictionary<int, FileRow>>> countries, DirectoryInfo outputFolder)
{
	Console.WriteLine("\nWrite processed data to output files ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var entry in countries)
	{
		var location_file = Path.Combine(outputFolder.FullName, $"M{locations[entry.Key].Code}.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(FileRow.GetCsvFileHeader());
			foreach (var year in entry.Value)
			{
				foreach (var age in year.Value)
				{
					sw.WriteLine(age.Value.ToCsv());
				}
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}

private static void CreateDevelopmentDatasets(IReadOnlyDictionary<int, Country> locations,
IReadOnlyDictionary<int, SortedDictionary<int, SortedDictionary<int, FileRow>>> countries,
DirectoryInfo outputFolder, DatasetFilter filter)
{
	Console.WriteLine("\nWrite processed data to output files ...");
	var timer = new Stopwatch();
	timer.Start();

	foreach (var entry in countries)
	{
		if (!filter.Locations.Contains(entry.Key))
		{
			continue;
		}

		var location_file = Path.Combine(outputFolder.FullName, $"M{locations[entry.Key].Code}Dev.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(FileRow.GetCsvFileHeader());
			foreach (var year in entry.Value)
			{
				if (!filter.TimeRange.Contains(year.Key))
				{
					continue;
				}

				foreach (var age in year.Value)
				{
					sw.WriteLine(age.Value.ToCsv());
				}
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds.\n", timer.Elapsed.TotalSeconds);
}