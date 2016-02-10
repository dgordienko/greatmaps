
 // ReSharper disable once CheckNamespace
namespace GMap.NET.WindowsPresentation
{
    using System.ComponentModel;
    using System.Windows;

    using GMap.NET;
    using System.Windows.Media;
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// GMap.NET marker
    /// </summary>
    [SuppressMessage("ReSharper", "ArrangeThisQualifier")]
    [SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class GMapMarker : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void OnPropertyChanged(PropertyChangedEventArgs name)
        {
            PropertyChanged?.Invoke(this, name);
        }

        private UIElement shape;
        static readonly PropertyChangedEventArgs ShapePropertyChangedEventArgs = new PropertyChangedEventArgs("Shape");

        /// <summary>
        /// marker visual
        /// </summary>
        public UIElement Shape
        {
            get
            {
                return shape;
            }
            set
            {
                // ReSharper disable once PossibleUnintendedReferenceComparison
                if (shape == value) return;
                shape = value;
                OnPropertyChanged(ShapePropertyChangedEventArgs);

                UpdateLocalPosition();
            }
        }

        private PointLatLng position;

        /// <summary>
        /// coordinate of marker
        /// </summary>
        public PointLatLng Position
        {
            get
            {
                return position;
            }
            set
            {
                if (position == value) return;
                position = value;
                UpdateLocalPosition();
            }
        }

        private GMapControl map;

        /// <summary>
        /// the map of this marker
        /// </summary>
        public GMapControl Map
        {
            get
            {
                if (Shape == null || map != null) return map;
                DependencyObject visual = Shape;
                while (visual != null && !(visual is GMapControl))
                {
                    visual = VisualTreeHelper.GetParent(visual);
                }
                map = visual as GMapControl;
                return map;
            }
            internal set
            {
                map = value;
            }
        }

        /// <summary>
        /// custom object
        /// </summary>
        public object Tag;

        private Point offset;
        /// <summary>
        /// offset of marker
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public Point Offset
        {
            get
            {
                return offset;
            }
            set
            {
                if (offset != value)
                {
                    offset = value;
                    UpdateLocalPosition();
                }
            }
        }

        private int localPositionX;
        static readonly PropertyChangedEventArgs LocalPositionXPropertyChangedEventArgs = new PropertyChangedEventArgs("LocalPositionX");

        /// <summary>
        /// local X position of marker
        /// </summary>
        public int LocalPositionX
        {
            get
            {
                return localPositionX;
            }
            internal set
            {
                if (localPositionX != value)
                {
                    localPositionX = value;
                    OnPropertyChanged(LocalPositionXPropertyChangedEventArgs);
                }
            }
        }

        private int localPositionY;
        static readonly PropertyChangedEventArgs LocalPositionYPropertyChangedEventArgs = new PropertyChangedEventArgs("LocalPositionY");

        /// <summary>
        /// local Y position of marker
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public int LocalPositionY
        {
            get
            {
                return localPositionY;
            }
            internal set
            {
                if (localPositionY != value)
                {
                    localPositionY = value;
                    OnPropertyChanged(LocalPositionYPropertyChangedEventArgs);
                }
            }
        }

        private int zIndex;
        static readonly PropertyChangedEventArgs ZIndexPropertyChangedEventArgs = new PropertyChangedEventArgs("ZIndex");

        /// <summary>
        /// the index of Z, render order
        /// </summary>
        public int ZIndex
        {
            get
            {
                return zIndex;
            }
            set
            {
                if (zIndex == value) return;
                zIndex = value;
                OnPropertyChanged(ZIndexPropertyChangedEventArgs);
            }
        }

        public GMapMarker(PointLatLng pos)
        {
            Position = pos;
        }

        internal GMapMarker()
        {
        }

        /// <summary>
        /// calls Dispose on shape if it implements IDisposable, sets shape to null and clears route
        /// </summary>
        // ReSharper disable once UnusedMemberHiearchy.Global
        protected virtual void Clear()
        {
            var s = Shape as IDisposable;
            s?.Dispose();
            Shape = null;
        }

        /// <summary>
        /// updates marker position, internal access usualy
        /// </summary>
        void UpdateLocalPosition()
        {
            if (Map == null) return;
            var p = Map.FromLatLngToLocal(Position);
            p.Offset(-(long)Map.MapTranslateTransform.X, -(long)Map.MapTranslateTransform.Y);

            LocalPositionX = (int)(p.X + (long)Offset.X);
            LocalPositionY = (int)(p.Y + (long)Offset.Y);
        }

        /// <summary>
        /// forces to update local marker  position
        /// dot not call it if you don't really need to ;}
        /// </summary>
        /// <param name="m"></param>
        internal void ForceUpdateLocalPosition(GMapControl m)
        {
            if (m != null)
            {
                map = m;
            }
            UpdateLocalPosition();
        }
    }
}