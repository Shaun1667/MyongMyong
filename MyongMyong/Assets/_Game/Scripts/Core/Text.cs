using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Myong.Core
{
    /// <summary>지원 언어. 표의 열 이름(ko, en)과 같다. 새 언어는 끝에만 추가한다.</summary>
    public enum Language
    {
        Ko = 0,
        En = 1,
    }

    /// <summary>키 → 언어별 문장 표. CSV(첫 줄 = key,ko,en)에서 읽는다.</summary>
    public sealed class TextTable
    {
        const string KeyColumn = "key";
        const char Comment = '#';
        const char Quote = '"';

        readonly Dictionary<string, string[]> rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        readonly int languageCount = Enum.GetValues(typeof(Language)).Length;

        public IEnumerable<string> Keys => rows.Keys;

        public int Count => rows.Count;

        public bool Has(string key) => key != null && rows.ContainsKey(key);

        /// <summary>그 언어의 문장이 있고 비어 있지 않으면 true.</summary>
        public bool TryGet(string key, Language language, out string text)
        {
            text = null;
            if (key == null || !rows.TryGetValue(key, out var row)) return false;
            text = row[(int)language];
            return !string.IsNullOrEmpty(text);
        }

        public static TextTable ParseCsv(string csv)
        {
            var table = new TextTable();
            var records = SplitRecords(csv ?? string.Empty);
            int[] columnOfLanguage = null;
            int keyColumn = 0;
            foreach (var fields in records)
            {
                if (fields.Count == 0) continue;
                string first = fields[0].Trim();
                if (fields.Count == 1 && first.Length == 0) continue;
                if (first.Length > 0 && first[0] == Comment) continue;

                if (columnOfLanguage == null)
                {
                    columnOfLanguage = new int[table.languageCount];
                    for (int i = 0; i < columnOfLanguage.Length; i++) columnOfLanguage[i] = -1;
                    for (int c = 0; c < fields.Count; c++)
                    {
                        string name = fields[c].Trim();
                        if (string.Equals(name, KeyColumn, StringComparison.OrdinalIgnoreCase)) keyColumn = c;
                        else if (Enum.TryParse(name, true, out Language language) && Enum.IsDefined(typeof(Language), language)) columnOfLanguage[(int)language] = c;
                    }
                    continue;
                }

                if (keyColumn >= fields.Count) continue;
                string key = fields[keyColumn].Trim();
                if (key.Length == 0) continue;
                var row = new string[table.languageCount];
                for (int l = 0; l < row.Length; l++)
                {
                    int c = columnOfLanguage[l];
                    row[l] = c >= 0 && c < fields.Count ? fields[c].Replace("\\n", "\n") : string.Empty;
                }
                table.rows[key] = row;
            }
            return table;
        }

        // 따옴표로 감싼 칸(안의 따옴표 두 개 = 따옴표 하나) · 쉼표 · 줄바꿈을 처리하는 작은 CSV 분해기.
        static List<List<string>> SplitRecords(string csv)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (quoted)
                {
                    if (ch == Quote)
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == Quote) { field.Append(Quote); i++; }
                        else quoted = false;
                    }
                    else field.Append(ch);
                    continue;
                }
                if (ch == Quote) quoted = true;
                else if (ch == ',') { record.Add(field.ToString()); field.Length = 0; }
                else if (ch == '\r') { }
                else if (ch == '\n')
                {
                    record.Add(field.ToString());
                    field.Length = 0;
                    records.Add(record);
                    record = new List<string>();
                }
                else field.Append(ch);
            }
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }

    /// <summary>
    /// 화면에 보이는 모든 글의 입구. 게임 코드는 Text.Get(키)만 부른다.
    /// 표 구현(CSV)을 바꿔도 이 클래스의 모양은 그대로 둔다.
    /// </summary>
    public static class Text
    {
        public const Language DefaultLanguage = Language.Ko;

        static TextTable table;

        public static Language Language { get; private set; } = DefaultLanguage;

        /// <summary>표나 언어가 바뀌면 불린다. 화면 쪽이 글을 다시 채운다.</summary>
        public static event Action Changed;

        public static bool IsLoaded => table != null;

        public static void Load(TextTable newTable)
        {
            table = newTable;
            Changed?.Invoke();
        }

        public static void SetLanguage(Language language)
        {
            if (Language == language) return;
            Language = language;
            Changed?.Invoke();
        }

        public static bool Has(string key) => table != null && table.Has(key);

        /// <summary>지금 언어의 문장. 없으면 기본 언어, 그것도 없으면 키를 그대로 돌려준다(빠진 글이 눈에 띄게).</summary>
        public static string Get(string key)
        {
            if (table != null)
            {
                if (table.TryGet(key, Language, out var text)) return text;
                if (table.TryGet(key, DefaultLanguage, out text)) return text;
            }
            return key ?? string.Empty;
        }

        /// <summary>{0} {1} 자리표시자를 채운 문장. 문장을 코드로 이어 붙이지 않는다.</summary>
        public static string Get(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }
    }
}
