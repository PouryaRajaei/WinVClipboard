# Microsoft Store publishing

1. Reserve the app name in Partner Center and copy its **Package Identity Name** and **Publisher** value.
2. Install the Windows 10/11 SDK.
3. Build each package with the exact identity supplied by Partner Center:

```powershell
./scripts/Build-MSIX.ps1 -Runtime win-x64 -IdentityName 'YOUR_IDENTITY' -Publisher 'CN=YOUR_PUBLISHER_ID' -Version '1.3.0.0'
./scripts/Build-MSIX.ps1 -Runtime win-arm64 -IdentityName 'YOUR_IDENTITY' -Publisher 'CN=YOUR_PUBLISHER_ID' -Version '1.3.0.0'
```

4. Use a certificate whose Subject exactly matches Publisher to sign packages submitted outside Partner Center. Store submissions are signed by Microsoft after certification.
5. Create a submission, upload both architecture packages, complete privacy/policy declarations, screenshots, descriptions, and submit for certification.

## GitHub signing secrets

The release workflow signs executables when these repository secrets exist:

- `WINDOWS_CERTIFICATE`: Base64-encoded PFX
- `WINDOWS_CERTIFICATE_PASSWORD`: PFX password

Never commit a PFX or its password to the repository.
