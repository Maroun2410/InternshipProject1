namespace InternshipProject1.Models
{
    public class LandPractices
    {
        public int Id { get; set; }
        public int LandId { get; set; }
        public string WateringMethod { get; set; }
        public string FertilizerType { get; set; }
        public string FertilizerFreq { get; set; }
        public bool PesticideUsed { get; set; }
        public string Notes { get; set; }

        public Land Lands { get; set; }
    }
}
