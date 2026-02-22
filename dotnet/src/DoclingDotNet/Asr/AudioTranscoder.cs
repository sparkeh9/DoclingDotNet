#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Pipes;

namespace DoclingDotNet.Asr;

/// <summary>
/// A utility for normalizing external audio streams (MP3, MP4, FLAC, etc.) into the strict format 
/// required by Whisper models: 16kHz, 16-bit Mono PCM.
/// </summary>
internal static class AudioTranscoder
{
    private const int WhisperSampleRate = 16000;
    private const int WhisperChannels = 1;

    /// <summary>
    /// Transcodes any FFmpeg-supported input stream to a 16kHz PCM WAV format in memory.
    /// </summary>
    /// <param name="inputStream">The unsupported (e.g., MP3/M4A) input audio stream.</param>
    /// <param name="cancellationToken">Token to cancel the transcoding process.</param>
    /// <returns>A MemoryStream containing the normalized raw PCM WAV data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if FFmpeg is not installed on the host system.</exception>
    public static async Task<Stream> NormalizeToWhisperPcmAsync(Stream inputStream, CancellationToken cancellationToken = default)
    {
        var outputStream = new MemoryStream();

        try
        {
            var result = await FFMpegArguments
                .FromPipeInput(new StreamPipeSource(inputStream))
                .OutputToPipe(new StreamPipeSink(outputStream), options => options
                    .ForceFormat("wav")
                    .WithCustomArgument($"-ar {WhisperSampleRate} -ac {WhisperChannels} -f s16le"))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously()
                .ConfigureAwait(false);

            if (!result)
            {
                throw new InvalidOperationException("FFmpeg failed to process the audio stream.");
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.Message.Contains("The system cannot find the file specified"))
        {
            throw new InvalidOperationException("FFmpeg is not installed or not available on the system PATH. Transcoding multi-format audio requires FFmpeg.", ex);
        }

        outputStream.Position = 0; // Reset position so it's ready for downstream reading
        return outputStream;
    }
}
