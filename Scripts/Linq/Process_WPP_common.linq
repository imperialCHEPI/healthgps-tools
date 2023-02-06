<Query Kind="Program">
  <Output>DataGrids</Output>
</Query>

#load "Process_common.linq"

void Main()
{
	var key1 = new WPPRowKey(826, 2022, 45);
	var key2 = new WPPRowKey(826, 2022, 50);
	var key3 = new WPPRowKey(826, 2023);
	Console.WriteLine("Row Key:");
	Console.WriteLine($"{key1} = {key2} ? {key1 == key2}");
	Console.WriteLine($"{key1} = {key3} ? {key1 == key3}");
	Console.WriteLine($"{key1} > {key2} ? {key1.CompareTo(key2) > 0}");
	Console.WriteLine($"{key1} < {key2} ? {key1.CompareTo(key2) < 0}");
	Console.WriteLine($"{key1} > {key3} ? {key1.CompareTo(key3) > 0}");
	Console.WriteLine($"{key1} < {key3} ? {key1.CompareTo(key3) < 0}");
}

public interface WPPRow
{
	int LocID { get; set; }
	string Location { get; set; }
	int VarID { get; set; }
	string Variant { get; set; }
	int Time { get; set; }
	string RawData { get; }

	WPPRowKey ToRowKey();
	string ToString();
}

public interface WPPDataset<T> where T : WPPRow
{
	public string CsvFileHeader { get; }

	public string RawFileHeader { get; }

	public Dictionary<int, SortedDictionary<WPPRowKey, T>> Data { get; }
}

public class WPPRowKey : IEquatable<WPPRowKey>, IComparable<WPPRowKey>
{
	public WPPRowKey(int locationId, int timeYears, int ageYears)
	{
		this.LocID = locationId;
		this.Time = timeYears;
		this.Age = ageYears;
	}

	public WPPRowKey(int locationId, int timeYears)
		: this(locationId, timeYears, 0) { }

	public int LocID { get; }
	public int Time { get; }
	public int Age { get; }

	public int CompareTo(WPPRowKey other)
	{
		if (other == null)
			return 1;

		var result = this.LocID.CompareTo(other.LocID);
		if (result == 0)
		{
			result = this.Time.CompareTo(other.Time);
			if (result == 0)
			{
				return this.Age.CompareTo(other.Age);
			}
		}

		return result;
	}

	public override string ToString()
	{
		return $"{LocID},{Time},{Age}";
	}

	public bool Equals(WPPRowKey other)
	{
		return this.LocID.Equals(other.LocID) &&
				   this.Time.Equals(other.Time) &&
				   this.Age.Equals(other.Age);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
			return false;

		WPPRowKey other = obj as WPPRowKey;
		if (other == null)
			return false;
		else
			return Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(this.LocID.GetHashCode(), this.Time.GetHashCode(), this.Age.GetHashCode());
	}

	public static bool operator ==(WPPRowKey left, WPPRowKey right)
	{
		if (((object)left) == null || ((object)right) == null)
			return object.Equals(left, right);

		return left.Equals(right);
	}

	public static bool operator !=(WPPRowKey left, WPPRowKey right)
	{
		return !(left == right);
	}
}

void CreateCountryFiles<T>(IReadOnlyDictionary<int, Country> locations, WPPDataset<T> dataset, DirectoryInfo outputFolder, string filePrefix) where T : WPPRow
{
	Console.WriteLine("\nWrite processed data to output folder: {0} ...", outputFolder.FullName);
	var timer = new Stopwatch();
	timer.Start();

	foreach (var country in dataset.Data)
	{
		var location_file = Path.Combine(outputFolder.FullName, $"{filePrefix}{locations[country.Key].Code}.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(dataset.CsvFileHeader);
			foreach (var year in country.Value)
			{
				sw.WriteLine(year.Value.ToString());
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed writing {0} files, elapsed time: {1} seconds.", dataset.Data.Count, timer.Elapsed.TotalSeconds);
}

void CreateCountryRawFiles<T>(IReadOnlyDictionary<int, Country> locations, WPPDataset<T> dataset, DirectoryInfo outputFolder, string filePrefix) where T : WPPRow
{
	Console.WriteLine("\nWrite processed data to test output folder: {0} ...", outputFolder.FullName);
	var timer = new Stopwatch();
	timer.Start();

	foreach (var country in dataset.Data)
	{
		var location_file = Path.Combine(outputFolder.FullName, $"{filePrefix}{locations[country.Key].Code}.csv");
		using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
		{
			sw.WriteLine(dataset.RawFileHeader);
			foreach (var year in country.Value)
			{
				sw.WriteLine(year.Value.RawData);
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed writing {0} files, elapsed time: {1} seconds.", dataset.Data.Count, timer.Elapsed.TotalSeconds);
}