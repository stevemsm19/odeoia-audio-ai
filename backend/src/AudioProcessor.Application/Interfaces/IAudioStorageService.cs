using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Defines operations for uploading and hosting raw/processed audio objects in external storage.
    /// </summary>
    public interface IAudioStorageService
    {
        /// <summary>
        /// Uploads an audio stream to the external storage container.
        /// </summary>
        /// <param name="fileStream">The readable stream of the audio file to upload.</param>
        /// <param name="fileName">The destination file name in storage.</param>
        /// <param name="contentType">The MIME type representing the audio media format.</param>
        /// <param name="folderId">A unique folder identifier used to group/segregate uploaded objects.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The fully qualified public HTTP/HTTPS URL of the uploaded audio file.</returns>
        Task<string> UploadAudioAsync(Stream fileStream, string fileName, string contentType, Guid folderId, CancellationToken cancellationToken = default);
    }
}
