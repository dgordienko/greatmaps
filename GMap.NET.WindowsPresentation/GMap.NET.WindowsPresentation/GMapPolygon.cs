
 // ReSharper disable once CheckNamespace
namespace GMap.NET.WindowsPresentation
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Windows.Shapes;

    // ReSharper disable once UnusedMember.Global
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "ArrangeThisQualifier")]
    public sealed class GMapPolygon : GMapMarker, IShapable
    {
        public readonly List<PointLatLng> Points = new List<PointLatLng>();

        public GMapPolygon(IEnumerable<PointLatLng> points)
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
        /// regenerates shape of polygon
        /// </summary>
        public void RegenerateShape(GMapControl map)
        {
            if (map == null) return;
            Map = map;                 
            if(Points.Count > 1)
            {
                Position = Points[0];                   
                var localPath = new List<System.Windows.Point>(Points.Count);
                var offset = Map.FromLatLngToLocal(Points[0]);
                localPath.AddRange(Points.Select(i => Map.FromLatLngToLocal(i))
                    .Select(p => new System.Windows.Point(p.X - offset.X, p.Y - offset.Y)));
                var shape = map.CreatePolygonPath(localPath);
                var path = Shape as Path;
                if(path != null)
                {
                    path.Data = shape.Data;
                }
                else
                {
                    Shape = shape;
                }
            }
            else
            {
                Shape = null;
            }
        }
    }
}
