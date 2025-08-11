using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using CSVcorrectionTool.Models;

using Color = System.Windows.Media.Color;
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
            var control = (ViewPort3DView)d;

            if (e.OldValue is ObservableCollection<CSVPointModel> oldCollection)
                oldCollection.CollectionChanged -= control.Points_CollectionChanged;

            if (e.NewValue is ObservableCollection<CSVPointModel> newCollection)
                newCollection.CollectionChanged += control.Points_CollectionChanged;

            control.RenderPoints();
        }

        private void Points_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
                RenderPoints();
            else
                Dispatcher.Invoke(() => RenderPoints());
        }

        private bool _isRotating = false;
        private bool _isPanning = false;
        private System.Windows.Point _lastMousePosition;
        private Point3D _lookAtPoint = new Point3D(0, 0, 0);
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
                RotateCamera(deltaX * 0.01, deltaY * 0.01);
            else if (_isPanning)
                PanCamera(deltaX, deltaY);

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
            axisModelGroup.Children.Add(CreateLine(new Point3D(0, 0, 0), new Point3D(50, 0, 0), Colors.Red));   // X축 (빨강)
            axisModelGroup.Children.Add(CreateLine(new Point3D(0, 0, 0), new Point3D(0, 50, 0), Colors.Green)); // Y축 (초록)
            axisModelGroup.Children.Add(CreateLine(new Point3D(0, 0, 0), new Point3D(0, 0, 50), Colors.Blue));  // Z축 (파랑)
        }


        private GeometryModel3D CreateLine(Point3D start, Point3D end, Color color)
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
            model.BackMaterial = material;
            return model;
        }
        private void RenderPoints()
        {
            pointsModelGroup.Children.Clear();

            if (Points == null || Points.Count == 0)
            {
                pointsModelGroup.Children.Add(CreateSphere(new Point3D(0, 0, 0), 5, 0));
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
                    pointsModelGroup.Children.Add(CreateLine(start, end, Colors.Red));
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

            LineThickness = 1.0;

            foreach (var point in Points)
            {
                var center = new Point3D(point.X, point.Y, point.Z);
                pointsModelGroup.Children.Add(CreateSphere(center, sphereRadius, point.RotZ));

                double vectorLength = sphereRadius * (6.0 + 6.0 * point.RotZ);

             
                var rotatedDirection = new Vector3D(point.RotX, point.RotY, point.RotZ);

                if (rotatedDirection.Length > 0.0001)
                {
                    rotatedDirection.Normalize();
                    rotatedDirection *= vectorLength;
                    var vectorColor = GetColorByTheta(point.RotZ);

                    var vectorEnd = center + rotatedDirection;
                    pointsModelGroup.Children.Add(CreateLine(center, vectorEnd, Colors.Blue, 4.0));
                }
            }
        }



        private GeometryModel3D CreateLine(Point3D start, Point3D end, Color color, double thickness)
        {
            var mesh = new MeshGeometry3D();

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
            model.BackMaterial = material;
            return model;
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


        private Color GetColorByTheta(double theta)
        {
            theta = Math.Max(0, Math.Min(1, theta));
            byte r = (byte)(theta * 255);
            byte b = (byte)((1 - theta) * 255);
            return Color.FromRgb(r, 0, b);
        }

        private GeometryModel3D CreateSphere(Point3D center, double radius, double theta)
        {
            var mesh = new MeshGeometry3D();
            var positions = new Point3DCollection();
            var triangleIndices = new Int32Collection();

            int latSegments = 8;
            int lonSegments = 12;

            for (int lat = 0; lat <= latSegments; lat++)
            {
                double phi = lat * Math.PI / latSegments;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    double thetaLon = lon * 2 * Math.PI / lonSegments;
                    double sinThetaLon = Math.Sin(thetaLon);
                    double cosThetaLon = Math.Cos(thetaLon);

                    double x = center.X + radius * sinPhi * cosThetaLon;
                    double y = center.Y + radius * sinPhi * sinThetaLon;
                    double z = center.Z + radius * cosPhi;

                    positions.Add(new Point3D(x, y, z));
                }
            }

            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int current = lat * (lonSegments + 1) + lon;
                    int next = current + lonSegments + 1;

                    triangleIndices.Add(current);
                    triangleIndices.Add(next);
                    triangleIndices.Add(current + 1);

                    triangleIndices.Add(current + 1);
                    triangleIndices.Add(next);
                    triangleIndices.Add(next + 1);
                }
            }

            mesh.Positions = positions;
            mesh.TriangleIndices = triangleIndices;

            var sphereColor = GetColorByTheta(theta);
            var material = new DiffuseMaterial(new SolidColorBrush(sphereColor));
            return new GeometryModel3D(mesh, material);
        }
    }
}
