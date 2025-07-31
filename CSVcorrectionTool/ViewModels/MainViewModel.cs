using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using CSVcorrectionTool.Models;
using CSVcorrectionTool.Services;
using System.Windows;
using System.Collections.ObjectModel;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Media.Media3D;

namespace CSVcorrectionTool.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<CSVPointModel> _points = new();

        [ObservableProperty]
        private double _lineThickness = 0.5;


        [ObservableProperty]
        private double xDegree = 90;

        [ObservableProperty]
        private double yDegree = 90;

        [ObservableProperty]
        private double zDegree = 90;


        private readonly ICSVService _csvService;

        [ObservableProperty]
        private CSVadjustmentModel _csvModel = new();    

        [ObservableProperty]
        private bool _isLoading;


        public MainViewModel(ICSVService csvService)
        {
            _csvService = csvService;
       
          

        }

        [RelayCommand]
        private async Task ConventCommand()
        {
            if (Points == null || Points.Count < 3)
            {
                MessageBox.Show("포인트가 3개 이상 필요합니다.");
                return;
            }

            MessageBox.Show("포인트 카운트 : " + Points.Count);

            for (int i = 0; i < Points.Count; i++)
            {
                var point = Points[i];

                Vector3D perpendicularDirection;

                if (i == 0)
                {
                    if (Points.Count > 1)
                    {
                        var next = Points[1];
                        perpendicularDirection = CalculatePerpendicularBisector(point, next);
                    }
                    else
                    {
                        perpendicularDirection = new Vector3D(1, 0, 0); // 기본값
                    }
                }
                else if (i == Points.Count - 1)
                {
                    var prev2 = Points[i - 2];
                    var prev1 = Points[i - 1];
                    perpendicularDirection = CalculatePerpendicularBisector(prev2, prev1);
                }
                else
                {
                    var prev = Points[i - 1];
                    var next = Points[i + 1];
                    perpendicularDirection = CalculatePerpendicularBisector(prev, next);
                }

                perpendicularDirection.Normalize();

                point.RotX = Math.Atan2(perpendicularDirection.Y, perpendicularDirection.Z) * 180.0 / Math.PI;
                point.RotY = Math.Atan2(-perpendicularDirection.X, Math.Sqrt(perpendicularDirection.Y * perpendicularDirection.Y + perpendicularDirection.Z * perpendicularDirection.Z)) * 180.0 / Math.PI;
                point.RotZ = Math.Atan2(perpendicularDirection.Y, perpendicularDirection.X) * 180.0 / Math.PI;
            }
        }

        private Vector3D CalculatePerpendicularBisector(CSVPointModel p1, CSVPointModel p2)
        {
            double midX = (p1.X + p2.X) / 2.0;
            double midY = (p1.Y + p2.Y) / 2.0;
            double midZ = (p1.Z + p2.Z) / 2.0;

            Vector3D lineDirection = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);

            Vector3D perpendicular;

          
            if (Math.Abs(lineDirection.Z) < 0.9) 
            {
                perpendicular = Vector3D.CrossProduct(lineDirection, new Vector3D(0, 0, 1));
            }
            else 
            {
                perpendicular = Vector3D.CrossProduct(lineDirection, new Vector3D(1, 0, 0));
            }

            
            perpendicular.Normalize();

            return perpendicular;
        }


        [RelayCommand]
        private async Task OpenFileAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
                Title = "CSV 파일 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsLoading = true;

                var result = await _csvService.LoadCsvFileAsync(openFileDialog.FileName);

                if (result.Success)
                {
                   
                    CsvModel.FilePath = openFileDialog.FileName;
                    CsvModel.CsvData.Clear();
                    foreach (var item in result.Data)
                    {
                        CsvModel.CsvData.Add(item);
                    }
                    CsvModel.Headers = result.Headers;
                    CsvModel.IsDataLoaded = true;
                    CsvModel.ErrorMessage = string.Empty;

                    await LoadPointsFromCsvData();
                }
                else
                {
                    CsvModel.ErrorMessage = result.ErrorMessage;
                    CsvModel.IsDataLoaded = false;
                }

                IsLoading = false;
            }
        }

        private async Task LoadPointsFromCsvData()
        {
            //AllocConsole();

            await Task.Run(() =>
            {
                var points = new List<CSVPointModel>();
                int pointIndex = 0;

                Console.WriteLine("=== CSV 포인트 데이터 로딩 시작 ===");

                foreach (dynamic row in CsvModel.CsvData)
                {
                    try
                    {
                        var dict = row as IDictionary<string, object>;
                        if (dict == null) continue;

                        var values = dict.Values.Select(v => v?.ToString() ?? string.Empty).ToArray();

                        if (values.Length < 6) continue;

                        var point = new CSVPointModel();
                        var extraValues = new List<string>();

                        if (double.TryParse(values[0], out double x)) point.X = x;
                        if (double.TryParse(values[1], out double y)) point.Y = y;
                        if (double.TryParse(values[2], out double z)) point.Z = z;
                        if (double.TryParse(values[3], out double rotX)) point.RotX = rotX;
                        if (double.TryParse(values[4], out double rotY)) point.RotY = rotY;
                        if (double.TryParse(values[5], out double rotZ)) point.RotZ = rotZ;

                        for (int i = 6; i < values.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(values[i]))
                            {
                                extraValues.Add(values[i]);
                            }
                        }

                        point.ExtraValues = extraValues;
                        points.Add(point);

                        Console.WriteLine($"Point {pointIndex++}: X={point.X:F5}, Y={point.Y:F5}, Z={point.Z:F5}");
                        Console.WriteLine($"         Rotation: RotX={point.RotX:F5}, RotY={point.RotY:F5}, RotZ={point.RotZ:F5}");
                        if (point.ExtraValues.Count > 0)
                        {
                            Console.WriteLine($"         Extra: {string.Join(", ", point.ExtraValues)}");
                        }
                        Console.WriteLine();

                        System.Diagnostics.Debug.WriteLine($"Point {pointIndex - 1}: ({point.X:F5}, {point.Y:F5}, {point.Z:F5})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"포인트 파싱 실패: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"포인트 파싱 실패: {ex.Message}");
                        continue;
                    }
                }


                Console.WriteLine($"=== 총 {points.Count}개의 포인트 로딩 완료 ===");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Points.Clear();
                    foreach (var point in points)
                    {
                        Points.Add(point);
                    }
                });
            });
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [RelayCommand]
  
        private async Task SaveFileAsync()
        {
            if (!CsvModel.IsDataLoaded) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                Title = "CSV 파일 저장"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsLoading = true;

                var lines = new List<string>();
                for (int pointIndex = 0; pointIndex < Points.Count; pointIndex++)
                {
                    var point = Points[pointIndex];
                    var values = new List<string>
            {
                point.X.ToString("F5"),
                point.Y.ToString("F5"),
                point.Z.ToString("F5"),
                point.RotX.ToString("F5"),
                point.RotY.ToString("F5"),
                point.RotZ.ToString("F5")
            };

                    // 첫 번째 라인은 항상 "Curve,0"
                    if (pointIndex == 0)
                    {
                        values.Add("Curve");
                        values.Add("0");
                    }
                    else
                    {
                        // 나머지 라인은 기존 ExtraValues 처리
                        if (point.ExtraValues != null && point.ExtraValues.Count > 0)
                        {
                            foreach (var val in point.ExtraValues)
                            {
                                if (int.TryParse(val, out int number))
                                {
                                    values.Add("Curve");
                                    values.Add(val);
                                }
                                else
                                {
                                    values.Add(val);
                                }
                            }
                        }
                    }

                    lines.Add(string.Join(",", values));
                }

                await File.WriteAllLinesAsync(saveFileDialog.FileName, lines);

                IsLoading = false;
            }
        }






    }

}
