using UnityEditor;

namespace OrbitSort.Editor
{
    public sealed class OrbitSortModelImporter : AssetPostprocessor
    {
        private const string BoardModelPath =
            "Assets/OrbitSort/Resources/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(
                    BoardModelPath,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode =
                ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.useFileScale = true;
        }
    }
}
