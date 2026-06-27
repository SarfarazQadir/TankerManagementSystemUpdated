using Microsoft.AspNetCore.Identity;

namespace TankerManagementSystem.Models
{
    public class UserPermission
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ModuleId { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public IdentityUser User { get; set; }
        public AppModule Module { get; set; }
    }
}