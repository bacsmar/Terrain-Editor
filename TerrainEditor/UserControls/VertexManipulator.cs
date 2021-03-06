﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using MoreLinq;
using TerrainEditor.Utilities;
using TerrainEditor.ViewModels;

namespace TerrainEditor.UserControls
{

    public class VertexManipulator : ModelVisual3D
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof (DynamicMesh), typeof (VertexManipulator), new PropertyMetadata(default(DynamicMesh),OnSourceChanged));
        public static readonly DependencyProperty InputSourceProperty = DependencyProperty.Register(nameof(InputSource), typeof (UIElement), typeof (VertexManipulator), new PropertyMetadata(default(UIElement), OnInputSourceChanged));

        private static readonly VertexDirection?[] Directions = Enum.GetValues(typeof(VertexDirection)).Cast<VertexDirection?>().Skip(1).ToArray();

        public static readonly Material DotVertexMaterial;
        public static readonly Material DotAddMaterial;
        public static readonly Material DotDeleteMaterial;
        public static readonly Material DotAutoMaterial;
        public static readonly Material DotTopMaterial;
        public static readonly Material DotLeftMaterial;
        public static readonly Material DotRightMaterial;
        public static readonly Material DotDownMaterial;

        private enum HitType
        {
            None,
            Vertex,
            AddNew,
            Direction
        }
        private delegate void HitTest2DDelegate(int index, HitType type);
        private struct PreviewInfo
        {
            public Vector Position;
            public int InsertionIndex;
        }

        private int m_currentVertexIndex = -1;
        private Material m_vertexMaterial = DotVertexMaterial;
        private PreviewInfo m_previewInfo = new PreviewInfo { InsertionIndex = -1 };

        private readonly LinesVisual3D m_lines;
        private readonly LinesVisual3D m_previewLines;
        private List<BillboardVisual3D> m_vertices;
        private List<BillboardVisual3D> m_addVertexCallouts;
        private List<BillboardVisual3D> m_changeDirectionCallouts;

        public DynamicMesh Source
        {
            get { return (DynamicMesh) GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        public UIElement InputSource
        {
            get { return (UIElement)GetValue(InputSourceProperty); }
            set { SetValue(InputSourceProperty, value); }
        }

        static VertexManipulator()
        {
            DotVertexMaterial = Utils.CreateImageMaterial(Utils.LoadBitmapFromResource("Resources/dot.png"));
            DotAddMaterial = Utils.CreateImageMaterial(Utils.LoadBitmapFromResource("Resources/dot-add.png"));
            DotDeleteMaterial = Utils.CreateImageMaterial(Utils.LoadBitmapFromResource("Resources/dot-delete.png"));
            DotAutoMaterial = Utils.CreateImageMaterial(Utils.LoadBitmapFromResource("Resources/dot-auto.png"));

            DiffuseMaterial dotDirMat = Utils.CreateImageMaterial(Utils.LoadBitmapFromResource("Resources/dot-dir.png"), false, false);
            DotTopMaterial = new DiffuseMaterial((Brush)dotDirMat.Brush.GetCurrentValueAsFrozen());

            dotDirMat.Brush.RelativeTransform = new RotateTransform(-90, 0.5, 0.5);
            DotLeftMaterial = new DiffuseMaterial((Brush)dotDirMat.Brush.GetCurrentValueAsFrozen());

            dotDirMat.Brush.RelativeTransform = new RotateTransform(90, 0.5, 0.5);
            DotRightMaterial = new DiffuseMaterial((Brush)dotDirMat.Brush.GetCurrentValueAsFrozen());

            dotDirMat.Brush.RelativeTransform = new RotateTransform(180, 0.5, 0.5);
            DotDownMaterial = new DiffuseMaterial((Brush)dotDirMat.Brush.GetCurrentValueAsFrozen());

            DotVertexMaterial.Freeze();
            DotAddMaterial.Freeze();
            DotDeleteMaterial.Freeze();
            DotAutoMaterial.Freeze();
            DotTopMaterial.Freeze();
            DotLeftMaterial.Freeze();
            DotRightMaterial.Freeze();
            DotDownMaterial.Freeze();
        }
        public VertexManipulator()
        {
            m_lines = new LinesVisual3D {Color =  Colors.White, Thickness = 2};
            m_previewLines = new LinesVisual3D {Color =  Colors.PaleGreen, Thickness = 2};
        }

        private static void OnSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            VertexManipulator instance = (VertexManipulator) obj;

            DynamicMesh newMesh = (DynamicMesh) args.NewValue;
            DynamicMesh oldMesh = (DynamicMesh) args.OldValue;


            if (oldMesh != null)
                instance.UnregisterSource(oldMesh);

            if (newMesh != null)
                instance.RegisterSource();
        }
        private static void OnInputSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            VertexManipulator instance = (VertexManipulator)d;

            UIElement oldInputSource = e.OldValue as UIElement;
            UIElement newInputSource = e.NewValue as UIElement;

            if (oldInputSource != null)
                instance.UnregisterInputSource(oldInputSource);

            if (newInputSource != null)
                instance.RegisterInputSource();
        }

        private void RegisterSource()
        {
            Source.RecursivePropertyChanged += SourceOnRecursivePropertyChanged;
            SourceOnRecursivePropertyChanged(null,null);
        }

        private void UnregisterSource(DynamicMesh oldMesh)
        {
            oldMesh.RecursivePropertyChanged -= SourceOnRecursivePropertyChanged;
        }

        private void RegisterInputSource()
        {
            InputSource.MouseDown += StartManipulation;
            InputSource.MouseMove += DeltaManipulation;
            InputSource.MouseUp += EndManipulation;

            InputSource.KeyDown += ModifierActivated;
            InputSource.KeyUp += ModifierDeactivated;
        }
        private void UnregisterInputSource(UIElement oldInputSource)
        {
            oldInputSource.MouseDown -= StartManipulation;
            oldInputSource.MouseMove -= DeltaManipulation;
            oldInputSource.MouseUp -= EndManipulation;
            InputSource.KeyDown -= ModifierActivated;
            InputSource.KeyUp -= ModifierDeactivated;
        }

        private void StartManipulation(object sender, MouseButtonEventArgs e)
        {
            if (Source == null || e.ChangedButton != MouseButton.Left)
                return;

            var element = (UIElement)sender;
            var position = e.GetPosition(element);

            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.None:
                    FeedHitTest(position, (index, type) =>
                    {
                        switch (type)
                        {
                            case HitType.AddNew:
                                Source.Vertices.Insert(index + 1, new VertexInfo(m_addVertexCallouts[index].Position.ToVector(), Source.Vertices[index].Direction));
                                break;
                            case HitType.Vertex:
                                m_currentVertexIndex = index;
                                break;
                            case HitType.Direction:
                                VertexInfo vertex = Source.Vertices[index];
                                vertex.Direction = Directions.CircularIndex(Array.IndexOf(Directions, vertex.Direction) + 1, true).Value;
                                break;
                        }
                    });
                    break;
                case ModifierKeys.Control:
                    FeedHitTest(position, (index, type) =>
                    {
                        if (type == HitType.Vertex)
                        {
                            if (Source.Vertices.Count > 2)
                                Source.Vertices.RemoveAt(index);

                            if (Source.Vertices.Count < 3)
                            {
                                m_vertexMaterial = DotVertexMaterial;
                                Tesellate();
                            }
                        }
                    });
                    break;
                case ModifierKeys.Shift:
                    if (m_previewInfo.InsertionIndex != -1)
                    {
                        var index = m_previewInfo.InsertionIndex;
                        Source.Vertices.Insert(index, new VertexInfo(m_previewInfo.Position, index == 0 ? VertexDirection.Auto : Source.Vertices[index - 1].Direction));
                        m_previewInfo.InsertionIndex = -1;
                    }
                break;
            }

        }
        private void DeltaManipulation(object sender, MouseEventArgs e)
        {
            if (Source == null)
                return;

            var element = (UIElement) sender;
            var position = e.GetPosition(element);

            if (m_currentVertexIndex != -1)
            {
                VertexInfo vertex = Source.Vertices[m_currentVertexIndex];
                vertex.Position = ScreenPointToWorld(position).ToVector();
            }

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var worldPos = ScreenPointToWorld(position).ToVector();

                var closestPoints = 
                    Source.Vertices.Select((p, i) => new { Distance = (p.Position - worldPos).LengthSquared, Index = i, Point = p.Position })
                        .OrderBy(arg => arg.Distance)
                        .ToArray();

                var closest = closestPoints[0];
                var secondClosest = closestPoints.First(arg => Math.Abs(arg.Index - closest.Index) == 1);

                var vectorA = worldPos - closest.Point;
                var vectorB = secondClosest.Point - closest.Point;

                vectorA.Normalize();
                vectorB.Normalize();

                var dotProduct = vectorA * vectorB;

                Children.Remove(m_previewLines);
                m_previewLines.Points.Clear();

                if (( closest.Index == 0 || closest.Index == Source.Vertices.Count - 1 ) && dotProduct < 0.5)
                {
                    m_previewLines.Points.Add(worldPos.ToPoint3D());
                    m_previewLines.Points.Add(Source.Vertices[closest.Index].Position.ToPoint3D());
                    m_previewInfo.Position = worldPos;
                    m_previewInfo.InsertionIndex = closest.Index == 0 ? 0 : closest.Index + 1;
                }
                else
                {
                    m_previewLines.Points.Add(closest.Point.ToPoint3D());
                    m_previewLines.Points.Add(worldPos.ToPoint3D());
                    m_previewLines.Points.Add(worldPos.ToPoint3D());
                    m_previewLines.Points.Add(secondClosest.Point.ToPoint3D());
                    m_previewInfo.Position = worldPos;
                    m_previewInfo.InsertionIndex = closest.Index > secondClosest.Index ? closest.Index : secondClosest.Index;
                }

                Children.Add(m_previewLines);

            }
        }
        private void EndManipulation(object sender, MouseButtonEventArgs e)
        {
            if (Source == null || e.ChangedButton != MouseButton.Left)
                return;

            m_currentVertexIndex = -1;
        }
        private void ModifierActivated(object sender, KeyEventArgs e)
        {
            if (Source == null || (e.Key != Key.LeftShift && e.Key != Key.RightShift && e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl))
                return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Source.Vertices.Count > 2 && m_vertexMaterial == DotVertexMaterial)
            {
                m_vertexMaterial = DotDeleteMaterial;
                Tesellate();
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && m_previewInfo.InsertionIndex == -1 && !Children.Contains(m_previewLines))
            {
                Children.Add(m_previewLines);
                DeltaManipulation(InputSource,new MouseEventArgs(Mouse.PrimaryDevice,0));
            }
        }
        private void ModifierDeactivated(object sender, KeyEventArgs e)
        {
            if (Source == null || (e.Key != Key.LeftShift && e.Key != Key.RightShift && e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl))
                return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) == false && m_vertexMaterial == DotDeleteMaterial)
            {
                m_vertexMaterial = DotVertexMaterial;
                Tesellate();
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) == false)
            {
                if (Children.Contains(m_previewLines))
                    Children.Remove(m_previewLines);
                m_previewInfo.InsertionIndex = -1;
            }
        }

        private Point3D ScreenPointToWorld(Point point)
        {
            var ray = this.GetViewport3D().Point2DtoRay3D(point);
            var swp = ray.PlaneIntersection(Source.Mesh.Bounds.Location, new Vector3D(0, 0, -1)).Value;

            return Transform.Inverse.Transform(swp);
        }
        private void FeedHitTest(Point position, HitTest2DDelegate callBack)
        {
            var callout = this.GetViewport3D().FindNearestVisual(position) as BillboardVisual3D;

            if (callout == null)
            {
                callBack(-1, HitType.None);
                return;
            }

            var index = m_addVertexCallouts.IndexOf(callout);

            if (index != -1)
            {
                callBack(index, HitType.AddNew);
                return;
            }

            index = m_vertices.IndexOf(callout);

            if (index != -1)
            {
                if (index == Source.Vertices.Count) index = 0;

                callBack(index, HitType.Vertex);
                return;
            }


            index = m_changeDirectionCallouts.IndexOf(callout);

            if (index != -1)
            {
                callBack(index, HitType.Direction);
                return;
            }
        }
        private void SourceOnRecursivePropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            Tesellate();
        }

        public void Tesellate()
        {
            Children.Clear();

            if (Source == null)
                return;

            var vertices = (Source.IsClosed ? Source.Vertices.Concat(Source.Vertices[0]) : Source.Vertices).ToArray();

            //Lines
            m_lines.Points = new Point3DCollection(vertices.Pairwise((fst, snd) => new {fst, snd}).SelectMany(pair => new[]
            {
                pair.fst.Position.ToPoint3D(), pair.snd.Position.ToPoint3D(),
            }));
            Children.Add(m_lines);

            //Vertices
            m_vertices = vertices.Select(info => new BillboardVisual3D
            {
                Position = info.Position.ToPoint3D(0.02),
                Width = 15,
                Height = 15,
                Material = m_vertexMaterial
            }).ToList();
            m_vertices.ForEach(Children.Add);

            //AddVertex
            m_addVertexCallouts = vertices.Pairwise((fst, snd) => new {fst, snd}).Select(info => new BillboardVisual3D
            {
                Position = Utils.LinearLerp(info.fst.Position, info.snd.Position, 0.5).ToPoint3D(0.01),
                Width = 15,
                Height = 15,
                Material = DotAddMaterial
            }).ToList();
            m_addVertexCallouts.ForEach(Children.Add);

            //Direction
            m_changeDirectionCallouts = vertices.Pairwise((fst, snd) => new {fst, snd}).Select(info =>
            {
                Material mat;

                switch (info.fst.Direction)
                {
                case VertexDirection.Auto: mat = DotAutoMaterial; break;
                case VertexDirection.Top:  mat = DotTopMaterial; break;
                case VertexDirection.Down: mat = DotDownMaterial; break;
                case VertexDirection.Left: mat = DotLeftMaterial; break;
                case VertexDirection.Right:mat = DotRightMaterial; break;
                default: throw new ArgumentOutOfRangeException();
                }

                return new BillboardVisual3D
                {
                    Position = Utils.LinearLerp(info.fst.Position, info.snd.Position, 0.1).ToPoint3D(0.01),
                    Width = 15,
                    Height = 15,
                    Material = mat
                };
            }).ToList();
            m_changeDirectionCallouts.ForEach(Children.Add);

            //Transform
            var transform = Source.Mesh.Transform;
            Source.Mesh.Transform = Transform3D.Identity;
            Transform = new Transform3DGroup
            {
                Children = new Transform3DCollection
                {
                    transform,
                    new TranslateTransform3D(0, 0, Source.Mesh.Bounds.SizeZ + Source.Mesh.Bounds.Z + 0.01)
                }
            };
            Source.Mesh.Transform = transform;
        }
    }
}