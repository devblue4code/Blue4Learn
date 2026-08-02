using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    public string Username { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = "Participante";
    public string TenantName { get; set; } = "—";
    public IReadOnlyList<string> ClassNames { get; set; } = [];
    public string Initials { get; set; } = "?";

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(120, ErrorMessage = "Use até {1} caracteres.")]
        [Display(Name = "Nome completo")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Telefone inválido.")]
        [Display(Name = "Telefone")]
        public string? PhoneNumber { get; set; }
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Username = await _userManager.GetUserNameAsync(user) ?? user.Email ?? string.Empty;
        var email = await _userManager.GetEmailAsync(user) ?? string.Empty;
        var phone = await _userManager.GetPhoneNumberAsync(user);

        Input = new InputModel
        {
            FullName = user.FullName,
            Email = email,
            PhoneNumber = phone
        };

        var roles = await _userManager.GetRolesAsync(user);
        RoleLabel = roles.Contains(AppRoles.Admin) ? AppRoles.Admin
            : roles.Contains(AppRoles.Teacher) ? AppRoles.Teacher
            : roles.Contains(AppRoles.Student) ? AppRoles.Student
            : "Participante";

        if (user.TenantId is Guid tenantId)
        {
            TenantName = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync() ?? "—";
        }

        ClassNames = await _db.Enrollments.AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .Select(e => e.ClassGroup.Name)
            .OrderBy(n => n)
            .ToListAsync();

        var source = string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName;
        Initials = string.Concat(
            source.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(p => char.ToUpperInvariant(p[0])));
        if (string.IsNullOrWhiteSpace(Initials) && source.Length > 0)
        {
            Initials = char.ToUpperInvariant(source[0]).ToString();
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Usuário não encontrado.");
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Usuário não encontrado.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        user.FullName = Input.FullName.Trim();

        var email = await _userManager.GetEmailAsync(user);
        if (Input.Email != email)
        {
            var setEmail = await _userManager.SetEmailAsync(user, Input.Email);
            if (!setEmail.Succeeded)
            {
                StatusMessage = "Error: não foi possível atualizar o e-mail.";
                await LoadAsync(user);
                return Page();
            }

            await _userManager.SetUserNameAsync(user, Input.Email);
        }

        var phone = await _userManager.GetPhoneNumberAsync(user);
        if (Input.PhoneNumber != phone)
        {
            var setPhone = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!setPhone.Succeeded)
            {
                StatusMessage = "Error: não foi possível atualizar o telefone.";
                await LoadAsync(user);
                return Page();
            }
        }

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            StatusMessage = "Error: não foi possível salvar o perfil.";
            await LoadAsync(user);
            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Perfil atualizado com sucesso.";
        return RedirectToPage();
    }
}
