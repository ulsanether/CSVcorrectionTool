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
        private string _stateText = string.Empty;

        [ObservableProperty]
        private double xDegree = 90;

        private readonly ICSVService _csvService;

        [ObservableProperty]
        private CSVadjustmentModel _csvModel = new();

        [ObservableProperty]
        private bool _isLoading;

        public MainViewModel(ICSVService csvService)
        {
            _csvService = csvService;
            StateText = "준비 상태";
        }

        [RelayCommand]
        private async Task ConvertAsync()
        {
            StateText = "보정작업 중";
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

                // 진행 방향(탄젠트)만 계산하여 RotX/Y/Z에 저장
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

                        directionVector.Normalize();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            currentPoint.RotX = directionVector.X;
                            currentPoint.RotY = directionVector.Y;
                            currentPoint.RotZ = directionVector.Z;
                        });
                    }
                }

                StateText = "방향 벡터 보정 완료";
            });
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

                if (CsvModel.Headers != null && CsvModel.Headers.Length > 0)
                {
                    lines.Add(string.Join(",", CsvModel.Headers));
                }
                else
                {
                    lines.Add("X,Y,Z,nx,ny,nz,Type,Value");
                }

                for (int pointIndex = 0; pointIndex < Points.Count; pointIndex++)
                {
                    var point = Points[pointIndex];

                    var values = new List<string>
                    {
                        point.X.ToString("F5"),
                        point.Y.ToString("F5"),
                        point.Z.ToString("F5"),
                        point.RotX.ToString("F5"),  // nx
                        point.RotY.ToString("F5"),  // ny
                        point.RotZ.ToString("F5")   // nz
                    };

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
                StateText = "파일이 저장되었습니다.";
                MessageBox.Show($"CSV 파일이 성공적으로 저장되었습니다.\n파일 위치: {saveFileDialog.FileName}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private async Task SettingAsync()
        {
            StateText = "설정아직 준비 안됨";
            //var settingWindow = new SettingWindow
            //{
            //    Owner = Application.Current.MainWindow
            //};
            //if (settingWindow.ShowDialog() == true)
            //{
            //    // 설정이 변경되었을 때 필요한 작업을 여기에 추가할 수 있습니다.
            //    StateText = "설정이 저장되었습니다.";
            //}
            //else
            //{
            //    StateText = "설정창이 닫혔습니다.";
            //}
        }

        [RelayCommand]
        private async Task TestCheckAsync()
        {
            MessageBox.Show("테스트 메시지입니다.", "테스트", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task OpenFileAsync()
        {
            StateText = "CSV 파일을 불러오는 중...";

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
