using Microsoft.Data.SqlClient;
using MileageByStateGoogle.Models;
using System.Data;

namespace MileageByStateGoogle.AppData
{
    public class TravelMileageRepository
    {
        private readonly string _connectionString;

        public TravelMileageRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void InsertTravelMileage(string travelId, string travelDate, string stateCode, double rate, double travelMiles,
            double deducted, double finalMile, double reimbursement,int travel_leg_no,int merch_no,int apiCallscount)
        {
            try
            {
                using SqlConnection conn = new SqlConnection(_connectionString);
                using SqlCommand cmd = new SqlCommand("usp_TravelMileagebyState", conn);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@travel_id", System.Data.SqlDbType.NVarChar, 100).Value = travelId;
                cmd.Parameters.Add("@travel_dt", System.Data.SqlDbType.NVarChar).Value = travelDate;
                cmd.Parameters.Add("@state_cd", System.Data.SqlDbType.NVarChar, 10).Value = stateCode;
                cmd.Parameters.Add("@rate", System.Data.SqlDbType.Decimal).Value = rate;
                cmd.Parameters.Add("@travel_Miles", System.Data.SqlDbType.Decimal).Value = travelMiles;
                cmd.Parameters.Add("@deducted", System.Data.SqlDbType.Decimal).Value = deducted;
                cmd.Parameters.Add("@final_mile", System.Data.SqlDbType.Decimal).Value = finalMile;
                cmd.Parameters.Add("@reimbursement", System.Data.SqlDbType.Decimal).Value = reimbursement;
                cmd.Parameters.Add("@travel_leg_no", System.Data.SqlDbType.Int).Value = travel_leg_no;
                cmd.Parameters.Add("@merch_no", System.Data.SqlDbType.Int).Value = merch_no;  
                cmd.Parameters.Add("@apicallcount", System.Data.SqlDbType.Int).Value = apiCallscount;
                //  cmd.Parameters.Add("@create_usrid", System.Data.SqlDbType.NVarChar, 10).Value = userId;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine("error", e.Message);
            }
        }
        public async Task InsertTravelMileageSummaryAsync(string travelId, string travelDate, decimal travelDistance, decimal actualAmount, 
        decimal milesByState, decimal adjustedAmount,string highpay_state_flag)
        {
            using SqlConnection conn = new SqlConnection(_connectionString);
            using SqlCommand cmd = new SqlCommand("usp_TravelMileageSummary", conn);

            cmd.CommandType = System.Data.CommandType.StoredProcedure;

            cmd.Parameters.Add("@travel_id", System.Data.SqlDbType.NVarChar, 150).Value = travelId;
            cmd.Parameters.Add("@travel_dt", System.Data.SqlDbType.NVarChar).Value = travelDate;
            cmd.Parameters.Add("@travel_distance", System.Data.SqlDbType.Decimal).Value = travelDistance;
            cmd.Parameters.Add("@actual_amount", System.Data.SqlDbType.Decimal).Value = actualAmount;
            cmd.Parameters.Add("@MilesByState", System.Data.SqlDbType.Decimal).Value = milesByState;
            cmd.Parameters.Add("@adjusted_amount", System.Data.SqlDbType.Decimal).Value = adjustedAmount;   
            // cmd.Parameters.Add("@create_usrid", SqlDbType.NVarChar, 20).Value = userId;
            cmd.Parameters.Add("@highpay_state_flag", System.Data.SqlDbType.NVarChar, 10).Value = highpay_state_flag;
         //   cmd.Parameters.Add("@apicallcount", System.Data.SqlDbType.Int).Value = apiCallscount;

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<TravelDataContainer> GetTravelInfoAsync(int langId)
        {

            List<TravelItem> travelItems = new List<TravelItem>();
            List<TravelDetail> travelDetails = new List<TravelDetail>();

            using SqlConnection conn = new SqlConnection(_connectionString);
            using SqlCommand cmd = new SqlCommand("usp_mileagebystate_get_travel_info_v3", conn);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add("@lang_id", SqlDbType.Int).Value = langId;

            await conn.OpenAsync();

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                TravelItem item = new TravelItem
                {
                    travel_id = reader["travel_id"].ToString(),
                    travel_dt = reader["travel_dt"].ToString(),
                    job_no = reader["job_no"].ToString(),
                    wave_no = reader["wave_no"].ToString(),
                    task_no = reader["task_no"].ToString(),
                    store_id = reader["store_id"].ToString(),
                    merch_no =Convert.ToInt32(reader["merch_no"]),
                    rep_homestate = reader["rep_homestate"].ToString(),
                    travel_distance = Convert.ToDouble(reader["travel_distance"]),
                    deduct_miles = Convert.ToDouble(reader["deducted_miles"]),
                    actual_amount = Convert.ToDouble(reader["actual_amount"]),
                    start_leg_deduction = reader["start_leg_deduction"].ToString(),
                    travel_leg_no = Convert.ToInt32(reader["travel_leg_no"])
                };

                travelItems.Add(item);

                TravelDetail detail = new TravelDetail
                {
                    travel_id = reader["travel_id"].ToString(),
                    travel_dt = reader["travel_dt"].ToString(),
                    Start_latitude = Convert.ToDouble(reader["Start_latitude"]),
                    Start_longitude = Convert.ToDouble(reader["Start_longitude"]),
                    End_latitude = Convert.ToDouble(reader["End_latitude"]),
                    End_longitude = Convert.ToDouble(reader["End_longitude"]),
                    travel_distance = Convert.ToDouble(reader["travel_distance"]),
                    travel_leg_no = Convert.ToInt32(reader["travel_leg_no"])
                };

                travelDetails.Add(detail);
            }

            return new TravelDataContainer
            {
                TravelItems = travelItems,
                TravelDetails = travelDetails
            };
        }

    }
}
