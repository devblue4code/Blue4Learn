using Microsoft.AspNetCore.Identity;

namespace Blue4Learn.Web.Domain;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
}
