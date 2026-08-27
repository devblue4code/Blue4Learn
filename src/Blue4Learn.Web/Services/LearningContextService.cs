using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Services;

public interface ILearningContextService
{
    Guid? SelectedCourseId { get; }
    Guid? SelectedClassId { get; }
    void SetCourse(Guid courseId, Guid? classGroupId = null);
    void Clear();
    Task<Course?> ResolveCourseAsync(ApplicationUser user, Guid? preferredCourseId = null);
    Task<ClassGroup?> ResolveClassAsync(ApplicationUser user, Guid? preferredClassId = null);
}

public class LearningContextService : ILearningContextService
{
    private const string CourseKey = "b4l.selectedCourseId";
    private const string ClassKey = "b4l.selectedClassId";

    private readonly IHttpContextAccessor _http;
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;

    public LearningContextService(
        IHttpContextAccessor http,
        ApplicationDbContext db,
        IAccessService access)
    {
        _http = http;
        _db = db;
        _access = access;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public Guid? SelectedCourseId =>
        Guid.TryParse(Session?.GetString(CourseKey), out var id) ? id : null;

    public Guid? SelectedClassId =>
        Guid.TryParse(Session?.GetString(ClassKey), out var id) ? id : null;

    public void SetCourse(Guid courseId, Guid? classGroupId = null)
    {
        var session = Session;
        if (session is null) return;

        session.SetString(CourseKey, courseId.ToString());
        if (classGroupId is Guid classId)
        {
            session.SetString(ClassKey, classId.ToString());
        }
        else
        {
            session.Remove(ClassKey);
        }
    }

    public void Clear()
    {
        Session?.Remove(CourseKey);
        Session?.Remove(ClassKey);
    }

    public async Task<Course?> ResolveCourseAsync(ApplicationUser user, Guid? preferredCourseId = null)
    {
        var accessible = await _access.GetAccessibleCourseIdsAsync(user);
        if (accessible.Count == 0) return null;

        async Task<Course?> Load(Guid id) =>
            await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && accessible.Contains(c.Id));

        if (preferredCourseId is Guid preferred)
        {
            var course = await Load(preferred);
            if (course is not null)
            {
                SetCourse(course.Id, SelectedClassId);
                return course;
            }
        }

        if (SelectedCourseId is Guid selected)
        {
            var course = await Load(selected);
            if (course is not null) return course;
        }

        if (SelectedClassId is Guid classId)
        {
            var fromClass = await _db.ClassGroups.AsNoTracking()
                .Where(c => c.Id == classId && accessible.Contains(c.CourseId))
                .Select(c => c.CourseId)
                .FirstOrDefaultAsync();
            if (fromClass != Guid.Empty)
            {
                var course = await Load(fromClass);
                if (course is not null)
                {
                    SetCourse(course.Id, classId);
                    return course;
                }
            }
        }

        var primary = await _access.GetPrimaryClassAsync(user);
        if (primary is not null && accessible.Contains(primary.CourseId))
        {
            SetCourse(primary.CourseId, primary.Id);
            return primary.Course;
        }

        var fallback = await _db.Courses.AsNoTracking()
            .Where(c => accessible.Contains(c.Id))
            .OrderBy(c => c.Title)
            .FirstOrDefaultAsync();

        if (fallback is not null)
        {
            SetCourse(fallback.Id);
        }

        return fallback;
    }

    public async Task<ClassGroup?> ResolveClassAsync(ApplicationUser user, Guid? preferredClassId = null)
    {
        var accessible = await _access.GetAccessibleCourseIdsAsync(user);
        if (accessible.Count == 0) return null;
        var tenantOk = user.TenantId;

        async Task<ClassGroup?> LoadAnyAccessible(Guid id) =>
            await _db.ClassGroups.AsNoTracking()
                .Include(c => c.Course)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c =>
                    c.Id == id
                    && accessible.Contains(c.CourseId)
                    && (tenantOk == null || c.TenantId == tenantOk));

        if (preferredClassId is Guid preferred)
        {
            var cls = await LoadAnyAccessible(preferred);
            if (cls is not null)
            {
                SetCourse(cls.CourseId, cls.Id);
                return cls;
            }
        }

        if (SelectedClassId is Guid selected)
        {
            var cls = await LoadAnyAccessible(selected);
            if (cls is not null)
            {
                SetCourse(cls.CourseId, cls.Id);
                return cls;
            }
        }

        var course = await ResolveCourseAsync(user);
        if (course is null) return null;

        async Task<ClassGroup?> LoadFromCourse(Guid id) =>
            await _db.ClassGroups.AsNoTracking()
                .Include(c => c.Course)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c =>
                    c.Id == id
                    && c.CourseId == course.Id
                    && (tenantOk == null || c.TenantId == tenantOk));

        if (SelectedClassId is Guid selectedInCourse)
        {
            var cls = await LoadFromCourse(selectedInCourse);
            if (cls is not null)
            {
                return cls;
            }
        }

        var first = await _db.ClassGroups.AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Where(c => c.CourseId == course.Id && (tenantOk == null || c.TenantId == tenantOk))
            .OrderBy(c => c.Name)
            .FirstOrDefaultAsync();

        if (first is not null)
        {
            SetCourse(course.Id, first.Id);
        }

        return first;
    }
}
