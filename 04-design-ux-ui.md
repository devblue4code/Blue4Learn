# Definição de UX/UI e design system

## Direção de design

O Blue4Learn deve transmitir clareza, acolhimento, organização e evolução. A experiência precisa parecer mais próxima de um caderno inteligente do que de um sistema administrativo pesado.

## Personalidade

- humano;
- curioso;
- organizado;
- encorajador;
- confiável;
- tecnológico sem ser frio.

## Princípios de experiência

1. O aluno deve saber imediatamente o que estudar e o que registrar.
2. Cada aula deve ter uma ação principal clara.
3. A escrita deve ser confortável no celular.
4. O sistema deve valorizar progresso e tentativa, não apenas conclusão.
5. Indicadores devem apoiar decisões, não rotular estudantes.
6. Professora e estudante devem ter espaços claramente separados.

## Navegação principal

### Estudante

- Início;
- Minhas turmas;
- Conteúdos;
- Meu diário;
- Atividades;
- Dúvidas;
- Portfólio;
- Perfil.

### Professora

- Visão geral;
- Turmas;
- Conteúdos;
- Aulas;
- Atividades;
- Dúvidas da turma;
- Acompanhamento;
- Configurações.

## Tela central da aula

```text
Cabeçalho da aula
 ├── objetivo
 ├── progresso
 └── ações rápidas

Conteúdo Markdown
 ├── índice
 ├── texto e código
 └── materiais

Meu registro
 ├── anotação
 ├── dúvida
 ├── conceito marcado
 ├── evidência
 └── reflexão
```

## Paleta inicial

| Função | Cor sugerida | Uso |
|---|---|---|
| Azul principal | `#2563EB` | marca, navegação e ações principais |
| Azul profundo | `#172554` | cabeçalhos e contraste |
| Ciano/teal | `#0F766E` | progresso e conceitos compreendidos |
| Âmbar | `#D97706` | atenção e revisão |
| Vermelho suave | `#DC2626` | erros, pendências e alertas |
| Fundo claro | `#F8FAFC` | área geral |
| Superfície | `#FFFFFF` | cartões e conteúdo |
| Texto | `#1E293B` | leitura principal |

As cores devem ser complementadas por texto, ícones ou padrões; nunca depender apenas da cor para comunicar estado.

## Tipografia

- Interface: Inter ou fonte sans-serif equivalente;
- Conteúdo: Inter, system sans ou fonte de leitura com boa altura de linha;
- Código: JetBrains Mono ou fonte monoespaçada equivalente.

## Componentes prioritários

- barra lateral responsiva;
- cabeçalho de aula;
- cartão de progresso;
- bloco de conteúdo;
- bloco de código;
- callout de atenção, dica e desafio;
- editor de anotação;
- cartão de dúvida;
- checklist;
- timeline de aprendizagem;
- tabela de conceitos;
- empty states;
- toast e feedback de salvamento.

## Responsividade

### Mobile

- uma coluna;
- menu recolhível;
- ações fixas ou facilmente acessíveis;
- leitura sem excesso de elementos;
- autosave com indicação visível;
- código com rolagem horizontal controlada.

### Desktop

- navegação lateral;
- conteúdo central com largura de leitura;
- painel contextual para diário e progresso;
- atalhos de teclado no editor.

## Acessibilidade

- contraste adequado;
- navegação por teclado;
- foco visível;
- labels explícitos;
- mensagens de erro compreensíveis;
- hierarquia correta de títulos;
- suporte a leitor de tela;
- áreas de toque adequadas no celular.

## Tom de voz

O produto deve falar como uma professora orientadora: claro, respeitoso e estimulante.

Exemplos:

- “O que você conseguiu compreender nesta aula?”
- “Registre a dúvida para retomarmos depois.”
- “Você já possui uma evidência para esta atividade?”
- “Este conceito aparece novamente em aulas posteriores.”

Evitar:

- “Você falhou.”
- “Resposta errada” sem orientação;
- linguagem excessivamente infantil;
- notificações que pressionem ou exponham o aluno.

