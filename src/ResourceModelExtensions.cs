using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Digitalisert.Raven
{
    public static class ResourceModelExtensions
    {
        public static IEnumerable<dynamic> Properties(IEnumerable<dynamic> properties)
        {
            foreach(var propertyG in ((IEnumerable<dynamic>)properties).GroupBy(p => p.Name))
            {
                var name = propertyG.Key;
                var value = propertyG.SelectMany(p => (IEnumerable<dynamic>)p.Value).Distinct();
                var tags = propertyG.SelectMany(p => (IEnumerable<dynamic>)p.Tags).Distinct();
                var resources = propertyG.SelectMany(p => (IEnumerable<dynamic>)p.Resources).Distinct();

                if (tags.Contains("@wkt"))
                {
                    var wktreader = new WKTReader();
                    var geometries = value.Select(v => v.ToString()).Cast<string>().Select(v => wktreader.Read(v));

                    if (geometries.Any(g => !g.IsValid))
                    {
                        tags = tags.Union(new[] { "@invalid" }).Distinct();
                        geometries = geometries.Select(g => (g.IsValid) ? g : g.Buffer(0));
                    }

                    if (tags.Contains("@union"))
                    {
                        value = new[] { new NetTopologySuite.Operation.Union.UnaryUnionOp(geometries.ToList()).Union().ToString() };
                    }
                }

                yield return new {
                    Name = name,
                    Value = value,
                    Tags = tags,
                    Resources = resources,
                };
            }
        }

        public static string GenerateHash(string str)
        {
            using (var md5Hasher = System.Security.Cryptography.MD5.Create())
            {
                var data = md5Hasher.ComputeHash(System.Text.Encoding.Default.GetBytes(str));
                return Convert.ToBase64String(data).Substring(0,2);
            }
        }

        public static IEnumerable<string> WKTEncodeGeohash(string wkt)
        {
            foreach (var geohash in WKTEncodeGeohash(wkt, FindGeohashPrecision(wkt)))
            {
                yield return geohash;
            }
        }

        private static int FindGeohashPrecision(string wkt)
        {
            var geometryEnvelope = new WKTReader().Read(wkt).EnvelopeInternal;
            var geohasher = new Geohash.Geohasher();

            foreach (var precision in Enumerable.Range(1, 7))
            {
                var geohash = geohasher.Encode(geometryEnvelope.Centre.Y, geometryEnvelope.Centre.X, precision);
                var geohashsize = geohasher.GetBoundingBox(geohash);

                var geohashEnvelope = new Envelope(geohashsize[2], geohashsize[3], geohashsize[0], geohashsize[1]);

                if (geometryEnvelope.Width > geohashEnvelope.Width || geometryEnvelope.Height > geohashEnvelope.Height)
                {
                    return precision;
                }
            }

            return 8;
        }

        private static IEnumerable<string> WKTEncodeGeohash(string wkt, int precision)
        {
            var geometry = new WKTReader().Read(wkt);

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

                    shapeFactory.Centre = new Coordinate(geohashdecoded.Item2, geohashdecoded.Item1);

                    if (shapeFactory.CreateRectangle().Intersects(geometry))
                    {
                        yield return geohash;
                    }
                }
            }
        }

        public static bool WKTIntersects(string wkt1, string wkt2)
        {
            var wktreader = new WKTReader();
            return wktreader.Read(wkt1).Intersects(wktreader.Read(wkt2));
        }

        public static string WKTBuffer(string wkt, int distance)
        {
            var geometry = new WKTReader().Read(wkt);
            var geometryUTM = Transform(geometry, GeographicCoordinateSystem.WGS84, ProjectedCoordinateSystem.WGS84_UTM(33, true));

            return Transform(geometryUTM.Buffer(distance), ProjectedCoordinateSystem.WGS84_UTM(33, true), GeographicCoordinateSystem.WGS84).ToString();
        }

        public static double WKTArea(string wkt)
        {
            var geometry = new WKTReader().Read(wkt);

            return Transform(geometry, GeographicCoordinateSystem.WGS84, ProjectedCoordinateSystem.WGS84_UTM(33, true)).Area;
        }

        public static string WKTProjectToWGS84(string wkt, int fromsrid)
        {
            var geometry = new WKTReader().Read(wkt);

            return Transform(geometry, ProjectedCoordinateSystem.WGS84_UTM(33, true), GeographicCoordinateSystem.WGS84).ToString();
        }

        private static Geometry Transform(this Geometry geometry, CoordinateSystem from, CoordinateSystem to)
        {
            var transformation = new CoordinateTransformationFactory().CreateFromCoordinateSystems(from, to);

            geometry = geometry.Copy();
            geometry.Apply(new MathTransformFilter(transformation.MathTransform));
            return geometry;
        }

        private sealed class MathTransformFilter : ICoordinateSequenceFilter
        {
            private readonly MathTransform _mathTransform;

            public MathTransformFilter(MathTransform mathTransform)
                => _mathTransform = mathTransform;

            public bool Done => false;
            public bool GeometryChanged => true;

            public void Filter(CoordinateSequence seq, int i)
            {
                (double x, double y, double z) = ((double, double, double))_mathTransform.Transform(seq.GetX(i), seq.GetY(i), seq.GetZ(i));
                seq.SetX(i, x);
                seq.SetY(i, y);
                seq.SetZ(i, z);
            }
        }
    }
}
