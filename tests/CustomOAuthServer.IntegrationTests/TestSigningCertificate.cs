using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CustomOAuthServer.IntegrationTests;

internal static class TestSigningCertificate
{
    public const string Password = "integration-test-signing-password";

    public static string CreatePfxPath()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=CustomOAuthServer Integration Tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        var pfxPath = Path.Combine(Path.GetTempPath(), $"customoauth-test-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pfx, Password));
        return pfxPath;
    }
}
