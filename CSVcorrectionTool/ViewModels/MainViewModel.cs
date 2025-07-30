using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using CSVcorrectionTool.Models;
using CSVcorrectionTool.Services;
using System.Windows;
using System.Collections.ObjectModel;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace CSVcorrectionTool.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<CSVPointModel> _points = new();


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
            AllocConsole();

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

                var success = await _csvService.SaveCsvFileAsync(
                    saveFileDialog.FileName,
                    CsvModel.CsvData,
                    CsvModel.Headers);

                if (!success)
                {
                    CsvModel.ErrorMessage = "파일 저장에 실패했습니다.";
                }

                IsLoading = false;
            }
        }



    }

}
