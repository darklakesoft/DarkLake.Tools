namespace FlattenToCSV
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.Json;

	public class CsvFlattener
	{
		public static void WriteObjectListToCsv(IEnumerable<object> objects, string filePath)
		{
			var flattenedList = objects.Select(o => FlattenObject(o)).ToList();
			var allKeys = flattenedList.SelectMany(d => d.Keys).Distinct().ToList();

			using var writer = new StreamWriter(filePath);
			writer.WriteLine(string.Join(",", allKeys));

			foreach (var dict in flattenedList)
			{
				var row = allKeys.Select(k => QuoteCsvValue(dict.ContainsKey(k) ? dict[k] : "")).ToArray();
				writer.WriteLine(string.Join(",", row));
			}
		}

		private static Dictionary<string, string> FlattenObject(object obj, string prefix = "")
		{
			var dict = new Dictionary<string, string>();

			if (obj is JsonElement jsonElement)
			{
				if (jsonElement.ValueKind == JsonValueKind.Object)
				{
					foreach (var prop in jsonElement.EnumerateObject())
					{
						var child = FlattenObject(prop.Value, $"{prefix}{prop.Name}.");
						foreach (var kvp in child)
							dict[kvp.Key] = kvp.Value;
					}
				}
				else if (jsonElement.ValueKind == JsonValueKind.Array)
				{
					int index = 0;
					foreach (var item in jsonElement.EnumerateArray())
					{
						var child = FlattenObject(item, $"{prefix}[{index}].");
						foreach (var kvp in child)
							dict[kvp.Key] = kvp.Value;
						index++;
					}
				}
				else
				{
					dict[prefix.TrimEnd('.')] = jsonElement.ToString();
				}
			}
			else
			{
				var json = JsonSerializer.Serialize(obj);
				var jsonDoc = JsonDocument.Parse(json);
				return FlattenObject(jsonDoc.RootElement, prefix);
			}

			return dict;
		}

		private static string QuoteCsvValue(string value)
		{
			if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
				return $"\"{value.Replace("\"", "\"\"")}\"";
			return value;
		}
	}

}
