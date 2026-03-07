namespace BiteTheBookie.Models
{
    public class CBBPlayer
    {
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public bool IsStarter { get; set; }
        public string Year { get; set; } = string.Empty; // FR, SO, JR, SR
    }
}
