using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class MyAccountViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public SubscriptionTier SubscriptionTier { get; set; }
        public DateTime? SubscriptionExpiry { get; set; }
        public bool IsPro { get; set; }
        public DateTime MemberSince { get; set; }
    }
}