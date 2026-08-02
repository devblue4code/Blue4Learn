using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blue4Learn.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ChangePasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string DisplayName { get; set; } = string.Empty;
    public string Initials { get; set; } = "?";
    public string RoleLabel { get; set; } = "Participante";

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Informe a senha atual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha atual")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a nova senha.")]
        [StringLength(100, ErrorMessage = "A senha deve ter pelo menos {2} caracteres.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Nova senha")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar nova senha")]
        [Compare("NewPassword", ErrorMessage = "As senhas não coincidem.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    private async Task LoadHeaderAsync(ApplicationUser user)
    {
        DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Conta" : user.FullName;
        Initials = string.Concat(
            DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(p => char.ToUpperInvariant(p[0])));
        if (string.IsNullOrWhiteSpace(Initials))
        {
            Initials = "?";
        }

        var roles = await _userManager.GetRolesAsync(user);
        RoleLabel = roles.Contains(AppRoles.Admin) ? AppRoles.Admin
            : roles.Contains(AppRoles.Teacher) ? AppRoles.Teacher
            : roles.Contains(AppRoles.Student) ? AppRoles.Student
            : "Participante";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Usuário não encontrado.");
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (!hasPassword)
        {
            return RedirectToPage("./Index");
        }

        await LoadHeaderAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound("Usuário não encontrado.");
        }

        await LoadHeaderAsync(user);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Senha atualizada com sucesso.";
        return RedirectToPage();
    }
}
