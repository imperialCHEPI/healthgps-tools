<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Globalization</Namespace>
</Query>

void Main()
{
	Console.WriteLine("Hello, the process common algorithm sucessfully compile.");
	var time_range = new Interval(1950, 1955);
	var time_edge = new Interval(2095, 2100);
	time_range.Dump();
	time_edge.Dump();

	var age_range = new Interval(5, 9);
	var age_edge = new Interval(95, 100);
	age_range.Dump();
	age_edge.Dump();
}

private static void ListCsvFilesInFolder(string folderFullname)
{
	var files = Directory.GetFiles(folderFullname, "*.csv");
	Console.WriteLine("The number of *.csv files in {0} is {1}.", folderFullname, files.Length);
	foreach (string dir in files)
	{
		Console.WriteLine(dir);
	}
}

public static float ParseAndScaleFloat(string value, float multiplier, int digits = 0)
{
	return Convert.ToSingle(Math.Round(float.Parse(value, CultureInfo.InvariantCulture) * multiplier, digits, MidpointRounding.AwayFromZero));
}

public static IReadOnlyDictionary<string, int> CreateCsvFieldsMapping(IReadOnlyCollection<string> fields, string csv_file_header)
{
	var mapping = new Dictionary<string, int>();
	string[] parts = null;
	if (csv_file_header.Contains("\""))
	{
		parts = SplitCsv(csv_file_header);
	}
	else
	{
		parts = csv_file_header.Split(",");
	}

	if (parts.Length < fields.Count)
	{
		throw new InvalidDataException($"Invalid file header format: {csv_file_header}, expected at least {fields.Count} fields [{parts.Length}].");
	}
	
	foreach (var field in fields)
	{
		var index = Array.FindIndex(parts, t => t.Equals(field, StringComparison.InvariantCultureIgnoreCase));
		if (index < 0)
		{
			throw new InvalidDataException($"Invalid file header format: {csv_file_header}, field {field} not found.");
		}

		mapping.Add(field, index);
	}

	return mapping;
}

public static void SmoothDataInplace(int times, Dictionary<int, double> data)
{
	var start_offset = data.First().Key;
	var count = data.Count + start_offset;
	var count_minus_one = count - 1;
	var keys = new List<int>(count);
	var working = new double[count];
	foreach (var item in data)
	{
		keys.Add(item.Key);
		working[item.Key] = item.Value;
	}

	if (data.Count > 2)
	{
		const double divisor = 3.0;
		for (var j = 0; j < times; j++)
		{
			var tmp = Array.ConvertAll(working, s => s);
			for (var i = 0; i < count; i++)
			{
				if (i == 0)
				{
					working[i] = (2.0 * tmp[i] + tmp[i + 1]) / divisor;
				}
				else if (i == count_minus_one)
				{
					working[i] = (tmp[i - 1] + tmp[i] * 2.0) / divisor;
				}
				else
				{
					working[i] = (tmp[i - 1] + tmp[i] + tmp[i + 1]) / divisor;
				}
			}
		}
	}

	var index = start_offset;
	foreach (var key in keys)
	{
		data[key] = working[index];
		index++;
	}
}

public struct Country
{
	public int Code { get; set; }
	public string Name { get; set; }
	public string Alpha2 { get; set; }
	public string Alpha3 { get; set; }

	public override string ToString()
	{
		return $"{Code,-5} {Alpha2,3} {Alpha3,4} {Name}";
	}
}

public class Interval : IEquatable<Interval>, IComparable<Interval>
{
	public Interval(int start, int end)
	: this(start, end, false)
	{ }

	public Interval(int start, int end, bool isEdge)
	{
		this.Start = start;
		this.End = end;
		this.IsEdge = isEdge;
	}

	public int Start { get; set; }
	public int End { get; set; }
	public bool IsEdge { get; set; }
	public int Span => End - Start;

	public override string ToString()
	{
		return $"{Start}-{End}";
	}
	
	public bool Contains(int value)
	{
		return this.Start <= value && value <= this.End;
	}

	public int CompareTo(Interval other)
	{
		if (other == null)
			return 1;

		var result = Start.CompareTo(other.Start);
		if (result == 0)
		{
			return End.CompareTo(other.End);
		}

		return result;
	}

	public bool Equals(Interval other)
	{
		if (other == null)
			return false;

		return this.Start.Equals(other.Start) && this.End.Equals(other.End);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
			return false;

		Interval other = obj as Interval;
		if (other == null)
			return false;
		else
			return Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(this.Start.GetHashCode(), this.End.GetHashCode());
	}

	public static bool operator ==(Interval left, Interval right)
	{
		if (((object)left) == null || ((object)right) == null)
			return object.Equals(left, right);

		return left.Equals(right);
	}

	public static bool operator !=(Interval left, Interval right)
	{
		return !(left == right);
	}

	public static Interval Parse(string strValue, int upper_end)
	{
		var index = strValue.IndexOf("-", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			var end_index = index + 1;
			return new Interval(
				int.Parse(strValue.Substring(0, index)),
				int.Parse(strValue.Substring(end_index, strValue.Length - end_index)));
		}

		index = strValue.IndexOf("to", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			var end_index = index + 2;
			return new Interval(
				int.Parse(strValue.Substring(0, index)),
				int.Parse(strValue.Substring(end_index, strValue.Length - end_index)));
		}

		index = strValue.IndexOf("+", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return new Interval(int.Parse(strValue.Substring(0, index)), upper_end, true);
		}

		index = strValue.IndexOf("Plus", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return new Interval(int.Parse(strValue.Substring(0, index)), upper_end, true);
		}

		throw new InvalidCastException($"Invalid interval string format (xx-xx): {strValue}.");
	}
}

public static string[] SplitCsv(string line)
{
	var result = new List<string>();
	var currentStr = new StringBuilder("");
	bool inQuotes = false;
	for (int i = 0; i < line.Length; i++)
	{
		if (line[i] == '\"')    // Quotes are closing or opening
		{
			inQuotes = !inQuotes;
		}
		else if (line[i] == ',') // Comma
		{
			if (!inQuotes) // If not in quotes, end of current string, add it to result
			{
				result.Add(currentStr.ToString());
				currentStr.Clear();
			}
			else
			{
				currentStr.Append(line[i]); // If in quotes, just add it 
			}
		}
		else
		{
			currentStr.Append(line[i]);  // Add any other character to current string
		}
	}

	result.Add(currentStr.ToString());
	return result.ToArray(); // Return array of all strings
}

public IReadOnlyDictionary<int, Country> LoadCountries(string fileName)
{
	var countries = new Dictionary<int, Country>();
	if (!File.Exists(fileName))
	{
		throw new ArgumentException($"Countries file {fileName} not found.");
	}

	Console.WriteLine("Processing population file {0} ... ", fileName);
	var timer = new Stopwatch();
	timer.Start();
	using (var reader = new StreamReader(fileName, Encoding.UTF8))
	{
		string line;
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", fileName);
			return countries;
		}

		int code = 0;
		while ((line = reader.ReadLine()) != null)
		{
			var parts = line.Split(",");
			if (parts.Length < 4)
			{
				throw new InvalidDataException($"Country file line: {line}, not a valid format.");
			}

			code = int.Parse(parts[0].Trim());
			countries.Add(code, new Country
			{
				Code = code,
				Name = parts[1].Trim(),
				Alpha2 = parts[2].Trim(),
				Alpha3 = parts[3].Trim(),
			});
		}
	}

	return countries;
}
