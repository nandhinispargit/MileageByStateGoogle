namespace MileageByStateGoogle.Models
{
    public class TravelItem
    {
        public string travel_id { get; set; }
        public string travel_dt { get; set; }
        public string job_no { get; set; }
        public string wave_no { get; set; }
        public string task_no { get; set; }
        public string store_id { get; set; }
        public string merch_no { get; set; }
        public string rep_homestate { get; set; }
        public double travel_distance { get; set; }
        public double deduct_miles { get; set; }
        public double actual_amount { get; set; }
        public string start_leg_deduction { get; set; }
        
    }
}