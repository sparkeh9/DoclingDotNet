#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DoclingDotNet.Asr;

/// <summary>
/// Represents the result of an ASR (Audio Speech Recognition) execution.
/// </summary>
/// <param name="Segments">The sequenced transcribed text chunks and their temporal boundaries.</param>
/// <param name="ProviderName">The name of the ASR provider backend that handled the request.</param>
/// <param name="Language">The ISO language code used during the transcription.</param>
public record AsrExecutionResult(
    IReadOnlyList<AsrSegment> Segments,
    string ProviderName,
    string Language
);

/// <summary>
/// Represents a single segmented chunk of transcribed text.
/// </summary>
/// <param name="StartTime">The start time of the audio segment in seconds.</param>
/// <param name="EndTime">The end time of the audio segment in seconds.</param>
/// <param name="Text">The transcribed spoken text.</param>
/// <param name="Voice">Optional identifier mapping to a unique speaker.</param>
public record AsrSegment(
    double StartTime,
    double EndTime,
    string Text,
    string? Voice = null
);

/// <summary>
/// Contract for any Audio Speech Recognition engine that can provide text extraction for an audio stream.
/// </summary>
public interface IDoclingAsrProvider
{
    /// <summary>
    /// The unique name of this provider instance.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks whether this provider is correctly initialized, installed, and able to perform transcription.
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// Asynchronously processes an audio stream and returns the structured extraction components.
    /// </summary>
    Task<AsrExecutionResult> ProcessAudioAsync(
        Stream audioStream, 
        CancellationToken cancellationToken = default);
}
