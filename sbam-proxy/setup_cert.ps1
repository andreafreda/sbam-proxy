$CurrentDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$cert = New-SelfSignedCertificate -Subject 'CN=localhost' -DnsName 'localhost' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyExportPolicy Exportable -KeySpec Signature
$password = ConvertTo-SecureString -String 'pass' -Force -AsPlainText
$pfxPath = Join-Path -Path $CurrentDir -ChildPath "localhost.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'CurrentUser')
$rootStore.Open('ReadWrite')
$rootStore.Add($cert)
$rootStore.Close()

Write-Host "Certificate created and trusted at: $pfxPath"
