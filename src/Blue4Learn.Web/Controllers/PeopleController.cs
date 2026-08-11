using Blue4Learn.Web.Data;
using Blue4Learn.Web.Data.Seed;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class PeopleController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly UserManager<ApplicationUser> _userManager;

    public PeopleController(
        ApplicationDbContext db,
        IAccessService access,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _access = access;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var admin = await RequireAdminAsync();
        if (admin is null) return Challenge();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == admin.TenantId)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var courseByTeacher = await _db.Courses
            .AsNoTracking()
            .Where(c => c.TenantId == admin.TenantId && c.TeacherUserId != null)
            .ToDictionaryAsync(c => c.TeacherUserId!, c => c.Title);

        var enrollCounts = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassGroup.TenantId == admin.TenantId)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var items = new List<PersonListItemViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new PersonListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                RoleLabel = PrimaryRole(roles),
                CourseTitle = courseByTeacher.GetValueOrDefault(user.Id),
                ClassCount = enrollCounts.GetValueOrDefault(user.Id)
            });
        }

        return View(new PeopleListViewModel { People = items });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var admin = await RequireAdminAsync();
        if (admin is null) return Challenge();

        return View("Edit", await BuildFormAsync(admin, new PersonFormViewModel
        {
            Role = AppRoles.Student
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var admin = await RequireAdminAsync();
        if (admin is null) return Challenge();

        var person = await _userManager.FindByIdAsync(id);
        if (person is null || person.TenantId != admin.TenantId) return NotFound();

        var roles = await _userManager.GetRolesAsync(person);
        var ownedCourseId = await _db.Courses
            .AsNoTracking()
            .Where(c => c.TeacherUserId == person.Id)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        return View(await BuildFormAsync(admin, new PersonFormViewModel
        {
            Id = person.Id,
            FullName = person.FullName,
            Email = person.Email ?? string.Empty,
            Role = PrimaryRole(roles),
            CourseId = ownedCourseId
        }, person.Id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PersonFormViewModel model)
    {
        var admin = await RequireAdminAsync();
        if (admin is null) return Challenge();

        model.Email = model.Email.Trim().ToLowerInvariant();
        model.FullName = model.FullName.Trim();
        model.Role = NormalizeRole(model.Role);

        if (model.Role != AppRoles.Teacher)
        {
            model.CourseId = null;
        }
        else if (model.CourseId is Guid courseId)
        {
            var courseOk = await _db.Courses.AnyAsync(c =>
                c.Id == courseId && c.TenantId == admin.TenantId);
            if (!courseOk)
            {
                ModelState.AddModelError(nameof(model.CourseId), "Disciplina inválida.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", await BuildFormAsync(admin, model, model.Id));
        }

        ApplicationUser person;
        if (model.IsEdit)
        {
            person = await _userManager.FindByIdAsync(model.Id!)
                     ?? throw new InvalidOperationException("Pessoa não encontrada.");
            if (person.TenantId != admin.TenantId) return Forbid();

            var emailOwner = await _userManager.FindByEmailAsync(model.Email);
            if (emailOwner is not null && emailOwner.Id != person.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "Já existe uma conta com este e-mail.");
                return View("Edit", await BuildFormAsync(admin, model, model.Id));
            }

            person.FullName = model.FullName;
            person.Email = model.Email;
            person.UserName = model.Email;
            person.NormalizedEmail = _userManager.NormalizeEmail(model.Email);
            person.NormalizedUserName = _userManager.NormalizeName(model.Email);

            var update = await _userManager.UpdateAsync(person);
            if (!update.Succeeded)
            {
                foreach (var err in update.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }

                return View("Edit", await BuildFormAsync(admin, model, model.Id));
            }
        }
        else
        {
            if (await _userManager.FindByEmailAsync(model.Email) is not null)
            {
                ModelState.AddModelError(nameof(model.Email), "Já existe uma conta com este e-mail.");
                return View("Edit", await BuildFormAsync(admin, model));
            }

            person = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                FullName = model.FullName,
                TenantId = admin.TenantId
            };

            var create = await _userManager.CreateAsync(person, DbSeeder.DemoPassword);
            if (!create.Succeeded)
            {
                foreach (var err in create.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }

                return View("Edit", await BuildFormAsync(admin, model));
            }
        }

        await SetPrimaryRoleAsync(person, model.Role);
        await AssignCourseTeacherAsync(admin.TenantId!.Value, person.Id, model.Role, model.CourseId);

        TempData["Success"] = model.IsEdit
            ? "Pessoa atualizada."
            : $"Pessoa criada. Senha inicial: {DbSeeder.DemoPassword}";

        return RedirectToAction(nameof(Edit), new { id = person.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(string id, Guid classGroupId)
    {
        var admin = await RequireAdminAsync();
        if (admin is null) return Challenge();

        var person = await _userManager.FindByIdAsync(id);
        if (person is null || person.TenantId != admin.TenantId) return NotFound();

        var classGroup = await _db.ClassGroups
            .FirstOrDefaultAsync(c => c.Id == classGroupId && c.TenantId == admin.TenantId);
        if (classGroup is null)
        {
            TempData["Error"] = "Turma inválida.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var already = await _db.Enrollments.AnyAsync(e =>
            e.ClassGroupId == classGroupId && e.UserId == person.Id);
        if (already)
        {
            TempData["Info"] = "Esta pessoa já está nesta turma.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        _db.Enrollments.Add(new Enrollment
        {
            ClassGroupId = classGroupId,
            UserId = person.Id
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Matrícula na turma concluída.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unenroll(string id, Guid enrollmentId)
    {
        var admin = await RequireAdminAsync();
        if (admin is null) return Challenge();

        var person = await _userManager.FindByIdAsync(id);
        if (person is null || person.TenantId != admin.TenantId) return NotFound();

        var enrollment = await _db.Enrollments
            .Include(e => e.ClassGroup)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == person.Id);

        if (enrollment is null || enrollment.ClassGroup.TenantId != admin.TenantId)
        {
            return NotFound();
        }

        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Matrícula removida.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<ApplicationUser?> RequireAdminAsync()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null || user.TenantId is null) return null;
        if (!await _userManager.IsInRoleAsync(user, AppRoles.Admin)) return null;
        return user;
    }

    private async Task<PersonFormViewModel> BuildFormAsync(
        ApplicationUser admin,
        PersonFormViewModel model,
        string? personId = null)
    {
        model.Courses = await _db.Courses
            .AsNoTracking()
            .Where(c => c.TenantId == admin.TenantId)
            .OrderBy(c => c.Title)
            .Select(c => new CourseOptionViewModel { Id = c.Id, Title = c.Title })
            .ToListAsync();

        if (personId is not null)
        {
            model.Enrollments = await _db.Enrollments
                .AsNoTracking()
                .Where(e => e.UserId == personId)
                .OrderBy(e => e.ClassGroup.Name)
                .Select(e => new PersonEnrollmentViewModel
                {
                    EnrollmentId = e.Id,
                    ClassGroupId = e.ClassGroupId,
                    ClassName = e.ClassGroup.Name,
                    ClassCode = e.ClassGroup.Code,
                    CourseTitle = e.ClassGroup.Course.Title
                })
                .ToListAsync();

            var enrolledIds = model.Enrollments.Select(e => e.ClassGroupId).ToHashSet();
            model.AvailableClasses = await _db.ClassGroups
                .AsNoTracking()
                .Where(c => c.TenantId == admin.TenantId && !enrolledIds.Contains(c.Id))
                .OrderBy(c => c.Course.Title)
                .ThenBy(c => c.Name)
                .Select(c => new ClassOptionViewModel
                {
                    Id = c.Id,
                    Label = $"{c.Course.Title} · {c.Name} ({c.Code})"
                })
                .ToListAsync();
        }
        else
        {
            model.Enrollments = [];
            model.AvailableClasses = [];
        }

        return model;
    }

    private async Task SetPrimaryRoleAsync(ApplicationUser person, string role)
    {
        var current = await _userManager.GetRolesAsync(person);
        var academic = current
            .Where(r => r is AppRoles.Student or AppRoles.Teacher or AppRoles.Admin)
            .ToList();

        if (academic.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(person, academic);
        }

        await _userManager.AddToRoleAsync(person, role);
    }

    private async Task AssignCourseTeacherAsync(
        Guid tenantId,
        string userId,
        string role,
        Guid? courseId)
    {
        var owned = await _db.Courses
            .Where(c => c.TenantId == tenantId && c.TeacherUserId == userId)
            .ToListAsync();

        foreach (var course in owned)
        {
            if (role != AppRoles.Teacher || courseId != course.Id)
            {
                course.TeacherUserId = null;
            }
        }

        if (role == AppRoles.Teacher && courseId is Guid id)
        {
            var course = await _db.Courses
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (course is not null)
            {
                // Liberar disciplina se outra professora a tinha.
                if (course.TeacherUserId is not null && course.TeacherUserId != userId)
                {
                    // ok to reassign
                }

                course.TeacherUserId = userId;
            }
        }

        await _db.SaveChangesAsync();
    }

    private static string NormalizeRole(string? role) => role switch
    {
        AppRoles.Teacher => AppRoles.Teacher,
        AppRoles.Admin => AppRoles.Admin,
        _ => AppRoles.Student
    };

    private static string PrimaryRole(IList<string> roles)
    {
        if (roles.Contains(AppRoles.Admin)) return AppRoles.Admin;
        if (roles.Contains(AppRoles.Teacher)) return AppRoles.Teacher;
        if (roles.Contains(AppRoles.Student)) return AppRoles.Student;
        return "Sem perfil";
    }
}
