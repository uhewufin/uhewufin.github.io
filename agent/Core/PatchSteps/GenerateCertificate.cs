using System;

namespace MelonLoader.Installer.Core.PatchSteps;

/// <summary>
/// Uses the installer's built-in signing certificate. Generating a fresh one needs BouncyCastle,
/// which doesn't work in this AOT-compiled agent, so every patch is signed with this certificate for now.
/// </summary>
internal class GenerateCertificate : IPatchStep
{
    private const string BUILT_IN_CERT = @"-----BEGIN CERTIFICATE-----
MIICNTCCAZ6gAwIBAgIUeXP9Gyg714ZW2GVMXKbZzAKZIhEwDQYJKoZIhvcNAQEL
BQAwFjEUMBIGA1UEAwwLbGVtb25fbWVsb24wIBcNMjMwNjA2MDU1NjI2WhgPMzAy
MTEwMDcwNTU2MjZaMBYxFDASBgNVBAMMC2xlbW9uX21lbG9uMIGfMA0GCSqGSIb3
DQEBAQUAA4GNADCBiQKBgQDTxm5a2ooyAwzeBBdPkkMYadD2zsxFCcIRqteEjC9p
3R0J5rYSXJjGTrY17O4BEsngJ1ffDhHVWfz9CTByWiDhLqrZWsRMPqyP/bx4Ar1d
y4TwcyD3sIEkPTDhKzGtWi9+OENesL6zYCzpzWOZKZjOgi0a7hTGxe3QHaOnFUWL
twIDAQABo34wfDAdBgNVHQ4EFgQUPIiLPMKTkZAxh5D4kiIKZ+yx1pAwHwYDVR0j
BBgwFoAUPIiLPMKTkZAxh5D4kiIKZ+yx1pAwCwYDVR0PBAQDAgKEMBMGA1UdJQQM
MAoGCCsGAQUFBwMDMBgGA1UdEQQRMA+CDW1lbG9ud2lraS54eXowDQYJKoZIhvcN
AQELBQADgYEAw/SPOp/f2ssuS+Vh+CL9+UGRDaIMBfNaG8KNxsEuq+Ctw2/8M33j
UxSr/+a1ho1LxlS1chz35w+oI93eEObY3WwBp5TjcGgaH7WjrxHNvt5S6A1Hb7/v
R7N2eEM//D9Cl70NQN/837HJxUm45tjhRVVPAKdXg7pxZP/2HF8FW84=
-----END CERTIFICATE-----
-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQDTxm5a2ooyAwzeBBdPkkMYadD2zsxFCcIRqteEjC9p3R0J5rYS
XJjGTrY17O4BEsngJ1ffDhHVWfz9CTByWiDhLqrZWsRMPqyP/bx4Ar1dy4TwcyD3
sIEkPTDhKzGtWi9+OENesL6zYCzpzWOZKZjOgi0a7hTGxe3QHaOnFUWLtwIDAQAB
AoGBAKmXuBpT9uW0IaLOPejAJbEwVGLCGz2SYfMKEIuaRAIQS8f5FYfA1avBrxOi
SLtdU4OJnkoHl2p3JS1yJXT+DmMxqHRH4FkNxGkMfz9l+XsHnTCSiynRo5FzQo4j
fg8v3o8GdODxS2LUQLT89KUCXp+jDwSrNi0rIufeWD0sLOcBAkEA7AnKAOaC0D/3
k+xu7Ii2fKKjW5Rw18wMVw8sG8FNonJXH0ddB/i73P+Il1BllNpILbUjgTckRGko
c9eDJqMjNwJBAOWvWOV9TM3DSLOGKETs8MgSNG5onqVn77T7tR5dLTLynRS9v+L2
p/jty/y9FapkwRRRX/qojZQqYIyN/rt2G4ECQQCjJiUFOE+FCCHlkhAd2GVigrwt
Sc4xqu2Ao5EWYid6OFQ134rTPr8Dg3DzPfPoznQDe+fdobKkwpbec0FIzIxDAkEA
q8GkSHiapoQSKa15D5HfvL1gV/AEMsy2hDB2EG69Dgw/SvNaOu8YTR4GHMmJGhKe
EAOKMnc46EOIT5Mfmi+IAQJAMyPfohx7AkrhalBIiTDV6pl332pdfVTBHdPUf4XV
IAE6kTSMMHC6bVbrbS/CC8hRW8m7yD3LUa1EjFJmRWXsCQ==
-----END RSA PRIVATE KEY-----";

    public bool Run(Patcher patcher)
    {
        patcher.Logger.Log("Using the built-in signing certificate");
        patcher.Info.PemData = BUILT_IN_CERT;
        return true;
    }
}
