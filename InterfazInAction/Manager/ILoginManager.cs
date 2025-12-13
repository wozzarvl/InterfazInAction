using InterfazInAction.Models;

namespace InterfazInAction.Manager
{
    public interface ILoginManager
    {
        AuthResponseModel  Login(LoginModel login);
        AuthResponseModel RefreshToken(AuthResponseModel request);

        string Register(LoginModel login, string role);
    }
}
