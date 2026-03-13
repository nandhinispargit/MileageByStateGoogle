namespace MileageByStateGoogle.Models
{
    public class StateMileage
    {
        public string State { get; set; }
        public double Miles { get; set; }
        public double Deducted { get; set; }
        public int Apicalls { get; set; }
        public string has_highppayrate_state { get; set; }
    }
}