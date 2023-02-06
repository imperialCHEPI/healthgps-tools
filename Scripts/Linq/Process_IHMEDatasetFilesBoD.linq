<Query Kind="Program">
  <Output>DataGrids</Output>
</Query>

void Main()
{
	var maximum_age = 100;
	var unit_multiplier = 1.0 / 100000.0;
	
	var data_root_folder = @"C:\Work\Data\IHME";
	var locations_file = Path.Combine(data_root_folder,"IHME-Country-Mapping.csv");
	
	Console.WriteLine("*** Loading IHME Locations and ISO mapping. ***\n");
	var locations = LoadLocations(locations_file);
	
	// Configure the source dataset folders
	var bod_source_root = new DirectoryInfo(Path.Combine(data_root_folder, $@"BoD\Source"));
	
	// Configure the processed output folders
	var bod_output_folder = new DirectoryInfo(Path.Combine(data_root_folder, $@"BoD\\Output"));
		
	// 1. Clear ouput folder before we start
	try
    {
		if (bod_output_folder.Exists) {
			bod_output_folder.Delete(true);
		}
		
		// 2. Create ouput folder;
		bod_output_folder.Create();
		
		// 3. Process IHME disease data file
		ProcessIHMEBoDiseaseFolder(bod_source_root, bod_output_folder, locations, maximum_age, unit_multiplier);
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}
}

// Helper function to handle CVS format with quotes
public static string[] SplitCsv(string line)
{
    List<string> result = new List<string>();
    StringBuilder currentStr = new StringBuilder("");
    bool inQuotes = false;
    for (int i = 0; i < line.Length; i++) // For each character
    {
        if (line[i] == '\"') // Quotes are closing or opening
            inQuotes = !inQuotes;
			
        else if (line[i] == ',') // Comma
        {
            if (!inQuotes) // If not in quotes, end of current string, add it to result
            {
                result.Add(currentStr.ToString());
                currentStr.Clear();
            }
            else
                currentStr.Append(line[i]); // If in quotes, just add it 
        }
        else // Add any other character to current string
            currentStr.Append(line[i]); 
    }
	
    result.Add(currentStr.ToString());
    return result.ToArray(); // Return array of all strings
}

// -------------------------------------------------------
// Parse Locations mapping file
// -------------------------------------------------------
struct Location
{
	public int Id {get; set;}
	public string Name {get; set;}
	public int IsoCode {get; set;}
	public override string ToString() => $"{Id},{Name},{IsoCode}";
}

private static IReadOnlyDictionary<int, Location> LoadLocations(string fileName)
{
	var locations = new Dictionary<int, Location>();
	if (!File.Exists(fileName)) {
		throw new ArgumentException($"Locations file {fileName} not found.");
	}
	
	var timer = new Stopwatch();
	timer.Start();
	using(var reader = new StreamReader(fileName, Encoding.UTF8))
	{
		string line;	
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", fileName);		
			return locations;
		}
		
		int location_id = 0;
		string[] parts = null;
		while ((line = reader.ReadLine()) != null)
		{
			try{			
				parts = SplitCsv(line);
				if (parts.Length < 2){
					throw new InvalidDataException($"Locations file line: {line}, not a valid format.");
				}
				
				location_id = int.Parse(parts[0].Trim());
				locations.Add(location_id, new Location
				{
					Id = location_id,
					Name = parts[1].Trim(),
					IsoCode = int.Parse(parts[2].Trim()),
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error parsing line: {0}, cause: {1}", line, ex.Message);
				parts.Dump();
				throw;
			}
		}
	}
	
	return locations;
}

// -------------------------------------------------------
// Parse IHME datasets generated with GBDx and Epi Tools 
// -------------------------------------------------------

public enum KnownFileFormat { None, GBDx, Epi }

public interface IHMEDataRow
{
	int LocationId {get;}
	string Location {get;}
	
	int GenderId {get;}
	string Gender {get;}
	
	int DiseaseId {get;}
	string Disease {get;}	
	
	int AgeGroupId {get;}
	string AgeGroup {get;}
	
	int MeasureId {get;}
	string Measure {get;}		
	
	double Mean {get;}
	double Lower {get;}
	double Upper {get;}

	RowKey ToRowKey();
}

public struct GBDxDataRow: IHMEDataRow
{
	public int MeasureId {get; set;}
	public string Measure {get; set;}
	
	public int LocationId {get; set;}
	public string Location {get; set;}
	
	public int GenderId {get; set;}
	public string Gender {get; set;}
	
	public int AgeGroupId {get; set;}
	public string AgeGroup {get; set;}
	
	public int DiseaseId {get; set;}
	public string Disease {get; set;}
	
	public int MetricId {get; set;}
	public string Metric {get; set;}
	
	public int Year {get; set;}
	
	public double Mean {get; set;}
	
	public double Upper {get; set;}
	
	public double Lower {get; set;}
	
	public RowKey ToRowKey()
	{
		return new RowKey(this.LocationId, this.Year, this.AgeGroupId, this.GenderId);
	}
	
	public static IHMEDataRow Parse(string line)
	{
		return Parse(line, 1.0);
	}
	
	public static IHMEDataRow Parse(string line, double unit_multiplier)
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
		
		if (parts.Length != 16) {
			throw new InvalidDataException($"Invalid input format: {line}, must have 16 fields.");
		}
		
		var metric_name = parts[11].Trim();
		if (!string.Equals(metric_name, "rate",  StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException($"Invalid metric type: {metric_name} in data, must be rate.");
		}
		
		try
		{
			return new GBDxDataRow{
				MeasureId = int.Parse(parts[0].Trim()),
				Measure = parts[1].Trim(),
				
				LocationId = int.Parse(parts[2].Trim()),
				Location = parts[3].Trim(),
				
				GenderId = int.Parse(parts[4].Trim()),
				Gender = parts[5].Trim(),
				
				AgeGroupId = int.Parse(parts[6].Trim()),
				AgeGroup = parts[7].Trim(),
				
				DiseaseId = int.Parse(parts[8].Trim()),
				Disease = parts[9].Trim(),
				
				MetricId = int.Parse(parts[10].Trim()),
				Metric = metric_name,
				
				Year = int.Parse(parts[12].Trim()),
				
				Mean = double.Parse(parts[13].Trim()) * unit_multiplier,
				Lower = double.Parse(parts[14].Trim()) * unit_multiplier,
				Upper = double.Parse(parts[15].Trim()) * unit_multiplier,
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid line format: {line}, {ex.Message}");
			throw;
		}
	}
}

public struct EpiDataRow: IHMEDataRow
{
	public int ModelVersion {get; set;}
	
	public int DiseaseId {get; set;}
	public string Disease {get; set;}
	
	public int MeasureId {get; set;}
	public string Measure {get; set;}
	
	public int LocationId {get; set;}
	public string Location {get; set;}

	public int AgeGroupId {get; set;}
	public string AgeGroup {get; set;}
	
	public int Year {get; set;}
	
	public int GenderId {get; set;}
	public string Gender {get; set;}
	
	public double Mean {get; set;}
	
	public double Upper {get; set;}
	
	public double Lower {get; set;}
	
	public RowKey ToRowKey()
	{
		return new RowKey(this.LocationId, this.Year, this.AgeGroupId, this.GenderId);
	}
	
	public static IHMEDataRow Parse(string line)
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
		
		if (parts.Length != 15) {
			throw new InvalidDataException($"Invalid input format: {line}, must have 15 fields.");
		}
		
		try
		{
			return new EpiDataRow{
				ModelVersion = int.Parse(parts[0].Trim()),
				
				DiseaseId = int.Parse(parts[1].Trim()),
				Disease = parts[2].Trim(),
				
				MeasureId = int.Parse(parts[3].Trim()),
				Measure = parts[4].Trim(),
				
				LocationId = int.Parse(parts[5].Trim()),
				Location = parts[6].Trim(),
				
				AgeGroupId = int.Parse(parts[7].Trim()),
				AgeGroup = parts[8].Trim(),
				
				Year = int.Parse(parts[9].Trim()),
				
				GenderId = int.Parse(parts[10].Trim()),
				Gender = parts[11].Trim(),
				
				Mean = double.Parse(parts[12].Trim()),
				Lower = double.Parse(parts[13].Trim()),
				Upper = double.Parse(parts[14].Trim()),
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid line format: {line}, {ex.Message}");
			throw;
		}		
	}
}

public struct Metric
{
	public double Value {get;set;}
	
	public double Lower {get;set;}
	
	public double Upper {get;set;}
	
	public override string ToString()
	{
		return $"{this.Value}, {this.Lower}, {this.Upper}";
	} 
}

public class RowKey : IEquatable<RowKey>, IComparable<RowKey>
{
	public RowKey(int locId, int year, int age, int gender)
	{
		this.LocationId = locId;
		this.Year = year;
		this.AgeId = age;
		this.GenderId = gender;
	}
	
	public int LocationId {get;}
	
	public int Year {get;}
	
	public int AgeId {get;}
	
	public int GenderId {get; }
	
	public override string ToString()
	{
		return $"{LocationId}, {Year}, {AgeId}, {GenderId}";
	}
	
	public int CompareTo(RowKey other)
	{
		if (other == null) return 1;
		
		var result = LocationId.CompareTo(other.LocationId);
		if (result == 0)
		{
			result = Year.CompareTo(other.Year);
			if (result == 0)
			{
				result = AgeId.CompareTo(other.AgeId);
				if (result == 0)
				{
					return GenderId.CompareTo(other.GenderId);
				}
			}
		}
		
		return result;
	}
	
	public bool Equals(RowKey other)
	{
		if (other == null)
         return false;
		 
		return this.LocationId.Equals(other.LocationId) &&
			   this.Year.Equals(other.Year) &&
			   this.AgeId.Equals(other.AgeId) &&
			   this.GenderId.Equals(other.GenderId);
	}
	
	public override bool Equals(object obj)
	{
      if (obj == null)
         return false;

      RowKey rowObj = obj as RowKey;
      if (rowObj == null)
         return false;
      else
         return Equals(rowObj);
   }
   
   public override int GetHashCode()
   {
		return HashCode.Combine(
		this.LocationId.GetHashCode(),
		this.Year.GetHashCode(),
		this.AgeId.GetHashCode(),
		this.GenderId.GetHashCode());
   }
   
   public static bool operator == (RowKey left, RowKey right)
   {
      if (((object)left) == null || ((object)right) == null)
         return object.Equals(left, right);

      return left.Equals(right);
   }
   
   public static bool operator != (RowKey left, RowKey right)
   {
   		return !(left == right);
   } 
}

struct AgeGroup
{
	public int Start {get; set;}
	
	public int End {get; set;}
	
	public int Span => End-Start;
	
	public bool IsEdge {get; set;}
	
	public override string ToString()
	{
		return $"{Start} to {End}";
	}
	
	public static AgeGroup Parse(string group, int max_age)
	{
		var index = group.IndexOf("to", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			var end_index = index + 2;
			return new AgeGroup
			{
				Start = int.Parse(group.Substring(0, index)),
				End = int.Parse(group.Substring(end_index, group.Length - end_index)),
				IsEdge = false,
			};
		}
		
		if (group.Trim().StartsWith("<"))
		{
			index = group.IndexOf("year", StringComparison.OrdinalIgnoreCase);
			
			return new AgeGroup
			{
				Start = 0,
				End = int.Parse(group.Substring(1, index-1)),
				IsEdge = true,
			};
		}
		
		index = group.IndexOf("plus", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return new AgeGroup
			{
				Start = int.Parse(group.Substring(0,index)),
				End = max_age,
				IsEdge = true,
			};
		}
		
		index = group.IndexOf("Neonatal", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return new AgeGroup
			{
				Start = 0,
				End = 0,
				IsEdge = true,
			};
		}
		
		throw new InvalidCastException($"Invalid age groups format: {group}.");
	}
}

private static string SimplifyBoDMeasureName(string original)
{
	var indexOf1stSpace = original.IndexOf(' ');
	if (indexOf1stSpace > 0)
	{
		return original.Substring(0, indexOf1stSpace-1).Trim();
	}
	
	return original;
}

private static void ProcessIHMEBoDiseaseFolder(DirectoryInfo sourceFolder,
DirectoryInfo ouputFolderInfo, IReadOnlyDictionary<int, Location> locations,
int maximum_age, double unit_multiplier)
{
	var sourceFiles = new List<FileInfo>();
	if (!sourceFolder.Exists)
	{
		throw new ArgumentException($"Source data folder: {sourceFolder.FullName} not found.");
	}
		
	var folderFiles = sourceFolder.GetFiles("*.csv");
	Console.WriteLine("Folder {0} contains {1} files.", sourceFolder.FullName, folderFiles.Length);
	sourceFiles.AddRange(folderFiles);
	
	sourceFiles.TrimExcess();
	Console.WriteLine("\nThe dataset has a total of {0} files to process.\n", sourceFiles.Count);

	var timer = new Stopwatch();
	timer.Start();
	
	var disease = string.Empty;
	var genders = new Dictionary<int, string>();
	var ageGroups = new Dictionary<int, AgeGroup>();
	var measures = new Dictionary<int, string>();
	
	// dataset: Key[ Location Id, Year, Age Group Id, Gender ID], Values [Metric Id, Metric]
	var summary = new SortedDictionary<RowKey, Dictionary<int, Metric>>();
	KnownFileFormat fileFormat;
	foreach (var fileInfo in sourceFiles)
	{
		Console.WriteLine("Processing file {0} ... ", fileInfo.FullName);
		using(var reader = new StreamReader(fileInfo.FullName, Encoding.UTF8))
		{
			string line;	
			if ((line = reader.ReadLine()) == null)
			{
				Console.WriteLine("File {0} is empty.", fileInfo.FullName);		
				continue;
			}
			
			fileFormat = KnownFileFormat.None;
			if (line.Trim().StartsWith("measure_id", StringComparison.OrdinalIgnoreCase))
			{
				fileFormat = KnownFileFormat.GBDx;
			}
			else if (line.Trim().StartsWith("model_version_id", StringComparison.OrdinalIgnoreCase))
			{
				fileFormat = KnownFileFormat.Epi;
			}
			else
			{
				Console.WriteLine("Unknown file format: {0}.", fileInfo.FullName);		
				continue;
			}
			
			IHMEDataRow row;
			while ((line = reader.ReadLine()) != null)
			{
				if (fileFormat == KnownFileFormat.GBDx)
					row = GBDxDataRow.Parse(line, unit_multiplier);
				else
					row = EpiDataRow.Parse(line);
					
				if (!locations.ContainsKey(row.LocationId))
				{
					// Console.WriteLine("Unknow location identifier: {0} in line: {1}", row.LocationId, line);
					continue;
				}
				
				if (disease.Equals(string.Empty)){
					disease = row.Disease;
				} else if (row.Disease.IndexOf(disease, StringComparison.OrdinalIgnoreCase) < 0) {
						throw new InvalidDataException(
						$"The folder contains multiple diseases: {disease} and {row.Disease} in file: {fileInfo.FullName}.");				
				}
				
				if (!genders.TryGetValue(row.GenderId, out string gender)) {
					genders.Add(row.GenderId, row.Gender);
				}
				
				if (!ageGroups.TryGetValue(row.AgeGroupId, out AgeGroup group)) {
					ageGroups.Add(row.AgeGroupId, AgeGroup.Parse(row.AgeGroup, maximum_age));
				}
				
				if (!measures.TryGetValue(row.MeasureId, out string measure)) {
					measures.Add(row.MeasureId, row.Measure);
				}
							
				var rowKey = row.ToRowKey();
				if (summary.TryGetValue(rowKey, out Dictionary<int, Metric> metrics))
				{
					if (metrics.ContainsKey(row.MeasureId)) {
						throw new InvalidDataException(
						$"The folder contains duplicated measure entries for: {measures[row.MeasureId]} in file: {fileInfo.FullName}.");
					}
					
					metrics.Add(row.MeasureId, new Metric { Value = row.Mean, Lower = row.Lower, Upper = row.Upper});
				}
				else
				{
					summary.Add(rowKey, new  Dictionary<int, Metric>{
							{row.MeasureId, new Metric { Value = row.Mean, Lower = row.Lower, Upper = row.Upper}}
						});
				}
			}
		}
	}
	
	// Write to countries file
	Console.WriteLine("\nWrite processed data to output files ...");
	var fileHeader = "location_id,location,disease,time,age_group_id,age_group,age,is_filled,gender_id,gender,measure_id,measure,mean,lower,upper";
	var lastLocationId = 0;
	StreamWriter sw = null;
	try
	{
		var is_filled = false;
		var numberOfMetrics = measures.Count;
		foreach(var entry in summary)
		{
			if (entry.Value.Count != numberOfMetrics)
			{
				Console.WriteLine("# of metrics mismatch in row: {0,-20} ({1} vs. {2})  -  {3} aged {4} in {5}",
								  entry.Key, entry.Value.Count, numberOfMetrics,
								  genders[entry.Key.GenderId], ageGroups[entry.Key.AgeId],
								  locations[entry.Key.LocationId].Name);
			}
			
			if (entry.Key.LocationId != lastLocationId)
			{
				// Close current stream
				if (sw != null)
				{
					sw.Flush();
					sw.Close();
					sw.Dispose();
				}
				
				// Create new file stream
				var location_file = Path.Combine(
				ouputFolderInfo.FullName,
				$"BoD{locations[entry.Key.LocationId].IsoCode}.csv");
				sw = new StreamWriter(location_file, true, Encoding.UTF8);
				sw.WriteLine(fileHeader);
				
				lastLocationId = entry.Key.LocationId;
			}
			
			foreach(var metric in entry.Value)
			{
				var loc = locations[entry.Key.LocationId];
				var age_group = ageGroups[entry.Key.AgeId];
				is_filled = false;
				for (var age = age_group.Start; age <= age_group.End; age++)
				{
					sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}", 
					loc.IsoCode, loc.Name, disease, entry.Key.Year, entry.Key.AgeId, age_group,
					age, is_filled, entry.Key.GenderId, genders[entry.Key.GenderId], metric.Key, 
					SimplifyBoDMeasureName(measures[metric.Key]), metric.Value.Value, metric.Value.Lower, metric.Value.Upper);
					is_filled = true;
				}
			}
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine("Error Writing to file: {0}", ex.Message);
		throw;
	}
	finally
	{
		if (sw != null)
		{
			sw.Flush();
			sw.Close();
			sw.Dispose();
		}
	}

	timer.Stop();
	Console.WriteLine("\nCompleted, elapsed time: {0} seconds:", timer.Elapsed.TotalSeconds);
	
	genders.Dump();
	measures.Dump();	
	ageGroups.Dump();
	summary.Dump();
}