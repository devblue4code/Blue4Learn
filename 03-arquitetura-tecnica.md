# Arquitetura técnica

## Diretriz

A arquitetura deve ser suficientemente simples para ser ensinada em sala, mas organizada para evoluir para um SaaS multi-tenant.

## Stack inicial

- .NET 8;
- ASP.NET Core MVC com Razor Views;
- Entity Framework Core;
- SQL Server;
- ASP.NET Core Identity;
- Bootstrap ou Tailwind CSS;
- JavaScript modular;
- Markdown renderer seguro;
- Highlight.js ou equivalente para código;
- armazenamento local no piloto, com abstração para S3 futuramente;
- GitHub para versionamento do código e dos conteúdos.

## Camadas

```text
Browser/PWA
    ↓
ASP.NET Core MVC
    ├── Controllers
    ├── ViewModels
    ├── Application Services
    ├── Domain Rules
    ├── Infrastructure
    └── Persistence
             ↓
        SQL Server + File Storage
```

## Módulos de aplicação

1. **Identity:** usuários, perfis e permissões.
2. **Tenancy:** instituições, ambientes e isolamento lógico.
3. **Learning Content:** disciplinas, módulos, aulas, Markdown e materiais.
4. **Student Journal:** registros, anotações, dúvidas e reflexões.
5. **Activities:** atividades, entregas, evidências e feedback.
6. **Insights:** indicadores simples e agregações pedagógicas.
7. **Files:** anexos, validação e armazenamento.
8. **Integrations:** GitHub e IA em etapas posteriores.

## Entidades iniciais

```text
Tenant
 ├── User
 ├── Course
 │    ├── Module
 │    │    └── Lesson
 │    │         ├── ContentDocument
 │    │         ├── Activity
 │    │         └── Concept
 │    └── Class
 └── Enrollment

StudentJournalEntry
 ├── Note
 ├── Question
 ├── Reflection
 └── Evidence
```

## Decisões técnicas

### Markdown

O Markdown será a fonte editorial principal. O sistema armazenará o conteúdo original, metadados e uma versão renderizada somente quando isso trouxer ganho de desempenho. HTML recebido deve passar por sanitização.

### Entity versus ViewModel

Entidades de banco nunca serão enviadas diretamente para a tela. Controllers receberão e devolverão ViewModels específicos, reduzindo exposição de dados e acoplamento.

### Multi-tenancy

No primeiro ciclo, o isolamento pode usar `TenantId` nas entidades principais e filtros de autorização. A evolução poderá separar bancos ou schemas conforme o plano contratado.

### Arquivos

Anexos terão limite de tamanho, extensão permitida, nome seguro, registro de proprietário e vínculo com o tenant. O armazenamento deve ser abstraído para permitir migração para S3.

## Segurança

- Identity e políticas de autorização;
- antiforgery em formulários;
- validação server-side e client-side;
- sanitização de Markdown/HTML;
- proteção contra upload malicioso;
- rate limiting em endpoints sensíveis;
- segregação por tenant;
- auditoria de publicação e exclusão;
- minimização de dados pessoais;
- consentimento e política de retenção.

## Testes

- testes unitários para regras do diário e publicação;
- testes de integração para persistência;
- testes funcionais para login, publicação e entrega;
- testes de autorização por perfil e tenant;
- testes de renderização segura de Markdown.

## Evolução técnica

Depois do MVP:

1. Web API para integrações;
2. PWA com cache de leitura;
3. canvas baseado em biblioteca consolidada;
4. integração GitHub via OAuth;
5. serviço de IA com fila e controle de custo;
6. dashboards analíticos;
7. separação gradual de frontend e backend, se necessário.

