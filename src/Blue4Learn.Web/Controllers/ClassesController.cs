using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class ClassesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClassesController(
        ApplicationDbContext db,
        IAccessService access,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _access = access;
        _userManager = userManager;
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Index()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageClassesAsync(user)) return Forbid();

        var tenantId = user.TenantId!.Value;
        var isAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);

        var query = _db.ClassGroups
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Where(c => c.TenantId == tenantId);

        if (!isAdmin)
        {
            query = query.Where(c => c.Enrollments.Any(e => e.UserId == user.Id));
        }

        var classes = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        var items = new List<ClassListItemViewModel>();
        foreach (var c in classes)
        {
            var studentCount = 0;
            foreach (var enrollment in c.Enrollments)
            {
                var member = await _userManager.FindByIdAsync(enrollment.UserId);
                if (member is not null && await _userManager.IsInRoleAsync(member, AppRoles.Student))
                {
                    studentCount++;
                }
            }

            items.Add(new ClassListItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                CourseTitle = c.Course.Title,
                MemberCount = c.Enrollments.Count,
                StudentCount = studentCount,
                IsMember = c.Enrollments.Any(e => e.UserId == user.Id)
            });
        }

        return View(new ClassListViewModel { Classes = items });
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Create()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageClassesAsync(user)) return Forbid();

        return View("Edit", await BuildFormAsync(user, new ClassFormViewModel()));
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageClassAsync(user, id)) return Forbid();

        var classGroup = await _db.ClassGroups.FirstOrDefaultAsync(c => c.Id == id);
        if (classGroup is null) return NotFound();

        return View(await BuildFormAsync(user, new ClassFormViewModel
        {
            Id = classGroup.Id,
            Name = classGroup.Name,
            Code = classGroup.Code,
            CourseId = classGroup.CourseId
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Save(ClassFormViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageClassesAsync(user) || user.TenantId is null) return Forbid();

        model.Code = model.Code.Trim().ToUpperInvariant();
        model.Name = model.Name.Trim();
        model.Courses = await LoadCoursesAsync(user);

        var courseOk = await _db.Courses.AnyAsync(c => c.Id == model.CourseId && c.TenantId == user.TenantId);
        if (!courseOk)
        {
            ModelState.AddModelError(nameof(model.CourseId), "Disciplina inválida.");
        }

        var codeTaken = await _db.ClassGroups.AnyAsync(c =>
            c.TenantId == user.TenantId &&
            c.Code == model.Code &&
            (!model.Id.HasValue || c.Id != model.Id.Value));

        if (codeTaken)
        {
            ModelState.AddModelError(nameof(model.Code), "Já existe uma turma com este código na instituição.");
        }

        if (model.Id is Guid editId && !await _access.CanManageClassAsync(user, editId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        ClassGroup classGroup;
        if (model.Id is Guid id)
        {
            classGroup = await _db.ClassGroups.Include(c => c.Enrollments).FirstAsync(c => c.Id == id);
            classGroup.Name = model.Name;
            classGroup.Code = model.Code;
            classGroup.CourseId = model.CourseId;
        }
        else
        {
            classGroup = new ClassGroup
            {
                TenantId = user.TenantId.Value,
                CourseId = model.CourseId,
                Name = model.Name,
                Code = model.Code
            };
            _db.ClassGroups.Add(classGroup);
            classGroup.Enrollments.Add(new Enrollment { UserId = user.Id });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = model.IsEdit
            ? "Turma atualizada."
            : "Turma criada. Você já está matriculada nela.";
        return RedirectToAction(nameof(Details), new { id = classGroup.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageClassAsync(user, id)) return Forbid();

        var classGroup = await _db.ClassGroups
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (classGroup is null) return NotFound();

        var members = new List<ClassMemberViewModel>();
        foreach (var enrollment in classGroup.Enrollments.OrderBy(e => e.User.FullName))
        {
            var roles = await _userManager.GetRolesAsync(enrollment.User);
            var roleLabel = roles.Contains(AppRoles.Admin) ? AppRoles.Admin
                : roles.Contains(AppRoles.Teacher) ? AppRoles.Teacher
                : AppRoles.Student;

            members.Add(new ClassMemberViewModel
            {
                EnrollmentId = enrollment.Id,
                UserId = enrollment.UserId,
                FullName = enrollment.User.FullName,
                Email = enrollment.User.Email ?? string.Empty,
                RoleLabel = roleLabel,
                EnrolledAtUtc = enrollment.EnrolledAtUtc,
                CanRemove = enrollment.UserId != user.Id || await _userManager.IsInRoleAsync(user, AppRoles.Admin)
            });
        }

        return View(new ClassDetailsViewModel
        {
            Id = classGroup.Id,
            Name = classGroup.Name,
            Code = classGroup.Code,
            CourseTitle = classGroup.Course.Title,
            CourseId = classGroup.CourseId,
            Members = members,
            EnrollForm = new EnrollMemberFormViewModel
            {
                ClassGroupId = classGroup.Id,
                Role = AppRoles.Student
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Enroll([Bind(Prefix = "EnrollForm")] EnrollMemberFormViewModel model)
    {
        var actor = await _access.GetCurrentUserAsync(User);
        if (actor is null) return Challenge();
        if (!await _access.CanManageClassAsync(actor, model.ClassGroupId) || actor.TenantId is null)
        {
            return Forbid();
        }

        model.Email = model.Email.Trim().ToLowerInvariant();
        model.Role = model.Role is AppRoles.Teacher or AppRoles.Student ? model.Role : AppRoles.Student;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Informe um e-mail válido.";
            return RedirectToAction(nameof(Details), new { id = model.ClassGroupId });
        }

        var classGroup = await _db.ClassGroups.FirstOrDefaultAsync(c => c.Id == model.ClassGroupId);
        if (classGroup is null) return NotFound();

        var target = await _userManager.FindByEmailAsync(model.Email);
        var created = false;

        if (target is null)
        {
            var fullName = string.IsNullOrWhiteSpace(model.FullName)
                ? model.Email.Split('@')[0]
                : model.FullName.Trim();

            target = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                FullName = fullName,
                TenantId = actor.TenantId
            };

            var create = await _userManager.CreateAsync(target, "Demo@123");
            if (!create.Succeeded)
            {
                TempData["Error"] = string.Join(" ", create.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Details), new { id = model.ClassGroupId });
            }

            await _userManager.AddToRoleAsync(target, model.Role);
            created = true;
        }
        else
        {
            if (target.TenantId is not null && target.TenantId != actor.TenantId)
            {
                TempData["Error"] = "Este usuário pertence a outra instituição.";
                return RedirectToAction(nameof(Details), new { id = model.ClassGroupId });
            }

            target.TenantId ??= actor.TenantId;
            await _userManager.UpdateAsync(target);

            if (!await _userManager.IsInRoleAsync(target, model.Role)
                && !await _userManager.IsInRoleAsync(target, AppRoles.Admin))
            {
                // keep existing academic role; only add if none of the app roles
                var roles = await _userManager.GetRolesAsync(target);
                if (!roles.Any(r => r is AppRoles.Student or AppRoles.Teacher or AppRoles.Admin))
                {
                    await _userManager.AddToRoleAsync(target, model.Role);
                }
            }
        }

        var already = await _db.Enrollments.AnyAsync(e =>
            e.ClassGroupId == model.ClassGroupId && e.UserId == target.Id);

        if (already)
        {
            TempData["Error"] = "Esta pessoa já está matriculada na turma.";
            return RedirectToAction(nameof(Details), new { id = model.ClassGroupId });
        }

        _db.Enrollments.Add(new Enrollment
        {
            ClassGroupId = model.ClassGroupId,
            UserId = target.Id
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = created
            ? $"Conta criada e matriculada ({model.Email} / Demo@123)."
            : $"{target.FullName} matriculado(a) na turma.";
        return RedirectToAction(nameof(Details), new { id = model.ClassGroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Unenroll(Guid enrollmentId, Guid classGroupId)
    {
        var actor = await _access.GetCurrentUserAsync(User);
        if (actor is null) return Challenge();
        if (!await _access.CanManageClassAsync(actor, classGroupId)) return Forbid();

        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.ClassGroupId == classGroupId);

        if (enrollment is null) return NotFound();

        if (enrollment.UserId == actor.Id && !await _userManager.IsInRoleAsync(actor, AppRoles.Admin))
        {
            TempData["Error"] = "Você não pode remover a si mesma da turma. Peça a um administrador.";
            return RedirectToAction(nameof(Details), new { id = classGroupId });
        }

        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Matrícula removida.";
        return RedirectToAction(nameof(Details), new { id = classGroupId });
    }

    [HttpGet]
    public IActionResult Join() => View(new JoinClassViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(JoinClassViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        model.Code = model.Code.Trim().ToUpperInvariant();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var classGroup = await _db.ClassGroups
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.Code == model.Code);

        if (classGroup is null)
        {
            ModelState.AddModelError(nameof(model.Code), "Código de turma não encontrado.");
            return View(model);
        }

        if (user.TenantId is not null && user.TenantId != classGroup.TenantId)
        {
            ModelState.AddModelError(nameof(model.Code), "Esta turma pertence a outra instituição.");
            return View(model);
        }

        user.TenantId ??= classGroup.TenantId;
        await _userManager.UpdateAsync(user);

        var already = await _db.Enrollments.AnyAsync(e =>
            e.ClassGroupId == classGroup.Id && e.UserId == user.Id);

        if (already)
        {
            TempData["Info"] =
                $"Você já está matriculado em {classGroup.Name} ({classGroup.Course.Title}). Não é preciso inserir o código de novo.";
            return View(model);
        }

        _db.Enrollments.Add(new Enrollment
        {
            ClassGroupId = classGroup.Id,
            UserId = user.Id
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Matrícula em {classGroup.Name} ({classGroup.Course.Title}) concluída.";
        return RedirectToAction("Dashboard", "Home");
    }

    private async Task<ClassFormViewModel> BuildFormAsync(ApplicationUser user, ClassFormViewModel model)
    {
        model.Courses = await LoadCoursesAsync(user);
        if (model.CourseId == Guid.Empty && model.Courses.Count > 0)
        {
            model.CourseId = model.Courses[0].Id;
        }

        return model;
    }

    private async Task<IReadOnlyList<CourseOptionViewModel>> LoadCoursesAsync(ApplicationUser user)
    {
        if (user.TenantId is null) return [];

        return await _db.Courses
            .AsNoTracking()
            .Where(c => c.TenantId == user.TenantId)
            .OrderBy(c => c.Title)
            .Select(c => new CourseOptionViewModel { Id = c.Id, Title = c.Title })
            .ToListAsync();
    }
}
