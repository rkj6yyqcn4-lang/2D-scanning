using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DistanceMeassurementApp2.PostProcessing;

namespace DistanceMeassurementApp2
{
    internal sealed class ScanReconstructionOptions
    {
        public int ExpectedRows { get; set; }
        public double XStepUm { get; set; }
        public double YStepUm { get; set; }
        public double ZScale { get; set; }
        public bool CalibrationConfirmed { get; set; }
    }

    internal sealed class ScanReconstructionResult
    {
        public string HeightCsv { get; set; }
        public string HeightTiff { get; set; }
        public string HeightGsf { get; set; }
        public string PointCsv { get; set; }
        public string PointPly { get; set; }
        public string MetadataFile { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int FailedSolutions { get; set; }
        public bool UnevenRows { get; set; }
    }

    internal static class ScanReconstruction
    {
        private const int SpectrumLength = 1750;

        public static ScanReconstructionResult Run(string rawCsv, ScanReconstructionOptions options)
        {
            if (options == null || options.ExpectedRows < 1)
                throw new ArgumentException("扫描行数无效。");

            string dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            foreach (string name in new[] { "wn.txt", "bk.txt", "so.txt" })
            {
                if (!File.Exists(Path.Combine(dataFolder, name)))
                    throw new FileNotFoundException("距离解算所需的标定文件缺失：" + name);
            }

            // This is the distance solver from DistanceMeassurementApp2-ParallelProcessing.
            // Use a separate instance because live processing also owns one.
            var solver = new CalculateDistance(SpectrumLength);
            var lines = new List<double>[options.ExpectedRows];
            for (int row = 0; row < lines.Length; row++)
                lines[row] = new List<double>();

            int failed = 0;
            using (var reader = new StreamReader(rawCsv, Encoding.UTF8))
            {
                reader.ReadLine(); // CSV header
                string csvLine;
                while ((csvLine = reader.ReadLine()) != null)
                {
                    string[] cells = csvLine.Split(',');
                    if (cells.Length < 7 + SpectrumLength || cells[4] != "光谱" ||
                        !int.TryParse(cells[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) ||
                        row < 0 || row >= options.ExpectedRows)
                        continue;

                    double[] spectrum = new double[SpectrumLength];
                    bool valid = int.TryParse(cells[6], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int count) && count == SpectrumLength;
                    if (valid)
                    {
                        for (int i = 0; i < SpectrumLength; i++)
                        {
                            if (!double.TryParse(cells[7 + i], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out spectrum[i]))
                            {
                                valid = false;
                                break;
                            }
                        }
                    }

                    double distance = double.NaN;
                    if (valid)
                    {
                        try
                        {
                            distance = solver.Calculate(spectrum) * options.ZScale;
                        }
                        catch (Exception)
                        {
                            // Keep the sample position so that gaps can be interpolated.
                        }
                    }
                    if (double.IsNaN(distance) || double.IsInfinity(distance))
                        failed++;
                    lines[row].Add(distance);
                }
            }

            int totalSolutions = lines.Sum(line => line.Count);
            if (totalSolutions == 0 || failed > totalSolutions * 0.2)
                throw new InvalidOperationException("正扫光谱的距离解算失败比例超过 20%；原始光谱已保存，未生成可能失真的形貌图。");

            for (int row = 0; row < lines.Length; row++)
            {
                if (lines[row].Count < 2)
                    throw new InvalidOperationException("第 " + (row + 1) +
                        " 行正向扫描光谱少于 2 帧；原始光谱已保存，暂不能重建完整形貌。");
                FillMissingSolutions(lines[row], row);
            }

            int[] counts = lines.Select(line => line.Count).OrderBy(count => count).ToArray();
            int width = counts[counts.Length / 2];
            bool uneven = counts[0] < width * 0.8 || counts[counts.Length - 1] > width * 1.2;
            var heights = new double[lines.Length, width];
            for (int row = 0; row < lines.Length; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    double source = (double)column * (lines[row].Count - 1) / (width - 1);
                    int left = (int)Math.Floor(source);
                    int right = Math.Min(left + 1, lines[row].Count - 1);
                    heights[row, column] = lines[row][left] +
                        (lines[row][right] - lines[row][left]) * (source - left);
                }
            }

            // Reuse the existing CSV and PLY exporters from the supplied stage project.
            var map = new SurfaceHeightMap(heights, options.XStepUm, options.YStepUm,
                options.CalibrationConfirmed);
            string prefix = Path.Combine(Path.GetDirectoryName(rawCsv),
                Path.GetFileNameWithoutExtension(rawCsv));
            var result = new ScanReconstructionResult
            {
                HeightCsv = prefix + "_height_map.csv",
                HeightTiff = prefix + "_height_map.tif",
                HeightGsf = prefix + "_height_map.gsf",
                PointCsv = prefix + "_point_cloud.csv",
                PointPly = prefix + "_point_cloud.ply",
                MetadataFile = prefix + "_reconstruction.txt",
                Rows = lines.Length,
                Columns = width,
                FailedSolutions = failed,
                UnevenRows = uneven
            };
            map.SaveHeightCsv(result.HeightCsv);
            map.SaveHeightTiff(result.HeightTiff);
            map.SaveGwyddionSimpleField(result.HeightGsf);
            map.SavePointCloudCsv(result.PointCsv);
            map.SavePointCloudPly(result.PointPly);
            File.WriteAllLines(result.MetadataFile, new[]
            {
                "Height solver: CalculateDistance.Calculate from DistanceMeassurementApp2-ParallelProcessing",
                "Source spectra: " + rawCsv,
                "Rows: " + result.Rows.ToString(CultureInfo.InvariantCulture),
                "Columns: " + result.Columns.ToString(CultureInfo.InvariantCulture),
                "Row frame counts: " + string.Join(",", lines.Select(line => line.Count.ToString(CultureInfo.InvariantCulture))),
                "Failed distance solutions (interpolated): " + failed.ToString(CultureInfo.InvariantCulture),
                "Uneven row counts: " + uneven,
                "X step input (um): " + options.XStepUm.ToString("R", CultureInfo.InvariantCulture),
                "Y step input (um): " + options.YStepUm.ToString("R", CultureInfo.InvariantCulture),
                "Z multiplier: " + options.ZScale.ToString("R", CultureInfo.InvariantCulture),
                "Calibration confirmed: " + options.CalibrationConfirmed,
                "Gwyddion TIFF: " + result.HeightTiff,
                "TIFF scaling: unsigned 16-bit grayscale, min/max and step values in ImageDescription; enter physical scales manually when importing TIFF.",
                "Gwyddion Simple Field: " + result.HeightGsf,
                "GSF calibration: X/Y/Z are in metres only when calibration is confirmed; otherwise values are unitless relative coordinates.",
                "Calibration: X/Y step and Z multiplier must be checked against the real stage and probe before treating coordinates as physical dimensions.",
                options.ExpectedRows == 1 ? "Only one X scan row was captured; this is a line profile, not a 2D surface grid." :
                    "Each row contains forward X motion only; return and Y step frames were excluded."
            }, Encoding.UTF8);
            return result;
        }

        private static void FillMissingSolutions(List<double> line, int row)
        {
            int first = line.FindIndex(value => !double.IsNaN(value) && !double.IsInfinity(value));
            if (first < 0)
                throw new InvalidOperationException("第 " + (row + 1) + " 行的距离解算全部失败；原始光谱已保存。");

            for (int i = 0; i < first; i++)
                line[i] = line[first];
            int previous = first;
            for (int i = first + 1; i < line.Count; i++)
            {
                if (double.IsNaN(line[i]) || double.IsInfinity(line[i]))
                    continue;
                for (int gap = previous + 1; gap < i; gap++)
                    line[gap] = line[previous] + (line[i] - line[previous]) *
                        (gap - previous) / (i - previous);
                previous = i;
            }
            for (int i = previous + 1; i < line.Count; i++)
                line[i] = line[previous];
        }
    }
}
