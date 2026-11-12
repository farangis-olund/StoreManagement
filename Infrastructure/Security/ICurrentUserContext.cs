namespace Infrastructure.Security;

public interface ICurrentUserContext
{
    int UserId { get; }
    bool IsLoggedIn { get; }
}
