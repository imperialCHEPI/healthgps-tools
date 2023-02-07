using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HealthGPS.Tools;

public static class FileHelper
{
    public static int GetIntFromEndOfString(string text)
    {
        int i = text.Length - 1;
        while (i >= 0)
        {
            if (!char.IsNumber(text[i])) break;
            i--;
        }

        if (int.TryParse(text.AsSpan(i + 1), out int number))
        {
            return number;
        }

        return -1;
    }

    public static SortedDictionary<int, FileInfo> GetBatchResultFiles(DirectoryInfo sourceFolder, string filterPattem, int? maxFiles)
    {
        var batchFiles = sourceFolder.GetFiles(filterPattem);
        var orderedBacthFiles = new SortedDictionary<int, FileInfo>();
        foreach (var file in batchFiles)
        {
            var batch_job_idx = GetIntFromEndOfString(Path.GetFileNameWithoutExtension(file.Name));
            if (batch_job_idx < 1)
            {
                Console.WriteLine($"Invalid batch result file name: {file.FullName}, missing job identifier.");
                continue;
            }

            if (!orderedBacthFiles.TryAdd(batch_job_idx, file))
            {
                Console.WriteLine($"Duplicated batch job id: {batch_job_idx} file: {file.FullName}.");
            }
        }

        if (maxFiles.HasValue && maxFiles.Value > 0 && maxFiles.Value < orderedBacthFiles.Count)
        {
            return new SortedDictionary<int, FileInfo>(
                orderedBacthFiles.Take(maxFiles.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        return orderedBacthFiles;
    }
}
