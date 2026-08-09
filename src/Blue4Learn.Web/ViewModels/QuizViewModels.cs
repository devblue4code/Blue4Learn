using System.ComponentModel.DataAnnotations;

namespace Blue4Learn.Web.ViewModels;

public class QuizResultsViewModel
{
    public string CourseTitle { get; set; } = string.Empty;
    public string QuizTitle { get; set; } = string.Empty;
    public Guid QuizId { get; set; }
    public int QuestionCount { get; set; }
    public int AttemptCount { get; set; }
    public double AveragePercent { get; set; }
    public List<QuizAttemptRowViewModel> Attempts { get; set; } = [];
}

public class QuizAttemptRowViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int Percent { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}

public class QuizListViewModel
{
    public List<QuizListItemViewModel> Quizzes { get; set; } = [];
}

public class QuizListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public bool AlreadyAttempted { get; set; }
    public int? LastScore { get; set; }
    public int? LastMaxScore { get; set; }
}

public class QuizTakeViewModel
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<QuizQuestionTakeViewModel> Questions { get; set; } = [];
}

public class QuizQuestionTakeViewModel
{
    public Guid Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    [Required]
    public string? SelectedOption { get; set; }
}

public class QuizSubmitViewModel
{
    public Guid QuizId { get; set; }
    public List<QuizAnswerInput> Answers { get; set; } = [];
}

public class QuizAnswerInput
{
    public Guid QuestionId { get; set; }
    public string SelectedOption { get; set; } = string.Empty;
}
