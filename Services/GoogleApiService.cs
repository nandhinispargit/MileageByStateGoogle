using System.Net.Http.Json;
using System.Web;
using MileageByStateGoogle.Models;

namespace MileageByStateGoogle.Services;

public class GoogleApiService
{
    private readonly HttpClient _http = new();
    private readonly string _apiKey;

    private readonly Dictionary<string, string> _stateCache = new();
    public GoogleApiService(string apiKey)
    {
        _apiKey = apiKey;
    }

    // ---------------------------------------------------------
    // Get Google Directions route (polyline + distance)
    // ---------------------------------------------------------
    public async Task<DirectionsResponse> GetDirections(
        double startLat, double startLon,
        double endLat, double endLon)
    {
        string origin = $"{startLat},{startLon}";
        string dest   = $"{endLat},{endLon}";

        string url =
            $"https://maps.googleapis.com/maps/api/directions/json?" +
            $"origin={origin}&destination={dest}&key={_apiKey}";

        return await _http.GetFromJsonAsync<DirectionsResponse>(url);
    }

    // ---------------------------------------------------------
    // Reverse Geocode → Get State (cached)
    // ---------------------------------------------------------
    public async Task<string> GetState(double lat, double lon)
    {
        string key = $"{lat:F4},{lon:F4}";

        if (_stateCache.TryGetValue(key, out var s))
            return s;

        var url =
            $"https://maps.googleapis.com/maps/api/geocode/json?" +
            $"latlng={lat},{lon}&key={_apiKey}";

        var res = await _http.GetFromJsonAsync<GeocodeResponse>(url);

        var stateComp = res?.results?
            .SelectMany(r => r.address_components)
            .FirstOrDefault(c => c.types.Contains("administrative_area_level_1"));

        string state = stateComp?.short_name ?? "UNK";

        _stateCache[key] = state;
    
        return state;
    }
}