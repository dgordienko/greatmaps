
// ReSharper disable once CheckNamespace
namespace GMap.NET.WindowsPresentation
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Windows.Shapes;

    public interface IShapable
    {
        void RegenerateShape(GMapControl map);
    }

    // ReSharper disable once UnusedMember.Global
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "ArrangeThisQualifier")]
    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
    public class GMapRoute : GMapMarker, IShapable
    {
        public readonly List<PointLatLng> Points = new List<PointLatLng>();

        public GMapRoute(IEnumerable<PointLatLng> points)
        {
            Points.AddRange(points);
            RegenerateShape(null);
        }

        protected override void Clear()
        {
            base.Clear();
            Points.Clear();
        }

        /// <summary>
        /// regenerates shape of route
        /// </summary>
        public virtual void RegenerateShape(GMapControl map)
        {
            if (map == null) return;
            this.Map = map;

            if (this.Points.Count > 1)
            {
                this.Position = this.Points[0];

                var localPath = new List<System.Windows.Point>(this.Points.Count);
                var offset = this.Map.FromLatLngToLocal(this.Points[0]);
                localPath.AddRange(this.Points.Select(i => this.Map.FromLatLngToLocal(i))
                    .Select(p => new System.Windows.Point(p.X - offset.X, p.Y - offset.Y)));
                var shape = map.CreateRoutePath(localPath);
                var path = this.Shape as Path;
                if (path != null)
                {
                    path.Data = shape.Data;
                }
                else
                {
                    this.Shape = shape;
                }
            }
            else
            {
                this.Shape = null;
            }
        }
    }
}
