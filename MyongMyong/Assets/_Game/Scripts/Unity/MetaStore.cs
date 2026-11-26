using System;
using System.Collections.Generic;
using System.IO;
using Myong.Core;
using UnityEngine;

namespace Myong.Unity
{
    /// <summary>
    /// 판 밖 진행(지갑 · 영구 강화 단계) 파일 읽기 · 쓰기 한 곳(1-12). 형식 · 검사 규칙은 Core MetaSave, 여기는 파일과 JSON만.
    /// 기본 자리 = Application.persistentDataPath/meta.json. PlayerPrefs는 쓰지 않는다.
    /// 파일이 깨졌거나 새 형식이면 0에서 시작하고, 원본은 지우지 않고 옆에 백업(meta.json.broken-날짜)한다.
    /// 쓸 때는 임시 파일에 다 쓴 뒤 옮긴다(쓰다 멈춰도 원본이 반쯤 지워지지 않게).
    /// </summary>
    public sealed class MetaStore
    {
        public const string DefaultFileName = "meta.json";
        const string TempSuffix = ".tmp";
        const string BrokenSuffix = ".broken-";

        public MetaStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("save path is empty", nameof(path));
            Path = path;
        }

        /// <summary>게임이 쓰는 저장 파일 자리.</summary>
        public static string DefaultPath => System.IO.Path.Combine(Application.persistentDataPath, DefaultFileName);

        public string Path { get; }

        /// <summary>마지막 Load가 원본을 백업한 자리(없으면 null).</summary>
        public string LastBackupPath { get; private set; }

        /// <summary>깨진 원본을 백업하지 못해 저장을 막았는지(원본을 덮어쓰지 않게).</summary>
        public bool SaveBlocked { get; private set; }

        /// <summary>파일을 읽어 진행을 만든다. 없거나 깨졌으면 0에서 시작한 진행(깨졌으면 원본을 백업).</summary>
        public MetaLoadStatus Load(IReadOnlyList<string> ids, PermanentRulesData rules, out MetaProgress progress)
        {
            LastBackupPath = null;
            SaveBlocked = false;
            if (!File.Exists(Path))
            {
                progress = new MetaProgress(ids.Count);
                return MetaLoadStatus.Missing;
            }

            MetaSaveData data = null;
            try
            {
                string json = File.ReadAllText(Path);
                if (!string.IsNullOrWhiteSpace(json)) data = JsonUtility.FromJson<MetaSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Myong] save file could not be read ({e.GetType().Name}): {Path}");
                data = null;
            }

            MetaLoadStatus status = MetaSave.FromSave(data, ids, rules, out progress);
            if (status == MetaLoadStatus.Corrupt || status == MetaLoadStatus.NewerVersion)
            {
                LastBackupPath = Backup();
                // 백업을 못 했으면 원본을 덮어쓰지 않도록 이번 실행 동안 저장을 막는다.
                SaveBlocked = LastBackupPath == null;
                Debug.LogWarning($"[Myong] save file {status}, starting from zero. Original kept at: {LastBackupPath}");
            }
            return status;
        }

        /// <summary>진행을 파일에 쓴다. 성공하면 true.</summary>
        public bool Save(MetaProgress progress, IReadOnlyList<string> ids)
        {
            if (SaveBlocked) return false;
            string temp = Path + TempSuffix;
            try
            {
                string folder = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(temp, JsonUtility.ToJson(MetaSave.ToSave(progress, ids), true));
                File.Copy(temp, Path, true);
                File.Delete(temp);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Myong] save failed ({e.GetType().Name}: {e.Message}): {Path}");
                return false;
            }
        }

        // 원본을 옆에 복사해 둔다(지우지 않음). 같은 이름이 있으면 번호를 붙인다.
        string Backup()
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                string target = Path + BrokenSuffix + stamp;
                for (int n = 1; File.Exists(target); n++) target = Path + BrokenSuffix + stamp + "-" + n;
                File.Copy(Path, target, false);
                return target;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Myong] save backup failed ({e.GetType().Name}: {e.Message}): {Path}");
                return null;
            }
        }
    }
}
