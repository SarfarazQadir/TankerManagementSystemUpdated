using TankerManagementSystem.Models.Email;
namespace TankerManagementSystem.Services
{
    public interface IEmailService
    {
        void SendEmail(Message message);
    }
}
