using System;
using System.IO;
using UnityEngine;

namespace DriftTogether.Coop
{
    /// <summary>Snapshot for UC-14: продолжить сплав с чекпоинта после перезапуска.</summary>
    [Serializable]
    public sealed class CoopSaveData
    {
        public int version = 1;
        public float posX, posY, posZ;
        public float rotY;
        public int food;
        public int logs;
        public int hull;
        public int modulesMask;

        public Vector3 Position => new Vector3(posX, posY, posZ);
        public Quaternion Rotation => Quaternion.Euler(0f, rotY, 0f);
    }

    /// <summary>JSON autosave in persistentDataPath (host only, UC-14).</summary>
    public static class CoopSave
    {
        public static string PathOverrideForTests;

        static string FilePath =>
            PathOverrideForTests ?? Path.Combine(Application.persistentDataPath, "coop_save.json");

        public static void Write(CoopSaveData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoopSave] write failed: " + e.Message);
            }
        }

        public static CoopSaveData TryLoad()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;
                var data = JsonUtility.FromJson<CoopSaveData>(File.ReadAllText(FilePath));
                return data != null && data.version == 1 ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoopSave] load failed: " + e.Message);
                return null;
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Итоговая оценка состояния плота для отчёта (UC-15).</summary>
    public static class RaftCondition
    {
        public static string Describe(int hullAtFinish, int modulesBuilt)
        {
            if (hullAtFinish >= 4 && modulesBuilt >= 2)
                return "приплыли на яхте";
            if (hullAtFinish >= 4)
                return "крепкий плот";
            if (hullAtFinish >= 2)
                return "потрёпан, но горд";
            return "доползли на двери от шкафа";
        }
    }
}
