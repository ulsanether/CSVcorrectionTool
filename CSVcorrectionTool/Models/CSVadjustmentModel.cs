using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CsvHelper;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;


namespace CSVcorrectionTool.Models
{
    public partial class CSVadjustmentModel : ObservableObject
    {


        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double RotX { get; set; }
        public double RotY { get; set; }
        public double RotZ { get; set; }
        public List<string> ExtraValues { get; set; } = new();




        [ObservableProperty]
        private string _filePath = string.Empty;


        [ObservableProperty]
        private ObservableCollection<dynamic> _csvData = new();


        [ObservableProperty]
        private bool _isDataLoaded;


        [ObservableProperty]
        private string _errorMessage = string.Empty;

        //헤더 정보 
        [ObservableProperty]
        private string[] _headers = Array.Empty<string>();

    }
}
