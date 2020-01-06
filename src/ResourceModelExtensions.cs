using System.Collections.Generic;
using System.Linq;

namespace Digitalisert.Raven
{
    public static class ResourceModelExtensions
    {
        public static IEnumerable<dynamic> Properties(IEnumerable<dynamic> properties)
        {
            foreach(var propertyG in ((IEnumerable<dynamic>)properties).GroupBy(p => p.Name))
            {
                if (propertyG.Any(p => p.Tags.Contains("@union")))
                {
                    yield
                        return new {
                            Name = propertyG.Key,
                            Value = propertyG.SelectMany(p => (IEnumerable<dynamic>)p.Value).Distinct(),
                            Tags = propertyG.SelectMany(p => (IEnumerable<dynamic>)p.Tags).Distinct(),
                            Resources = propertyG.SelectMany(p => (IEnumerable<dynamic>)p.Resources).Distinct(),
                        };
                }
                else
                {
                    foreach(var property in propertyG.Distinct())
                    {
                        yield return property;
                    }
                }
            }
        }

        public static IEnumerable<string> WKTEncodeGeohash(string wkt, int precision)
        {
            var geometry = new NetTopologySuite.IO.WKTReader().Read(wkt);

            var geohasher = new Geohash.Geohasher();
            var geohashsize = geohasher.GetBoundingBox(geohasher.Encode(geometry.EnvelopeInternal.MinY, geometry.EnvelopeInternal.MinX, precision));

            var shapeFactory = new NetTopologySuite.Utilities.GeometricShapeFactory();
            shapeFactory.Height = geohashsize[1] - geohashsize[0];
            shapeFactory.Width = geohashsize[3] - geohashsize[2];
            shapeFactory.NumPoints = 4;

            for (double y = geometry.EnvelopeInternal.MinY - shapeFactory.Height; y <= geometry.EnvelopeInternal.MaxY + shapeFactory.Height; y += shapeFactory.Height)
            {
                for (double x = geometry.EnvelopeInternal.MinX - shapeFactory.Width; x <= geometry.EnvelopeInternal.MaxX + shapeFactory.Width; x += shapeFactory.Width)
                {
                    var geohash = geohasher.Encode(y, x, precision);
                    var geohashdecoded = geohasher.Decode(geohash);

                    shapeFactory.Centre = new NetTopologySuite.Geometries.Coordinate(geohashdecoded.Item2, geohashdecoded.Item1);

                    if (shapeFactory.CreateRectangle().Intersects(geometry))
                    {
                        yield return geohash;
                    }
                }
            }
        }

        public static bool WKTIntersects(string wkt1, string wkt2)
        {
            var wktreader = new NetTopologySuite.IO.WKTReader();
            return wktreader.Read(wkt1).Intersects(wktreader.Read(wkt2));
        }

        public static string WKTProjectToWGS84(string wkt, int fromsrid)
        {
            var geometry = new NetTopologySuite.IO.WKTReader().Read(wkt);

            ProjNet.CoordinateSystems.CoordinateSystem utm = ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(33, true) as ProjNet.CoordinateSystems.CoordinateSystem;
            ProjNet.CoordinateSystems.CoordinateSystem wgs84 = ProjNet.CoordinateSystems.GeographicCoordinateSystem.WGS84 as ProjNet.CoordinateSystems.CoordinateSystem;

            var transformation = new ProjNet.CoordinateSystems.Transformations.CoordinateTransformationFactory().CreateFromCoordinateSystems(utm, wgs84);

            return Digitalisert.Raven.ResourceModelExtensions.Transform(geometry, transformation.MathTransform).ToString();
        }

        private static NetTopologySuite.Geometries.Geometry Transform(this NetTopologySuite.Geometries.Geometry geometry, ProjNet.CoordinateSystems.Transformations.MathTransform mathTransform)
        {
            geometry = geometry.Copy();
            geometry.Apply(new MathTransformFilter(mathTransform));
            return geometry;
        }

        private sealed class MathTransformFilter : NetTopologySuite.Geometries.ICoordinateSequenceFilter
        {
            private readonly ProjNet.CoordinateSystems.Transformations.MathTransform _mathTransform;

            public MathTransformFilter(ProjNet.CoordinateSystems.Transformations.MathTransform mathTransform)
                => _mathTransform = mathTransform;

            public bool Done => false;
            public bool GeometryChanged => true;

            public void Filter(NetTopologySuite.Geometries.CoordinateSequence seq, int i)
            {
                (double x, double y, double z) = ((double, double, double))_mathTransform.Transform(seq.GetX(i), seq.GetY(i), seq.GetZ(i));
                seq.SetX(i, x);
                seq.SetY(i, y);
                seq.SetZ(i, z);
            }
        }
    }
}
