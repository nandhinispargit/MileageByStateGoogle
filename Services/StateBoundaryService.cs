using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using MileageByStateGoogle.Models;

namespace MileageByStateGoogle.Services;

public class StateBoundaryService
{
    private readonly FeatureCollection _states;

    public StateBoundaryService()
    {
        var reader = new GeoJsonReader();
        var json = File.ReadAllText("Data/us_states.json");
        _states = reader.Read<FeatureCollection>(json);
    }

    public List<StateMileage> CalculateMileage(LineString route)
    {
        var result = new List<StateMileage>();

        foreach (var feature in _states)
        {
            // string state =
            //   feature.Attributes.Exists("NAME")
            //    ? feature.Attributes["NAME"].ToString()
            //    : feature.Attributes["name"].ToString();

            string state =
          feature.Attributes.Exists("STUSPS")
          ? feature.Attributes["STUSPS"].ToString()
          : feature.Attributes["stusps"].ToString();

            var geometry = feature.Geometry;

            if (!route.EnvelopeInternal.Intersects(geometry.EnvelopeInternal))
                continue;

            if (!route.Intersects(geometry))
                continue;

            var clipped = route.Intersection(geometry);

            double miles = CalculateMiles(clipped);

            if (miles > 0.1)
            {
                result.Add(new StateMileage
                {
                    State = state,
                    Miles = miles
                });
            }
        }

        return result;
    }

    private double CalculateMiles(Geometry geometry)
    {
        double miles = 0;

        for (int i = 0; i < geometry.NumGeometries; i++)
        {
            if (geometry.GetGeometryN(i) is LineString line)
            {
                var coords = line.Coordinates;

                for (int k = 0; k < coords.Length - 1; k++)
                {
                    miles += Haversine(
                        coords[k].Y, coords[k].X,
                        coords[k + 1].Y, coords[k + 1].X);
                }
            }
        }

        return miles;
    }

    private double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 3958.8;

        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) *
            Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) *
            Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    public bool IsPointInState(double lat, double lon, string stateCode)
    {
        var point = new NetTopologySuite.Geometries.Point(lon, lat);

        foreach (var feature in _states)
        {
            string state =
                feature.Attributes.Exists("STUSPS")
                ? feature.Attributes["STUSPS"].ToString()
                : feature.Attributes["stusps"].ToString();

            if (state != stateCode)
                continue;

            if (feature.Geometry.Contains(point))
                return true;
        }

        return false;
    }

    private double ToRad(double d) => d * Math.PI / 180;
}