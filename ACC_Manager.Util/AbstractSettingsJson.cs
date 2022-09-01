﻿using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace ACC_Manager.Util
{
    public interface IGenericSettingsJson
    {
    }

    public abstract class AbstractSettingsJson<T>
        where T : IGenericSettingsJson
    {
        public abstract T Default();
        public abstract string Path { get; }
        public abstract string FileName { get; }
        private FileInfo SettingsFile => new FileInfo(Path + FileName);

        public static T Cached { get; private set; }

        public AbstractSettingsJson()
        {
            Cached = Get(false);
        }

        public T Get(bool cached = true)
        {
            if (cached && Cached != null)
                return Cached;

            if (!SettingsFile.Exists)
                return Default();

            try
            {
                using (FileStream fileStream = SettingsFile.OpenRead())
                {
                    Cached = ReadJson(fileStream);
                    return Cached;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return Default();
        }

        public void Save(T genericJson)
        {
            try
            {
                string jsonString = JsonConvert.SerializeObject(genericJson, Formatting.Indented);

                if (!SettingsFile.Exists && !Directory.Exists(Path))
                    Directory.CreateDirectory(Path);

                File.WriteAllText(Path + "\\" + FileName, jsonString);

                Cached = genericJson;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private T ReadJson(Stream stream)
        {
            string jsonString = string.Empty;
            try
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    jsonString = reader.ReadToEnd();
                    jsonString = jsonString.Replace("\0", "");
                    reader.Close();
                    stream.Close();
                }

                return JsonConvert.DeserializeObject<T>(jsonString);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return Default();
        }

        public void Delete()
        {
            try
            {
                if (SettingsFile.Exists)
                    SettingsFile.Delete();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }
}
