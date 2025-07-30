namespace CSVcorrectionTool.Models
{
    public class CSVPointModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double RotX { get; set; }
        public double RotY { get; set; }
        public double RotZ { get; set; }
        public List<string> ExtraValues { get; set; }
    }
}