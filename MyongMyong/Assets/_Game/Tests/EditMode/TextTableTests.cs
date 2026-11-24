using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Myong.Core;
using NUnit.Framework;
using UnityEngine;
using GameText = Myong.Core.Text;

namespace Myong.Tests
{
    /// <summary>텍스트 표 규칙(MY_DEV_GUIDE 1번)을 지키는지.</summary>
    public sealed class TextTableTests
    {
        const string KeySeparator = " @ ";

        static string GameFolder => Application.dataPath + "/_Game";

        static TextTable LoadTable() => TextTable.ParseCsv(File.ReadAllText(GameFolder + "/Text/strings.csv"));

        // 게임 코드 = Core + Unity. 에디터 도구(디버그 창)와 테스트는 대상이 아니다.
        static IEnumerable<string> GameCodeFiles()
        {
            return new[] { "Core", "Unity" }.SelectMany(d => Directory.GetFiles(GameFolder + "/Scripts/" + d, "*.cs", SearchOption.AllDirectories));
        }

        [TearDown]
        public void ResetText()
        {
            GameText.SetLanguage(GameText.DefaultLanguage);
            GameText.Load(null);
        }

        // ① 코드 · 씬 · 프리팹이 쓰는 키가 표에 모두 있다
        [Test]
        public void UsedKeys_ExistInTable()
        {
            var table = LoadTable();
            var used = new List<string>();

            var inCode = new Regex("Text\\.Get\\(\\s*\"([^\"]+)\"");
            foreach (string file in GameCodeFiles())
            {
                foreach (Match m in inCode.Matches(File.ReadAllText(file))) used.Add(m.Groups[1].Value + KeySeparator + Path.GetFileName(file));
            }

            var inAsset = new Regex("^\\s*textKey:\\s*(\\S+)\\s*$", RegexOptions.Multiline);
            var assets = Directory.GetFiles(GameFolder, "*.unity", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(GameFolder, "*.prefab", SearchOption.AllDirectories));
            foreach (string file in assets)
            {
                foreach (Match m in inAsset.Matches(File.ReadAllText(file))) used.Add(m.Groups[1].Value + KeySeparator + Path.GetFileName(file));
            }

            var missing = used.Where(u => !table.Has(u.Substring(0, u.IndexOf(KeySeparator, System.StringComparison.Ordinal)))).ToList();
            Assert.IsEmpty(missing, "Keys missing from strings.csv: " + string.Join(", ", missing));
        }

        // ② 모든 키에 기본 언어 문장이 있다
        [Test]
        public void EveryKey_HasDefaultLanguageText()
        {
            var table = LoadTable();
            Assert.Greater(table.Count, 0);
            var missing = table.Keys.Where(k => !table.TryGet(k, GameText.DefaultLanguage, out _)).ToList();
            Assert.IsEmpty(missing, "Keys without default-language text: " + string.Join(", ", missing));
        }

        // 번역 대상 언어(영어)도 빠진 칸이 없다
        [Test]
        public void EveryKey_HasEveryLanguage()
        {
            var table = LoadTable();
            var missing = new List<string>();
            foreach (Language language in System.Enum.GetValues(typeof(Language)))
            {
                missing.AddRange(table.Keys.Where(k => !table.TryGet(k, language, out _)).Select(k => k + ":" + language));
            }
            Assert.IsEmpty(missing, "Missing translations: " + string.Join(", ", missing));
        }

        // ③ 게임 코드에 한글 문자열이 없다 (주석은 괜찮다)
        [Test]
        public void GameCode_HasNoHangulStringLiterals()
        {
            var found = new List<string>();
            foreach (string file in GameCodeFiles())
            {
                foreach (string literal in StringLiterals(File.ReadAllText(file)))
                {
                    if (literal.Any(IsHangul)) found.Add(Path.GetFileName(file) + ": " + literal);
                }
            }
            Assert.IsEmpty(found, "Hangul string literals in game code (move them to strings.csv): " + string.Join(" | ", found));
        }

        [Test]
        public void ParseCsv_HandlesQuotesCommasAndComments()
        {
            var table = TextTable.ParseCsv("key,ko,en\n# comment\na.b.c,\"one, two\",\"say \"\"hi\"\"\"\n\nx.y.z,ko only,\n");
            Assert.AreEqual(2, table.Count);
            Assert.IsTrue(table.TryGet("a.b.c", Language.Ko, out string ko));
            Assert.AreEqual("one, two", ko);
            Assert.IsTrue(table.TryGet("a.b.c", Language.En, out string en));
            Assert.AreEqual("say \"hi\"", en);
            Assert.IsFalse(table.TryGet("x.y.z", Language.En, out _));
        }

        [Test]
        public void Get_UsesLanguage_FallsBackToDefault_ThenKey()
        {
            GameText.Load(TextTable.ParseCsv("key,ko,en\nk.one,KO1,EN1\nk.two,KO2,\n"));
            Assert.AreEqual("KO1", GameText.Get("k.one"));
            GameText.SetLanguage(Language.En);
            Assert.AreEqual("EN1", GameText.Get("k.one"));
            Assert.AreEqual("KO2", GameText.Get("k.two"));
            Assert.AreEqual("k.none", GameText.Get("k.none"));
        }

        [Test]
        public void Get_FillsPlaceholders_InTableOrder()
        {
            GameText.Load(TextTable.ParseCsv("key,ko,en\nk.fmt,{1} <- {0},{0} -> {1}\n"));
            Assert.AreEqual("b <- a", GameText.Get("k.fmt", "a", "b"));
            GameText.SetLanguage(Language.En);
            Assert.AreEqual("a -> b", GameText.Get("k.fmt", "a", "b"));
        }

        [Test]
        public void SetLanguage_RaisesChangedOnce()
        {
            int count = 0;
            System.Action onChanged = () => count++;
            GameText.Changed += onChanged;
            try
            {
                GameText.SetLanguage(Language.En);
                GameText.SetLanguage(Language.En);
                Assert.AreEqual(1, count);
            }
            finally
            {
                GameText.Changed -= onChanged;
            }
        }

        static bool IsHangul(char c)
        {
            return (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3131 && c <= 0x318E) || (c >= 0x1100 && c <= 0x11FF);
        }

        // 소스에서 문자열 리터럴의 내용만 뽑는다(주석 · 문자 리터럴은 건너뛴다).
        static List<string> StringLiterals(string source)
        {
            var literals = new List<string>();
            var current = new StringBuilder();
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                }
                else if (c == '/' && next == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i += 2;
                }
                else if (c == '\'')
                {
                    i++;
                    while (i < source.Length && source[i] != '\'') { if (source[i] == '\\') i++; i++; }
                    i++;
                }
                else if (c == '"')
                {
                    bool verbatim = i > 0 && (source[i - 1] == '@' || (i > 1 && source[i - 1] == '$' && source[i - 2] == '@'));
                    i++;
                    current.Length = 0;
                    while (i < source.Length)
                    {
                        if (verbatim)
                        {
                            if (source[i] == '"' && i + 1 < source.Length && source[i + 1] == '"') { current.Append('"'); i += 2; continue; }
                            if (source[i] == '"') break;
                        }
                        else
                        {
                            if (source[i] == '\\' && i + 1 < source.Length) { current.Append(source[i + 1]); i += 2; continue; }
                            if (source[i] == '"') break;
                        }
                        current.Append(source[i]);
                        i++;
                    }
                    i++;
                    literals.Add(current.ToString());
                }
                else
                {
                    i++;
                }
            }
            return literals;
        }
    }
}
