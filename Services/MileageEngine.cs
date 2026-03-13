using MileageByStateGoogle.Models;
using MileageByStateGoogle.Utils;
using MileageByStateGoogle.AppData;
using Serilog;
using System.Diagnostics;
using NetTopologySuite.Geometries;

namespace MileageByStateGoogle.Services;

public class MileageEngine
{
    private readonly GoogleApiService _google;
    private readonly TravelMileageRepository _repo;

    private readonly StateBoundaryService _stateService;

    private readonly Dictionary<string, double> _stateRates = new()
    {
        { "CA", 0.70 },
        { "IL", 0.70 },
        { "MA", 0.70 }
    };

    public MileageEngine(GoogleApiService google, TravelMileageRepository repo, StateBoundaryService stateService)
    {
        _google = google;
        _repo = repo;
        _stateService = stateService;
    }

    public async Task<MileageResult> CalculateMileageByState(
        List<TravelItem> travelItems,
        List<TravelDetail> travelDetails)
    {
        var swFull = Stopwatch.StartNew();

        var output = new List<OutputRecord>();
        var apiStatsList = new List<ApiCallStatsRecord>();

        bool hasHighPayState_thisleg = false;

        var groups = travelDetails.GroupBy(d => new { d.travel_id, d.travel_leg_no });

        foreach (var group in groups)
        {
            string travelId = group.Key.travel_id;
            int travellegno = group.Key.travel_leg_no;
            Log.Information("Processing travel_id: {TravelId}", travelId);

            var sw = Stopwatch.StartNew();

            int directionsCalls = 0;
            int geocodeCalls = 0;
            string hasHighPayState = "N";
            var travelStateApiCounts = new Dictionary<string, int>();

            try
            {
                var travelItem = travelItems.First(t => t.travel_id == travelId && t.travel_leg_no == travellegno);
                var allSegments = new List<StateMileage>();
                if (Math.Abs(travelItem.travel_distance - travelItem.deduct_miles) < 0.01)
                {
                    Log.Information("Travel distance equals deducted miles. Skipping API calculation for travel_id {TravelId}", travelId);
                    var firstLeg = group.First();
                    string startState = await _google.GetState(firstLeg.Start_latitude, firstLeg.Start_longitude);
                    double rate = _stateRates.ContainsKey(startState) ? 0.70 : 0.30;
                    output.Add(new OutputRecord
                    {
                        travel_id = travelId,
                        travel_dt = travelItem.travel_dt,
                        State = startState,
                        Rate = rate,
                        Miles = travelItem.travel_distance,
                        Deducted = travelItem.deduct_miles,
                        Final_Mile = 0,
                        Reimbursement = 0,
                        TravelLegNo = travelItem.travel_leg_no
                    });
                    _repo.InsertTravelMileage(travelId, travelItem.travel_dt, startState, rate, travelItem.travel_distance, travelItem.deduct_miles, 0, 0, travelItem.travel_leg_no, travelItem.merch_no,0);
                    Log.Information("State summary travel_id={TravelId} State={State} Miles={Miles:F2} Deducted={Deduct:F2} Final={Final:F2} TravelLegNo={TravelLegNo}",
                            travelId, startState, travelItem.travel_distance, travelItem.deduct_miles, 0, travelItem.travel_leg_no
                        );

                }
                else //deducted miles are less than travel distance
                {
                    foreach (var leg in group)
                    {
                        try
                        {
                            var counts = await ProcessLeg(allSegments, leg);
                            directionsCalls += counts.Directions;
                            geocodeCalls += counts.Geocodes;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Leg error for travel_id {TravelId}. Continuing.", travelId);
                        }
                    }

                    // APPLY NEW DEDUCTION LOGIC (START-STATE-FIRST)
                    if ((travelItem.start_leg_deduction == "Y") && (travelItem.deduct_miles > 0))
                    {
                        ApplyDeductionStartStateFirst(travelItem.deduct_miles, allSegments, travelId);
                    }
                    // APPLY NEW DEDUCTION LOGIC (START-STATE-FIRST)
                    if ((travelItem.start_leg_deduction == "N") && (travelItem.deduct_miles > 0))
                    {
                        ApplyDeductionFromLastState(travelItem.deduct_miles, allSegments, travelId);
                    }

                    // Aggregate by state
                    var stateAggregates = allSegments
                        .GroupBy(s => s.State)
                        .Select(g => new
                        {
                            State = g.Key,
                            Miles = g.Sum(x => x.Miles),
                            Deducted = g.Sum(x => x.Deducted),
                            Apicalls = travelStateApiCounts.ContainsKey(g.Key) ? travelStateApiCounts[g.Key] : 0,
                            
    
                        })
                        .ToList();

                         hasHighPayState_thisleg = stateAggregates.Any(s => _stateRates.ContainsKey(s.State));
                         int firststate=0;
                         int apicall=0;

                    // Build output rows
                    foreach (var st in stateAggregates)
                    {
                        double rate = _stateRates.ContainsKey(st.State) ? 0.70 : 0.30;
                        double finalMiles = st.Miles - st.Deducted;
                        if(firststate==0)
                        {
                            firststate=1;
                            apicall=directionsCalls;
                        }
                        else
                        {
                            apicall=geocodeCalls;
                        }

                        output.Add(new OutputRecord
                        {
                            travel_id = travelId,
                            travel_dt = travelItem.travel_dt,
                            State = st.State,
                            Rate = rate,
                            Miles = st.Miles,
                            Deducted = st.Deducted,
                            Final_Mile = finalMiles,
                            Reimbursement = finalMiles * rate,
                            TravelLegNo =travelItem.travel_leg_no
                        });
                        if(hasHighPayState_thisleg==true)
                        {
                            _repo.InsertTravelMileage(travelId, travelItem.travel_dt, st.State, rate, st.Miles, st.Deducted, finalMiles, finalMiles * rate, travelItem.travel_leg_no, travelItem.merch_no,apicall);
                        }
                        Log.Information(
                            "State summary travel_id={TravelId} State={State} Miles={Miles:F2} Deducted={Deduct:F2} Final={Final:F2} TravelLegNo={TravelLegNo} Apicall={Apicallcount}",
                           travelId, st.State, st.Miles, st.Deducted, finalMiles, travelItem.travel_leg_no, st.Apicalls);
                    }

                }
                sw.Stop();
                Log.Information(
                    "Completed travel_id {TravelId} in {Seconds:F2}s (Directions={Dir}, Geocode={Geo}, Total={Tot})",
                    travelId, sw.Elapsed.TotalSeconds, directionsCalls, geocodeCalls,
                    directionsCalls + geocodeCalls
                );
                apiStatsList.Add(new ApiCallStatsRecord
                {
                    travel_id = travelId,
                    DirectionsCalls = directionsCalls,
                    GeocodeCalls = geocodeCalls,
                    TotalApiCalls = directionsCalls + geocodeCalls
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FATAL travel_id error {TravelId}. Skipping.", travelId);
            }
        }

        swFull.Stop();
        Log.Information("Full run time: {Seconds:F2}s", swFull.Elapsed.TotalSeconds);

        return new MileageResult
        {
            OutputRecords = output,
            ApiCallStats = apiStatsList
        };
    }

    // ==========================================================
    // PROCESS ONE LEG — (UNCHANGED)
    // ==========================================================
    private async Task<(int Directions, int Geocodes)> ProcessLeg(
        List<StateMileage> result,
        TravelDetail leg)
    {
        int directionsCalls = 0;
        int geocodeCalls = 0;

        var dir = await _google.GetDirections(
            leg.Start_latitude, leg.Start_longitude,
            leg.End_latitude, leg.End_longitude);

        directionsCalls++;

        if (dir == null || dir.routes == null || dir.routes.Count == 0)
        {
            Log.Warning("Google returned NO ROUTE for travel_id {TravelId}", leg.travel_id);
            return (directionsCalls, geocodeCalls);
        }

        if (dir.status == "OVER_QUERY_LIMIT")
        {
            Log.Warning("Google RATE LIMIT HIT for travel_id {TravelId}", leg.travel_id);
        }

        var poly = dir.routes[0].overview_polyline.points;
        var points = PolylineDecoder.Decode(poly);

        Log.Information("Decoded polyline for travel_id {TravelId} with {PointCount} points", leg.travel_id, points.Count);

        if (points.Count < 2)
        {
            Log.Warning("Polyline too short for travel_id {TravelId}", leg.travel_id);
            return (directionsCalls, geocodeCalls);
        }
        //code added 
        var coordinates = points.Select(p => new Coordinate(p.lon, p.lat)).ToArray();

        var routeLine = new LineString(coordinates.Distinct().ToArray());

        Log.Information("Route LineString created for travel_id {TravelId}", leg.travel_id);

        // CALCULATE STATE MILEAGE

        var stateMiles = _stateService.CalculateMileage(routeLine);

        if (stateMiles == null || stateMiles.Count == 0)
        {
            Log.Warning("No state mileage detected for travel_id {TravelId}", leg.travel_id);
            return (directionsCalls, geocodeCalls);
        }

        var orderedStates = stateMiles
            .OrderBy(s =>
                routeLine.Coordinates
                    .Select((c, i) => new { c, i })
                    .Where(x => _stateService.IsPointInState(x.c.Y, x.c.X, s.State))
                    .Select(x => x.i)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min())
            .ToList();


        Log.Information("States detected for travel_id {TravelId}: {States}", leg.travel_id, string.Join(" → ", stateMiles.Select(s => s.State)));

        foreach (var sm in orderedStates)
        {
           

            result.Add(new StateMileage
            {
                State = sm.State,
                Miles = sm.Miles
            });

            Log.Information(
                "State segment travel_id={TravelId} State={State} Miles={Miles:F2}",
                leg.travel_id,
                sm.State,
                sm.Miles);
        }

        //end here
        /*
        //commented for testing purpose
                int sampleCount = Math.Max(10, points.Count / 3);
                int interval = Math.Max(1, points.Count / sampleCount);

                var sampledStates = new List<string>();

                for (int i = 0; i < points.Count; i += interval)
                {
                    sampledStates.Add(await _google.GetState(points[i].lat, points[i].lon));
                    geocodeCalls++;
                }

                sampledStates.Add(await _google.GetState(points[^1].lat, points[^1].lon));
                geocodeCalls++;

                Log.Information("Detected route states for travel_id {TravelId}: {States}",
                    leg.travel_id, string.Join(" → ", sampledStates.Distinct()));

                // Assign segments to states
                for (int i = 0; i < points.Count - 1; i++)
                {
                    double miles = Haversine.Calculate(
                        points[i].lat, points[i].lon,
                        points[i + 1].lat, points[i + 1].lon);

                    int idx = Math.Min(i / interval, sampledStates.Count - 1);
                    string st = sampledStates[idx];

                    result.Add(new StateMileage
                    {
                        State = st,
                        Miles = miles
                    });
                }
                */

        return (directionsCalls, geocodeCalls);
    }

    // ==========================================================
    // NEW DEDUCTION LOGIC — START STATE FIRST
    // ==========================================================
    private void ApplyDeductionStartStateFirst(double deduct, List<StateMileage> segments, string travelId)
    {
        Log.Information(
            "Applying {Deduct:F2} miles deduction for travel_id {TravelId} using start-state-first rule",
            deduct, travelId
        );

        if (segments.Count == 0)
            return;

        // Determine state travel order based on when states FIRST appear
        var orderedStateSequence = segments
            .GroupBy(s => s.State)
            .Select(g => new
            {
                State = g.Key,
                FirstIndex = segments.FindIndex(x => x.State == g.Key)
            })
            .OrderBy(g => g.FirstIndex)
            .Select(g => g.State)
            .ToList();

        Log.Information("Deduction travel order for travel_id {TravelId}: {States}",
            travelId, string.Join(" → ", orderedStateSequence));

        // Flatten segments based on actual travel sequence
        var travelOrderedSegments = orderedStateSequence
            .SelectMany(st => segments.Where(s => s.State == st))
            .ToList();

        // Deduct in travel order
        foreach (var sm in travelOrderedSegments)
        {
            if (deduct <= 0)
            {
                Log.Information("Deduction complete for travel_id {TravelId}", travelId);
                break;
            }

            double take = Math.Min(sm.Miles, deduct);

            Log.Information(
                "Deduction step travel_id={TravelId}: State={State}, Miles={Miles:F2}, Taking={Take:F2}, RemainingBefore={Deduct:F2}",
                travelId, sm.State, sm.Miles, take, deduct
            );

            sm.Deducted = take;
            deduct -= take;
        }
    }
    private void ApplyDeductionFromLastState(double deduct, List<StateMileage> segments, string travelId)
    {
        if (deduct <= 0 || segments == null || segments.Count == 0)
            return;

        Log.Warning(
            "Applying fallback deduction from LAST state for travel_id {TravelId}. Remaining={Remaining:F2}",
            travelId, deduct);

        // Traverse from last to first
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            if (deduct <= 0)
                break;

            var sm = segments[i];

            double available = sm.Miles - sm.Deducted;
            if (available <= 0)
                continue;

            double take = Math.Min(available, deduct);

            sm.Deducted += take;
            deduct -= take;

            Log.Information(
                "Last-State Deduction travel_id={TravelId}: State={State}, Available={Available:F2}, Taking={Take:F2}, Remaining={Remaining:F2}",
                travelId, sm.State, available, take, deduct);
        }

        if (deduct > 0)
        {
            Log.Warning(
                "Still unable to fully deduct miles for travel_id {TravelId}. Undeducted={Remaining:F2}",
                travelId, deduct);
        }
    }

    private bool IsHighPayState(string state)
    {
        return _stateRates.ContainsKey(state);
    }

}