namespace MileageByStateGoogle.Models
{
    public class OutputRecord
    {
        public string travel_id { get; set; }
        public string travel_dt { get; set; }

        public string State { get; set; }
        public double Rate { get; set; }

        public double Miles { get; set; }
        public double Deducted { get; set; }
        public double Final_Mile { get; set; }

        public double Reimbursement { get; set; }

        public int TravelLegNo { get; set; }
    }
}