using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace Sep490_Backend.Services.GoogleDriveService
{
    public interface IGoogleDriveService
    {
        Task<string> UploadFile(Stream fileStream, string fileName, string mimeType);
        Task<List<string>> UploadFiles(List<(Stream fileStream, string fileName, string mimeType)> files);
        Task DeleteFile(string fileId);
        Task DeleteFilesByLinks(List<string> fileLinks);
        bool IsValidFileType(string fileName, string mimeType);
        bool IsValidImageFile(string fileName, string mimeType);
    }

    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly string[] Scopes = { DriveService.Scope.DriveFile };
        private readonly string ApplicationName = "SLMS";
        private readonly DriveService _service;
        private readonly string[] AllowedMimeTypes = new[] 
        { 
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        private readonly string[] AllowedImageMimeTypes = new[]
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/gif",
            "image/bmp",
            "image/webp",
            "image/tiff"
        };
        private const int MaxFileSizeInMB = 10;

        public GoogleDriveService()
        {
            try 
            {
                GoogleCredential credential;
                var jsonCredentials = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CREDENTIALS_JSON");
                if (string.IsNullOrEmpty(jsonCredentials))
                {
                    throw new InvalidOperationException("Google Drive credentials not found in environment variables.");
                }

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonCredentials)))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(DriveService.ScopeConstants.Drive);
                }

                _service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Google Drive service: {ex.Message}", ex);
            }
        }

        public bool IsValidFileType(string fileName, string mimeType)
        {
            return AllowedMimeTypes.Contains(mimeType);
        }

        public bool IsValidImageFile(string fileName, string mimeType)
        {
            return AllowedImageMimeTypes.Contains(mimeType);
        }

        public async Task<string> UploadFile(Stream fileStream, string fileName, string mimeType)
        {
            try
            {
                if (fileStream == null || fileStream.Length == 0)
                {
                    throw new ArgumentException("File stream is empty or null.");
                }

                // Check file type based on mime type
                bool isImage = mimeType.StartsWith("image/");
                if (isImage)
                {
                    if (!IsValidImageFile(fileName, mimeType))
                    {
                        throw new ArgumentException("Invalid image file type. Only JPEG, PNG, GIF, BMP, WebP and TIFF are allowed.");
                    }
                }
                else
                {
                    if (!IsValidFileType(fileName, mimeType))
                    {
                        throw new ArgumentException("Invalid file type. Only PDF and Word documents are allowed.");
                    }
                }

                if (fileStream.Length > MaxFileSizeInMB * 1024 * 1024)
                {
                    throw new ArgumentException($"File size exceeds maximum limit of {MaxFileSizeInMB}MB.");
                }

                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = fileName,
                    Description = "Uploaded by SLMS"
                };

                FilesResource.CreateMediaUpload request;
                request = _service.Files.Create(fileMetadata, fileStream, mimeType);
                request.Fields = "id";
                
                var progress = await request.UploadAsync();
                if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    throw new Exception($"Upload failed: {progress.Status}");
                }

                var file = request.ResponseBody;
                await SetPublicPermissionAsync(file.Id);

                return $"https://drive.usercontent.google.com/download?id={file.Id}&export=view";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload file: {ex.Message}", ex);
            }
        }

        public async Task<List<string>> UploadFiles(List<(Stream fileStream, string fileName, string mimeType)> files)
        {
            if (files == null || !files.Any())
            {
                throw new ArgumentException("No files provided for upload.");
            }

            var links = new List<string>();
            var failedUploads = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var link = await UploadFile(file.fileStream, file.fileName, file.mimeType);
                    links.Add(link);
                }
                catch (Exception ex)
                {
                    failedUploads.Add($"{file.fileName}: {ex.Message}");
                }
            }

            if (failedUploads.Any())
            {
                throw new AggregateException($"Some files failed to upload: {string.Join(", ", failedUploads)}");
            }

            return links;
        }

        private async Task SetPublicPermissionAsync(string fileId)
        {
            try
            {
                var permission = new Google.Apis.Drive.v3.Data.Permission()
                {
                    Role = "reader",
                    Type = "anyone"
                };

                await _service.Permissions.Create(permission, fileId).ExecuteAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set public permission: {ex.Message}", ex);
            }
        }

        public async Task DeleteFile(string fileId)
        {
            try
            {
                if (string.IsNullOrEmpty(fileId))
                {
                    throw new ArgumentException("File ID cannot be null or empty.");
                }

                await _service.Files.Delete(fileId).ExecuteAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete file: {ex.Message}", ex);
            }
        }

        public async Task DeleteFilesByLinks(List<string> fileLinks)
        {
            if (fileLinks == null || !fileLinks.Any())
            {
                return;
            }

            var failedDeletions = new List<string>();

            foreach (var link in fileLinks)
            {
                try
                {
                    var fileId = ExtractFileIdFromLink(link);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        await DeleteFile(fileId);
                    }
                }
                catch (Exception ex)
                {
                    failedDeletions.Add($"{link}: {ex.Message}");
                }
            }

            if (failedDeletions.Any())
            {
                throw new AggregateException($"Some files failed to delete: {string.Join(", ", failedDeletions)}");
            }
        }

        private string ExtractFileIdFromLink(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return null;
            }

            var regex = new Regex(@"(?:/d/|id=)([a-zA-Z0-9-_]+)");
            var match = regex.Match(link);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
