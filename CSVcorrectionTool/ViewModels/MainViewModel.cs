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
        private async Task ConvertAsync()
        {
            await Task.Run(() =>
            {
                if (Points.Count == 0) return;

                var segments = new List<List<CSVPointModel>>();
                var currentSegment = new List<CSVPointModel>();
                int currentCurveNumber = -1;

                for (int i = 0; i < Points.Count; i++)
                {
                    var point = Points[i];
                    if (point.ExtraValues != null &&
                        point.ExtraValues.Count >= 2 &&
                        point.ExtraValues[0] == "Curve" &&
                        int.TryParse(point.ExtraValues[1], out int curveNumber))
                    {
                        if (currentCurveNumber != curveNumber)
                        {
                            if (currentSegment.Count > 0)
                            {
                                segments.Add(currentSegment);
                            }
                            currentSegment = new List<CSVPointModel>();
                            currentCurveNumber = curveNumber;
                        }
                    }
                    currentSegment.Add(point);
                }

                if (currentSegment.Count > 0)
                {
                    segments.Add(currentSegment);
                }

                for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
                {
                    var segment = segments[segmentIndex];
                    Console.WriteLine($"\n=== Processing Segment {segmentIndex} (Points: {segment.Count}) ===");

                    for (int i = 0; i < segment.Count; i++)
                    {
                        int prevIndex = Math.Max(0, i - 1);
                        int nextIndex = Math.Min(segment.Count - 1, i + 1);

                        var prevPoint = segment[prevIndex];
                        var currentPoint = segment[i];
                        var nextPoint = segment[nextIndex];

                        Vector3D directionVector = new Vector3D(
                            nextPoint.X - prevPoint.X,
                            nextPoint.Y - prevPoint.Y,
                            nextPoint.Z - prevPoint.Z
                        );

                        double length = directionVector.Length;

                        if (length > 0)
                        {
                            directionVector.Normalize();

                            Matrix3D rotation = new Matrix3D();

                            //dlfeks 
                            rotation.Rotate(new Quaternion(new Vector3D(1, 0, 0), -90));
                            directionVector = Vector3D.Multiply(directionVector, rotation);

                            directionVector.Normalize();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                currentPoint.RotX = directionVector.X;
                                currentPoint.RotY = directionVector.Y;
                                currentPoint.RotZ = directionVector.Z;

                                Console.WriteLine($"Point {i} in Segment {segmentIndex}: ({directionVector.X:F5}, {directionVector.Y:F5}, {directionVector.Z:F5})");
                                double theta = Math.Acos(directionVector.Z) * 180.0 / Math.PI;
                                Console.WriteLine($"Slope angle = {theta:F2}°");
                            });
                        }
                    }
                }
            });

            MessageBox.Show("세그먼트별 방향 벡터 계산이 완료되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }


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

                // 헤더 추가
                if (CsvModel.Headers != null && CsvModel.Headers.Length > 0)
                {
                    lines.Add(string.Join(",", CsvModel.Headers));
                }
                else
                {
                    lines.Add("X,Y,Z,nx,ny,nz,Type,Value");
                }

                // 데이터 추가
                for (int pointIndex = 0; pointIndex < Points.Count; pointIndex++)
                {
                    var point = Points[pointIndex];

                    // 방향 벡터 값을 직접 저장 (-1 ~ 1 범위의 값)
                    var values = new List<string>
            {
                point.X.ToString("F5"),
                point.Y.ToString("F5"),
                point.Z.ToString("F5"),
                point.RotX.ToString("F5"),  // nx
                point.RotY.ToString("F5"),  // ny
                point.RotZ.ToString("F5")   // nz
            };

                    // ExtraValues 처리
                    if (point.ExtraValues != null && point.ExtraValues.Count > 0)
                    {
                        bool isFirstCurveZero = pointIndex > 0 &&
                                              point.ExtraValues.Count == 2 &&
                                              point.ExtraValues[0] == "Curve" &&
                                              point.ExtraValues[1] == "0";

                        if (!isFirstCurveZero)
                        {
                            values.AddRange(point.ExtraValues);
                        }
                    }

                    lines.Add(string.Join(",", values));
                }

                await File.WriteAllLinesAsync(saveFileDialog.FileName, lines);

                IsLoading = false;

                MessageBox.Show($"CSV 파일이 성공적으로 저장되었습니다.\n파일 위치: {saveFileDialog.FileName}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
            await Task.Run(() =>
            {
                var points = new List<CSVPointModel>();
                int pointIndex = 0;

                Console.WriteLine("=== CSV 포인트 데이터 로딩 시작 ===");

                if (!string.IsNullOrEmpty(CsvModel.FilePath) && File.Exists(CsvModel.FilePath))
                {
                    var lines = File.ReadAllLines(CsvModel.FilePath);

                    int startIndex = 0;
                    if (lines.Length > 0)
                    {
                        var firstLineTokens = lines[0].Split(',');
                        if (firstLineTokens.Length > 0 && !double.TryParse(firstLineTokens[0], out _))
                        {
                            startIndex = 1; 
                        }
                    }

                    for (int lineIndex = startIndex; lineIndex < lines.Length; lineIndex++)
                    {
                        var line = lines[lineIndex];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var tokens = line.Split(',');
                        if (tokens.Length < 6) continue;

                        try
                        {
                            var point = new CSVPointModel();
                            var extraValues = new List<string>();

                            if (double.TryParse(tokens[0], out double x)) point.X = x;
                            if (double.TryParse(tokens[1], out double y)) point.Y = y;
                            if (double.TryParse(tokens[2], out double z)) point.Z = z;
                            if (double.TryParse(tokens[3], out double rotX)) point.RotX = rotX;
                            if (double.TryParse(tokens[4], out double rotY)) point.RotY = rotY;
                            if (double.TryParse(tokens[5], out double rotZ)) point.RotZ = rotZ;

                            for (int i = 6; i < tokens.Length; i++)
                            {
                                extraValues.Add(tokens[i]);
                            }

                            point.ExtraValues = extraValues;
                            points.Add(point);

                            Console.WriteLine($"Point {pointIndex++}: X={point.X:F5}, Y={point.Y:F5}, Z={point.Z:F5}");
                            if (point.ExtraValues.Count > 0)
                            {
                                Console.WriteLine($"         Extra: [{string.Join("], [", point.ExtraValues)}]");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"라인 {lineIndex} 파싱 실패: {ex.Message}");
                            continue;
                        }
                    }
                }

                if (points.Count > 0)
                {
                    Console.WriteLine($"첫 번째 포인트 삭제: X={points[0].X:F5}, Y={points[0].Y:F5}, Z={points[0].Z:F5}");
                    points.RemoveAt(0);
                }

                Console.WriteLine($"=== 총 {points.Count}개의 포인트 로딩 완료 (첫 번째 라인 삭제됨) ===");

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

        
       


    }
}
