namespace LIFT.Models;

public sealed record AdminCertificateVm(Guid Id, string OriginalFileName, DateTimeOffset UploadedAt);

public sealed record AdminReviewVm(Guid Id, string Name, string Email, string Role,
    string Status, string Specialty, string? Bio, byte? ExperienceYears,
    short Capacity, IReadOnlyList<AdminCertificateVm> Certificates);

public sealed record CertificateDownload(byte[] Content, string FileName);
