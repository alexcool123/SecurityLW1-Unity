using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PasswordCheckerUI : MonoBehaviour
{
    [Header("Ввод")]
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Toggle showPasswordToggle;
    [SerializeField] private Button analyzeButton;

    [Header("Вывод")]
    [SerializeField] private Slider strengthBar;
    [SerializeField] private Image strengthFill;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text detailsText;

    [Header("Настройки")]
    [Tooltip("Анализировать при каждом вводе символа")]
    [SerializeField] private bool liveAnalysis = false;
    [Tooltip("Имя JSON-файла в Application.persistentDataPath")]
    [SerializeField] private string jsonFileName = "my_database.json";

    private PersonalData personalData = new PersonalData();

    private void Start()
    { 
        personalData = LoadPersonalData();

        analyzeButton.onClick.AddListener(Analyze);
        if (liveAnalysis) passwordInput.onValueChanged.AddListener(_ => Analyze());

        showPasswordToggle.onValueChanged.AddListener(show =>
        {
            passwordInput.contentType = show
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
        });

    }

    private PersonalData LoadPersonalData()
    {
        string path = Path.Combine(Application.persistentDataPath, jsonFileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"JSON з даними студента не знайдено: {path}");
            return null;
        }

        PersonalData data = JsonUtility.FromJson<PersonalData>(File.ReadAllText(path));
        return data;      
    }

    public void Analyze()
    {
        var result = PasswordAnalyzer.Analyze(passwordInput.text, personalData);
        Show(result);
    }

    private void Show(PasswordAnalysisResult r)
    {
        strengthBar.value = r.TotalScore;
        strengthFill.color = LevelColor(r.Level);
        levelText.text = $"{r.LevelName()} ({r.TotalScore}/100)";
        levelText.color = LevelColor(r.Level);

        var sb = new StringBuilder();

        sb.AppendLine("<b>Оцінка за критеріями</b>");
        sb.AppendLine($"Довжина: {r.LengthScore:0.#}/30");
        sb.AppendLine($"Різноманітність символів: {r.DiversityScore:0.#}/30");
        sb.AppendLine($"Унікальність: {r.UniquenessScore:0.#}/40");
        sb.AppendLine($"Ентропія: ~{r.EntropyBits:0} біт");
        sb.AppendLine();

        AppendList(sb, "Зв'язок з особистими даними", r.PersonalIssues);
        AppendList(sb, "Словникові слова", r.DictionaryIssues);
        AppendList(sb, "Шаблони", r.PatternIssues);
        AppendList(sb, "Рекомендації", r.Recommendations);

        detailsText.text = sb.ToString();
    }

    private static void AppendList(StringBuilder sb, string title, System.Collections.Generic.List<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"<b>{title}</b>");
        foreach (var i in items) sb.AppendLine("• " + i);
        sb.AppendLine();
    }

    private static Color LevelColor(StrengthLevel l)
    {
        switch (l)
        {
            case StrengthLevel.VeryWeak: return new Color(0.85f, 0.15f, 0.15f);
            case StrengthLevel.Weak: return new Color(0.95f, 0.5f, 0.1f);
            case StrengthLevel.Medium: return new Color(0.95f, 0.8f, 0.1f);
            case StrengthLevel.Strong: return new Color(0.5f, 0.8f, 0.2f);
            default: return new Color(0.1f, 0.7f, 0.3f);
        }
    }
}
