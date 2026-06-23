using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuthService.Infrastructure.OpenIddict;

public static class OpenIddictCertificateExtensions
{
    public static OpenIddictServerBuilder AddAuthServiceCertificates(
        this OpenIddictServerBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment
        )
    {
        if (environment.IsDevelopment())
        {
            builder.AddDevelopmentEncryptionCertificate();
            builder.AddDevelopmentSigningCertificate();

            return builder;
        }


        var options = configuration
          .GetSection("OpenIddict:Certificates")
          .Get<OpenIddictCertificateOptions>();

        if (options is null)
        {
            throw new InvalidOperationException("OpenIddict certificate options are not configured.");
        }

        var signingCertificate = LoadCertificate(
            options.SigningCertificatePath,
            options.SigningCertificatePassword,
            "signing"
            );

        var encryptionCertificate = LoadCertificate(
            options.EncryptionCertificatePath,
            options.EncryptionCertificatePassword,
            "encryption"
            );

        builder.AddSigningCertificate(signingCertificate);
        builder.AddEncryptionCertificate(encryptionCertificate);

        return builder;
    }

    private static X509Certificate2 LoadCertificate(
        string? path,
        string? password,
        string? purpose
        )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"OpenIddict {purpose} certificate path is not configured.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"OpenIddict {purpose} certificate file was not found.", path);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet
          );
    }


}
