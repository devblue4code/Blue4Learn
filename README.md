# Blue4Learn — Diário de Bordo Digital

**Empresa:** Blue4Code  
**Status:** protótipo do MVP em execução  

Plataforma de aprendizagem reflexiva que reúne conteúdo Markdown, diário individual, dúvidas, atividades, evidências e acompanhamento pedagógico.

## Documentação

- [Brief completo do produto](./01-brief-produto.md)
- [Requisitos e escopo do MVP](./02-requisitos-mvp.md)
- [Arquitetura técnica](./03-arquitetura-tecnica.md)
- [Definição de UX/UI e design system](./04-design-ux-ui.md)
- [Roadmap e estratégia de evolução](./05-roadmap.md)

## Protótipo

Solução em `src/Blue4Learn.Web` (ASP.NET Core MVC 8 + Identity + EF Core + SQLite).

### Como rodar

**Docker (recomendado):**

```bash
docker compose up --build
```

Abra [http://localhost:5080](http://localhost:5080). Dados SQLite e uploads ficam em volumes Docker.

**Local com .NET SDK:**

```bash
cd src/Blue4Learn.Web
dotnet run
```

Abra a URL indicada no terminal (ex.: `http://localhost:5273`).

### Contas demo

| Perfil | E-mail | Senha |
|---|---|---|
| Estudante | `aluno@blue4learn.local` | `Demo@123` |
| Professora | `professora@blue4learn.local` | `Demo@123` |
| Admin | `admin@blue4learn.local` | `Demo@123` |

### O que já funciona no protótipo

1. Landing + login
2. Dashboard do estudante com aulas publicadas
3. Workspace da aula: Markdown seguro, diário, conceitos, dúvidas, reflexão
4. Entrega de evidência (texto + GitHub)
5. Painel da professora: dúvidas, conceitos, visão individual
6. Publicação de Markdown pela professora (criar/editar, preview, importar `.md`, publicar/arquivar)
7. Feedback da professora com status revisada
8. Anexos nas evidências (até 5 MB)
9. Meu diário: progresso / faltam registrar / preciso revisar
10. Autorização por instituição e turma
11. Recuperação de senha em dev via `App_Data/email-outbox.txt`
12. Gestão de turmas: criar/editar, matricular por e-mail, entrar com código

### Stack do piloto

- .NET 8 / ASP.NET Core MVC / Razor
- EF Core + SQLite (SQL Server fica para o ciclo seguinte)
- ASP.NET Core Identity com papéis
- Markdig + HtmlSanitizer
- Bootstrap + CSS próprio + Highlight.js

## Decisões fundamentais

1. Markdown como fonte editorial principal
2. Primeiro público: cursos técnicos, disciplina Programação Web
3. Diário registra o processo, não só a entrega final
4. IA tutora fica fora do MVP
5. Multi-tenant no desenho, piloto com uma instituição
