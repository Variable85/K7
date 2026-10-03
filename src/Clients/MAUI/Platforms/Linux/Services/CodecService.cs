using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI.Platforms.Linux.Services;

/// <summary>
/// Linux Direct Play decodes in LibVLC 4 (VA-API when the GPU has the decoder, avcodec
/// otherwise), so the advertised formats are the same LibVLC catalog as Windows: MKV, HEVC,
/// EAC3 and friends stay muxed. HLS transcode targets Video.js in WebKitGTK (h264/aac), not
/// this list. HDR passthrough is not wired on the GTK surface.
/// </summary>
public class CodecService : ICodecService
{
    public Task<bool> GetHdrSupportAsync() => Task.FromResult(false);

    public Task<string[]> GetSupportedVideoCodecsAsync() =>
        Task.FromResult(LibVlcWindowsCapabilities.VideoCodecs);

    public Task<string[]> GetSupportedAudioCodecsAsync() =>
        Task.FromResult(LibVlcWindowsCapabilities.AudioCodecs);

    public Task<string[]> GetSupportedContainersAsync() =>
        Task.FromResult(LibVlcWindowsCapabilities.GetContainers());

    public Task<string[]> GetSupportedVideoProfilesAsync() =>
        Task.FromResult(Array.Empty<string>());
}
