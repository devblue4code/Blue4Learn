using Blue4Learn.Web.Data;
using Blue4Learn.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class FilesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly IFileStorageService _files;

    public FilesController(ApplicationDbContext db, IAccessService access, IFileStorageService files)
    {
        _db = db;
        _access = access;
        _files = files;
    }

    public async Task<IActionResult> Attachment(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var attachment = await _db.SubmissionAttachments
            .Include(a => a.Submission).ThenInclude(s => s.User)
            .Include(a => a.Submission).ThenInclude(s => s.Activity).ThenInclude(act => act.Lesson).ThenInclude(l => l.Module)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment is null) return NotFound();

        var submission = attachment.Submission;
        var isOwner = submission.UserId == user.Id;
        var canTeacher = await _access.CanViewStudentAsync(user, submission.UserId);
        var canCourse = await _access.CanAccessCourseAsync(user, submission.Activity.Lesson.Module.CourseId);

        if (!isOwner && !(canTeacher && canCourse))
        {
            return Forbid();
        }

        var tenantId = submission.User.TenantId ?? user.TenantId;
        if (tenantId is null) return NotFound();

        var path = _files.GetPhysicalPath(tenantId.Value, submission.Id, attachment.StoredFileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, attachment.ContentType, attachment.OriginalFileName);
    }
}
