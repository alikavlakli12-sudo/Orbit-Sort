using System;
using UnityEngine;

namespace OrbitSort.Data
{
    public static class LevelCatalogLoader
    {
        public const string DefaultResourcePath = "Levels/levels";

        public static LevelCatalogData LoadFromResources(
            string resourcePath = DefaultResourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Level catalog resource '{resourcePath}' was not found.");
            }

            return Parse(asset.text);
        }

        public static LevelCatalogData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "The level catalog JSON is empty.",
                    nameof(json));
            }

            LevelCatalogData catalog =
                JsonUtility.FromJson<LevelCatalogData>(json);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Unity could not deserialize the level catalog.");
            }

            LevelCatalogValidator.ValidateAndThrow(catalog);
            return catalog;
        }
    }
}
