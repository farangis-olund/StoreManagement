namespace PresentationWpf.Services;
public class UserSessionService
{
    public string UserId { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public double ExchangeRate { get; set; }
	public double CustomerDiscountPercentage { get; set; }
}
