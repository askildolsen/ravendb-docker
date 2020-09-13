using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.IO;
using Newtonsoft.Json;
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

        public static IEnumerable<string> ResourceFormat(string value, dynamic resource)
        {
            var formatter = SmartFormat.Smart.CreateDefaultSmartFormat();
            formatter.Parser.AddAdditionalSelectorChars("æøå");
            formatter.Settings.FormatErrorAction = SmartFormat.Core.Settings.ErrorAction.Ignore;

            return formatter.Format(value, ResourceFormatData(resource)).Split(new[] { '\n' } , StringSplitOptions.RemoveEmptyEntries);
        }

        private static Dictionary<string, object> ResourceFormatData(dynamic resource)
        {
            var resourceData = new Dictionary<string, object>() { 
                { "Context", resource.Context ?? "" },
                { "ResourceId", resource.ResourceId ?? "" },
                { "Type", ((IEnumerable<dynamic>)resource.Type ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "SubType", ((IEnumerable<dynamic>)resource.SubType ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "Title", ((IEnumerable<dynamic>)resource.Title ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "SubTitle", ((IEnumerable<dynamic>)resource.SubTitle ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "Code", ((IEnumerable<dynamic>)resource.Code ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "Status", ((IEnumerable<dynamic>)resource.Status ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "Tags", ((IEnumerable<dynamic>)resource.Tags ?? new object[] { }).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToArray() },
                { "Properties", new Dictionary<string, object>() }
            };

            foreach(var property in ((IEnumerable<dynamic>)resource.Properties ?? new object[] { }) ) {
                if (!property.Name.StartsWith("@")) {
                    var value = ((IEnumerable<dynamic>)property.Value ?? new object[] {}).Select(v => v.ToString()).Where(v => !String.IsNullOrWhiteSpace(v)).ToList();
                    var resources = ((IEnumerable<dynamic>)property.Resources ?? new object[] {}).Select(r => ResourceFormatData(r)).ToList();

                    if ((value.Any() || resources.Any()) && !resourceData.ContainsKey(property.Name)) {
                        resourceData.Add(property.Name, value.Union(resources.SelectMany(r => (IEnumerable<dynamic>)r["Title"]).Where(v => !String.IsNullOrWhiteSpace(v))).Distinct());
                    }

                    if ((value.Any() || resources.Any()) && !((Dictionary<string, object>)resourceData["Properties"]).ContainsKey(property.Name)) {
                        ((Dictionary<string, object>)resourceData["Properties"]).Add(property.Name, new Dictionary<string, object>() { 
                            { "Value", value },
                            { "Resources", resources }
                        });
                    }
                }
            }

            return resourceData;
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
            var geometry = new WKTReader().Read(wkt);
            var convexhull = new NetTopologySuite.Algorithm.ConvexHull(geometry).GetConvexHull();
            foreach (var geohash in WKTEncodeGeohash(geometry, convexhull, FindGeohashPrecision(convexhull) + 1))
            {
                yield return geohash;
            }
        }

        public static string WKTDecodeGeohash(string geohash)
        {   
            return WKTDecodeGeohashImpl(geohash).ToString();
        }

        private static Geometry WKTDecodeGeohashImpl(string geohash)
        {
            var geohasher = new Geohash.Geohasher();
            var geohashsize = geohasher.GetBoundingBox(geohash);
            var shapeFactory = new NetTopologySuite.Utilities.GeometricShapeFactory();
            shapeFactory.Height = geohashsize[1] - geohashsize[0];
            shapeFactory.Width = geohashsize[3] - geohashsize[2];
            shapeFactory.NumPoints = 4;

            var geohashdecoded = geohasher.Decode(geohash);

            shapeFactory.Centre = new Coordinate(geohashdecoded.Item2, geohashdecoded.Item1);
            
            return shapeFactory.CreateRectangle();
        }

        private static int FindGeohashPrecision(Geometry geometry)
        {
            var geometryEnvelope = geometry.EnvelopeInternal;
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

        private static IEnumerable<string> WKTEncodeGeohash(Geometry geometry, Geometry convexhull, int precision)
        {
            var geometryPrepared = new PreparedGeometryFactory().Create(geometry);
            var convexhullPrepared = new PreparedGeometryFactory().Create(convexhull);

            var geohasher = new Geohash.Geohasher();
            var geohashbase = geohasher.Encode(convexhull.EnvelopeInternal.MinY, convexhull.EnvelopeInternal.MinX, precision);
            var baserectangle = WKTDecodeGeohashImpl(geohashbase);

            for (double y = convexhull.EnvelopeInternal.MinY - baserectangle.EnvelopeInternal.Height; y <= convexhull.EnvelopeInternal.MaxY + baserectangle.EnvelopeInternal.Height; y += baserectangle.EnvelopeInternal.Height)
            {
                for (double x = convexhull.EnvelopeInternal.MinX - baserectangle.EnvelopeInternal.Width; x <= convexhull.EnvelopeInternal.MaxX + baserectangle.EnvelopeInternal.Width; x += baserectangle.EnvelopeInternal.Width)
                {
                    var geohash = geohasher.Encode(y, x, precision);
                    var rectangle = WKTDecodeGeohashImpl(geohash);

                    if (convexhullPrepared.Intersects(rectangle))
                    {
                        if (geometryPrepared.Covers(rectangle))
                        {
                            yield return geohash + "+";
                        }
                        else if (geometryPrepared.Intersects(rectangle))
                        {
                            yield return geohash;
                        }
                    }
                }
            }
        }

        public static string WKTEnvelope(string wkt)
        {
            var wktreader = new WKTReader();
            return wktreader.Read(wkt).Envelope.ToString();
        }

        public static string WKTConvexHull(string wkt)
        {
            var wktreader = new WKTReader();
            return new NetTopologySuite.Algorithm.ConvexHull(wktreader.Read(wkt)).GetConvexHull().ToString();
        }

        public static IEnumerable<dynamic> WKTIntersectingProperty(IEnumerable<dynamic> wkts, IEnumerable<dynamic> properties)
        {
            var wktreader = new WKTReader();
            var factory = new PreparedGeometryFactory();
            var geometries = wkts.Select(v => wktreader.Read(v)).Select(g => factory.Create(g)).ToList();

            foreach (dynamic property in properties)
            {
                var result = JsonConvert.DeserializeAnonymousType(JsonConvert.SerializeObject(property), new { Value = new string[] { } });

                var comparegeometries = ((IEnumerable<dynamic>)result.Value).Select(v => wktreader.Read(v));

                if (geometries.Any(g => comparegeometries.Any(cg => g.Intersects(cg) )))
                {
                    yield return property;
                }
            }
        }

        public static bool WKTIntersects(string wkt1, string wkt2)
        {
            var wktreader = new WKTReader();
            var geometryPrepared = new PreparedGeometryFactory().Create(wktreader.Read(wkt1));

            return geometryPrepared.Intersects(wktreader.Read(wkt2));
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
