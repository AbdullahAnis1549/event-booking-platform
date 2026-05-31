using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace WebApplication4.Services
{
    /// <summary>
    /// Cloudinary service for uploading images to cloud storage
    /// </summary>
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryService> _logger;

        public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
        {
            _logger = logger;

            // Initialize Cloudinary account
            var account = new Account(
                config["CloudinarySettings:CloudName"],
                config["CloudinarySettings:ApiKey"],
                config["CloudinarySettings:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
        }

        /// <summary>
        /// Upload image file to Cloudinary
        /// </summary>
        /// <param name="file">Image file to upload</param>
        /// <param name="folder">Optional folder name in Cloudinary</param>
        /// <returns>URL of the uploaded image</returns>
        public async Task<string> UploadImageAsync(IFormFile file, string folder = "user-images")
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("File is empty or null");
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    throw new ArgumentException("Invalid file type. Only images are allowed.");
                }

                // Validate file size (max 5MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    throw new ArgumentException("File size exceeds 5MB limit.");
                }

                // Convert IFormFile to byte array
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                // Create upload parameters
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    Transformation = new Transformation()
                        .Quality("auto")
                        .FetchFormat("auto")
                };

                // Upload to Cloudinary
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.LogInformation($"Image uploaded successfully: {uploadResult.SecureUrl}");
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    throw new Exception($"Cloudinary upload failed: {uploadResult.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image to Cloudinary");
                throw;
            }
        }

        /// <summary>
        /// Delete image from Cloudinary (optional - for cleanup)
        /// </summary>
        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return false;
                }

                // Extract public ID from URL
                var uri = new Uri(imageUrl);
                var pathParts = uri.AbsolutePath.Split('/');
                var publicId = string.Join("/", pathParts.SkipWhile(p => p != "image" && p != "upload").Skip(1))
                    .Replace(Path.GetExtension(uri.AbsolutePath), "");

                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);

                return result.Result == "ok";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting image from Cloudinary: {imageUrl}");
                return false;
            }
        }
    }
}
