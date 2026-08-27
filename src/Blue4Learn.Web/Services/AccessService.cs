using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Services;

public interface IAccessService
{
    Task<ApplicationUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal);
    Task<bool> IsTeacherOrAdminAsync(ApplicationUser user);
    Task<IReadOnlyList<Guid>> GetAccessibleCourseIdsAsync(ApplicationUser user);
    Task<IReadOnlyList<Guid>> GetAccessibleClassGroupIdsAsync(ApplicationUser user);
    Task<bool> CanAccessCourseAsync(ApplicationUser user, Guid courseId);
    Task<bool> CanAccessLessonAsync(ApplicationUser user, Guid lessonId);
    Task<bool> CanManageContentAsync(ApplicationUser user);
    Task<bool> CanManageClassesAsync(ApplicationUser user);
    Task<bool> CanManageClassAsync(ApplicationUser user, Guid classGroupId);
    Task<bool> CanViewStudentAsync(ApplicationUser teacher, string studentUserId);
    Task<ClassGroup?> GetPrimaryClassAsync(ApplicationUser user);
    Task<IReadOnlyList<string>> GetStudentIdsInSharedClassesAsync(ApplicationUser teacher);
}

public class AccessService : IAccessService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccessService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public Task<ApplicationUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal)
        => _userManager.GetUserAsync(principal);

    public async Task<bool> IsTeacherOrAdminAsync(ApplicationUser user)
        => await _userManager.IsInRoleAsync(user, AppRoles.Teacher)
           || await _userManager.IsInRoleAsync(user, AppRoles.Admin);

    public async Task<IReadOnlyList<Guid>> GetAccessibleCourseIdsAsync(ApplicationUser user)
    {
        // Admin (tenant): todas as disciplinas da instituição.
        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin) && user.TenantId is Guid tenantId)
        {
            return await _db.Courses
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.Id)
                .ToListAsync();
        }

        // Professora: apenas a(s) disciplina(s) atribuída(s) em Course.TeacherUserId (mesmo tenant).
        if (await _userManager.IsInRoleAsync(user, AppRoles.Teacher))
        {
            var query = _db.Courses.AsNoTracking().Where(c => c.TeacherUserId == user.Id);
            if (user.TenantId is Guid teacherTenantId)
            {
                query = query.Where(c => c.TenantId == teacherTenantId);
            }

            return await query.Select(c => c.Id).ToListAsync();
        }

        // Estudante: disciplinas das turmas em que está matriculado.
        return await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .Select(e => e.ClassGroup.CourseId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleClassGroupIdsAsync(ApplicationUser user)
    {
        if (await IsTeacherOrAdminAsync(user))
        {
            var courseIds = await GetAccessibleCourseIdsAsync(user);
            return await _db.ClassGroups
                .AsNoTracking()
                .Where(c => courseIds.Contains(c.CourseId))
                .Select(c => c.Id)
                .ToListAsync();
        }

        return await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .Select(e => e.ClassGroupId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<bool> CanAccessCourseAsync(ApplicationUser user, Guid courseId)
    {
        var courses = await GetAccessibleCourseIdsAsync(user);
        return courses.Contains(courseId);
    }

    public async Task<bool> CanAccessLessonAsync(ApplicationUser user, Guid lessonId)
    {
        var lesson = await _db.Lessons
            .AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => new { l.ClassGroupId, CourseId = l.Module.CourseId })
            .FirstOrDefaultAsync();

        if (lesson is null)
        {
            return false;
        }

        if (await IsTeacherOrAdminAsync(user))
        {
            return await CanAccessCourseAsync(user, lesson.CourseId);
        }

        return await _db.Enrollments.AnyAsync(e =>
            e.UserId == user.Id && e.ClassGroupId == lesson.ClassGroupId);
    }

    public async Task<bool> CanManageContentAsync(ApplicationUser user)
    {
        if (!await IsTeacherOrAdminAsync(user))
        {
            return false;
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return user.TenantId.HasValue;
        }

        return await _db.Courses.AnyAsync(c => c.TeacherUserId == user.Id);
    }

    public async Task<bool> CanManageClassesAsync(ApplicationUser user)
    {
        if (user.TenantId is null || !await IsTeacherOrAdminAsync(user))
        {
            return false;
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return true;
        }

        // Professora só gere turmas se tiver disciplina atribuída.
        return await _db.Courses.AnyAsync(c => c.TeacherUserId == user.Id);
    }

    public async Task<bool> CanManageClassAsync(ApplicationUser user, Guid classGroupId)
    {
        if (!await CanManageClassesAsync(user) || user.TenantId is null)
        {
            return false;
        }

        var classGroup = await _db.ClassGroups.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classGroupId);

        if (classGroup is null || classGroup.TenantId != user.TenantId)
        {
            return false;
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return true;
        }

        // Professora: só turmas da disciplina atribuída (matrícula não abre outra disciplina).
        return await _db.Courses.AsNoTracking()
            .AnyAsync(c => c.Id == classGroup.CourseId && c.TeacherUserId == user.Id);
    }

    public async Task<bool> CanViewStudentAsync(ApplicationUser teacher, string studentUserId)
    {
        if (!await IsTeacherOrAdminAsync(teacher))
        {
            return false;
        }

        if (await _userManager.IsInRoleAsync(teacher, AppRoles.Admin)
            && teacher.TenantId.HasValue)
        {
            var student = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentUserId);
            return student?.TenantId == teacher.TenantId;
        }

        var shared = await GetStudentIdsInSharedClassesAsync(teacher);
        return shared.Contains(studentUserId);
    }

    public async Task<ClassGroup?> GetPrimaryClassAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin) && user.TenantId is Guid tenantId)
        {
            return await _db.ClassGroups
                .AsNoTracking()
                .Include(c => c.Course)
                .Include(c => c.Enrollments)
                .Where(c => c.TenantId == tenantId)
                .OrderBy(c => c.Name)
                .FirstOrDefaultAsync();
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Teacher))
        {
            return await _db.ClassGroups
                .AsNoTracking()
                .Include(c => c.Course)
                .Include(c => c.Enrollments)
                .Where(c => c.Course.TeacherUserId == user.Id)
                .OrderBy(c => c.Name)
                .FirstOrDefaultAsync();
        }

        return await _db.ClassGroups
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Where(c => c.Enrollments.Any(e => e.UserId == user.Id))
            .OrderBy(c => c.Name)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<string>> GetStudentIdsInSharedClassesAsync(ApplicationUser teacher)
    {
        List<Guid> classIds;

        if (await _userManager.IsInRoleAsync(teacher, AppRoles.Admin) && teacher.TenantId is Guid tenantId)
        {
            classIds = await _db.ClassGroups
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.Id)
                .ToListAsync();
        }
        else if (await _userManager.IsInRoleAsync(teacher, AppRoles.Teacher))
        {
            classIds = await _db.ClassGroups
                .AsNoTracking()
                .Where(c => c.Course.TeacherUserId == teacher.Id)
                .Select(c => c.Id)
                .Distinct()
                .ToListAsync();
        }
        else
        {
            classIds = await _db.Enrollments
                .AsNoTracking()
                .Where(e => e.UserId == teacher.Id)
                .Select(e => e.ClassGroupId)
                .ToListAsync();
        }

        var candidateIds = await _db.Enrollments
            .AsNoTracking()
            .Where(e => classIds.Contains(e.ClassGroupId) && e.UserId != teacher.Id)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync();

        var students = new List<string>();
        foreach (var id in candidateIds)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is not null && await _userManager.IsInRoleAsync(user, AppRoles.Student))
            {
                students.Add(id);
            }
        }

        return students;
    }
}
