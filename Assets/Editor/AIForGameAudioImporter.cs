using UnityEditor;

public sealed class AIForGameAudioImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        bool isBgm = assetPath.StartsWith("Assets/Resources/Audio/BGM/", System.StringComparison.Ordinal);
        bool isSkillVoice = assetPath.StartsWith("Assets/Resources/Audio/SkillVoices/", System.StringComparison.Ordinal);
        if (!isBgm && !isSkillVoice) return;
        AudioImporter importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = isBgm
            ? UnityEngine.AudioClipLoadType.Streaming
            : UnityEngine.AudioClipLoadType.CompressedInMemory;
        settings.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
        settings.quality = isBgm ? 0.72f : 0.85f;
        settings.preloadAudioData = isSkillVoice;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = isBgm;
    }
}
