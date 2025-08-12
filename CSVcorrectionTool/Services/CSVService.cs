using System.Globalization;
using CsvHelper;
using CSVcorrectionTool.Models;
using System.IO;
using System.Windows.Media.Media3D;
using CSVcorrectionTool.Models;


namespace CSVcorrectionTool.Services
{
    public interface ICSVService
    {

        Task<(bool Success, List<dynamic> Data, string[] Headers, string ErrorMessage)> LoadCsvFileAsync(string filePatch);
        Task<bool> SaveCsvFileAsync(string filePath, IEnumerable<dynamic> data, string[] headers);

    }




    public class CSVService : ICSVService
    {

        public List<CSVPointModel> LoadPointsFromCsv(string filePath)
        {
            var lines = File.ReadLines(filePath).ToList();
            var points = new List<CSVPointModel>(lines.Count);

            // 병렬 처리
            var result = lines.AsParallel()
                .Select(line =>
                {
                    var tokens = line.Split(',');
                    if (tokens.Length < 6) return null;
                    if (double.TryParse(tokens[0], out double x) &&
                        double.TryParse(tokens[1], out double y) &&
                        double.TryParse(tokens[2], out double z) &&
                        double.TryParse(tokens[3], out double rotX) &&
                        double.TryParse(tokens[4], out double rotY) &&
                        double.TryParse(tokens[5], out double rotZ))
                    {
                        var extra = tokens.Skip(6).ToList();
                        return new CSVPointModel
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            RotX = rotX,
                            RotY = rotY,
                            RotZ = rotZ,
                            ExtraValues = extra
                        };
                    }
                    return null;
                })
                .Where(p => p != null)
                .ToList();

            return result;
        }


        public async Task<(bool Success, List<dynamic> Data, string[] Headers, string ErrorMessage)> LoadCsvFileAsync(string filePath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var reader = new StreamReader(filePath);
                    using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

                    var records = csvReader.GetRecords<dynamic>().ToList();
                    var headers = csvReader.HeaderRecord ?? Array.Empty<string>();

                    return (true, records, headers, string.Empty);
                });
            }
            catch (Exception ex)
            {
                return (false, new List<dynamic>(), Array.Empty<string>(), $"CSV 파일 로드 실패: {ex.Message}");
            }
        }

        public async Task<bool> SaveCsvFileAsync(string filePath, IEnumerable<dynamic> data, string[] headers)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var writer = new StreamWriter(filePath);
                    using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

                    // 헤더 쓰기
                    foreach (var header in headers)
                    {
                        csvWriter.WriteField(header);
                    }
                    csvWriter.NextRecord();

                    // 데이터 쓰기
                    csvWriter.WriteRecords(data);
                    return true;
                });
            }
            catch
            {
                return false;
            }
        }
    }

}
