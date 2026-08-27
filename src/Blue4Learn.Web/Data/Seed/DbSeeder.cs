using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Data.Seed;

public static class DbSeeder
{
    public const string DemoPassword = "Demo@123";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (await db.Tenants.AnyAsync())
        {
            await EnsureDemoQuizAsync(db);
            await EnrichCourseDescriptionAsync(db);
            await EnsureCourseTeachersAsync(db, userManager);
            await EnsureDemoLocalAccountsAsync(db, userManager);
            return;
        }

        var tenant = new Tenant
        {
            Name = "Blue4Code Academy",
            Slug = "blue4code"
        };

        var course = new Course
        {
            Tenant = tenant,
            Title = "Programação Web",
            Slug = "programacao-web",
            Description = "Estudo e desenvolvimento de aplicações para a Internet utilizando tecnologias web front-end e back-end. A disciplina aborda conceitos fundamentais da arquitetura Cliente-Servidor, protocolos web, marcação, estilização, e programação do lado do cliente (browser) e do servidor. O foco está na criação de sistemas web interativos, responsivos e acessíveis, preparando o aluno para os desafios do mercado de desenvolvimento de software moderno.",
            Syllabus = """
                Estudo dos fundamentos da World Wide Web e do desenvolvimento de páginas e interfaces. Aborda o modelo cliente-servidor, o protocolo HTTP, a estruturação semântica com HTML, a estilização e o layout responsivo com CSS, além da organização do processo de aprendizagem por meio de diário de bordo, dúvidas, práticas e evidências.

                ## Objetivos gerais

                - Compreender como a Web funciona, do navegador ao servidor.
                - Estruturar páginas com HTML semântico e acessível.
                - Aplicar CSS com abordagem moderna e responsiva.
                - Registrar a aprendizagem com diário, atividades e evidências.
                """,
            Methodologies = """
                ## Metodologias

                O componente combina exposição dialogada, estudo ativo do material Markdown, prática guiada e acompanhamento pedagógico por evidências.

                ### Estratégias

                - **Aula-conteúdo:** leitura e exploração do material publicado no Blue4Learn.
                - **Aprendizagem ativa:** atividades práticas com descrição do problema e da solução.
                - **Diário de bordo:** registro individual de anotações, dúvidas e conceitos.
                """
        };

        var module = new Module
        {
            Course = course,
            Title = "Fundamentos da Web",
            SortOrder = 1
        };

        var classGroup = new ClassGroup
        {
            Tenant = tenant,
            Course = course,
            Name = "Turma PW-2026.1",
            Code = "PW261"
        };

        var lesson1 = CreateLesson(module, classGroup, 1, "introducao-http", "Introdução à Web e HTTP",
            "Compreender o modelo cliente-servidor e o papel do HTTP.",
            Lesson1Markdown(),
            ["Cliente-servidor", "HTTP", "URL", "Navegador"],
            "Monte um mapa mental (texto) explicando o caminho de uma requisição HTTP.");

        var lesson2 = CreateLesson(module, classGroup, 2, "html-semantico", "HTML Semântico",
            "Estruturar páginas com tags semânticas e acessíveis.",
            Lesson2Markdown(),
            ["HTML", "Semântica", "Acessibilidade", "DOM"],
            "Crie uma página HTML semântica com header, main, article e footer. Publique o link do repositório.");

        var lesson3 = CreateLesson(module, classGroup, 3, "css-layout", "CSS e Layout Responsivo",
            "Organizar layout com Flexbox e pensar mobile-first.",
            Lesson3Markdown(),
            ["CSS", "Flexbox", "Mobile-first", "Responsividade"],
            "Adapte a página anterior para mobile usando Flexbox. Descreva o problema e a solução.");

        var lesson4 = CreateLesson(module, classGroup, 4, "diario-de-bordo", "Diário de Bordo na Prática",
            "Registrar dúvidas, evidências e reflexão como parte da aprendizagem.",
            Lesson4Markdown(),
            ["Metacognição", "Evidência", "Reflexão", "Feedback"],
            "Complete o diário desta aula e envie uma evidência do que você compreendeu.");

        db.Tenants.Add(tenant);
        db.Courses.Add(course);
        db.Modules.Add(module);
        db.ClassGroups.Add(classGroup);
        db.Lessons.AddRange(lesson1, lesson2, lesson3, lesson4);
        await db.SaveChangesAsync();

        var teacher = await EnsureUserAsync(userManager, "Ana Professora", "professora@blue4learn.local", tenant.Id, AppRoles.Teacher);
        var student = await EnsureUserAsync(userManager, "Lucas Estudante", "aluno@blue4learn.local", tenant.Id, AppRoles.Student);
        var student2 = await EnsureUserAsync(userManager, "Marina Estudante", "marina@blue4learn.local", tenant.Id, AppRoles.Student);
        await EnsureUserAsync(userManager, "Admin Blue4", "admin@blue4learn.local", tenant.Id, AppRoles.Admin);

        course.TeacherUserId = teacher.Id;

        db.Enrollments.AddRange(
            new Enrollment { ClassGroupId = classGroup.Id, UserId = teacher.Id },
            new Enrollment { ClassGroupId = classGroup.Id, UserId = student.Id },
            new Enrollment { ClassGroupId = classGroup.Id, UserId = student2.Id });

        await db.SaveChangesAsync();
        await EnsureDemoQuizAsync(db);
    }

    private static async Task EnsureCourseTeachersAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var unassigned = await db.Courses
            .Where(c => c.TeacherUserId == null)
            .ToListAsync();
        if (unassigned.Count == 0)
        {
            return;
        }

        foreach (var course in unassigned)
        {
            // Prefer a teacher already enrolled in a class of this course.
            var enrolledTeacherId = await (
                from e in db.Enrollments.AsNoTracking()
                join cg in db.ClassGroups.AsNoTracking() on e.ClassGroupId equals cg.Id
                where cg.CourseId == course.Id
                select e.UserId
            ).FirstOrDefaultAsync();

            if (enrolledTeacherId is not null)
            {
                var enrolledUser = await userManager.FindByIdAsync(enrolledTeacherId);
                if (enrolledUser is not null && await userManager.IsInRoleAsync(enrolledUser, AppRoles.Teacher))
                {
                    course.TeacherUserId = enrolledTeacherId;
                    continue;
                }
            }

            // Fallback: first teacher in the same tenant.
            var tenantTeachers = await userManager.GetUsersInRoleAsync(AppRoles.Teacher);
            var tenantTeacher = tenantTeachers.FirstOrDefault(u => u.TenantId == course.TenantId);
            if (tenantTeacher is not null)
            {
                course.TeacherUserId = tenantTeacher.Id;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnrichCourseDescriptionAsync(ApplicationDbContext db)
    {
        var course = await db.Courses.OrderBy(c => c.Title).FirstOrDefaultAsync();
        if (course is null || course.Description.Length > 120)
        {
            return;
        }

        course.Description =
            "Estudo e desenvolvimento de aplicações para a Internet utilizando tecnologias web front-end e back-end. A disciplina aborda conceitos fundamentais da arquitetura Cliente-Servidor, protocolos web, marcação, estilização, e programação do lado do cliente (browser) e do servidor. O foco está na criação de sistemas web interativos, responsivos e acessíveis, preparando o aluno para os desafios do mercado de desenvolvimento de software moderno.";
        await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoQuizAsync(ApplicationDbContext db)
    {
        if (await db.Quizzes.AnyAsync())
        {
            return;
        }

        var course = await db.Courses.OrderBy(c => c.Title).FirstOrDefaultAsync();
        if (course is null)
        {
            return;
        }

        var quiz = new Quiz
        {
            CourseId = course.Id,
            Title = "Quiz · Fundamentos da Web",
            Description = "Verifique conceitos de HTTP, HTML e arquitetura cliente-servidor.",
            IsPublished = true,
            Questions =
            [
                new QuizQuestion
                {
                    SortOrder = 1,
                    Prompt = "Qual protocolo é usado para transferir páginas na Web?",
                    OptionA = "FTP",
                    OptionB = "HTTP",
                    OptionC = "SMTP",
                    OptionD = "SSH",
                    CorrectOption = "B"
                },
                new QuizQuestion
                {
                    SortOrder = 2,
                    Prompt = "Em uma arquitetura cliente-servidor, o navegador atua como:",
                    OptionA = "Servidor de banco",
                    OptionB = "Cliente",
                    OptionC = "Proxy DNS",
                    OptionD = "Balanceador",
                    CorrectOption = "B"
                },
                new QuizQuestion
                {
                    SortOrder = 3,
                    Prompt = "Qual tag HTML representa o conteúdo principal da página?",
                    OptionA = "<div>",
                    OptionB = "<section>",
                    OptionC = "<main>",
                    OptionD = "<span>",
                    CorrectOption = "C"
                }
            ]
        };

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var student = await db.Users.FirstOrDefaultAsync(u => u.Email == "aluno@blue4learn.local");
        if (student is not null)
        {
            db.QuizAttempts.Add(new QuizAttempt
            {
                QuizId = quiz.Id,
                UserId = student.Id,
                Score = 2,
                MaxScore = 3,
                SubmittedAtUtc = DateTime.UtcNow.AddHours(-5)
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureDemoLocalAccountsAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        var tenantId = await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();

        if (tenantId == Guid.Empty)
        {
            return;
        }

        await EnsureUserAsync(userManager, "Admin Blue4", "admin@blue4learn.local", tenantId, AppRoles.Admin);
        await EnsureUserAsync(userManager, "Ana Professora", "professora@blue4learn.local", tenantId, AppRoles.Teacher);
        await EnsureUserAsync(userManager, "Lucas Estudante", "aluno@blue4learn.local", tenantId, AppRoles.Student);
        await EnsureUserAsync(userManager, "Marina Estudante", "marina@blue4learn.local", tenantId, AppRoles.Student);

        // Garante senha demo conhecida nas contas *.blue4learn.local (só se já existirem).
        foreach (var email in new[]
                 {
                     "admin@blue4learn.local",
                     "professora@blue4learn.local",
                     "aluno@blue4learn.local",
                     "marina@blue4learn.local"
                 })
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null) continue;

            user.EmailConfirmed = true;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            await userManager.UpdateAsync(user);

            if (!await userManager.CheckPasswordAsync(user, DemoPassword))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                await userManager.ResetPasswordAsync(user, token, DemoPassword);
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string fullName,
        string email,
        Guid tenantId,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                TenantId = tenantId
            };

            var result = await userManager.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static Lesson CreateLesson(
        Module module,
        ClassGroup classGroup,
        int order,
        string slug,
        string title,
        string objective,
        string markdown,
        IEnumerable<string> concepts,
        string activityPrompt)
    {
        var lesson = new Lesson
        {
            Module = module,
            ClassGroup = classGroup,
            Title = title,
            Slug = slug,
            Objective = objective,
            SortOrder = order,
            Status = ContentStatus.Published,
            ContentDocument = new ContentDocument
            {
                Title = title,
                Markdown = markdown
            }
        };

        foreach (var concept in concepts)
        {
            lesson.Concepts.Add(new Concept
            {
                Name = concept,
                Description = $"Conceito: {concept}"
            });
        }

        lesson.Activities.Add(new Activity
        {
            Title = $"Atividade — {title}",
            Prompt = activityPrompt,
            DueAtUtc = DateTime.UtcNow.AddDays(7 + order)
        });

        return lesson;
    }

    private static string Lesson1Markdown() => """
# Introdução à Web e HTTP

A Web funciona como uma conversa entre **cliente** e **servidor**.

## O que acontece quando você abre um site?

1. Você digita uma URL no navegador
2. O navegador monta uma requisição HTTP
3. O servidor responde com HTML, CSS, imagens etc.
4. O navegador renderiza a página

## Exemplo de requisição

```http
GET /aulas/introducao HTTP/1.1
Host: blue4learn.local
Accept: text/html
```

> **Dica:** não memorize jargão. Explique com suas palavras o caminho da requisição.

## Desafio rápido

Antes de registrar no diário, responda mentalmente:

- Quem é o cliente?
- Quem é o servidor?
- O que viaja na rede?
""";

    private static string Lesson2Markdown() => """
# HTML Semântico

HTML semântico comunica **estrutura e significado**, não só aparência.

## Estrutura básica

```html
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <title>Minha página</title>
</head>
<body>
  <header>...</header>
  <main>
    <article>...</article>
  </main>
  <footer>...</footer>
</body>
</html>
```

## Por que importa?

- leitores de tela navegam melhor
- buscadores entendem a hierarquia
- o código fica mais fácil de manter

## Callout

> **Atenção:** `div` não é errado, mas use tags semânticas quando houver significado claro.
""";

    private static string Lesson3Markdown() => """
# CSS e Layout Responsivo

Comece pelo celular e depois amplie o layout.

## Flexbox em 30 segundos

```css
.container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

@media (min-width: 768px) {
  .container {
    flex-direction: row;
  }
}
```

## Checklist mental

- o texto continua legível?
- os botões são fáceis de tocar?
- o código CSS descreve intenção ou só “conserta” pixels?

> **Desafio:** reduza a largura da tela e observe o que quebra primeiro.
""";

    private static string Lesson4Markdown() => """
# Diário de Bordo na Prática

No Blue4Learn, o diário não é burocracia: é evidência do seu processo.

## O ciclo

1. Ler o conteúdo
2. Anotar o que faz sentido
3. Registrar dúvida sem vergonha
4. Praticar e anexar evidência
5. Refletir: o que mudou na sua compreensão?

## Exemplo de reflexão

> “Entendi o fluxo HTTP, mas ainda confundo status code com método. Vou revisar com um exemplo GET vs POST.”

## Para a professora

Seus registros ajudam a decidir o que retomar na próxima aula — sem expor sua anotação privada para a turma.
""";
}
