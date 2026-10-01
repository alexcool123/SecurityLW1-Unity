using System.Collections.Generic;

public enum StrengthLevel
{
    VeryWeak,
    Weak,
    Medium,
    Strong,
    VeryStrong
}

public class PasswordAnalysisResult
{
    //0..100
    public int TotalScore;
    public StrengthLevel Level;

    public float LengthScore;      // 0..30
    public float DiversityScore;   // 0..30
    public float UniquenessScore;  // 0..40

    public double EntropyBits;

    public bool HasLower, HasUpper, HasDigit, HasSpecial;

    public List<string> DictionaryIssues = new List<string>();
    public List<string> PersonalIssues = new List<string>();
    public List<string> PatternIssues = new List<string>();

    // Рекомендации
    public List<string> Recommendations = new List<string>();

    public string LevelName()
    {
        switch (Level)
        {
            case StrengthLevel.VeryWeak: return "Дуже слабкий";
            case StrengthLevel.Weak: return "Слабкий";
            case StrengthLevel.Medium: return "Середній";
            case StrengthLevel.Strong: return "Надійний";
            default: return "Дуже надійний";
        }
    }
}
