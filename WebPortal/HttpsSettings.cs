// Copyright 2026 ZiYueCommentary
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpsPolicy;

namespace CbmrWebAdmin.WebPortal;

public sealed class HttpsSettings
{
    public const string SectionName = "Https";
    private const string SelfSignedCertificateFileName = "https-self-signed.pfx";

    public bool Enabled { get; init; }
    public int Port { get; init; } = 5001;
    public string? CertificatePath { get; init; }
    public string? CertificateKeyPath { get; init; }
    public string? CertificatePassword { get; init; }
    public string? Domain { get; init; }

    private string? NormalizedDomain => string.IsNullOrWhiteSpace(Domain) ? null : Domain.Trim().TrimEnd('.');
    private bool HasCertificate => !string.IsNullOrWhiteSpace(CertificatePath);

    public static HttpsSettings Apply(WebApplicationBuilder builder)
    {
        HttpsSettings settings = builder.Configuration.GetSection(SectionName).Get<HttpsSettings>() ?? new HttpsSettings();

        if (settings.NormalizedDomain is { } domain)
        {
            builder.Services.Configure<HostFilteringOptions>(options =>
                options.AllowedHosts = [domain, "localhost", "127.0.0.1", "[::1]"]);
        }

        if (!settings.Enabled)
            return settings;

        X509Certificate2 certificate = settings.HasCertificate
            ? settings.LoadCertificate(builder.Environment.ContentRootPath)
            : settings.LoadOrCreateSelfSignedCertificate(builder.Environment.ContentRootPath);

        builder.WebHost.ConfigureKestrel(options =>
            options.ListenAnyIP(settings.Port, listenOptions => listenOptions.UseHttps(certificate)));
        builder.Services.Configure<HttpsRedirectionOptions>(options =>
        {
            options.HttpsPort = settings.Port;
            options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        });
        return settings;
    }

    public void UseHttps(WebApplication app)
    {
        if (!Enabled)
            return;

        // HSTS would stop browsers from accepting the self-signed fallback certificate.
        if (HasCertificate && !app.Environment.IsDevelopment())
            app.UseHsts();
        app.UseHttpsRedirection();
    }

    private X509Certificate2 LoadCertificate(string contentRoot)
    {
        string certificatePath = Path.GetFullPath(CertificatePath!.Trim(), contentRoot);
        string? password = string.IsNullOrEmpty(CertificatePassword) ? null : CertificatePassword;

        if (string.IsNullOrWhiteSpace(CertificateKeyPath))
        {
            string extension = Path.GetExtension(certificatePath);
            if (extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".p12", StringComparison.OrdinalIgnoreCase))
                return X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password);
        }

        string? keyPath = string.IsNullOrWhiteSpace(CertificateKeyPath)
            ? null
            : Path.GetFullPath(CertificateKeyPath.Trim(), contentRoot);
        using X509Certificate2 pemCertificate = password is null
            ? X509Certificate2.CreateFromPemFile(certificatePath, keyPath)
            : X509Certificate2.CreateFromEncryptedPemFile(certificatePath, password, keyPath);

        // Windows SslStream cannot use the ephemeral key of a PEM-loaded certificate.
        return X509CertificateLoader.LoadPkcs12(pemCertificate.Export(X509ContentType.Pkcs12), null);
    }

    private X509Certificate2 LoadOrCreateSelfSignedCertificate(string contentRoot)
    {
        string hostName = NormalizedDomain ?? "localhost";
        string path = Path.Combine(contentRoot, "Data", SelfSignedCertificateFileName);

        if (File.Exists(path))
        {
            X509Certificate2 existing = X509CertificateLoader.LoadPkcs12FromFile(path, null);
            if (existing.NotAfter > DateTime.Now.AddDays(30) &&
                existing.GetNameInfo(X509NameType.DnsName, false) == hostName)
                return existing;
            existing.Dispose();
        }

        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={hostName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder subjectAlternativeNames = new();
        subjectAlternativeNames.AddDnsName(hostName);
        if (hostName != "localhost")
            subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], false));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 created = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        byte[] pfx = created.Export(X509ContentType.Pkcs12);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, pfx);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return X509CertificateLoader.LoadPkcs12(pfx, null);
    }
}
