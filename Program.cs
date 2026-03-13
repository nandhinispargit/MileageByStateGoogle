using MileageByStateGoogle.Services;
using MileageByStateGoogle.Models;
using MileageByStateGoogle.AppData;
using Microsoft.Extensions.Configuration;
using Serilog;




internal class Program
{
    static readonly string ApiKey;
    static readonly string ConnectionString;
    static readonly IConfiguration config;    

    static Program()
    {
        // LOAD CONFIG
        config = new ConfigurationBuilder()
       // .AddJsonFile("appsettings.json", optional: false)
       .SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
           .AddEnvironmentVariables()
           .AddUserSecrets<Program>()
           .Build();

        ApiKey = config["GApiKey"]
         ?? throw new InvalidOperationException("Google API Key not found in secrets.");

        //   ConnectionString = config.GetConnectionString("ConnectionStrings")
        // ?? throw new InvalidOperationException("Connection string not found.");

        // SETUP SERILOG
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("Logs/mileage.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Program initialization completed.");
    }

     static async Task Main(string[] args)
    {
        Log.Information("Mileage calculation started.");
        args = new string[1];
        args[0] = "1";
        string countrycode = args[0];
        Int32 iLangId = 0;
        string sCountryName = "";
        string sConnString = "";

        try
        {
            if (countrycode != "")
            {
                iLangId = Convert.ToInt32(countrycode);
            }
            sCountryName = Get_CountryCode(iLangId);
            string sConn = "ConnectionString:" + sCountryName;
            sConnString = config.GetSection(sConn).Value ?? string.Empty;

            var google = new GoogleApiService(ApiKey);
            var csv = new CsvService();
            var repo = new TravelMileageRepository(sConnString);
            var stateService = new StateBoundaryService();
            var engine = new MileageEngine(google, repo,stateService);

            var travelinforesult = await repo.GetTravelInfoAsync(iLangId);
            var travelItems = travelinforesult.TravelItems;
            var travelDetails = travelinforesult.TravelDetails;

            // LOAD INPUT CSVs
            //  var travelItems = csv.LoadCsv<TravelItem>("Data/Input/TravelItems.csv");
            //   var travelDetails = csv.LoadCsv<TravelDetail>("Data/Input/TravelItemDetails.csv");

            // RUN MILEAGE ENGINE
            var results = await engine.CalculateMileageByState(travelItems, travelDetails);

            // EXPORT MAIN OUTPUT
            csv.ExportCsv("Data/Output/AllStateMileageOutput.csv", results.OutputRecords);

            // ----------------------------------------------------------
            // HIGH-PAY TRAVEL ONLY (UNCHANGED)
            // ----------------------------------------------------------
            var highPayStates = new HashSet<string> { "CA", "IL", "MA" };

            var highOnly = results.OutputRecords
                .Where(r => highPayStates.Contains(r.State))
                .ToList();

            csv.ExportCsv("Data/Output/HighPayTravelOnly.csv", highOnly);

            // ----------------------------------------------------------
            // CORRECTED SUMMARY LOGIC
            //
            // *Include ALL states mileage for ANY travel_id that
            //  contains a HIGH-PAY state*
            //
            // This now matches your business requirement.
            // ----------------------------------------------------------

            var summary = results.OutputRecords
                .GroupBy(r => new{r.travel_id, r.TravelLegNo})
                // .Where(g => g.Any(r => highPayStates.Contains(r.State))) // include full trip if ANY high-pay state exists
                .Select(g =>
                {
                    string travelId = g.Key.travel_id;
                    int travellegno = g.Key.TravelLegNo;
                    var travelItemRows = travelItems.Where(t => t.travel_id == travelId && t.travel_leg_no == travellegno).ToList();

                    string hasHighPayState =
            g.Any(r => highPayStates.Contains(r.State)) ? "Y" : "N";


                    return new SummaryRecord
                    {
                        travel_id = travelId,
                        travel_leg_no= travellegno,
                        travel_dt = travelItemRows.First().travel_dt,
                        travel_distance = travelItemRows.Sum(t => t.travel_distance),
                        actual_amount = travelItemRows.Sum(t => t.actual_amount),


                        MilesByState = g.Sum(r => r.Final_Mile),


                        adjusted_amount = g.Sum(r => r.Reimbursement),

                        has_highppayrate_state = hasHighPayState
                    };
                })
                .ToList();

            foreach (var s in summary)
            {
                await repo.InsertTravelMileageSummaryAsync(s.travel_id, s.travel_dt, (decimal)s.travel_distance,
                (decimal)s.actual_amount, (decimal)Math.Round(s.MilesByState, 2), (decimal)Math.Round(s.adjusted_amount, 2), s.has_highppayrate_state,s.travel_leg_no);
            }


            csv.ExportCsv("Data/Output/TravelSummaryComparison.csv", summary);

            // API CALL STATISTICS
            csv.ExportApiCallStats("Data/Output/ApiCallStats.csv", results.ApiCallStats);

            Log.Information("Mileage calculation completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "FATAL ERROR during mileage processing.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }


    static string Get_CountryCode(Int32 iCountry)
    {
        string sTempCountry = "US";

        switch (iCountry)
        {
            case 1:
                sTempCountry = "US";
                break;
            case 2:
                sTempCountry = "JP";
                break;
            case 4:
                sTempCountry = "CA";
                break;
            case 5:
                sTempCountry = "TR";
                break;
            case 6:
                sTempCountry = "SA";
                break;
            case 7:
                sTempCountry = "IN";
                break;
            case 8:
                sTempCountry = "RO";
                break;
            case 10:
                sTempCountry = "CH";
                break;
            case 11:
                sTempCountry = "LI";
                break;
            case 12:
                sTempCountry = "AU";
                break;
            case 16:
                sTempCountry = "MX";
                break;
            case 19:
                sTempCountry = "BR";
                break;
            default:
                sTempCountry = "US";
                break;
        }
        return sTempCountry;
    }




}