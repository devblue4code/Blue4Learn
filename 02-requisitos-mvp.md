# Requisitos e escopo do MVP

## Objetivo do MVP

Validar se uma turma consegue estudar o conteúdo, registrar sua aprendizagem e entregar evidências usando um único ambiente.

## Escopo incluído

### Autenticação e acesso

- login e logout;
- recuperação de acesso;
- perfis: estudante, professora e administrador;
- autorização por instituição, turma e ambiente.

### Organização pedagógica

- instituição;
- disciplina;
- turma;
- módulo;
- aula;
- conceitos;
- materiais e links.

### Conteúdo estilo GitBook

- importação de arquivos `.md`;
- cadastro de título, slug e ordem;
- renderização segura;
- índice da página;
- navegação anterior/próxima;
- blocos de código com destaque;
- imagens, tabelas, links e vídeos;
- estados: rascunho, publicado e arquivado;
- visibilidade por turma ou público.

### Diário do estudante

- registro por aula;
- anotação privada;
- dúvida;
- conceito marcado;
- reflexão final;
- checklist de aprendizagem;
- histórico básico de edição.

### Atividades e evidências

- enunciado e prazo;
- entrega textual;
- link de GitHub;
- anexos;
- descrição do problema e da solução;
- feedback da professora;
- status: não iniciada, em andamento, enviada, revisada.

### Painel da professora

- aulas publicadas;
- registros pendentes;
- atividades entregues;
- dúvidas abertas;
- conceitos mais marcados;
- visão individual do estudante;
- visão resumida da turma.

## Fora do MVP

- canvas completo semelhante ao Excalidraw;
- aplicativo nativo;
- sincronização bidirecional com GitHub;
- IA generativa ampla;
- análise automática de código;
- gamificação complexa;
- marketplace de cursos;
- pagamentos e emissão acadêmica.

## Requisitos não funcionais

- interface responsiva, mobile-first;
- renderização segura de Markdown e HTML;
- controle de autorização em todas as operações;
- validação de arquivos e anexos;
- logs de ações administrativas;
- backup do banco e arquivos;
- acessibilidade básica conforme WCAG;
- desempenho adequado em rede móvel;
- proteção de dados pessoais conforme LGPD.

## Histórias principais

### Estudante

- Como estudante, quero abrir a aula no celular para revisar o conteúdo durante o trajeto.
- Como estudante, quero registrar uma dúvida vinculada ao conteúdo para retomá-la depois.
- Como estudante, quero anexar o link do meu repositório para comprovar minha prática.
- Como estudante, quero visualizar o que já registrei e o que ainda preciso revisar.

### Professora

- Como professora, quero publicar um Markdown e associá-lo a uma aula.
- Como professora, quero ver as dúvidas recorrentes antes de iniciar a próxima aula.
- Como professora, quero comentar o registro de um estudante sem expor sua reflexão à turma.
- Como professora, quero identificar conceitos pouco compreendidos.

## Critério de sucesso do MVP

O MVP será considerado validado quando uma turma utilizar o fluxo completo por pelo menos quatro aulas e houver evidência de que os registros orientaram pelo menos uma revisão ou intervenção pedagógica.

