using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using CSVcorrectionTool.Models;

using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace CSVcorrectionTool.View
{
    public partial class ViewPort3DView : UserControl
    {

        public static readonly DependencyProperty LineThicknessProperty =
    DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(ViewPort3DView),
        new PropertyMetadata(0.5, OnLineThicknessChanged));

        public double LineThickness
        {
            get => (double)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        private static void OnLineThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ViewPort3DView)d;
            control.RenderPoints();
        }



        public static readonly DependencyProperty PointsProperty =
            DependencyProperty.Register(
                nameof(Points),
                typeof(ObservableCollection<CSVPointModel>),
                typeof(ViewPort3DView),
                new PropertyMetadata(null, OnPointsChanged));

        public ObservableCollection<CSVPointModel> Points
        {
            get => (ObservableCollection<CSVPointModel>)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("=== OnPointsChanged 호출됨 ===");

            var control = (ViewPort3DView)d;

            // 이전 컬렉션 이벤트 해제
            if (e.OldValue is ObservableCollection<CSVPointModel> oldCollection)
            {
                oldCollection.CollectionChanged -= control.Points_CollectionChanged;
            }

            // 새 컬렉션 이벤트 구독
            if (e.NewValue is ObservableCollection<CSVPointModel> newCollection)
            {
                newCollection.CollectionChanged += control.Points_CollectionChanged;
                Console.WriteLine($"새 컬렉션 구독 완료. Count: {newCollection.Count}");
            }

            control.RenderPoints();
        }

        private void Points_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Console.WriteLine($"=== Points_CollectionChanged 호출됨 ===");
            Console.WriteLine($"Action: {e.Action}");
            Console.WriteLine($"현재 Points Count: {Points?.Count ?? 0}");

            // UI 스레드에서 실행되도록 보장
            if (Dispatcher.CheckAccess())
            {
                RenderPoints();
            }
            else
            {
                Dispatcher.Invoke(() => RenderPoints());
            }
        }

        private bool _isRotating = false;
        private bool _isPanning = false;
        private System.Windows.Point _lastMousePosition;
        private Point3D _cameraPosition;
        private Point3D _lookAtPoint = new Point3D(0, 0, 0);
        private Vector3D _lookDirection;
        private double _cameraDistance = 500;

        public ViewPort3DView()
        {
            InitializeComponent();
            InitializeMouseControls();
            CreateAxis();
        }

        private void InitializeMouseControls()
        {
         
            this.MouseDown += Viewport_MouseDown;
            this.MouseUp += Viewport_MouseUp;
            this.MouseMove += Viewport_MouseMove;
            this.MouseWheel += Viewport_MouseWheel;

           
            viewport.MouseDown += Viewport_MouseDown;
            viewport.MouseUp += Viewport_MouseUp;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.MouseWheel += Viewport_MouseWheel;

   
            this.Background = System.Windows.Media.Brushes.Transparent;
     
            this.Focusable = true;
            viewport.Focusable = true;
        }

        #region 마우스관련 함수

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {

            this.Focus();

            _lastMousePosition = e.GetPosition(this);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isRotating = true;
                this.CaptureMouse();
                e.Handled = true;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                this.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isRotating = false;
            _isPanning = false;
            this.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isRotating && !_isPanning) return;

            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            if (_isRotating)
            {
                RotateCamera(deltaX * 0.01, deltaY * 0.01);
            }
            else if (_isPanning)
            {
                PanCamera(deltaX, deltaY);
            }

            _lastMousePosition = currentPosition;
            e.Handled = true;
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scaleFactor = e.Delta > 0 ? 0.9 : 1.1;
            _cameraDistance *= scaleFactor;
            UpdateCameraPosition();
            e.Handled = true;
        }

        #endregion


   



        private void RotateCamera(double deltaTheta, double deltaPhi)
        {
            if (camera == null) return;

            var relativePosition = camera.Position - _lookAtPoint;
            var radius = relativePosition.Length;

            var theta = Math.Atan2(relativePosition.Y, relativePosition.X);
            var phi = Math.Acos(relativePosition.Z / radius);

            theta += deltaTheta;
            phi += deltaPhi;
            phi = Math.Max(0.1, Math.Min(Math.PI - 0.1, phi));

            var newRelativeX = radius * Math.Sin(phi) * Math.Cos(theta);
            var newRelativeY = radius * Math.Sin(phi) * Math.Sin(theta);
            var newRelativeZ = radius * Math.Cos(phi);

            var newPosition = _lookAtPoint + new Vector3D(newRelativeX, newRelativeY, newRelativeZ);

            camera.Position = newPosition;
            camera.LookDirection = _lookAtPoint - newPosition;
        }

        private void PanCamera(double deltaX, double deltaY)
        {
            if (camera == null) return;

            var lookDirection = camera.LookDirection;
            lookDirection.Normalize();

            var upDirection = camera.UpDirection;
            upDirection.Normalize();

            var rightDirection = Vector3D.CrossProduct(lookDirection, upDirection);
            rightDirection.Normalize();

            var panScale = _cameraDistance * 0.002;

            var panVector = -rightDirection * deltaX * panScale + upDirection * deltaY * panScale;

            camera.Position = camera.Position + panVector;
            _lookAtPoint = _lookAtPoint + panVector;
        }

        private void UpdateCameraPosition()
        {
            if (camera == null) return;

            var direction = camera.LookDirection;
            direction.Normalize();

            camera.Position = _lookAtPoint - direction * _cameraDistance;
        }

        private void CreateAxis()
        {
            var xAxis = CreateLine(new Point3D(0, 0, 0), new Point3D(50, 0, 0), Colors.Red);
            axisModelGroup.Children.Add(xAxis);

            var yAxis = CreateLine(new Point3D(0, 0, 0), new Point3D(0, 50, 0), Colors.Green);
            axisModelGroup.Children.Add(yAxis);

            var zAxis = CreateLine(new Point3D(0, 0, 0), new Point3D(0, 0, 50), Colors.Blue);
            axisModelGroup.Children.Add(zAxis);
        }

       
        private GeometryModel3D CreateLine(Point3D start, Point3D end, System.Windows.Media.Color color)
        {
            var mesh = new MeshGeometry3D();
            var thickness = LineThickness;

            var direction = end - start;
            direction.Normalize();

            var perpendicular = new Vector3D(direction.Y, -direction.X, 0);
            if (perpendicular.Length < 0.1)
                perpendicular = new Vector3D(0, direction.Z, -direction.Y);
            perpendicular.Normalize();
            perpendicular *= thickness;

            var positions = new Point3DCollection
    {
        start + perpendicular, start - perpendicular,
        end + perpendicular, end - perpendicular
    };

            var triangleIndices = new Int32Collection { 0, 1, 2, 1, 3, 2 };

            mesh.Positions = positions;
            mesh.TriangleIndices = triangleIndices;

            var material = new DiffuseMaterial(new SolidColorBrush(color));
            var model = new GeometryModel3D(mesh, material);
            // 반대 방향에서도 보이도록 BackMaterial도 지정
            model.BackMaterial = material;
            return model;
        }


        private void RenderPoints()
        {

            pointsModelGroup.Children.Clear();

            if (Points == null || Points.Count == 0)
            {
                var testSphere = CreateSphere(new Point3D(0, 0, 0), 5);
                pointsModelGroup.Children.Add(testSphere);
                return;
            }


            if (Points.Count > 1)
            {
                for (int i = 0; i < Points.Count - 1; i++)
                {
                    var p1 = Points[i];
                    var p2 = Points[i + 1];

       
                    var start = new Point3D(p1.X, p1.Y, p1.Z);
                    var end = new Point3D(p2.X, p2.Y, p2.Z);
                    var line = CreateLine(start, end, Colors.Red);

                    pointsModelGroup.Children.Add(line);
                }
            }

            double minX = Points.Min(p => p.X);
            double maxX = Points.Max(p => p.X);
            double minY = Points.Min(p => p.Y);
            double maxY = Points.Max(p => p.Y);
            double minZ = Points.Min(p => p.Z);
            double maxZ = Points.Max(p => p.Z);

            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            double centerZ = (minZ + maxZ) / 2.0;
            double maxRange = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));

 
            _lookAtPoint = new Point3D(centerX, centerY, centerZ);
            _cameraDistance = Math.Max(maxRange * 1.5, 100);
            var cameraOffset = _cameraDistance / Math.Sqrt(3);
            camera.Position = new Point3D(centerX + cameraOffset, centerY + cameraOffset, centerZ + cameraOffset);
            camera.LookDirection = _lookAtPoint - camera.Position;

       
            double sphereRadius = Math.Max(maxRange * 0.01, 1.0);
            if (Points.Count > 100) sphereRadius *= 0.5;
            else if (Points.Count > 50) sphereRadius *= 0.7;

       

            int addedCount = 0;
            foreach (var point in Points)
            {
                var center = new Point3D(point.X, point.Y, point.Z);
                var sphere = CreateSphere(center, sphereRadius);
                pointsModelGroup.Children.Add(sphere);

                double lineLength = sphereRadius * 3.5; 

                var dirX = GetDirectionFromAngle(point.RotX, Axis.X, lineLength);
                var lineX = CreateLine(center, center + dirX, Colors.Red);
                pointsModelGroup.Children.Add(lineX);

                var dirY = GetDirectionFromAngle(point.RotY, Axis.Y, lineLength);
                var lineY = CreateLine(center, center + dirY, Colors.Green);
                pointsModelGroup.Children.Add(lineY);

                var dirZ = GetDirectionFromAngle(point.RotZ, Axis.Z, lineLength);
                var lineZ = CreateLine(center, center + dirZ, Colors.Blue);
                pointsModelGroup.Children.Add(lineZ);

                addedCount++;
            }


        }

        private enum Axis { X, Y, Z }

        private Vector3D GetDirectionFromAngle(double angleDegree, Axis axis, double length)
        {
            double angleRad = angleDegree * Math.PI / 180.0;
            switch (axis)
            {
                case Axis.X:
                    return new Vector3D(length * Math.Cos(angleRad), length * Math.Sin(angleRad), 0);
                case Axis.Y:
                    return new Vector3D(0, length * Math.Cos(angleRad), length * Math.Sin(angleRad));
                case Axis.Z:
                    return new Vector3D(length * Math.Sin(angleRad), 0, length * Math.Cos(angleRad));
                default:
                    return new Vector3D(length, 0, 0);
            }
        }





        private GeometryModel3D CreateSphere(Point3D center, double radius)
        {
            var mesh = new MeshGeometry3D();
            var positions = new Point3DCollection();
            var triangleIndices = new Int32Collection();

            int latSegments = 8;  // 위도 분할
            int lonSegments = 12; // 경도 분할

            // 정점 생성
            for (int lat = 0; lat <= latSegments; lat++)
            {
                double theta = lat * Math.PI / latSegments;
                double sinTheta = Math.Sin(theta);
                double cosTheta = Math.Cos(theta);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    double phi = lon * 2 * Math.PI / lonSegments;
                    double sinPhi = Math.Sin(phi);
                    double cosPhi = Math.Cos(phi);

                    double x = center.X + radius * sinTheta * cosPhi;
                    double y = center.Y + radius * sinTheta * sinPhi;
                    double z = center.Z + radius * cosTheta;

                    positions.Add(new Point3D(x, y, z));
                }
            }

            // 삼각형 인덱스 생성
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int current = lat * (lonSegments + 1) + lon;
                    int next = current + lonSegments + 1;

                    // 첫 번째 삼각형
                    triangleIndices.Add(current);
                    triangleIndices.Add(next);
                    triangleIndices.Add(current + 1);

                    // 두 번째 삼각형
                    triangleIndices.Add(current + 1);
                    triangleIndices.Add(next);
                    triangleIndices.Add(next + 1);
                }
            }

            mesh.Positions = positions;
            mesh.TriangleIndices = triangleIndices;

            System.Windows.Media.Color sphereColor = Colors.Red;
            if (center.X > 200) sphereColor = Colors.Blue;
            else if (center.X < 50) sphereColor = Colors.Green;

            var material = new DiffuseMaterial(new SolidColorBrush(sphereColor));
            return new GeometryModel3D(mesh, material);
        }
    }
}
