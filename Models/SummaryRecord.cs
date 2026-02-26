namespace MileageByStateGoogle.Models
{
    public class SummaryRecord
    {
        public string travel_id { get; set; }
        public string travel_dt { get; set; }

        public double travel_distance { get; set; }
        public double actual_amount { get; set; }

        public double MilesByState { get; set; }
        public double adjusted_amount { get; set; }

        public string has_highppayrate_state { get; set; }
    }
}