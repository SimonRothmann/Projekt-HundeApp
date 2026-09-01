using Dogity.Domain.Common;

namespace Dogity.Domain.Learning;

/// <summary>Eine Antwortmöglichkeit einer Auswahlfrage.</summary>
public class QuizOption : Entity
{
    public Guid QuestionId { get; set; }
    public QuizQuestion? Question { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int SortOrder { get; set; }
}
