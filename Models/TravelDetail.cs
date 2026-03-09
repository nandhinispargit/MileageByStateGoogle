namespace MileageByStateGoogle.Models
{
    public class TravelDetail
    {
        public string travel_id { get; set; }
        public string travel_dt { get; set; }

        public double Start_latitude { get; set; }
        public double Start_longitude { get; set; }

        public double End_latitude { get; set; }
        public double End_longitude { get; set; }

        public double travel_distance { get; set; }
        public int travel_leg_no { get; set; }
    }
}