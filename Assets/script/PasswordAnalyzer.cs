using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class PasswordAnalyzer
{
    static readonly HashSet<string> Common = new HashSet<string> {
        "123456","123456789","12345678","1234567","12345","111111","000000","123123","password",
        "password1","qwerty","qwerty123","qwertyuiop","abc123","admin","letmein","welcome","iloveyou",
        "monkey","dragon","football","master","login","princess","sunshine","shadow","superman",
        "1q2w3e4r","1qaz2wsx","zaq12wsx","passw0rd","qazwsx","asdfgh","zxcvbn","starwars"};

    static readonly string[] Words = {
        "password","passwd","admin","qwerty","welcome","login","letmein","master","dragon","monkey",
        "shadow","football","sunshine","princess","iloveyou","superman","batman","hello","secret",
        "student","university","unity","games","summer","winter","spring","autumn","guest",
        "default","parol","ukraine","kyiv","lviv","kharkiv" };

    static readonly string[] Rows = {
        "qwertyuiop","asdfghjkl","zxcvbnm","1234567890","йцукенгшщзхї","фівапролджє","ячсмитьбю" };

    // Транслитерация
    const string Cyr = "абвгґдеєжзиіїйклмнопрстуфхцчшщьюяыэёъ";
    static readonly string[] Lat = {
        "a","b","v","h","g","d","e","ye","zh","z","y","i","yi","y","k","l","m","n","o","p","r","s",
        "t","u","f","kh","ts","ch","sh","shch","","yu","ya","y","e","yo","" };

    // Leet
    const string LeetFrom = "03457@$!", LeetTo = "oeastasi";

    public static PasswordAnalysisResult Analyze(string pw, PersonalData d)
    {
        var r = new PasswordAnalysisResult();
        if (string.IsNullOrEmpty(pw)) { r.Recommendations.Add("Введіть пароль для аналізу."); return r; }

        // 1. Длина и разнообразие символов (по всему паролю)
        r.HasLower = pw.Any(char.IsLower);
        r.HasUpper = pw.Any(char.IsUpper);
        r.HasDigit = pw.Any(char.IsDigit);
        r.HasSpecial = pw.Any(c => !char.IsLetterOrDigit(c));
        r.LengthScore = Mathf.Min(30f, pw.Length * 2.5f);
        r.DiversityScore = 7.5f * new[] { r.HasLower, r.HasUpper, r.HasDigit, r.HasSpecial }.Count(b => b);

        // 2. Поиск передбачуваних частин.
        string low = pw.ToLowerInvariant();
        var vars = new[] { low, Leet(low, 'i'), Leet(low, 'l') };
        var weak = new bool[pw.Length];

        bool common = vars.Any(Common.Contains);
        if (common)
        {
            r.DictionaryIssues.Add("Пароль входить до списку найпопулярніших паролів.");
            Fill(weak, 0, pw.Length);
        }
        else
            foreach (var w in Words)
                if (Mark(weak, vars, w)) r.DictionaryIssues.Add($"Містить словникове слово: \"{w}\".");

        CheckPersonal(pw, vars, d, r, weak);
        CheckPatterns(pw, low, r, weak);

        // 3. Уникальность = энтропия только НЕпередбачуваних символов (0..40).
        //    Повторяющиеся символы считаются за 1/4.
        var free = pw.Where((c, i) => !weak[i]).ToArray();
        int distinct = free.Distinct().Count();
        r.EntropyBits = (distinct + 0.25 * (free.Length - distinct)) * Math.Log(Math.Max(2, Pool(free)), 2);
        r.UniquenessScore = 40f * Mathf.Pow(Mathf.Clamp01((float)r.EntropyBits / 70f), 2f);

        // 4. Итог + ограничения сверху
        float total = r.LengthScore + r.DiversityScore + r.UniquenessScore;
        if (common) total = Mathf.Min(total, 5f);
        if (r.PersonalIssues.Count > 0) total = Mathf.Min(total, 39f);
        if (r.DictionaryIssues.Count > 0) total = Mathf.Min(total, 39f);
        if (r.PatternIssues.Count > 0) total = Mathf.Min(total, 59f);
        if (pw.Length < 8) total = Mathf.Min(total, 35f);

        r.TotalScore = Mathf.Clamp(Mathf.RoundToInt(total), 0, 100);
        r.Level = (StrengthLevel)Mathf.Min(4, r.TotalScore / 20);   // 0-19, 20-39, 40-59, 60-79, 80+

        BuildRecommendations(pw, r);
        return r;
    }

    // ---------- Личные данные ----------
    static void CheckPersonal(string pw, string[] vars, PersonalData d, PasswordAnalysisResult r, bool[] weak)
    {
        if (d == null) return;
        var l = r.PersonalIssues;
        Debug.Log($"CheckPersonal: {d.firstName} {d.lastName} {d.birthYear} {d.phone} {d.studentId}");
        Add(l, TextHit(d.firstName, vars, weak), "Містить ім'я користувача (або транслітерацію / реверс).");
        Add(l, TextHit(d.lastName, vars, weak), "Містить прізвище користувача (або транслітерацію / реверс).");
        Add(l, d.birthYear > 999 && Mark(weak, new[] { pw }, d.birthYear.ToString()), $"Містить рік народження ({d.birthYear}).");
        Add(l, DigitChunk(pw, d.phone, weak), "Містить фрагмент номера телефону.");
        Add(l, DigitChunk(pw, d.studentId, weak) | TextHit(d.studentId, vars, weak), "Містить фрагмент ID студента.");
    }

    // Формы значения: оригинал, транслит, и обе задом наперёд
    static IEnumerable<string> Forms(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Enumerable.Empty<string>();
        value = value.Trim().ToLowerInvariant();
        return new[] { value, Translit(value) }
            .Where(s => s.Length >= 3)
            .SelectMany(s => new[] { s, Reverse(s) });
    }

    static bool TextHit(string value, string[] vars, bool[] weak)
    {
        bool hit = false;
        foreach (var f in Forms(value)) hit |= Mark(weak, vars, f);
        return hit;
    }

    // Куски из 4 цифр подряд из source (для коротких - по их длине, минимум 3)
    static bool DigitChunk(string pw, string source, bool[] weak)
    {
        string dg = new string((source ?? "").Where(char.IsDigit).ToArray());
        int n = Math.Min(4, dg.Length);
        bool hit = false;
        if (n >= 3)
            for (int i = 0; i + n <= dg.Length; i++)
                hit |= Mark(weak, new[] { pw }, dg.Substring(i, n));
        return hit;
    }

    // ---------- Шаблоны ----------
    static void CheckPatterns(string pw, string low, PasswordAnalysisResult r, bool[] weak)
    {
        bool rep = false, blk = false, seq = false, kbd = false;

        foreach (Match m in Regex.Matches(pw, @"(.)\1{2,}"))          // aaa, 1111 (первый символ остаётся)
        {
            rep = true;
            Fill(weak, m.Index + 1, m.Length - 1);
        }
        foreach (Match m in Regex.Matches(pw, @"(.{2,}?)\1+"))        // abcabc, zebrazebra (первый блок остаётся)
        {
            blk = true;
            int first = m.Groups[1].Length;
            Fill(weak, m.Index + first, m.Length - first);
        }
        for (int i = 0; i + 3 <= low.Length; i++)                     // abc, 321
            if (Math.Abs(low[i + 1] - low[i]) == 1 && low[i + 1] - low[i] == low[i + 2] - low[i + 1])
            {
                seq = true;
                Fill(weak, i, 3);
            }
        for (int i = 0; i + 4 <= low.Length; i++)                     // qwer, asdf, йцук
        {
            string c = low.Substring(i, 4);
            if (Rows.Any(row => row.Contains(c) || Reverse(row).Contains(c)))
            {
                kbd = true;
                Fill(weak, i, 4);
            }
        }

        var l = r.PatternIssues;
        Add(l, rep, "Містить повторювані символи поспіль (aaa, 111).");
        Add(l, blk, "Містить повторюваний фрагмент (abcabc).");
        Add(l, seq, "Містить послідовність символів (abc, 123 тощо).");
        Add(l, kbd, "Містить клавіатурну послідовність (qwer, asdf, йцук…).");
        Add(l, pw.All(char.IsDigit), "Пароль складається лише з цифр.");
        Add(l, pw.All(char.IsLetter), "Пароль складається лише з літер.");
    }

    // ---------- Рекомендации ----------
    static void BuildRecommendations(string pw, PasswordAnalysisResult r)
    {
        var rec = r.Recommendations;
        if (pw.Length < 12) rec.Add($"Збільште довжину пароля до 12+ символів (зараз {pw.Length}).");
        if (!r.HasUpper) rec.Add("Додайте великі літери.");
        if (!r.HasLower) rec.Add("Додайте малі літери.");
        if (!r.HasDigit) rec.Add("Додайте цифри.");
        if (!r.HasSpecial) rec.Add("Додайте спеціальні символи (!, #, %, _ тощо).");
        if (r.PersonalIssues.Count > 0) rec.Add("Не використовуйте ім'я, прізвище, дату народження, телефон чи ID.");
        if (r.DictionaryIssues.Count > 0) rec.Add("Уникайте словникових слів і простих замін (a→@, o→0).");
        if (r.PatternIssues.Count > 0) rec.Add("Уникайте послідовностей (123, abc, qwerty) і повторів символів.");
        if (r.TotalScore < 80) rec.Add("Спробуйте парольну фразу з 4+ випадкових слів, напр.: «Хмара-Кіт-Лампа-7-Річка».");
        if (rec.Count == 0) rec.Add("Чудовий пароль! Використовуйте його лише для одного сервісу.");
    }

    // ---------- Помощники ----------
    static void Add(List<string> list, bool cond, string msg) { if (cond) list.Add(msg); }

    static void Fill(bool[] a, int start, int len) { for (int i = start; i < start + len; i++) a[i] = true; }

    // Находит все вхождения needle в вариантах пароля и помечает эти символы как передбачувані
    static bool Mark(bool[] weak, string[] vars, string needle)
    {
        bool hit = false;
        foreach (var v in vars)
            for (int i = v.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = v.IndexOf(needle, i + 1, StringComparison.Ordinal))
            {
                hit = true;
                Fill(weak, i, needle.Length);
            }
        return hit;
    }

    // Размер алфавита по символам, которые реально есть в непередбачуваній части
    static int Pool(char[] cs) =>
        (cs.Any(char.IsLower) ? 26 : 0) + (cs.Any(char.IsUpper) ? 26 : 0) +
        (cs.Any(char.IsDigit) ? 10 : 0) + (cs.Any(c => !char.IsLetterOrDigit(c)) ? 32 : 0);

    static string Leet(string s, char one) => string.Concat(s.Select(c =>
        c == '1' ? one : LeetFrom.IndexOf(c) >= 0 ? LeetTo[LeetFrom.IndexOf(c)] : c));

    static string Translit(string s) => string.Concat(s.Select(c =>
        Cyr.IndexOf(c) >= 0 ? Lat[Cyr.IndexOf(c)] : c.ToString()));

    static string Reverse(string s) { var a = s.ToCharArray(); Array.Reverse(a); return new string(a); }
}