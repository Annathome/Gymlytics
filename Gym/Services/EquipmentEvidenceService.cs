namespace Gym.Services;

public sealed class EquipmentEvidenceService
{
    private const long MaximumBytes = 5 * 1024 * 1024;
    private readonly IWebHostEnvironment _environment;

    public EquipmentEvidenceService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string?> ValidateAsync(IFormFile photo)
    {
        if (photo.Length > MaximumBytes)
            return "The evidence photo must not exceed 5 MB.";

        await using var stream = photo.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await stream.ReadAsync(header.AsMemory());
        return DetectExtension(header.AsSpan(0, bytesRead)) is null
            ? "Evidence must be a valid JPG, PNG, or WebP image."
            : null;
    }

    public async Task<string> SaveAsync(IFormFile photo)
    {
        await using var input = photo.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAsync(header.AsMemory());
        var extension = DetectExtension(header.AsSpan(0, bytesRead))
            ?? throw new InvalidOperationException("The uploaded evidence image is invalid.");

        var relativeDirectory = Path.Combine("uploads", "equipment-evidence");
        var physicalDirectory = Path.Combine(_environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(physicalDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(physicalDirectory, fileName);

        await using var output = File.Create(physicalPath);
        await using var source = photo.OpenReadStream();
        await source.CopyToAsync(output);
        return "/" + Path.Combine(relativeDirectory, fileName).Replace('\\', '/');
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var safeRelativePath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, safeRelativePath));
        var evidenceRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "equipment-evidence"));
        if (physicalPath.StartsWith(evidenceRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(physicalPath))
            File.Delete(physicalPath);
    }

    private static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }
}
